using DeuxERP.API.Services;
using DeuxERP.Application.Common;
using DeuxERP.Application.Notifications;
using DeuxERP.Domain.Interfaces;
using DeuxERP.Domain.Sales.Events;
using DeuxERP.Infrastructure.Cash.Handlers;
using DeuxERP.Infrastructure.Data;
using DeuxERP.Infrastructure.Notifications;
using DeuxERP.Infrastructure.Repositories;
using DeuxERP.Infrastructure.Services;
using FluentValidation;
using FluentValidation.AspNetCore;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuestPDF.Infrastructure;
using System.Net;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

var runMigrationsOnly = args.Contains("--migrate", StringComparer.OrdinalIgnoreCase);
var runMode = Environment.GetEnvironmentVariable("RUN_MODE") ?? "PROD";
var isDevMode = runMode == "DEV";
var disableRateLimiting = builder.Configuration.GetValue("DisableRateLimiting", false);

var secret = builder.Configuration.GetValue<string>("JwtSettings:Secret")
             ?? throw new Exception("JWT Secret não encontrada!");

var key = JwtSigningKey.FromSecret(secret);


builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;

    var forwardedHeadersSection = builder.Configuration.GetSection("ForwardedHeaders");

    foreach (var proxy in forwardedHeadersSection.GetSection("KnownProxies").Get<string[]>() ?? Array.Empty<string>())
    {
        if (IPAddress.TryParse(proxy, out var proxyAddress))
        {
            options.KnownProxies.Add(proxyAddress);
        }
    }

    foreach (var network in forwardedHeadersSection.GetSection("KnownNetworks").Get<string[]>() ?? Array.Empty<string>())
    {
        var parts = network.Split('/', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            continue;
        }

        if (IPAddress.TryParse(parts[0], out var networkAddress) && int.TryParse(parts[1], out var prefixLength))
        {
            options.KnownIPNetworks.Add(new System.Net.IPNetwork(networkAddress, prefixLength));
        }
    }
});

builder.Services.AddAuthentication(x => {
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(x => {
    x.MapInboundClaims = false;
    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = "DeuxERP-api",
        ValidateAudience = true,
        ValidAudience = "DeuxERP-client",
        ClockSkew = TimeSpan.Zero,
        NameClaimType = "email",
        RoleClaimType = "role"
    };
});

builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
builder.Services.AddScoped<CashFlowAuditInterceptor>();
builder.Services.AddHttpClient();

builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
{
    options.UseNpgsql(connectionString);
    options.AddInterceptors(sp.GetRequiredService<CashFlowAuditInterceptor>());
});
builder.Services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

var vapidSubject = builder.Configuration["VAPID_SUBJECT"] ?? "mailto:admin@deuxcerie.com.br";
var vapidPublicKey = builder.Configuration["VAPID_PUBLIC_KEY"];
var vapidPrivateKey = builder.Configuration["VAPID_PRIVATE_KEY"];
var pushEnabled = GetOptionalConfigurationFlag(builder.Configuration, "Push:Enabled", "PUSH_ENABLED");
var pushRequired = GetConfigurationFlag(builder.Configuration, "Push:Required", "PUSH_REQUIRED");
var pushExplicitlyDisabled = pushEnabled == false && !pushRequired;
var hasAnyVapidConfig =
    !string.IsNullOrWhiteSpace(builder.Configuration["VAPID_SUBJECT"]) ||
    !string.IsNullOrWhiteSpace(vapidPublicKey) ||
    !string.IsNullOrWhiteSpace(vapidPrivateKey);
var hasVapidKeys = !string.IsNullOrWhiteSpace(vapidPublicKey) && !string.IsNullOrWhiteSpace(vapidPrivateKey);
var pushRequested = pushEnabled == true || pushRequired || (!pushExplicitlyDisabled && hasAnyVapidConfig);

if (pushRequested && (!hasVapidKeys || string.IsNullOrWhiteSpace(vapidSubject)))
{
    throw new InvalidOperationException(
        "Push notifications are enabled or partially configured, but VAPID_SUBJECT, VAPID_PUBLIC_KEY and VAPID_PRIVATE_KEY are required.");
}

if (!pushExplicitlyDisabled && hasVapidKeys)
{
    builder.Services.AddSingleton<IPushNotificationAvailability>(
        new PushNotificationAvailability(isAvailable: true, disabledReason: null));

    builder.Services.AddOptions<VapidSettings>()
        .Configure(settings =>
        {
            settings.Subject = vapidSubject;
            settings.PublicKey = vapidPublicKey!;
            settings.PrivateKey = vapidPrivateKey!;
        })
        .Validate(settings => !string.IsNullOrWhiteSpace(settings.Subject), "VAPID_SUBJECT not set")
        .Validate(settings => !string.IsNullOrWhiteSpace(settings.PublicKey), "VAPID_PUBLIC_KEY not set")
        .Validate(settings => !string.IsNullOrWhiteSpace(settings.PrivateKey), "VAPID_PRIVATE_KEY not set")
        .ValidateOnStart();

    builder.Services.AddSingleton(sp =>
    {
        var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<VapidSettings>>().Value;
        var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();

        return new PushServiceClient(httpClientFactory.CreateClient("WebPush"))
        {
            DefaultAuthentication = new VapidAuthentication(
                publicKey: options.PublicKey,
                privateKey: options.PrivateKey)
            {
                Subject = options.Subject
            },
            DefaultAuthenticationScheme = VapidAuthenticationScheme.Vapid,
            AutoRetryAfter = true,
            MaxRetriesAfter = 3
        };
    });

    builder.Services.AddScoped<IPushNotificationService, WebPushNotificationService>();
}
else
{
    builder.Services.AddSingleton<IPushNotificationAvailability>(
        new PushNotificationAvailability(
            isAvailable: false,
            disabledReason: pushExplicitlyDisabled
                ? "Push notifications are disabled by configuration."
                : "Push notifications are disabled because VAPID keys are not configured."));
    builder.Services.AddSingleton<IPushNotificationService, NoOpPushNotificationService>();
}

// CORS Policy
var allowedOriginsRaw = builder.Configuration["ALLOWED_ORIGINS"];
var allowedOrigins = !string.IsNullOrWhiteSpace(allowedOriginsRaw)
    ? allowedOriginsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    : isDevMode
        ? new[] { "http://localhost:3000", "http://127.0.0.1:3000" }
        : throw new Exception("ALLOWED_ORIGINS não configurado!");

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .WithHeaders("Authorization", "Content-Type")
              .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
              .AllowCredentials();
    });
});

// Repository config
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IDashboardRepository, DashboardRepository>();
builder.Services.AddScoped<IPaymentTransactionRepository, PaymentTransactionRepository>();
builder.Services.AddScoped<ITokenService, TokenService>();

// Error handling config
builder.Services.AddExceptionHandler<DeuxERP.API.Middlewares.GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Controllers config
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
builder.Services.AddScoped<IDomainEventHandler<OrderPaidEvent>, OrderPaidEventHandler>();
builder.Services.AddScoped<IDomainEventHandler<OrderPaymentReversedEvent>, OrderPaymentReversedEventHandler>();
builder.Services.AddScoped<DeuxERP.Domain.Interfaces.ICashFlowRepository, DeuxERP.Infrastructure.Repositories.CashFlowRepository>();
builder.Services.AddScoped<DeuxERP.Application.Services.CashFlowService>();
builder.Services.AddHostedService<DailyOrderReminderService>();

// Services config
builder.Services.AddScoped<DeuxERP.Application.Services.OrderService>();
builder.Services.AddScoped<DeuxERP.Application.Services.InventoryService>();
builder.Services.AddScoped<DeuxERP.Application.Services.DashboardService>();
builder.Services.AddSingleton<ExportService>();
builder.Services.AddSingleton<IStorageService, StorageService>();

// Rate limiting config
builder.Services.AddRateLimiter(options =>
{
    // Auth endpoint: strict fixed window per IP — 10 req/min
    options.AddPolicy<string>("auth", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
        });
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Apply global policy to all endpoints by default
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetSlidingWindowLimiter(ip, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 6,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
        });
    });
});

// Application builder
var app = builder.Build();

if (runMigrationsOnly)
{
    ApplyDatabaseMigrations(app.Services);
    return;
}

app.UseForwardedHeaders();

app.UseExceptionHandler();

if (isDevMode)
{
    app.MapOpenApi();
}
else
{
    app.UseHttpsRedirection();
}

// Security headers — before CORS so they apply to all responses including preflight
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'none'";
    await next();
});

// CORS
app.UseCors("FrontendPolicy");

if (!disableRateLimiting)
{
    app.UseRateLimiter();
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

if (builder.Configuration.GetValue("Database:RunMigrationsAtStartup", isDevMode))
{
    ApplyDatabaseMigrations(app.Services);
}

app.Run();

static void ApplyDatabaseMigrations(IServiceProvider serviceProvider)
{
    using var scope = serviceProvider.CreateScope();
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();

    Console.WriteLine(">>> Verificando conectividade e rodando migrations...");
    try
    {
        context.Database.Migrate();
        Console.WriteLine(">>> Migrations executadas com sucesso!");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "ERRO CRÍTICO: Falha ao conectar no banco ou rodar migrations. Verifique Security Groups e ConnectionString.");
        throw;
    }
}

static bool GetConfigurationFlag(IConfiguration configuration, string key, string environmentKey)
{
    var value = configuration[key] ?? configuration[environmentKey];
    return bool.TryParse(value, out var enabled) && enabled;
}

static bool? GetOptionalConfigurationFlag(IConfiguration configuration, string key, string environmentKey)
{
    var value = configuration[key] ?? configuration[environmentKey];
    return bool.TryParse(value, out var enabled) ? enabled : null;
}

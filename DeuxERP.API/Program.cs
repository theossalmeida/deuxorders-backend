using DeuxERP.API.Services;
using DeuxERP.Application.Common;
using DeuxERP.Domain.Interfaces;
using DeuxERP.Domain.Sales.Events;
using DeuxERP.Infrastructure.Cash.Handlers;
using DeuxERP.Infrastructure.Data;
using DeuxERP.Infrastructure.Repositories;
using DeuxERP.Infrastructure.Services;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuestPDF.Infrastructure;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

var runMode = Environment.GetEnvironmentVariable("RUN_MODE") ?? "PROD";
var isDevMode = runMode == "DEV";

var secret = builder.Configuration.GetValue<string>("JwtSettings:Secret")
             ?? throw new Exception("JWT Secret não encontrada!");

var key = Encoding.ASCII.GetBytes(secret);


builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
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

builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
{
    options.UseNpgsql(connectionString);
    options.AddInterceptors(sp.GetRequiredService<CashFlowAuditInterceptor>());
});

// CORS Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",
                "http://127.0.0.1:3000",
                "https://orders.deuxcerie.com.br"
              )
              .WithHeaders("Authorization", "Content-Type")
              .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
              .AllowCredentials();
    });
});

// Repository config
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IClientRepository, ClientRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IDashboardRepository, DashboardRepository>();
builder.Services.AddScoped<IPaymentTransactionRepository, PaymentTransactionRepository>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

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

// Services config
builder.Services.AddScoped<DeuxERP.Application.Services.OrderService>();
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

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        Console.WriteLine(">>> Verificando conectividade e rodando migrations...");
        context.Database.Migrate();
        Console.WriteLine(">>> Migrations executadas com sucesso!");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "ERRO CRÍTICO: Falha ao conectar no banco ou rodar migrations. Verifique Security Groups e ConnectionString.");
    }
}

app.Run();
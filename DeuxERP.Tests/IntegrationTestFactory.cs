using DeuxERP.API.Services;
using DeuxERP.Application.Notifications;
using DeuxERP.Domain.Notifications;
using DeuxERP.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using System.Text;

file sealed class NullStorageService : IStorageService
{
    public string GeneratePresignedUploadUrl(string objectKey, string contentType) => string.Empty;
    public List<string>? GetSignedReadUrls(List<string>? objectKeys) => null;
    public Task DeleteObjectAsync(string objectKey, CancellationToken ct = default) => Task.CompletedTask;
    public Task<string> UploadFileAsync(Stream stream, string objectKey, string contentType, CancellationToken ct = default) => Task.FromResult(objectKey);
    public string GetPublicUrl(string objectKey) => $"https://cdn.example.com/{objectKey}";
}

file sealed class NullPushNotificationService : IPushNotificationService
{
    public Task SendToAllAsync(
        NotificationType type,
        string title,
        string body,
        string? actionUrl = null,
        CancellationToken ct = default) => Task.CompletedTask;

    public Task SendToUserAsync(
        Guid userId,
        NotificationType type,
        string title,
        string body,
        string? actionUrl = null,
        CancellationToken ct = default) => Task.CompletedTask;
}

namespace DeuxERP.Tests
{
    public class IntegrationTestFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
    {
        private readonly string _dbName = $"DeuxERP_Test_{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            var testSecretKey = "chave_para_teste_de_integracao_123";

            builder.ConfigureAppConfiguration((context, config) =>
            {
                var testConfig = new Dictionary<string, string?>
                {
                    { "JWT_SECRET", testSecretKey },
                    { "JwtSettings:Secret", testSecretKey },
                    { "JWT_ISSUER", "DeuxERP-api" },
                    { "JWT_AUDIENCE", "DeuxERP-client" },
                    { "VAPID_PUBLIC_KEY", "test-public-key" },
                    { "VAPID_PRIVATE_KEY", "test-private-key" },
                    { "VAPID_SUBJECT", "mailto:test@example.com" }
                };

                config.AddInMemoryCollection(testConfig);
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IStorageService>();
                services.AddSingleton<IStorageService, NullStorageService>();
                services.RemoveAll<IPushNotificationService>();
                services.AddSingleton<IPushNotificationService, NullPushNotificationService>();

                services.RemoveAll(typeof(DbContextOptions<ApplicationDbContext>));
                services.RemoveAll(typeof(DbContextOptions));
                services.RemoveAll(typeof(IDbContextOptionsConfiguration<ApplicationDbContext>));
                services.RemoveAll(typeof(ApplicationDbContext));

                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase(_dbName);
                    options.ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
                });

                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    var testKeyBytes = Encoding.ASCII.GetBytes(testSecretKey);
                    options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(testKeyBytes);

                    options.TokenValidationParameters.ValidateIssuer = false;
                    options.TokenValidationParameters.ValidateAudience = false;
                });
            });
        }
    }
}

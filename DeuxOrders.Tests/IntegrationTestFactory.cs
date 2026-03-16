using DeuxOrders.API.Services;
using DeuxOrders.Infrastructure.Data;
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
    public Task DeleteObjectAsync(string objectKey) => Task.CompletedTask;
    public Task<string> UploadFileAsync(Stream stream, string objectKey, string contentType) => Task.FromResult(objectKey);
    public string GetPublicUrl(string objectKey) => $"https://cdn.example.com/{objectKey}";
}

namespace DeuxOrders.Tests
{
    public class IntegrationTestFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            var testSecretKey = "chave_para_teste_de_integracao_123";

            builder.ConfigureAppConfiguration((context, config) =>
            {
                var testConfig = new Dictionary<string, string>
                {
                    { "JWT_SECRET", testSecretKey },
                    { "JwtSettings:Secret", testSecretKey },
                    { "JWT_ISSUER", "deuxorders-api" },
                    { "JWT_AUDIENCE", "deuxorders-client" }
                };

                config.AddInMemoryCollection(testConfig);
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IStorageService>();
                services.AddSingleton<IStorageService, NullStorageService>();

                services.RemoveAll(typeof(DbContextOptions<ApplicationDbContext>));
                services.RemoveAll(typeof(DbContextOptions));
                services.RemoveAll(typeof(IDbContextOptionsConfiguration<ApplicationDbContext>));
                services.RemoveAll(typeof(ApplicationDbContext));

                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase("DeuxOrders_Integration_Static_Db");
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
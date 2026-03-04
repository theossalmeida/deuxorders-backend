using DeuxOrders.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace DeuxOrders.Tests
{
    public class IntegrationTestFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddEnvironmentVariables();

                var root = config.Build();
                var secret = root["JwtSettings:Secret"] ?? root["JWT_SECRET"];
            });

            builder.ConfigureTestServices(services =>
            {
                var descriptors = services.Where(d =>
                    d.ServiceType.Name.Contains("DbContextOptions") ||
                    d.ServiceType.Name.Contains("Npgsql")).ToList();

                foreach (var d in descriptors) services.Remove(d);
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase("DeuxOrders_Integration_Static_Db");
                });
            });
        }
    }
}
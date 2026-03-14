using DeuxOrders.Domain.Entities;
using DeuxOrders.Domain.Enums;
using DeuxOrders.Infrastructure.Data;
using DeuxOrders.Tests.DTOs;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;

namespace DeuxOrders.Tests
{
    public abstract class BaseIntegrationTest : IClassFixture<IntegrationTestFactory<Program>>
    {
        protected readonly HttpClient _client;
        protected readonly IntegrationTestFactory<Program> _factory;

        protected BaseIntegrationTest(IntegrationTestFactory<Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        protected async Task AuthenticateAsync()
        {
            _client.DefaultRequestHeaders.Authorization = null;

            var suffix = DateTime.Now.Ticks.ToString().Substring(12);
            var email = $"test{suffix}@admin.com";
            var password = "Password123!";

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var hash = BCrypt.Net.BCrypt.HashPassword(password);
                var user = new User("Teste", $"user{suffix}", hash, email, UserRole.User);
                db.Users.Add(user);
                await db.SaveChangesAsync();
            }

            var loginRequest = new { Email = email, Password = password };
            var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new Exception($"Login failed: {response.StatusCode} - {errorBody}");
            }

            var loginResult = await response.Content.ReadFromJsonAsync<LoginResponse>();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);
        }
    }
}
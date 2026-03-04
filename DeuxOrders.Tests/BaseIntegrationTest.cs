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
            var name = "Teste";
            var username = $"user{suffix}";
            var email = $"test{suffix}@admin.com";
            var password = "Password123!";

            var registerRequest = new { Name = name, Username = username, Email = email, Password = password };
            var regRes = await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);

            if (!regRes.IsSuccessStatusCode)
            {
                var error = await regRes.Content.ReadAsStringAsync();
                throw new Exception($"Register failed: {regRes.StatusCode} - {error}");
            }

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var userExists = db.Users.Any(u => u.Email == email);
                if (!userExists)
                {
                    throw new Exception($"INMEMORY: Register returned 200 OK, but user is not saved. Email: {email}");
                }
            }

            var loginRequest = new { Email = email, Password = password };
            var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                var authHeaderInfo = response.Headers.WwwAuthenticate.ToString();

                throw new Exception($@"
                    FALHA NA REQUISIÇÃO: {response.StatusCode}
                    CABEÇALHO DE AUTH REJEITADO: {authHeaderInfo}
                    DETALHES: {errorBody}
                ");
            }

            var loginResult = await response.Content.ReadFromJsonAsync<LoginResponse>();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);
        }
    }
}
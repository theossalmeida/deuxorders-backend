using DeuxERP.Tests.DTOs;
using System.Net;
using System.Net.Http.Json;

namespace DeuxERP.Tests.EndpointCoverage;

public class AuthEndpointTests : IClassFixture<IntegrationTestFactory<Program>>
{
    private readonly HttpClient _client;

    public AuthEndpointTests(IntegrationTestFactory<Program> factory) => _client = factory.CreateClient();

    [Fact]
    [Trait("TestSet", "Authentication")]
    public async Task RegistrationAndLogin_EnforceValidationUniquenessCredentialsAndBootstrapSecurity()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var request = new RegisterRequest("Admin", $"admin-{suffix}", $"admin-{suffix}@example.com", "Password123!");

        var created = await _client.PostAsJsonAsync("/api/v1/auth/register", request);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var unauthorizedSecondUser = await _client.PostAsJsonAsync("/api/v1/auth/register",
            request with { Username = $"second-{suffix}", Email = $"second-{suffix}@example.com" });
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedSecondUser.StatusCode);

        var wrongPassword = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(request.Email, "WrongPassword123!"));
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);

        var unknownUser = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest($"missing-{suffix}@example.com", request.Password));
        Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);

        var login = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(request.Email, request.Password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())?.Token;
        Assert.False(string.IsNullOrWhiteSpace(token));
        _client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var duplicate = await _client.PostAsJsonAsync("/api/v1/auth/register", request);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var invalid = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("", "", "invalid", "short"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }
}

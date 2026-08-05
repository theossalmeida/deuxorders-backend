using DeuxERP.Application.DTOs;
using DeuxERP.Domain.Cash.Enums;
using System.Net;
using System.Net.Http.Json;

namespace DeuxERP.Tests.EndpointCoverage;

public class AuthorizationCoverageTests : BaseIntegrationTest
{
    public AuthorizationCoverageTests(IntegrationTestFactory<Program> factory) : base(factory) { }

    [Fact]
    [Trait("TestSet", "Authentication")]
    public async Task ProtectedRouteGroups_RejectAnonymousRequests()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var requests = new HttpRequestMessage[]
        {
            new(HttpMethod.Get, "/api/v1/clients/all"),
            new(HttpMethod.Get, "/api/v1/products/all"),
            new(HttpMethod.Get, "/api/v1/orders/all"),
            new(HttpMethod.Get, "/api/v1/inventory/all"),
            new(HttpMethod.Get, "/api/v1/cash/entries"),
            new(HttpMethod.Get, "/api/v1/dashboard/summary"),
            new(HttpMethod.Post, "/api/v1/push/status") { Content = JsonContent.Create(new { Endpoint = "https://push.example.com/test" }) }
        };

        foreach (var request in requests)
        {
            using (request)
            {
                using var response = await _client.SendAsync(request);
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }
        }
    }

    [Fact]
    [Trait("TestSet", "Authentication")]
    public async Task AdministratorMutations_RejectRegularUsersBeforeResourceLookup()
    {
        await AuthenticateAsync();
        var id = Guid.NewGuid();
        var requests = new HttpRequestMessage[]
        {
            new(HttpMethod.Delete, $"/api/v1/clients/{id}"),
            new(HttpMethod.Delete, $"/api/v1/products/{id}"),
            new(HttpMethod.Delete, $"/api/v1/orders/{id}"),
            new(HttpMethod.Patch, $"/api/v1/orders/{id}/pay"),
            new(HttpMethod.Patch, $"/api/v1/orders/{id}/unpay") { Content = JsonContent.Create(new { Reason = "valid reason" }) },
            new(HttpMethod.Post, "/api/v1/cash/entries")
            {
                Content = JsonContent.Create(new CreateCashEntryRequest(
                    DateTime.UtcNow, CashFlowType.Outflow, CashFlowCategory.Other, "Vendor", 1000, null))
            }
        };

        foreach (var request in requests)
        {
            using (request)
            {
                using var response = await _client.SendAsync(request);
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }
        }
    }
}

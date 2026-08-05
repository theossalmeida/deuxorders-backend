using DeuxERP.Application.DTOs;
using DeuxERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeuxERP.Tests.EndpointCoverage;

public class DashboardEndpointTests : BaseIntegrationTest
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public DashboardEndpointTests(IntegrationTestFactory<Program> factory) : base(factory) { }

    [Fact]
    [Trait("TestSet", "Dashboard")]
    public async Task DashboardScenario_UsesOneDatasetAcrossAggregatesRankingTimelineAndCsvExport()
    {
        await AuthenticateAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var clientResponse = await _client.PostAsJsonAsync("/api/v1/clients/new",
            new CreateClient($"Dashboard Client {suffix}", "11999999999"));
        var client = (await clientResponse.Content.ReadFromJsonAsync<ClientResponse>())!;

        using var productForm = new MultipartFormDataContent();
        productForm.Add(new StringContent($"Dashboard Product {suffix}"), "Name");
        productForm.Add(new StringContent("2000"), "Price");
        var productResponse = await _client.PostAsync("/api/v1/products/new", productForm);
        var product = (await productResponse.Content.ReadFromJsonAsync<ProductResponse>())!;

        var activeOrder = await CreateOrderAsync(client.Id, product.Id, 2, 1500);
        var canceledOrder = await CreateOrderAsync(client.Id, product.Id, 5, 1000);
        Assert.Equal(HttpStatusCode.OK,
            (await _client.PatchAsync($"/api/v1/orders/{canceledOrder.Id}/cancel", null)).StatusCode);

        var summary = await _client.GetFromJsonAsync<DashboardSummaryResponse>("/api/v1/dashboard/summary");
        Assert.Equal(1, summary!.TotalOrders);
        Assert.Equal(1, summary.CanceledOrders);
        Assert.Equal(3000, summary.TotalRevenue);
        Assert.Equal(4000, summary.TotalValue);
        Assert.Equal(1000, summary.TotalDiscount);

        using var timelineResponse = await _client.GetAsync("/api/v1/dashboard/revenue-over-time");
        timelineResponse.EnsureSuccessStatusCode();
        using var timeline = JsonDocument.Parse(await timelineResponse.Content.ReadAsStringAsync());
        Assert.Single(timeline.RootElement.GetProperty("dataPoints").EnumerateArray());

        using var productsResponse = await _client.GetAsync("/api/v1/dashboard/top-products?limit=1");
        productsResponse.EnsureSuccessStatusCode();
        using var products = JsonDocument.Parse(await productsResponse.Content.ReadAsStringAsync());
        var topProduct = products.RootElement.EnumerateArray().Single();
        Assert.Equal(product.Id, topProduct.GetProperty("productId").GetGuid());
        Assert.Equal(2, topProduct.GetProperty("totalQuantitySold").GetInt32());

        using var clientsResponse = await _client.GetAsync("/api/v1/dashboard/top-clients?limit=1");
        clientsResponse.EnsureSuccessStatusCode();
        using var clients = JsonDocument.Parse(await clientsResponse.Content.ReadAsStringAsync());
        Assert.Equal(client.Id, clients.RootElement.EnumerateArray().Single().GetProperty("clientId").GetGuid());

        using var scope = _factory.Services.CreateScope();
        if (!scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.IsNpgsql())
            return; // The streaming SelectMany export is provider-specific and is exercised by CI PostgreSQL.

        using var export = await _client.GetAsync("/api/v1/dashboard/export?format=csv&status=Received");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.StartsWith("text/csv", export.Content.Headers.ContentType!.ToString());
        var csv = await export.Content.ReadAsStringAsync();
        Assert.Contains(activeOrder.Id.ToString(), csv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(canceledOrder.Id.ToString(), csv, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<OrderResponse> CreateOrderAsync(Guid clientId, Guid productId, int quantity, int unitPrice)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/orders/new",
            new CreateOrderRequest(clientId, DateTime.UtcNow.AddDays(1),
                [new CreateOrderItemRequest(productId, quantity, unitPrice, null, null, null)], null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions))!;
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeuxERP.Application.DTOs;
using DeuxERP.Domain.Sales;
using DeuxERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DeuxERP.Tests.EndpointCoverage;

public class OrderDateFilterTests : BaseIntegrationTest
{
    public OrderDateFilterTests(IntegrationTestFactory<Program> factory) : base(factory) { }

    private async Task<(Guid ClientId, Guid ProductId, Guid Delivered, Guid Created)> SeedAsync()
    {
        await AuthenticateAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var client = new Client($"Date filters {Guid.NewGuid()}");
        var product = new Product("Date filter product", 2000);
        db.Clients.Add(client);
        db.Products.Add(product);
        Order Add(string delivery, string creation, int amount, bool canceled = false, bool paid = false)
        {
            var order = new Order(client.Id, DateTime.Parse(delivery).ToUniversalTime());
            order.AddItem(product.Id, 1, amount, 2000, null);
            if (canceled) order.MarkAsCanceled();
            db.Orders.Add(order);
            db.Entry(order).Property(o => o.CreatedAt).CurrentValue = DateTime.Parse(creation).ToUniversalTime();
            if (paid) db.Entry(order).Property(o => o.PaidAt).CurrentValue = DateTime.Parse(delivery).ToUniversalTime();
            return order;
        }
        // 01:00 UTC on the next day is still the requested day in São Paulo.
        var delivered = Add("2026-07-11T01:00:00Z", "2026-07-01T12:00:00Z", 1500, paid: true);
        var created = Add("2026-07-20T12:00:00Z", "2026-07-10T12:00:00Z", 700);
        Add("2026-07-10T02:59:59Z", "2026-07-01T12:00:00Z", 300);
        Add("2026-07-11T03:00:00Z", "2026-07-01T12:00:00Z", 400);
        Add("2026-07-10T12:00:00Z", "2026-07-01T12:00:00Z", 900, canceled: true);
        await db.SaveChangesAsync();
        return (client.Id, product.Id, delivered.Id, created.Id);
    }

    [Theory]
    [InlineData("", 1500, 1)]
    [InlineData("&dateField=CreatedAt", 700, 0)]
    public async Task Summary_SelectsRequestedDateAndIncludesWholeBusinessDay(string extra, long revenue, int canceled)
    {
        var data = await SeedAsync();
        var summary = await _client.GetFromJsonAsync<DashboardSummaryResponse>(
            $"/api/v1/dashboard/summary?from=2026-07-10&to=2026-07-10&clientId={data.ClientId}{extra}");
        Assert.Equal(revenue, summary!.TotalRevenue);
        Assert.Equal(1, summary.TotalOrders);
        Assert.Equal(canceled, summary.CanceledOrders);
        Assert.Equal(2000 - revenue, summary.TotalDiscount);
    }

    [Fact]
    public async Task Dashboard_ExistingSaasDeliveryParametersRetainExactUtcBounds()
    {
        var data = await SeedAsync();
        var summary = await _client.GetFromJsonAsync<DashboardSummaryResponse>(
            $"/api/v1/dashboard/summary?deliveryDateFrom=2026-07-10T03:00:00Z&deliveryDateTo=2026-07-11T03:00:00Z&clientId={data.ClientId}");
        Assert.Equal(1500, summary!.TotalRevenue);
        Assert.Equal(1, summary.CanceledOrders);
    }

    [Theory]
    [InlineData("DeliveryDate", 1500)]
    [InlineData("CreatedAt", 700)]
    public async Task TimelineAndRankingsUseSameDatesAsSummary(string field, long revenue)
    {
        var data = await SeedAsync();
        var query = $"from=2026-07-10&to=2026-07-10&clientId={data.ClientId}&dateField={field}";
        using var timeline = JsonDocument.Parse(await _client.GetStringAsync($"/api/v1/dashboard/revenue-over-time?{query}"));
        var day = timeline.RootElement.GetProperty("dataPoints").EnumerateArray().Single();
        Assert.Equal("2026-07-10", day.GetProperty("date").GetString());
        Assert.Equal(revenue, day.GetProperty("revenue").GetInt64());
        foreach (var endpoint in new[] { "top-products", "top-clients" })
        {
            using var ranking = JsonDocument.Parse(await _client.GetStringAsync($"/api/v1/dashboard/{endpoint}?{query}"));
            Assert.Equal(revenue, ranking.RootElement.EnumerateArray().Single().GetProperty("totalRevenue").GetInt64());
        }
    }

    [Theory]
    [InlineData("DeliveryDate")]
    [InlineData("CreatedAt")]
    public async Task OrderSearch_SelectsDateClientProductAndStatus(string field)
    {
        var data = await SeedAsync();
        using var response = JsonDocument.Parse(await _client.GetStringAsync(
            $"/api/v1/orders/all?from=2026-07-10&to=2026-07-10&dateField={field}&clientId={data.ClientId}&productId={data.ProductId}&status=Received"));
        var order = response.RootElement.GetProperty("items").EnumerateArray().Single();
        Assert.Equal(field == "DeliveryDate" ? data.Delivered : data.Created, order.GetProperty("id").GetGuid());
        Assert.True(order.TryGetProperty("createdAt", out _));
        using var empty = JsonDocument.Parse(await _client.GetStringAsync(
            $"/api/v1/orders/all?clientId={data.ClientId}&productId={Guid.NewGuid()}"));
        Assert.Equal(0, empty.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task PaymentFilter_IsOptionalAndDoesNotRedefineRevenue()
    {
        var data = await SeedAsync();
        var query = $"from=2026-07-10&to=2026-07-10&clientId={data.ClientId}";
        var paid = await _client.GetFromJsonAsync<DashboardSummaryResponse>($"/api/v1/dashboard/summary?{query}&isPaid=true");
        var unpaid = await _client.GetFromJsonAsync<DashboardSummaryResponse>($"/api/v1/dashboard/summary?{query}&isPaid=false");
        Assert.Equal(1500, paid!.TotalRevenue);
        Assert.Equal(0, unpaid!.TotalRevenue);
        Assert.Equal(1, unpaid.CanceledOrders);
    }

    [Theory]
    [InlineData("from=2026-07-20&to=2026-07-10")]
    [InlineData("dateField=Invalid")]
    [InlineData("from=2026-07-10&createdAtFrom=2026-07-10T00:00:00Z")]
    [InlineData("deliveryDateFrom=2026-07-10T00:00:00Z&dateField=CreatedAt")]
    public async Task InvalidOrAmbiguousFilters_Return400(string query)
    {
        await AuthenticateAsync();
        var result = await _client.GetAsync($"/api/v1/dashboard/summary?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    [Theory]
    [InlineData("DeliveryDate")]
    [InlineData("CreatedAt")]
    public async Task CsvExport_UsesSameDateSelection(string field)
    {
        var data = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        if (!scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.IsNpgsql()) return;
        var csv = await _client.GetStringAsync(
            $"/api/v1/dashboard/export?from=2026-07-10&to=2026-07-10&dateField={field}&clientId={data.ClientId}&status=Received&format=csv");
        Assert.Contains((field == "DeliveryDate" ? data.Delivered : data.Created).ToString(), csv);
        Assert.DoesNotContain((field == "DeliveryDate" ? data.Created : data.Delivered).ToString(), csv);
    }

    [Fact]
    public async Task ProductMonth_DefaultsToDeliveryAndAllowsCreation()
    {
        var data = await SeedAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var order = await db.Orders.SingleAsync(o => o.Id == data.Delivered);
            db.Entry(order).Property(o => o.CreatedAt).CurrentValue = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
            await db.SaveChangesAsync();
        }
        using var delivery = JsonDocument.Parse(await _client.GetStringAsync($"/api/v1/products/{data.ProductId}/stats?month=2026-07"));
        using var creation = JsonDocument.Parse(await _client.GetStringAsync($"/api/v1/products/{data.ProductId}/stats?month=2026-07&dateField=CreatedAt"));
        Assert.Equal(2900, delivery.RootElement.GetProperty("revenueThisMonth").GetInt64());
        Assert.Equal(1400, creation.RootElement.GetProperty("revenueThisMonth").GetInt64());
    }
}

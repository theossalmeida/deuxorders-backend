using DeuxOrders.Application.DTOs;
using DeuxOrders.Tests.DTOs;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace DeuxOrders.Tests.Sales
{
    public record ProductStatsResponse(int SoldThisMonth, long RevenueThisMonth);

    public class ProductStatsTests : BaseIntegrationTest
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public ProductStatsTests(IntegrationTestFactory<Program> factory) : base(factory) { }

        private async Task<Guid> CreateClientAsync(string suffix)
        {
            var res = await _client.PostAsJsonAsync("/api/v1/clients/new",
                new CreateClient($"Stats Client {suffix}", null));
            res.EnsureSuccessStatusCode();
            return (await res.Content.ReadFromJsonAsync<ClientResponse>(JsonOptions))!.Id;
        }

        private async Task<Guid> CreateProductAsync(string suffix)
        {
            var form = new MultipartFormDataContent();
            form.Add(new StringContent($"Stats Product {suffix}"), "Name");
            form.Add(new StringContent("4000"), "Price");
            var res = await _client.PostAsync("/api/v1/products/new", form);
            res.EnsureSuccessStatusCode();
            return (await res.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions))!.Id;
        }

        private async Task<OrderResponse> CreateOrderAsync(Guid clientId, Guid productId, int paidPrice, int qty = 1)
        {
            var req = new CreateOrderRequest(clientId, DateTime.UtcNow.AddDays(1),
                new List<CreateOrderItemRequest> { new(productId, qty, paidPrice, null, null, null) }, null);
            var res = await _client.PostAsJsonAsync("/api/v1/orders/new", req);
            res.EnsureSuccessStatusCode();
            return (await res.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions))!;
        }

        [Fact]
        public async Task GetProductStats_ExcludesCanceledOrdersAndItems()
        {
            await AuthenticateAsync();
            var suffix = DateTime.Now.Ticks.ToString().Substring(12);
            var clientId = await CreateClientAsync(suffix);
            var productId = await CreateProductAsync(suffix);
            var otherProductId = await CreateProductAsync($"other-{suffix}");

            await CreateOrderAsync(clientId, productId, 3000, 2);
            await CreateOrderAsync(clientId, productId, 5000, 1);

            var canceledOrder = await CreateOrderAsync(clientId, productId, 9000, 1);
            await _client.PatchAsync($"/api/v1/orders/{canceledOrder.Id}/cancel", null);

            var now = DateTime.UtcNow;
            var month = $"{now.Year}-{now.Month:D2}";

            var res = await _client.GetAsync($"/api/v1/products/{productId}/stats?month={month}");

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var stats = await res.Content.ReadFromJsonAsync<ProductStatsResponse>(JsonOptions);
            Assert.NotNull(stats);
            Assert.Equal(3, stats.SoldThisMonth);
            Assert.Equal(11000, stats.RevenueThisMonth);
        }

        [Fact]
        public async Task GetProductStats_InvalidMonthFormat_Returns400()
        {
            await AuthenticateAsync();
            var suffix = DateTime.Now.Ticks.ToString().Substring(12);
            var productId = await CreateProductAsync(suffix);

            var res = await _client.GetAsync($"/api/v1/products/{productId}/stats?month=04-2026");

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }

        [Fact]
        public async Task GetProductStats_UnknownProduct_Returns404()
        {
            await AuthenticateAsync();
            var now = DateTime.UtcNow;
            var month = $"{now.Year}-{now.Month:D2}";

            var res = await _client.GetAsync($"/api/v1/products/{Guid.NewGuid()}/stats?month={month}");

            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }

        [Fact]
        public async Task GetProductStats_NoSalesInMonth_ReturnsZeroes()
        {
            await AuthenticateAsync();
            var suffix = DateTime.Now.Ticks.ToString().Substring(12);
            var productId = await CreateProductAsync(suffix);

            var res = await _client.GetAsync($"/api/v1/products/{productId}/stats?month=2020-01");

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var stats = await res.Content.ReadFromJsonAsync<ProductStatsResponse>(JsonOptions);
            Assert.Equal(0, stats!.SoldThisMonth);
            Assert.Equal(0, stats.RevenueThisMonth);
        }
    }
}

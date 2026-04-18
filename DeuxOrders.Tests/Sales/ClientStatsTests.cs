using DeuxOrders.Application.DTOs;
using DeuxOrders.Tests.DTOs;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace DeuxOrders.Tests.Sales
{
    public class ClientStatsTests : BaseIntegrationTest
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public ClientStatsTests(IntegrationTestFactory<Program> factory) : base(factory) { }

        private async Task<Guid> CreateClientAsync()
        {
            var suffix = DateTime.Now.Ticks.ToString().Substring(12);
            var res = await _client.PostAsJsonAsync("/api/v1/clients/new",
                new CreateClient($"Stats Test Client {suffix}", null));
            res.EnsureSuccessStatusCode();
            var client = await res.Content.ReadFromJsonAsync<ClientResponse>(JsonOptions);
            return client!.Id;
        }

        private async Task<Guid> CreateProductAsync()
        {
            var suffix = DateTime.Now.Ticks.ToString().Substring(12);
            var form = new MultipartFormDataContent();
            form.Add(new StringContent($"Stats Test Product {suffix}"), "Name");
            form.Add(new StringContent("5000"), "Price");
            var res = await _client.PostAsync("/api/v1/products/new", form);
            res.EnsureSuccessStatusCode();
            var product = await res.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
            return product!.Id;
        }

        private async Task<OrderResponse> CreateOrderAsync(Guid clientId, Guid productId, int paidPrice = 5000)
        {
            var req = new CreateOrderRequest(clientId, DateTime.UtcNow.AddDays(1),
                new List<CreateOrderItemRequest> { new(productId, 1, paidPrice, null, null, null) }, null);
            var res = await _client.PostAsJsonAsync("/api/v1/orders/new", req);
            res.EnsureSuccessStatusCode();
            return (await res.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions))!;
        }

        [Fact]
        public async Task GetStats_ClientWithMixedOrders_ExcludesCanceled()
        {
            await AuthenticateAsync();
            var clientId = await CreateClientAsync();
            var productId = await CreateProductAsync();

            var order1 = await CreateOrderAsync(clientId, productId, 5000);
            var order2 = await CreateOrderAsync(clientId, productId, 3000);
            var orderToCancel = await CreateOrderAsync(clientId, productId, 9000);

            await _client.PatchAsync($"/api/v1/orders/{orderToCancel.Id}/cancel", null);

            var res = await _client.GetAsync($"/api/v1/clients/{clientId}/stats");

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var stats = await res.Content.ReadFromJsonAsync<ClientStatsResponse>(JsonOptions);
            Assert.NotNull(stats);
            Assert.Equal(2, stats.TotalOrders);
            Assert.Equal(8000, stats.TotalSpent);
            Assert.NotNull(stats.LastOrderDate);
        }

        [Fact]
        public async Task GetStats_UnknownClient_Returns404()
        {
            await AuthenticateAsync();

            var res = await _client.GetAsync($"/api/v1/clients/{Guid.NewGuid()}/stats");

            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }

        [Fact]
        public async Task GetStats_ClientWithNoOrders_ReturnsZeroes()
        {
            await AuthenticateAsync();
            var clientId = await CreateClientAsync();

            var res = await _client.GetAsync($"/api/v1/clients/{clientId}/stats");

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var stats = await res.Content.ReadFromJsonAsync<ClientStatsResponse>(JsonOptions);
            Assert.NotNull(stats);
            Assert.Equal(0, stats.TotalOrders);
            Assert.Equal(0, stats.TotalSpent);
            Assert.Null(stats.LastOrderDate);
        }
    }
}

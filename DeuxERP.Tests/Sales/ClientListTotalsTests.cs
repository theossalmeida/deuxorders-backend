using DeuxERP.Application.DTOs;
using DeuxERP.Tests.DTOs;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace DeuxERP.Tests.Sales
{
    public class ClientListTotalsTests : BaseIntegrationTest
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public ClientListTotalsTests(IntegrationTestFactory<Program> factory) : base(factory) { }

        private async Task<Guid> CreateClientAsync(string suffix)
        {
            var res = await _client.PostAsJsonAsync("/api/v1/clients/new",
                new CreateClient($"Totals Client {suffix}", null));
            res.EnsureSuccessStatusCode();
            return (await res.Content.ReadFromJsonAsync<ClientResponse>(JsonOptions))!.Id;
        }

        private async Task<Guid> CreateProductAsync(string suffix)
        {
            var form = new MultipartFormDataContent();
            form.Add(new StringContent($"Totals Product {suffix}"), "Name");
            form.Add(new StringContent("2000"), "Price");
            var res = await _client.PostAsync("/api/v1/products/new", form);
            res.EnsureSuccessStatusCode();
            return (await res.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions))!.Id;
        }

        private async Task CreateOrderAsync(Guid clientId, Guid productId, int price)
        {
            var req = new CreateOrderRequest(clientId, DateTime.UtcNow.AddDays(1),
                new List<CreateOrderItemRequest> { new(productId, 1, price, null, null, null) }, null);
            var res = await _client.PostAsJsonAsync("/api/v1/orders/new", req);
            res.EnsureSuccessStatusCode();
        }

        [Fact]
        public async Task GetAll_WithIncludeTotals_ReturnsTotalsExcludingCanceled()
        {
            await AuthenticateAsync();
            var suffix = DateTime.Now.Ticks.ToString().Substring(12);
            var clientId = await CreateClientAsync(suffix);
            var productId = await CreateProductAsync(suffix);

            await CreateOrderAsync(clientId, productId, 3000);
            await CreateOrderAsync(clientId, productId, 7000);

            var canceledOrder = new CreateOrderRequest(clientId, DateTime.UtcNow.AddDays(1),
                new List<CreateOrderItemRequest> { new(productId, 1, 9999, null, null, null) }, null);
            var cancelRes = await _client.PostAsJsonAsync("/api/v1/orders/new", canceledOrder);
            cancelRes.EnsureSuccessStatusCode();
            var toCancel = (await cancelRes.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions))!;
            await _client.PatchAsync($"/api/v1/orders/{toCancel.Id}/cancel", null);

            var res = await _client.GetAsync($"/api/v1/clients/all?includeTotals=true&search=Totals+Client+{suffix}");

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var paged = await res.Content.ReadFromJsonAsync<PagedClientResponse>(JsonOptions);
            var client = paged!.Items.FirstOrDefault(c => c.Id == clientId);
            Assert.NotNull(client);
            Assert.Equal(2, client.TotalOrders);
            Assert.Equal(10000, client.TotalSpent);
        }

        [Fact]
        public async Task GetAll_WithoutIncludeTotals_TotalsAreNull()
        {
            await AuthenticateAsync();
            var suffix = DateTime.Now.Ticks.ToString().Substring(12);
            var clientId = await CreateClientAsync(suffix);
            var productId = await CreateProductAsync(suffix);
            await CreateOrderAsync(clientId, productId, 5000);

            var res = await _client.GetAsync($"/api/v1/clients/all?search=Totals+Client+{suffix}");

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var paged = await res.Content.ReadFromJsonAsync<PagedClientResponse>(JsonOptions);
            var client = paged!.Items.FirstOrDefault(c => c.Id == clientId);
            Assert.NotNull(client);
            Assert.Null(client.TotalOrders);
            Assert.Null(client.TotalSpent);
        }

        [Fact]
        public async Task GetAll_WithIncludeTotals_ClientWithNoOrders_ReturnZeroes()
        {
            await AuthenticateAsync();
            var suffix = DateTime.Now.Ticks.ToString().Substring(12);
            var clientId = await CreateClientAsync(suffix);

            var res = await _client.GetAsync($"/api/v1/clients/all?includeTotals=true&search=Totals+Client+{suffix}");

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var paged = await res.Content.ReadFromJsonAsync<PagedClientResponse>(JsonOptions);
            var client = paged!.Items.FirstOrDefault(c => c.Id == clientId);
            Assert.NotNull(client);
            Assert.Equal(0, client.TotalOrders);
            Assert.Equal(0, client.TotalSpent);
        }
    }
}

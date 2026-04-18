using DeuxERP.Application.DTOs;
using DeuxERP.Tests.DTOs;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace DeuxERP.Tests.Sales
{
    public class ClientOrderHistoryTests : BaseIntegrationTest
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public ClientOrderHistoryTests(IntegrationTestFactory<Program> factory) : base(factory) { }

        private async Task<Guid> CreateClientAsync(string suffix)
        {
            var res = await _client.PostAsJsonAsync("/api/v1/clients/new",
                new CreateClient($"History Client {suffix}", null));
            res.EnsureSuccessStatusCode();
            var client = await res.Content.ReadFromJsonAsync<ClientResponse>(JsonOptions);
            return client!.Id;
        }

        private async Task<Guid> CreateProductAsync(string suffix)
        {
            var form = new MultipartFormDataContent();
            form.Add(new StringContent($"History Product {suffix}"), "Name");
            form.Add(new StringContent("1000"), "Price");
            var res = await _client.PostAsync("/api/v1/products/new", form);
            res.EnsureSuccessStatusCode();
            var product = await res.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
            return product!.Id;
        }

        private async Task CreateOrderAsync(Guid clientId, Guid productId)
        {
            var req = new CreateOrderRequest(clientId, DateTime.UtcNow.AddDays(1),
                new List<CreateOrderItemRequest> { new(productId, 1, 1000, null, null, null) }, null);
            var res = await _client.PostAsJsonAsync("/api/v1/orders/new", req);
            res.EnsureSuccessStatusCode();
        }

        [Fact]
        public async Task GetClientOrders_PaginatesCorrectlyAndNoClientBleed()
        {
            await AuthenticateAsync();
            var suffix = DateTime.Now.Ticks.ToString().Substring(12);
            var clientAId = await CreateClientAsync($"A-{suffix}");
            var clientBId = await CreateClientAsync($"B-{suffix}");
            var productId = await CreateProductAsync(suffix);

            for (int i = 0; i < 25; i++)
                await CreateOrderAsync(clientAId, productId);

            for (int i = 0; i < 5; i++)
                await CreateOrderAsync(clientBId, productId);

            var res = await _client.GetAsync($"/api/v1/clients/{clientAId}/orders?page=1&size=10");

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var paged = await res.Content.ReadFromJsonAsync<PagedOrderResponse>(JsonOptions);
            Assert.NotNull(paged);
            Assert.Equal(10, paged.Items.Count);
            Assert.Equal(25, paged.TotalCount);
            Assert.All(paged.Items, o => Assert.Equal(clientAId, o.ClientId));
        }

        [Fact]
        public async Task GetClientOrders_UnknownClient_Returns404()
        {
            await AuthenticateAsync();

            var res = await _client.GetAsync($"/api/v1/clients/{Guid.NewGuid()}/orders");

            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }

        [Fact]
        public async Task GetClientOrders_SecondPageHasNonOverlappingItems()
        {
            await AuthenticateAsync();
            var suffix = DateTime.Now.Ticks.ToString().Substring(12);
            var clientId = await CreateClientAsync(suffix);
            var productId = await CreateProductAsync(suffix);

            for (int i = 0; i < 15; i++)
                await CreateOrderAsync(clientId, productId);

            var page1 = await (await _client.GetAsync($"/api/v1/clients/{clientId}/orders?page=1&size=10"))
                .Content.ReadFromJsonAsync<PagedOrderResponse>(JsonOptions);
            var page2 = await (await _client.GetAsync($"/api/v1/clients/{clientId}/orders?page=2&size=10"))
                .Content.ReadFromJsonAsync<PagedOrderResponse>(JsonOptions);

            Assert.Equal(15, page1!.TotalCount);
            Assert.Equal(10, page1.Items.Count);
            Assert.Equal(5, page2!.Items.Count);

            var page1Ids = page1.Items.Select(o => o.Id).ToHashSet();
            Assert.All(page2.Items, o => Assert.DoesNotContain(o.Id, page1Ids));
        }
    }
}

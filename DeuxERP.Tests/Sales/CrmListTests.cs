using DeuxERP.Application.DTOs;
using DeuxERP.Tests.DTOs;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace DeuxERP.Tests.Sales
{
    public class CrmListTests : BaseIntegrationTest
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public CrmListTests(IntegrationTestFactory<Program> factory) : base(factory) { }

        private async Task<Guid> CreateClientAsync(string suffix)
        {
            var res = await _client.PostAsJsonAsync("/api/v1/clients/new",
                new CreateClient($"Crm Client {suffix}", null));
            res.EnsureSuccessStatusCode();
            return (await res.Content.ReadFromJsonAsync<ClientResponse>(JsonOptions))!.Id;
        }

        private async Task<Guid> CreateProductAsync(string suffix, string name)
        {
            var form = new MultipartFormDataContent();
            form.Add(new StringContent($"{name} {suffix}"), "Name");
            form.Add(new StringContent("2000"), "Price");
            var res = await _client.PostAsync("/api/v1/products/new", form);
            res.EnsureSuccessStatusCode();
            return (await res.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions))!.Id;
        }

        private async Task<OrderResponse> CreateOrderAsync(Guid clientId, Guid productId, int price)
        {
            var req = new CreateOrderRequest(clientId, DateTime.UtcNow.AddDays(1),
                new List<CreateOrderItemRequest> { new(productId, 1, price, null, null, null) }, null);
            var res = await _client.PostAsJsonAsync("/api/v1/orders/new", req);
            res.EnsureSuccessStatusCode();
            return (await res.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions))!;
        }

        [Fact]
        public async Task GetList_ReturnsAggregatesAndLastOrderInfo_ExcludingCanceled()
        {
            await AuthenticateAsync();
            var suffix = DateTime.Now.Ticks.ToString().Substring(12);
            var clientId = await CreateClientAsync(suffix);
            var productA = await CreateProductAsync(suffix, "Bolo");
            var productB = await CreateProductAsync(suffix, "Torta");

            await CreateOrderAsync(clientId, productA, 3000);
            var lastOrder = await CreateOrderAsync(clientId, productB, 7000);

            var canceled = await CreateOrderAsync(clientId, productA, 9999);
            await _client.PatchAsync($"/api/v1/orders/{canceled.Id}/cancel", null);

            var res = await _client.GetAsync($"/api/v1/crm/list?search=Crm+Client+{suffix}");

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var paged = await res.Content.ReadFromJsonAsync<PagedCrmResponse>(JsonOptions);
            var entry = paged!.Items.FirstOrDefault(c => c.ClientId == clientId);

            Assert.NotNull(entry);
            Assert.Equal(2, entry.OrderCount);
            Assert.Equal(10000, entry.TotalSpend);
            Assert.Equal(5000, entry.AverageSpend);
            Assert.Equal(7000, entry.LastOrderInfo.TotalSpend);
            Assert.Contains(entry.LastOrderInfo.Products, p => p.StartsWith("Torta"));
            Assert.DoesNotContain(entry.LastOrderInfo.Products, p => p.StartsWith("Bolo"));
        }

        [Fact]
        public async Task GetList_ClientWithNoOrders_IsExcluded()
        {
            await AuthenticateAsync();
            var suffix = DateTime.Now.Ticks.ToString().Substring(12);
            var clientId = await CreateClientAsync(suffix);

            var res = await _client.GetAsync($"/api/v1/crm/list?search=Crm+Client+{suffix}");

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var paged = await res.Content.ReadFromJsonAsync<PagedCrmResponse>(JsonOptions);
            Assert.DoesNotContain(paged!.Items, c => c.ClientId == clientId);
        }

        [Fact]
        public async Task GetList_FiltersByLastOrderDateRange()
        {
            await AuthenticateAsync();
            var suffix = DateTime.Now.Ticks.ToString().Substring(12);
            var clientId = await CreateClientAsync(suffix);
            var productId = await CreateProductAsync(suffix, "Bolo");
            await CreateOrderAsync(clientId, productId, 5000);

            var now = DateTime.UtcNow;
            var searchQs = $"search=Crm+Client+{suffix}";

            var withinRange = await _client.GetAsync(
                $"/api/v1/crm/list?{searchQs}&lastOrderFrom={Uri.EscapeDataString(now.AddMinutes(-5).ToString("o"))}&lastOrderTo={Uri.EscapeDataString(now.AddMinutes(5).ToString("o"))}");
            Assert.Equal(HttpStatusCode.OK, withinRange.StatusCode);
            var withinPaged = await withinRange.Content.ReadFromJsonAsync<PagedCrmResponse>(JsonOptions);
            Assert.Contains(withinPaged!.Items, c => c.ClientId == clientId);

            var outsideRange = await _client.GetAsync(
                $"/api/v1/crm/list?{searchQs}&lastOrderTo={Uri.EscapeDataString(now.AddMinutes(-5).ToString("o"))}");
            Assert.Equal(HttpStatusCode.OK, outsideRange.StatusCode);
            var outsidePaged = await outsideRange.Content.ReadFromJsonAsync<PagedCrmResponse>(JsonOptions);
            Assert.DoesNotContain(outsidePaged!.Items, c => c.ClientId == clientId);
        }

        [Fact]
        public async Task GetList_ClientWithOnlyCanceledOrders_IsExcluded()
        {
            await AuthenticateAsync();
            var suffix = DateTime.Now.Ticks.ToString().Substring(12);
            var clientId = await CreateClientAsync(suffix);
            var productId = await CreateProductAsync(suffix, "Bolo");

            var order = await CreateOrderAsync(clientId, productId, 5000);
            await _client.PatchAsync($"/api/v1/orders/{order.Id}/cancel", null);

            var res = await _client.GetAsync($"/api/v1/crm/list?search=Crm+Client+{suffix}");

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var paged = await res.Content.ReadFromJsonAsync<PagedCrmResponse>(JsonOptions);
            Assert.DoesNotContain(paged!.Items, c => c.ClientId == clientId);
        }
    }
}

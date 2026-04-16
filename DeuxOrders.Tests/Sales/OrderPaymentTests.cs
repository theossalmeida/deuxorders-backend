using DeuxOrders.Application.DTOs;
using DeuxOrders.Tests.DTOs;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace DeuxOrders.Tests.Sales
{
    public class OrderPaymentTests : BaseIntegrationTest
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public OrderPaymentTests(IntegrationTestFactory<Program> factory) : base(factory) { }

        private async Task<(Guid clientId, Guid productId)> CreateClientAndProductAsync()
        {
            var suffix = DateTime.Now.Ticks.ToString().Substring(12);

            var clientRes = await _client.PostAsJsonAsync("/api/v1/clients/new",
                new CreateClient($"Cliente Pay Test {suffix}", "12345678901"));
            clientRes.EnsureSuccessStatusCode();
            var client = await clientRes.Content.ReadFromJsonAsync<ClientResponse>(JsonOptions);

            var productForm = new MultipartFormDataContent();
            productForm.Add(new StringContent($"Produto Pay Test {suffix}"), "Name");
            productForm.Add(new StringContent("2000"), "Price");
            var productRes = await _client.PostAsync("/api/v1/products/new", productForm);
            productRes.EnsureSuccessStatusCode();
            var product = await productRes.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);

            return (client!.Id, product!.Id);
        }

        private async Task<OrderResponse> CreateOrderWithItemAsync(Guid clientId, Guid productId)
        {
            var req = new CreateOrderRequest(clientId, DateTime.UtcNow.AddDays(1),
                new List<CreateOrderItemRequest> { new(productId, 1, 2000, null, null, null) }, null);
            var res = await _client.PostAsJsonAsync("/api/v1/orders/new", req);
            res.EnsureSuccessStatusCode();
            return (await res.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions))!;
        }

        [Fact]
        public async Task MarkAsPaid_ValidOrder_ReturnsPaidAtSet()
        {
            await AuthenticateAsAdminAsync();
            var (clientId, productId) = await CreateClientAndProductAsync();
            var order = await CreateOrderWithItemAsync(clientId, productId);

            var res = await _client.PatchAsync($"/api/v1/orders/{order.Id}/pay", null);

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var updated = await res.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);
            Assert.NotNull(updated!.PaidAt);
            Assert.NotNull(updated.PaidByUserName);
        }

        [Fact]
        public async Task MarkAsPaid_Twice_IsIdempotent()
        {
            await AuthenticateAsAdminAsync();
            var (clientId, productId) = await CreateClientAndProductAsync();
            var order = await CreateOrderWithItemAsync(clientId, productId);

            await _client.PatchAsync($"/api/v1/orders/{order.Id}/pay", null);
            var second = await _client.PatchAsync($"/api/v1/orders/{order.Id}/pay", null);

            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        }

        [Fact]
        public async Task MarkAsPaid_CanceledOrder_ReturnsBadRequest()
        {
            await AuthenticateAsAdminAsync();
            var (clientId, productId) = await CreateClientAndProductAsync();
            var order = await CreateOrderWithItemAsync(clientId, productId);

            await _client.PatchAsync($"/api/v1/orders/{order.Id}/cancel", null);
            var res = await _client.PatchAsync($"/api/v1/orders/{order.Id}/pay", null);

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }

        [Fact]
        public async Task UnmarkAsPaid_NeverPaidOrder_ReturnsBadRequest()
        {
            await AuthenticateAsAdminAsync();
            var (clientId, productId) = await CreateClientAndProductAsync();
            var order = await CreateOrderWithItemAsync(clientId, productId);

            var res = await _client.PatchAsJsonAsync($"/api/v1/orders/{order.Id}/unpay",
                new { Reason = "Motivo válido aqui" });

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }

        [Fact]
        public async Task UnmarkAsPaid_ShortReason_ReturnsBadRequest()
        {
            await AuthenticateAsAdminAsync();
            var (clientId, productId) = await CreateClientAndProductAsync();
            var order = await CreateOrderWithItemAsync(clientId, productId);
            await _client.PatchAsync($"/api/v1/orders/{order.Id}/pay", null);

            var res = await _client.PatchAsJsonAsync($"/api/v1/orders/{order.Id}/unpay",
                new { Reason = "ok" });

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }

        [Fact]
        public async Task UnmarkAsPaid_ValidReason_ClearsPaidAt()
        {
            await AuthenticateAsAdminAsync();
            var (clientId, productId) = await CreateClientAndProductAsync();
            var order = await CreateOrderWithItemAsync(clientId, productId);
            await _client.PatchAsync($"/api/v1/orders/{order.Id}/pay", null);

            var res = await _client.PatchAsJsonAsync($"/api/v1/orders/{order.Id}/unpay",
                new { Reason = "Motivo de reversão válido" });

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var updated = await res.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);
            Assert.Null(updated!.PaidAt);
        }

        [Fact]
        public async Task MarkAsPaid_ZeroValueOrder_ReturnsBadRequest()
        {
            await AuthenticateAsAdminAsync();
            var (clientId, productId) = await CreateClientAndProductAsync();

            var req = new CreateOrderRequest(clientId, DateTime.UtcNow.AddDays(1),
                new List<CreateOrderItemRequest> { new(productId, 1, 0, null, null, null) }, null);
            var orderRes = await _client.PostAsJsonAsync("/api/v1/orders/new", req);
            orderRes.EnsureSuccessStatusCode();
            var order = (await orderRes.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions))!;

            var res = await _client.PatchAsync($"/api/v1/orders/{order.Id}/pay", null);

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }

        [Fact]
        public async Task MarkAsPaid_NonAdminUser_ReturnsForbidden()
        {
            await AuthenticateAsync();
            var (clientId, productId) = await CreateClientAndProductAsync();
            var order = await CreateOrderWithItemAsync(clientId, productId);

            var res = await _client.PatchAsync($"/api/v1/orders/{order.Id}/pay", null);

            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        }
    }
}

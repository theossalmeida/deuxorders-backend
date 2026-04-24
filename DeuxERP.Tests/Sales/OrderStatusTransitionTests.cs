using DeuxERP.Application.DTOs;
using DeuxERP.Domain.Sales;
using DeuxERP.Tests.DTOs;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace DeuxERP.Tests.Sales
{
    public class OrderStatusTransitionTests : BaseIntegrationTest
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public OrderStatusTransitionTests(IntegrationTestFactory<Program> factory) : base(factory) { }

        private async Task<(Guid clientId, Guid productId)> CreateClientAndProductAsync()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];

            var clientRes = await _client.PostAsJsonAsync("/api/v1/clients/new",
                new CreateClient($"Cliente Trans {suffix}", "11999990000"));
            clientRes.EnsureSuccessStatusCode();
            var client = await clientRes.Content.ReadFromJsonAsync<ClientResponse>(JsonOptions);

            var productForm = new MultipartFormDataContent();
            productForm.Add(new StringContent($"Produto Trans {suffix}"), "Name");
            productForm.Add(new StringContent("1500"), "Price");
            var productRes = await _client.PostAsync("/api/v1/products/new", productForm);
            productRes.EnsureSuccessStatusCode();
            var product = await productRes.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);

            return (client!.Id, product!.Id);
        }

        private async Task<OrderResponse> CreateOrderAsync(Guid clientId, Guid productId, DateTime? deliveryDate = null)
        {
            var req = new CreateOrderRequest(clientId, deliveryDate ?? DateTime.UtcNow.AddDays(1),
                new List<CreateOrderItemRequest> { new(productId, 1, 1500, null, null, null) }, null);
            var res = await _client.PostAsJsonAsync("/api/v1/orders/new", req);
            res.EnsureSuccessStatusCode();
            return (await res.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions))!;
        }

        private async Task<OrderResponse> GetOrderAsync(Guid orderId)
        {
            var res = await _client.GetAsync($"/api/v1/orders/{orderId}");
            res.EnsureSuccessStatusCode();
            return (await res.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions))!;
        }

        [Fact]
        public async Task MarkAsCompleted_FromReceived_SetsCompleted()
        {
            await AuthenticateAsync();
            var (clientId, productId) = await CreateClientAndProductAsync();
            var order = await CreateOrderAsync(clientId, productId);
            Assert.Equal(OrderStatus.Received, order.Status);

            var res = await _client.PatchAsync($"/api/v1/orders/{order.Id}/complete", null);

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var updated = await GetOrderAsync(order.Id);
            Assert.Equal(OrderStatus.Completed, updated.Status);
        }

        [Fact]
        public async Task UpdateOrder_WithPastDeliveryDateAndCompletedStatus_Succeeds()
        {
            await AuthenticateAsync();
            var (clientId, productId) = await CreateClientAndProductAsync();
            var pastDeliveryDate = DateTime.UtcNow.AddDays(-3);
            var order = await CreateOrderAsync(clientId, productId, pastDeliveryDate);

            var res = await _client.PutAsJsonAsync($"/api/v1/orders/{order.Id}",
                new UpdateOrderRequest(pastDeliveryDate, (int)OrderStatus.Completed, null, null));

            res.EnsureSuccessStatusCode();
            var updated = await GetOrderAsync(order.Id);
            Assert.Equal(OrderStatus.Completed, updated.Status);
            Assert.Equal(pastDeliveryDate.Date, updated.DeliveryDate.Date);
        }

        [Fact]
        public async Task MarkAsCompleted_AlreadyCompleted_IsIdempotent()
        {
            await AuthenticateAsync();
            var (clientId, productId) = await CreateClientAndProductAsync();
            var order = await CreateOrderAsync(clientId, productId);

            await _client.PatchAsync($"/api/v1/orders/{order.Id}/complete", null);
            var second = await _client.PatchAsync($"/api/v1/orders/{order.Id}/complete", null);

            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            var updated = await GetOrderAsync(order.Id);
            Assert.Equal(OrderStatus.Completed, updated.Status);
        }

        [Fact]
        public async Task MarkAsCanceled_FromCompleted_ReturnsBadRequest()
        {
            await AuthenticateAsync();
            var (clientId, productId) = await CreateClientAndProductAsync();
            var order = await CreateOrderAsync(clientId, productId);

            await _client.PatchAsync($"/api/v1/orders/{order.Id}/complete", null);
            var res = await _client.PatchAsync($"/api/v1/orders/{order.Id}/cancel", null);

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }

        [Fact]
        public async Task MarkAsCanceled_AlreadyCanceled_ReturnsBadRequest()
        {
            await AuthenticateAsync();
            var (clientId, productId) = await CreateClientAndProductAsync();
            var order = await CreateOrderAsync(clientId, productId);

            await _client.PatchAsync($"/api/v1/orders/{order.Id}/cancel", null);
            var res = await _client.PatchAsync($"/api/v1/orders/{order.Id}/cancel", null);

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }

        [Fact]
        public async Task CancelOrder_FromPreparing_Succeeds()
        {
            await AuthenticateAsync();
            var (clientId, productId) = await CreateClientAndProductAsync();
            var order = await CreateOrderAsync(clientId, productId);

            await _client.PutAsJsonAsync($"/api/v1/orders/{order.Id}",
                new UpdateOrderRequest(null, (int)OrderStatus.Preparing, null, null));

            var res = await _client.PatchAsync($"/api/v1/orders/{order.Id}/cancel", null);

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var updated = await GetOrderAsync(order.Id);
            Assert.Equal(OrderStatus.Canceled, updated.Status);
        }

        [Fact]
        public async Task UpdateOrder_ReopenCompletedOrder_Succeeds()
        {
            await AuthenticateAsync();
            var (clientId, productId) = await CreateClientAndProductAsync();
            var order = await CreateOrderAsync(clientId, productId);

            await _client.PatchAsync($"/api/v1/orders/{order.Id}/complete", null);

            var res = await _client.PutAsJsonAsync($"/api/v1/orders/{order.Id}",
                new UpdateOrderRequest(null, (int)OrderStatus.Received, null, null));

            res.EnsureSuccessStatusCode();
            var updated = await GetOrderAsync(order.Id);
            Assert.Equal(OrderStatus.Received, updated.Status);
        }

        [Fact]
        public async Task UpdateOrder_ReopenCanceledOrder_Succeeds()
        {
            await AuthenticateAsync();
            var (clientId, productId) = await CreateClientAndProductAsync();
            var order = await CreateOrderAsync(clientId, productId);

            await _client.PatchAsync($"/api/v1/orders/{order.Id}/cancel", null);

            var res = await _client.PutAsJsonAsync($"/api/v1/orders/{order.Id}",
                new UpdateOrderRequest(null, (int)OrderStatus.Preparing, null, null));

            res.EnsureSuccessStatusCode();
            var updated = await GetOrderAsync(order.Id);
            Assert.Equal(OrderStatus.Preparing, updated.Status);
        }
    }
}

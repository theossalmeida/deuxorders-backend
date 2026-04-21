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
        public OrderStatusTransitionTests(IntegrationTestFactory<Program> factory) : base(factory) { }

        [Fact]
        public async Task UpdateOrder_ShouldAllowReopeningCompletedAndCanceledOrders()
        {
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };

            await AuthenticateAsync();

            var clientRequest = new CreateClient("Cliente Reabertura", "11999990000");
            var clientRes = await _client.PostAsJsonAsync("/api/v1/clients/new", clientRequest);
            clientRes.EnsureSuccessStatusCode();
            var customer = (await clientRes.Content.ReadFromJsonAsync<ClientResponse>(jsonOptions))!;

            var productForm = new MultipartFormDataContent();
            productForm.Add(new StringContent("Produto Reabertura"), "Name");
            productForm.Add(new StringContent("1500"), "Price");
            var productRes = await _client.PostAsync("/api/v1/products/new", productForm);
            productRes.EnsureSuccessStatusCode();
            var product = (await productRes.Content.ReadFromJsonAsync<ProductResponse>(jsonOptions))!;

            var completedOrderRes = await _client.PostAsJsonAsync(
                "/api/v1/orders/new",
                new CreateOrderRequest(
                    customer.Id,
                    DateTime.UtcNow.AddDays(1),
                    new List<CreateOrderItemRequest> { new(product.Id, 1, 1500, null, null, null) },
                    null));
            completedOrderRes.EnsureSuccessStatusCode();
            var completedOrder = (await completedOrderRes.Content.ReadFromJsonAsync<OrderResponse>(jsonOptions))!;

            var canceledOrderRes = await _client.PostAsJsonAsync(
                "/api/v1/orders/new",
                new CreateOrderRequest(
                    customer.Id,
                    DateTime.UtcNow.AddDays(1),
                    new List<CreateOrderItemRequest> { new(product.Id, 1, 1500, null, null, null) },
                    null));
            canceledOrderRes.EnsureSuccessStatusCode();
            var canceledOrder = (await canceledOrderRes.Content.ReadFromJsonAsync<OrderResponse>(jsonOptions))!;

            var completeRes = await _client.PatchAsync($"/api/v1/orders/{completedOrder.Id}/complete", null);
            completeRes.EnsureSuccessStatusCode();

            var cancelRes = await _client.PatchAsync($"/api/v1/orders/{canceledOrder.Id}/cancel", null);
            cancelRes.EnsureSuccessStatusCode();

            var reopenCompletedRes = await _client.PutAsJsonAsync(
                $"/api/v1/orders/{completedOrder.Id}",
                new UpdateOrderRequest(null, (int)OrderStatus.Received, null, null));
            reopenCompletedRes.EnsureSuccessStatusCode();
            var reopenedCompleted = (await reopenCompletedRes.Content.ReadFromJsonAsync<OrderResponse>(jsonOptions))!;
            Assert.Equal(OrderStatus.Received, reopenedCompleted.Status);

            var reopenCanceledRes = await _client.PutAsJsonAsync(
                $"/api/v1/orders/{canceledOrder.Id}",
                new UpdateOrderRequest(null, (int)OrderStatus.Preparing, null, null));
            reopenCanceledRes.EnsureSuccessStatusCode();
            var reopenedCanceled = (await reopenCanceledRes.Content.ReadFromJsonAsync<OrderResponse>(jsonOptions))!;
            Assert.Equal(OrderStatus.Preparing, reopenedCanceled.Status);
        }
    }
}

using DeuxERP.Application.DTOs;
using DeuxERP.Domain.Sales;
using DeuxERP.Tests.DTOs;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace DeuxERP.Tests
{
    public class OrderIntegrationFlowTests : BaseIntegrationTest
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public OrderIntegrationFlowTests(IntegrationTestFactory<Program> factory) : base(factory) { }

        private async Task<OrderResponse> GetOrderAsync(Guid id)
        {
            var res = await _client.GetAsync($"/api/v1/orders/{id}");
            res.EnsureSuccessStatusCode();
            return (await res.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions))!;
        }

        [Fact]
        public async Task Order_CompleteLifeCycle_ShouldFollowBusinessRules()
        {
            await AuthenticateAsync();

            var clientRes = await _client.PostAsJsonAsync("/api/v1/clients/new",
                new CreateClient("Cliente Teste Flow", "12345678901"));
            clientRes.EnsureSuccessStatusCode();
            var customer = (await clientRes.Content.ReadFromJsonAsync<ClientResponse>(JsonOptions))!;

            var productForm = new MultipartFormDataContent();
            productForm.Add(new StringContent("Produto Teste"), "Name");
            productForm.Add(new StringContent("1000"), "Price");
            var productRes = await _client.PostAsync("/api/v1/products/new", productForm);
            productRes.EnsureSuccessStatusCode();
            var product = (await productRes.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions))!;

            var order1Res = await _client.PostAsJsonAsync("/api/v1/orders/new",
                new CreateOrderRequest(customer.Id, DateTime.UtcNow.AddDays(1),
                    new List<CreateOrderItemRequest> { new(product.Id, 5, 1000, "obs 02", null, null) }, null));
            Assert.Equal(HttpStatusCode.Created, order1Res.StatusCode);

            var order2Res = await _client.PostAsJsonAsync("/api/v1/orders/new",
                new CreateOrderRequest(customer.Id, DateTime.UtcNow.AddDays(1),
                    new List<CreateOrderItemRequest> { new(product.Id, 5, 1000, "obs 01", null, null) }, null));
            Assert.Equal(HttpStatusCode.Created, order2Res.StatusCode);

            var order1 = (await order1Res.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions))!;
            var order2 = (await order2Res.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions))!;

            Assert.Equal(OrderStatus.Received, order1.Status);
            Assert.Equal(5000, order1.TotalPaid);
            Assert.Equal(5000, order1.TotalValue);
            Assert.Single(order1.Items);
            Assert.Equal(5, order1.Items.First().Quantity);

            var allOrdersRes = await _client.GetAsync("/api/v1/orders/all");
            allOrdersRes.EnsureSuccessStatusCode();
            var paged = (await allOrdersRes.Content.ReadFromJsonAsync<PagedOrderResponse>(JsonOptions))!;
            var fetched = paged.Items.First(o => o.Id == order1.Id);
            Assert.Equal("Cliente Teste Flow", fetched.ClientName);
            Assert.Equal("Produto Teste", fetched.Items.First().ProductName);

            var updateOk = await _client.PatchAsJsonAsync(
                $"/api/v1/orders/{order1.Id}/items/{product.Id}/quantity", new { Increment = 2 });
            updateOk.EnsureSuccessStatusCode();

            var afterIncrement = await GetOrderAsync(order1.Id);
            Assert.Equal(7, afterIncrement.Items.First().Quantity);
            Assert.Equal(7000, afterIncrement.TotalPaid);
            Assert.Equal(7000, afterIncrement.TotalValue);

            var updateError = await _client.PatchAsJsonAsync(
                $"/api/v1/orders/{order2.Id}/items/{product.Id}/quantity", new { Increment = -7 });
            Assert.Equal(HttpStatusCode.BadRequest, updateError.StatusCode);

            var order2Unchanged = await GetOrderAsync(order2.Id);
            Assert.Equal(5, order2Unchanged.Items.First().Quantity);
            Assert.Equal(5000, order2Unchanged.TotalPaid);

            var cancelItem = await _client.PatchAsync(
                $"/api/v1/orders/{order1.Id}/items/{product.Id}/cancel", null);
            cancelItem.EnsureSuccessStatusCode();

            var afterCancelItem = await GetOrderAsync(order1.Id);
            Assert.True(afterCancelItem.Items.First().ItemCanceled);
            Assert.Equal(0, afterCancelItem.TotalPaid);
            Assert.Equal(0, afterCancelItem.TotalValue);

            var cancelOrder = await _client.PatchAsync($"/api/v1/orders/{order1.Id}/cancel", null);
            cancelOrder.EnsureSuccessStatusCode();

            var afterCancel = await GetOrderAsync(order1.Id);
            Assert.Equal(OrderStatus.Canceled, afterCancel.Status);

            var completeOrder = await _client.PatchAsync($"/api/v1/orders/{order2.Id}/complete", null);
            completeOrder.EnsureSuccessStatusCode();

            var afterComplete = await GetOrderAsync(order2.Id);
            Assert.Equal(OrderStatus.Completed, afterComplete.Status);
            Assert.Equal(5000, afterComplete.TotalPaid);
        }
    }
}

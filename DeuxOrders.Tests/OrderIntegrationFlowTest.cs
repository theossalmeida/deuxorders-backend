using DeuxOrders.Application.DTOs;
using DeuxOrders.Tests.DTOs;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeuxOrders.Tests
{
    public class OrderIntegrationFlowTests : BaseIntegrationTest
    {
        public OrderIntegrationFlowTests(IntegrationTestFactory<Program> factory) : base(factory) { }

        [Fact]
        public async Task Order_CompleteLifeCycle_ShouldFollowBusinessRules()
        {
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };

            // 1st TEST: Create user and login
            Console.WriteLine("1st TEST: Starting...");
            await AuthenticateAsync();
            Console.WriteLine("1st TEST: Completed");

            // 2nd TEST: Create a client
            Console.WriteLine("2nd TEST: Starting...");
            var clientRequest = new CreateClient("Cliente Teste Flow", "12345678901");
            var clientRes = await _client.PostAsJsonAsync("/api/v1/clients/new", clientRequest);
            clientRes.EnsureSuccessStatusCode();
            var customer = await clientRes.Content.ReadFromJsonAsync<ClientResponse>();
            var customerId = customer!.Id;
            Console.WriteLine("2nd TEST: Completed!");

            // 3rd TEST: Create a product
            Console.WriteLine("3rd TEST: Starting...");
            var productRequest = new CreateProduct("Produto Teste", 1000);
            var productRes = await _client.PostAsJsonAsync("/api/v1/products/new", productRequest);
            productRes.EnsureSuccessStatusCode();
            var product = await productRes.Content.ReadFromJsonAsync<ProductResponse>();
            var productId = product!.Id;
            Console.WriteLine("2nd TEST: Completed!");

            // 4th TEST: Create 2 orders
            Console.WriteLine("4th TEST: Starting...");
            var order1Req = new CreateOrderRequest(customerId, new List<CreateOrderItemRequest>
                { new(productId, 5, 1000) });
            var order2Req = new CreateOrderRequest(customerId, new List<CreateOrderItemRequest>
                { new(productId, 5, 1000) });


            var resOrder1 = await _client.PostAsJsonAsync("/api/v1/orders/new", order1Req);
            resOrder1.StatusCode.Should().Be(HttpStatusCode.Created);
            Console.WriteLine("4th TEST: 1st order created");

            var resOrder2 = await _client.PostAsJsonAsync("/api/v1/orders/new", order2Req);
            resOrder2.StatusCode.Should().Be(HttpStatusCode.Created);
            Console.WriteLine("4th TEST: 2nd order created");

            var order1 = await resOrder1.Content.ReadFromJsonAsync<OrderResponse>(jsonOptions);
            var order2 = await resOrder2.Content.ReadFromJsonAsync<OrderResponse>(jsonOptions);
            Console.WriteLine("4th TEST: Completed!");

            // 5th TEST: Get all orders
            Console.WriteLine("5th TEST: Starting...");
            var allOrdersRes = await _client.GetAsync("/api/v1/orders/all");
            allOrdersRes.EnsureSuccessStatusCode();
            var pagedResponse = await allOrdersRes.Content.ReadFromJsonAsync<PagedOrderResponse>(jsonOptions);
            var allOrders = pagedResponse!.Items;
            allOrders.Should().NotBeEmpty();
            allOrders.Should().Contain(o => o.Id == order1!.Id);
            Console.WriteLine("5th TEST: Completed!");

            // 6th TEST: Edit quantity
            Console.WriteLine("6th TEST: starting...");
            // Success: (5 - 3 = 2)
            var updateOk = await _client.PatchAsJsonAsync($"/api/v1/orders/{order1!.Id}/items/{productId}/quantity", new { Increment = 2 });
            updateOk.IsSuccessStatusCode.Should().BeTrue();
            Console.WriteLine("6th TEST: Case 1 completed!");

            // Error: (5 - 7 = -2) - 400 BadRequest
            var updateError = await _client.PatchAsJsonAsync($"/api/v1/orders/{order2!.Id}/items/{productId}/quantity", new { Increment = -7 });
            updateError.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            Console.WriteLine("6th TEST: Case 2 completed!");
            Console.WriteLine("6th TEST: Completed!");

            // 7th TEST: Cancel just 1 item from order
            Console.WriteLine("7th TEST: Starting...");
            var cancelItem = await _client.PatchAsync($"/api/v1/orders/{order1.Id}/items/{productId}/cancel", null);
            cancelItem.EnsureSuccessStatusCode();
            Console.WriteLine("7th TEST: Completed!");

            // 8th TEST: Cancel entire order
            Console.WriteLine("8th TEST: Starting...");
            var cancelOrder = await _client.PatchAsync($"/api/v1/orders/{order1.Id}/cancel", null);
            cancelOrder.EnsureSuccessStatusCode();
            Console.WriteLine("8th TEST: Completed!");

            // 9th TEST: Complete order
            Console.WriteLine("9th TEST: Starting...");
            var completeOrder = await _client.PatchAsync($"/api/v1/orders/{order2.Id}/complete", null);
            completeOrder.EnsureSuccessStatusCode();
            Console.WriteLine("9th TEST: Completed!");
        }
    }
}
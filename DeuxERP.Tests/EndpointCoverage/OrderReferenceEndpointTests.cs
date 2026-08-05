using DeuxERP.Application.DTOs;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeuxERP.Tests.EndpointCoverage;

public class OrderReferenceEndpointTests : BaseIntegrationTest
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public OrderReferenceEndpointTests(IntegrationTestFactory<Program> factory) : base(factory) { }

    [Fact]
    [Trait("TestSet", "Storage")]
    public async Task ReferenceLifecycle_ValidatesTypeConsumesOnceRemovesAndCompensatesDeleteFailure()
    {
        await AuthenticateAsAdminAsync();
        _factory.Storage.Reset();

        var invalid = await _client.PostAsJsonAsync("/api/v1/orders/references/presigned-url",
            new { FileName = "payload.exe", ContentType = "application/octet-stream", OrderId = (Guid?)null });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var upload = await _client.PostAsJsonAsync("/api/v1/orders/references/presigned-url",
            new { FileName = "reference.png", ContentType = "image/png", OrderId = (Guid?)null });
        upload.EnsureSuccessStatusCode();
        using var uploadJson = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
        var objectKey = uploadJson.RootElement.GetProperty("objectKey").GetString()!;
        Assert.StartsWith("order-references/", objectKey);

        var (clientId, productId) = await CreateClientAndProductAsync();
        var request = new CreateOrderRequest(clientId, DateTime.UtcNow.AddDays(1),
            [new CreateOrderItemRequest(productId, 1, 1200, null, null, null)], [objectKey]);
        var createdResponse = await _client.PostAsJsonAsync("/api/v1/orders/new", request);
        createdResponse.EnsureSuccessStatusCode();
        var order = (await createdResponse.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions))!;
        Assert.Contains(order.References!, url => url.EndsWith(objectKey, StringComparison.Ordinal));

        var reused = await _client.PostAsJsonAsync("/api/v1/orders/new", request);
        Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);

        _factory.Storage.FailDeletes = true;
        var failedDelete = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Delete,
            $"/api/v1/orders/{order.Id}/references")
        {
            Content = JsonContent.Create(new { ObjectKey = objectKey })
        });
        Assert.Equal(HttpStatusCode.BadGateway, failedDelete.StatusCode);

        var restored = await _client.GetFromJsonAsync<OrderResponse>($"/api/v1/orders/{order.Id}", JsonOptions);
        Assert.Contains(restored!.References!, url => url.EndsWith(objectKey, StringComparison.Ordinal));

        _factory.Storage.FailDeletes = false;
        var removed = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Delete,
            $"/api/v1/orders/{order.Id}/references")
        {
            Content = JsonContent.Create(new { ObjectKey = objectKey })
        });
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.Contains(objectKey, _factory.Storage.DeletedKeys);

        Assert.Equal(HttpStatusCode.NoContent,
            (await _client.DeleteAsync($"/api/v1/orders/{order.Id}")).StatusCode);
        var canceled = await _client.GetFromJsonAsync<OrderResponse>($"/api/v1/orders/{order.Id}", JsonOptions);
        Assert.Equal(DeuxERP.Domain.Sales.OrderStatus.Canceled, canceled!.Status);
    }

    private async Task<(Guid ClientId, Guid ProductId)> CreateClientAndProductAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var clientResponse = await _client.PostAsJsonAsync("/api/v1/clients/new",
            new CreateClient($"Reference Client {suffix}", null));
        var client = (await clientResponse.Content.ReadFromJsonAsync<ClientResponse>())!;

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent($"Reference Product {suffix}"), "Name");
        form.Add(new StringContent("1500"), "Price");
        var productResponse = await _client.PostAsync("/api/v1/products/new", form);
        var product = (await productResponse.Content.ReadFromJsonAsync<ProductResponse>())!;
        return (client.Id, product.Id);
    }
}

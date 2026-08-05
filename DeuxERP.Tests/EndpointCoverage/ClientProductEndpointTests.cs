using DeuxERP.Application.DTOs;
using DeuxERP.Domain.Sales;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeuxERP.Tests.EndpointCoverage;

public class ClientProductEndpointTests : BaseIntegrationTest
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ClientProductEndpointTests(IntegrationTestFactory<Program> factory) : base(factory) { }

    [Fact]
    [Trait("TestSet", "ClientsAndProducts")]
    public async Task ClientLifecycle_CoversDetailsUpdateStatusDropdownAndOrderEligibility()
    {
        await AuthenticateAsAdminAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var create = await _client.PostAsJsonAsync("/api/v1/clients/new",
            new CreateClient($"Client {suffix}", "11999999999"));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var client = (await create.Content.ReadFromJsonAsync<ClientResponse>(JsonOptions))!;

        var update = await _client.PutAsJsonAsync($"/api/v1/clients/{client.Id}",
            new UpdateClient($"Updated {suffix}", "11888888888"));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var details = await _client.GetFromJsonAsync<ClientDetailResponse>(
            $"/api/v1/clients/{client.Id}?orders=true&includeStats=true&page=0&size=500", JsonOptions);
        Assert.Equal($"Updated {suffix}", details!.Name);
        Assert.NotNull(details.Stats);
        Assert.NotNull(details.Orders);
        Assert.Equal(100, details.Orders!.PageSize);

        Assert.Equal(HttpStatusCode.OK,
            (await _client.PatchAsync($"/api/v1/clients/{client.Id}/inactive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _client.PatchAsync($"/api/v1/clients/{client.Id}/inactive", null)).StatusCode);

        var inactiveDropdown = await _client.GetFromJsonAsync<List<JsonElement>>(
            "/api/v1/clients/dropdown?status=false", JsonOptions);
        Assert.Contains(inactiveDropdown!, item => item.GetProperty("id").GetGuid() == client.Id);

        var product = await CreateProductAsync($"Product {suffix}");
        var rejectedOrder = await _client.PostAsJsonAsync("/api/v1/orders/new",
            new CreateOrderRequest(client.Id, DateTime.UtcNow.AddDays(1),
                [new CreateOrderItemRequest(product.Id, 1, product.Price, null, null, null)], null));
        Assert.Equal(HttpStatusCode.BadRequest, rejectedOrder.StatusCode);

        Assert.Equal(HttpStatusCode.OK,
            (await _client.PatchAsync($"/api/v1/clients/{client.Id}/active", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _client.PatchAsync($"/api/v1/clients/{client.Id}/active", null)).StatusCode);

        var deletable = await _client.PostAsJsonAsync("/api/v1/clients/new",
            new CreateClient($"Delete {suffix}", null));
        var deletableClient = (await deletable.Content.ReadFromJsonAsync<ClientResponse>(JsonOptions))!;
        Assert.Equal(HttpStatusCode.NoContent,
            (await _client.DeleteAsync($"/api/v1/clients/{deletableClient.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync($"/api/v1/clients/{deletableClient.Id}")).StatusCode);
    }

    [Fact]
    [Trait("TestSet", "ClientsAndProducts")]
    public async Task ProductLifecycle_CoversDetailsUpdateFiltersStatusImageAndDeletionRules()
    {
        await AuthenticateAsAdminAsync();
        _factory.Storage.Reset();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var product = await CreateProductAsync($"Product {suffix}", includeImage: true);
        Assert.Single(_factory.Storage.UploadedKeys);

        var fetched = await _client.GetFromJsonAsync<ProductResponse>($"/api/v1/products/{product.Id}", JsonOptions);
        Assert.NotNull(fetched!.Image);

        using var updateForm = new MultipartFormDataContent();
        updateForm.Add(new StringContent($"Updated {suffix}"), "Name");
        updateForm.Add(new StringContent("2750"), "Price");
        updateForm.Add(new StringContent("Cake"), "Category");
        updateForm.Add(new StringContent("L"), "Size");
        var update = await _client.PutAsync($"/api/v1/products/{product.Id}", updateForm);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        Assert.Equal(HttpStatusCode.OK,
            (await _client.PatchAsync($"/api/v1/products/{product.Id}/inactive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _client.PatchAsync($"/api/v1/products/{product.Id}/inactive", null)).StatusCode);

        var orderClientResponse = await _client.PostAsJsonAsync("/api/v1/clients/new",
            new CreateClient($"Order Client {suffix}", null));
        var orderClient = (await orderClientResponse.Content.ReadFromJsonAsync<ClientResponse>(JsonOptions))!;
        var inactiveProductOrder = await _client.PostAsJsonAsync("/api/v1/orders/new",
            new CreateOrderRequest(orderClient.Id, DateTime.UtcNow.AddDays(1),
                [new CreateOrderItemRequest(product.Id, 1, 1000, null, null, null)], null));
        Assert.Equal(HttpStatusCode.BadRequest, inactiveProductOrder.StatusCode);

        var inactive = await _client.GetFromJsonAsync<List<JsonElement>>(
            "/api/v1/products/dropdown?status=false", JsonOptions);
        Assert.Contains(inactive!, item => item.GetProperty("id").GetGuid() == product.Id);

        using var list = await _client.GetAsync($"/api/v1/products/all?search=UPDATED+{suffix}&status=false");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var listJson = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Equal(1, listJson.RootElement.GetProperty("totalCount").GetInt32());

        Assert.Equal(HttpStatusCode.OK,
            (await _client.PatchAsync($"/api/v1/products/{product.Id}/active", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _client.DeleteAsync($"/api/v1/products/{product.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await _client.DeleteAsync($"/api/v1/products/{product.Id}/image")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await _client.DeleteAsync($"/api/v1/products/{product.Id}")).StatusCode);
    }

    [Fact]
    [Trait("TestSet", "Storage")]
    public async Task ProductImage_RejectsInvalidTypeAndRestoresReferenceWhenStorageDeleteFails()
    {
        await AuthenticateAsAdminAsync();
        _factory.Storage.Reset();

        using var invalidForm = ProductForm("Invalid image", 1000, new ByteArrayContent([1, 2, 3]), "malware.exe", "application/octet-stream");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _client.PostAsync("/api/v1/products/new", invalidForm)).StatusCode);

        var product = await CreateProductAsync($"Storage {Guid.NewGuid():N}", includeImage: true);
        _factory.Storage.FailDeletes = true;
        Assert.Equal(HttpStatusCode.BadGateway,
            (await _client.DeleteAsync($"/api/v1/products/{product.Id}/image")).StatusCode);

        var restored = await _client.GetFromJsonAsync<ProductResponse>($"/api/v1/products/{product.Id}", JsonOptions);
        Assert.NotNull(restored!.Image);
    }

    private async Task<ProductResponse> CreateProductAsync(string name, bool includeImage = false)
    {
        using var form = includeImage
            ? ProductForm(name, 2000, new ByteArrayContent([137, 80, 78, 71]), "image.png", "image/png")
            : ProductForm(name, 2000);
        var response = await _client.PostAsync("/api/v1/products/new", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions))!;
    }

    private static MultipartFormDataContent ProductForm(
        string name, int price, HttpContent? image = null, string? fileName = null, string? contentType = null)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(name), "Name");
        form.Add(new StringContent(price.ToString()), "Price");
        if (image != null)
        {
            image.Headers.ContentType = new(contentType!);
            form.Add(image, "Image", fileName!);
        }
        return form;
    }
}

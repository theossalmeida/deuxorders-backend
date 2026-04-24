using DeuxERP.API.Models;
using DeuxERP.Application.DTOs;
using DeuxERP.Domain.Inventory;
using DeuxERP.Domain.Sales;
using DeuxERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeuxERP.Tests
{
    public class InventoryIntegrationTests : BaseIntegrationTest
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public InventoryIntegrationTests(IntegrationTestFactory<Program> factory) : base(factory) { }

        private sealed record PagedInventoryResponse(
            List<InventoryMaterialResponse> Items,
            int TotalCount,
            int PageNumber,
            int PageSize);

        private static string NewSuffix() => Guid.NewGuid().ToString("N")[..8];

        private async Task<ClientResponse> CreateClientAsync(string suffix)
        {
            var response = await _client.PostAsJsonAsync(
                "/api/v1/clients/new",
                new CreateClient($"Inventory Client {suffix}", $"11999{suffix[..4]}"));

            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<ClientResponse>(JsonOptions))!;
        }

        private async Task<ProductResponse> CreateProductAsync(string suffix, string? name = null, int price = 2000)
        {
            var form = new MultipartFormDataContent();
            form.Add(new StringContent(name ?? $"Inventory Product {suffix}"), "Name");
            form.Add(new StringContent(price.ToString()), "Price");

            var response = await _client.PostAsync("/api/v1/products/new", form);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions))!;
        }

        private async Task<InventoryMaterialResponse> CreateMaterialAsync(string suffix, string baseName, int quantity, long totalCost, MeasureUnit measureUnit)
        {
            var response = await _client.PostAsJsonAsync(
                "/api/v1/inventory/new",
                new CreateMaterialRequest($"{baseName} {suffix}", quantity, totalCost, measureUnit));

            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<InventoryMaterialResponse>(JsonOptions))!;
        }

        private async Task<InventoryMaterialResponse> GetMaterialAsync(Guid materialId)
        {
            var response = await _client.GetAsync($"/api/v1/inventory/{materialId}");
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<InventoryMaterialResponse>(JsonOptions))!;
        }

        private async Task<ProductRecipeResponse> SetRecipeAsync(Guid productId, params RecipeItemRequest[] items)
        {
            var response = await _client.PutAsJsonAsync(
                $"/api/v1/products/{productId}/recipe",
                new SetRecipeRequest(items.ToList()));

            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<ProductRecipeResponse>(JsonOptions))!;
        }

        private async Task<ProductRecipeResponse> GetRecipeAsync(Guid productId)
        {
            var response = await _client.GetAsync($"/api/v1/products/{productId}/recipe");
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<ProductRecipeResponse>(JsonOptions))!;
        }

        private async Task<ProductRecipeOptionResponse> SetRecipeOptionAsync(
            Guid productId,
            ProductRecipeOptionType type,
            string name,
            params RecipeItemRequest[] items)
        {
            var response = await _client.PutAsJsonAsync(
                $"/api/v1/products/{productId}/recipe-options",
                new SetRecipeOptionRequest(type, name, items.ToList()));

            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<ProductRecipeOptionResponse>(JsonOptions))!;
        }

        private async Task<ProductRecipeOptionsResponse> GetRecipeOptionsAsync(Guid productId)
        {
            var response = await _client.GetAsync($"/api/v1/products/{productId}/recipe-options");
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<ProductRecipeOptionsResponse>(JsonOptions))!;
        }

        private async Task<OrderResponse> CreateOrderAsync(Guid clientId, params CreateOrderItemRequest[] items)
        {
            var response = await _client.PostAsJsonAsync(
                "/api/v1/orders/new",
                new CreateOrderRequest(clientId, DateTime.UtcNow.AddDays(1), items.ToList(), null));

            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions))!;
        }

        private async Task<(OrderResponse Order, List<string> Warnings)> ReadOrderPayloadAsync(HttpResponseMessage response)
        {
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(payload);

            var root = document.RootElement;
            var orderElement = root;
            var warnings = new List<string>();

            if (root.TryGetProperty("response", out var responseElement))
            {
                orderElement = responseElement;

                if (root.TryGetProperty("warnings", out var warningsElement))
                {
                    warnings = warningsElement
                        .EnumerateArray()
                        .Select(item => item.GetString()!)
                        .ToList();
                }
            }
            else if (root.TryGetProperty("warnings", out var inlineWarningsElement))
            {
                warnings = inlineWarningsElement
                    .EnumerateArray()
                    .Select(item => item.GetString()!)
                    .ToList();
            }

            var order = JsonSerializer.Deserialize<OrderResponse>(orderElement.GetRawText(), JsonOptions)!;
            return (order, warnings);
        }

        private async Task<InventoryMaterial> LoadMaterialEntityAsync(Guid materialId)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.InventoryMaterials.AsNoTracking().SingleAsync(material => material.Id == materialId);
        }

        [Fact]
        public async Task MaterialCrud_ShouldCreateUpdateListToggleStatusAndReturnDropdown()
        {
            await AuthenticateAsync();
            var suffix = NewSuffix();

            var created = await CreateMaterialAsync(suffix, "Farinha", 2500, 10000, MeasureUnit.G);
            Assert.Equal($"Farinha {suffix}", created.Name);
            Assert.Equal(2500, created.Quantity);
            Assert.Equal(4, created.UnitCost);
            Assert.True(created.Status);

            var byId = await GetMaterialAsync(created.Id);
            Assert.Equal(created.Id, byId.Id);
            Assert.Equal("G", byId.MeasureUnit);

            var updateResponse = await _client.PutAsJsonAsync(
                $"/api/v1/inventory/{created.Id}",
                new UpdateMaterialRequest($"Farinha Integral {suffix}", MeasureUnit.U));
            updateResponse.EnsureSuccessStatusCode();
            var updated = (await updateResponse.Content.ReadFromJsonAsync<InventoryMaterialResponse>(JsonOptions))!;
            Assert.Equal($"Farinha Integral {suffix}", updated.Name);
            Assert.Equal("U", updated.MeasureUnit);

            var deactivateResponse = await _client.PatchAsync($"/api/v1/inventory/{created.Id}/inactive", null);
            deactivateResponse.EnsureSuccessStatusCode();
            var inactive = (await deactivateResponse.Content.ReadFromJsonAsync<InventoryMaterialResponse>(JsonOptions))!;
            Assert.False(inactive.Status);

            var listResponse = await _client.GetAsync($"/api/v1/inventory/all?search={suffix}&status=false");
            listResponse.EnsureSuccessStatusCode();
            var paged = (await listResponse.Content.ReadFromJsonAsync<PagedInventoryResponse>(JsonOptions))!;
            Assert.Contains(paged.Items, item => item.Id == created.Id && !item.Status);

            var dropdownResponse = await _client.GetAsync("/api/v1/inventory/dropdown?status=false");
            dropdownResponse.EnsureSuccessStatusCode();
            var dropdownJson = await dropdownResponse.Content.ReadAsStringAsync();
            Assert.Contains($"Farinha Integral {suffix}", dropdownJson);

            var activateResponse = await _client.PatchAsync($"/api/v1/inventory/{created.Id}/active", null);
            activateResponse.EnsureSuccessStatusCode();
            var active = (await activateResponse.Content.ReadFromJsonAsync<InventoryMaterialResponse>(JsonOptions))!;
            Assert.True(active.Status);
        }

        [Fact]
        public async Task Restock_ShouldRecalculateWeightedAverageCost()
        {
            await AuthenticateAsync();
            var suffix = NewSuffix();

            var created = await CreateMaterialAsync(suffix, "Leite", 1000, 5000, MeasureUnit.ML);

            var restockResponse = await _client.PostAsJsonAsync(
                $"/api/v1/inventory/{created.Id}/restock",
                new RestockRequest(500, 4000));

            restockResponse.EnsureSuccessStatusCode();
            var restocked = (await restockResponse.Content.ReadFromJsonAsync<InventoryMaterialResponse>(JsonOptions))!;

            Assert.Equal(1500, restocked.Quantity);
            Assert.Equal(6, restocked.UnitCost);
        }

        [Fact]
        public async Task ProductRecipe_ShouldSetGetClearAndValidateMaterials()
        {
            await AuthenticateAsync();
            var suffix = NewSuffix();
            var product = await CreateProductAsync(suffix, $"Bolo {suffix}");
            var flour = await CreateMaterialAsync(suffix, "Farinha", 5000, 10000, MeasureUnit.G);
            var milk = await CreateMaterialAsync(suffix, "Leite", 3000, 9000, MeasureUnit.ML);

            var invalidResponse = await _client.PutAsJsonAsync(
                $"/api/v1/products/{product.Id}/recipe",
                new SetRecipeRequest([new RecipeItemRequest(Guid.NewGuid(), 100)]));

            Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);

            var setRecipe = await SetRecipeAsync(
                product.Id,
                new RecipeItemRequest(flour.Id, 500),
                new RecipeItemRequest(milk.Id, 200));

            Assert.True(setRecipe.HasRecipe);
            Assert.Equal(2, setRecipe.Items.Count);

            var recipe = await GetRecipeAsync(product.Id);
            Assert.True(recipe.HasRecipe);
            Assert.Contains(recipe.Items, item => item.MaterialId == flour.Id && item.Quantity == 500);
            Assert.Contains(recipe.Items, item => item.MaterialId == milk.Id && item.Quantity == 200);

            var clearResponse = await _client.PutAsJsonAsync(
                $"/api/v1/products/{product.Id}/recipe",
                new SetRecipeRequest([]));

            clearResponse.EnsureSuccessStatusCode();
            var cleared = (await clearResponse.Content.ReadFromJsonAsync<ProductRecipeResponse>(JsonOptions))!;
            Assert.False(cleared.HasRecipe);
            Assert.Empty(cleared.Items);

            var recipeAfterClear = await GetRecipeAsync(product.Id);
            Assert.False(recipeAfterClear.HasRecipe);
            Assert.Empty(recipeAfterClear.Items);
        }

        [Fact]
        public async Task ProductRecipeOptions_ShouldSetGetClearAndReturnOrderOptionCatalog()
        {
            await AuthenticateAsync();
            var suffix = NewSuffix();
            var product = await CreateProductAsync(suffix, $"Naked Cake {suffix}");
            var flour = await CreateMaterialAsync(suffix, "Farinha", 5000, 10000, MeasureUnit.G);
            var cocoa = await CreateMaterialAsync(suffix, "Cacau", 3000, 9000, MeasureUnit.G);

            var invalidResponse = await _client.PutAsJsonAsync(
                $"/api/v1/products/{product.Id}/recipe-options",
                new SetRecipeOptionRequest(ProductRecipeOptionType.Dough, "Chocolate", [new RecipeItemRequest(Guid.NewGuid(), 100)]));

            Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);

            var setOption = await SetRecipeOptionAsync(
                product.Id,
                ProductRecipeOptionType.Dough,
                "Chocolate",
                new RecipeItemRequest(flour.Id, 500),
                new RecipeItemRequest(cocoa.Id, 80));

            Assert.True(setOption.HasRecipe);
            Assert.Equal(ProductRecipeOptionType.Dough, setOption.Type);
            Assert.Equal("Chocolate", setOption.Name);
            Assert.Contains(setOption.Items, item => item.MaterialId == flour.Id && item.Quantity == 500);
            Assert.Contains(setOption.Items, item => item.MaterialId == cocoa.Id && item.Quantity == 80);

            var options = await GetRecipeOptionsAsync(product.Id);
            var dough = Assert.Single(options.Options);
            Assert.Equal(ProductRecipeOptionType.Dough, dough.Type);
            Assert.Equal("Chocolate", dough.Name);
            Assert.Equal(2, dough.Items.Count);

            var catalogResponse = await _client.GetAsync($"/api/v1/products/{product.Id}/order-options");
            catalogResponse.EnsureSuccessStatusCode();
            var catalog = (await catalogResponse.Content.ReadFromJsonAsync<ProductOrderRecipeOptionsResponse>(JsonOptions))!;
            Assert.Contains("Baunilha", catalog.CakeDoughs);
            Assert.Contains("cream cheese frosting", catalog.CakeFillings);
            Assert.Contains("casadinho", catalog.BrigadeiroFlavors);
            Assert.Contains("brookie", catalog.CookieFlavors);

            var cleared = await SetRecipeOptionAsync(product.Id, ProductRecipeOptionType.Dough, "Chocolate");
            Assert.False(cleared.HasRecipe);

            var optionsAfterClear = await GetRecipeOptionsAsync(product.Id);
            Assert.Empty(optionsAfterClear.Options);
        }

        [Fact]
        public async Task PreparingTransition_ShouldDeductInventoryFromRecipeMaterials()
        {
            await AuthenticateAsync();
            var suffix = NewSuffix();
            var client = await CreateClientAsync(suffix);
            var product = await CreateProductAsync(suffix, $"Bolo de Chocolate {suffix}");
            var flour = await CreateMaterialAsync(suffix, "Farinha", 5000, 10000, MeasureUnit.G);
            var milk = await CreateMaterialAsync(suffix, "Leite", 2000, 5000, MeasureUnit.ML);

            await SetRecipeAsync(
                product.Id,
                new RecipeItemRequest(flour.Id, 500),
                new RecipeItemRequest(milk.Id, 200));

            var order = await CreateOrderAsync(client.Id, new CreateOrderItemRequest(product.Id, 2, 2500, null, null, null));
            var updateResponse = await _client.PutAsJsonAsync(
                $"/api/v1/orders/{order.Id}",
                new UpdateOrderRequest(null, (int)OrderStatus.Preparing, null, null));

            var updateResult = await ReadOrderPayloadAsync(updateResponse);
            var updatedOrder = updateResult.Order;
            var warnings = updateResult.Warnings;
            Assert.Equal(OrderStatus.Preparing, updatedOrder.Status);
            Assert.Empty(warnings);

            var flourState = await LoadMaterialEntityAsync(flour.Id);
            var milkState = await LoadMaterialEntityAsync(milk.Id);
            Assert.Equal(4000, flourState.Quantity);
            Assert.Equal(1600, milkState.Quantity);
        }

        [Fact]
        public async Task PreparingTransition_ShouldReturnWarningWhenStockGoesNegative()
        {
            await AuthenticateAsync();
            var suffix = NewSuffix();
            var client = await CreateClientAsync(suffix);
            var product = await CreateProductAsync(suffix, $"Cobertura {suffix}");
            var topping = await CreateMaterialAsync(suffix, "Cobertura", 5, 500, MeasureUnit.U);

            await SetRecipeAsync(product.Id, new RecipeItemRequest(topping.Id, 10));

            var order = await CreateOrderAsync(client.Id, new CreateOrderItemRequest(product.Id, 1, 1200, null, null, null));
            var updateResponse = await _client.PutAsJsonAsync(
                $"/api/v1/orders/{order.Id}",
                new UpdateOrderRequest(null, (int)OrderStatus.Preparing, null, null));

            var updateResult = await ReadOrderPayloadAsync(updateResponse);
            var warnings = updateResult.Warnings;
            Assert.Single(warnings);
            Assert.Contains("Cobertura", warnings[0]);
            Assert.Contains("-5", warnings[0]);

            var toppingState = await LoadMaterialEntityAsync(topping.Id);
            Assert.Equal(-5, toppingState.Quantity);
        }

        [Fact]
        public async Task CancelOrder_ShouldRestoreInventory()
        {
            await AuthenticateAsync();
            var suffix = NewSuffix();
            var client = await CreateClientAsync(suffix);
            var product = await CreateProductAsync(suffix, $"Pao {suffix}");
            var flour = await CreateMaterialAsync(suffix, "Farinha", 100, 1000, MeasureUnit.G);

            await SetRecipeAsync(product.Id, new RecipeItemRequest(flour.Id, 30));

            var order = await CreateOrderAsync(client.Id, new CreateOrderItemRequest(product.Id, 2, 1500, null, null, null));
            await _client.PutAsJsonAsync(
                $"/api/v1/orders/{order.Id}",
                new UpdateOrderRequest(null, (int)OrderStatus.Preparing, null, null));

            var cancelResponse = await _client.PatchAsync($"/api/v1/orders/{order.Id}/cancel", null);
            cancelResponse.EnsureSuccessStatusCode();

            var flourState = await LoadMaterialEntityAsync(flour.Id);
            Assert.Equal(100, flourState.Quantity);
        }

        [Fact]
        public async Task CancelItem_ShouldRestoreOnlyThatItemInventory()
        {
            await AuthenticateAsync();
            var suffix = NewSuffix();
            var client = await CreateClientAsync(suffix);
            var cake = await CreateProductAsync(suffix, $"Bolo {suffix}");
            var drink = await CreateProductAsync(suffix, $"Suco {suffix}");
            var flour = await CreateMaterialAsync(suffix, "Farinha", 100, 500, MeasureUnit.G);
            var juice = await CreateMaterialAsync(suffix, "SucoBase", 100, 500, MeasureUnit.ML);

            await SetRecipeAsync(cake.Id, new RecipeItemRequest(flour.Id, 20));
            await SetRecipeAsync(drink.Id, new RecipeItemRequest(juice.Id, 30));

            var order = await CreateOrderAsync(
                client.Id,
                new CreateOrderItemRequest(cake.Id, 2, 2000, null, null, null),
                new CreateOrderItemRequest(drink.Id, 1, 1500, null, null, null));

            await _client.PutAsJsonAsync(
                $"/api/v1/orders/{order.Id}",
                new UpdateOrderRequest(null, (int)OrderStatus.Preparing, null, null));

            var cancelItemResponse = await _client.PatchAsync($"/api/v1/orders/{order.Id}/items/{cake.Id}/cancel", null);
            cancelItemResponse.EnsureSuccessStatusCode();

            var flourState = await LoadMaterialEntityAsync(flour.Id);
            var juiceState = await LoadMaterialEntityAsync(juice.Id);
            Assert.Equal(100, flourState.Quantity);
            Assert.Equal(70, juiceState.Quantity);
        }

        [Fact]
        public async Task UpdateItemQuantity_ShouldAdjustInventoryByDelta()
        {
            await AuthenticateAsync();
            var suffix = NewSuffix();
            var client = await CreateClientAsync(suffix);
            var product = await CreateProductAsync(suffix, $"Cookie {suffix}");
            var dough = await CreateMaterialAsync(suffix, "Massa", 100, 1000, MeasureUnit.G);

            await SetRecipeAsync(product.Id, new RecipeItemRequest(dough.Id, 10));

            var order = await CreateOrderAsync(client.Id, new CreateOrderItemRequest(product.Id, 2, 1200, null, null, null));
            await _client.PutAsJsonAsync(
                $"/api/v1/orders/{order.Id}",
                new UpdateOrderRequest(null, (int)OrderStatus.Preparing, null, null));

            var increaseResponse = await _client.PatchAsJsonAsync(
                $"/api/v1/orders/{order.Id}/items/{product.Id}/quantity",
                new UpdateItemQuantityRequest(3));

            var increaseResult = await ReadOrderPayloadAsync(increaseResponse);
            var warnings = increaseResult.Warnings;
            Assert.Empty(warnings);

            var doughState = await LoadMaterialEntityAsync(dough.Id);
            Assert.Equal(50, doughState.Quantity);
        }

        [Fact]
        public async Task PreparingTransition_ShouldIgnoreProductsWithoutRecipe()
        {
            await AuthenticateAsync();
            var suffix = NewSuffix();
            var client = await CreateClientAsync(suffix);
            var product = await CreateProductAsync(suffix, $"Sem Receita {suffix}");
            var unrelatedMaterial = await CreateMaterialAsync(suffix, "Material", 100, 1000, MeasureUnit.U);

            var order = await CreateOrderAsync(client.Id, new CreateOrderItemRequest(product.Id, 2, 1000, null, null, null));
            var updateResponse = await _client.PutAsJsonAsync(
                $"/api/v1/orders/{order.Id}",
                new UpdateOrderRequest(null, (int)OrderStatus.Preparing, null, null));

            var updateResult = await ReadOrderPayloadAsync(updateResponse);
            var warnings = updateResult.Warnings;
            Assert.Empty(warnings);

            var materialState = await LoadMaterialEntityAsync(unrelatedMaterial.Id);
            Assert.Equal(100, materialState.Quantity);
        }

        [Fact]
        public async Task Restock_ShouldUseIntegerWeightedAverageFormula()
        {
            await AuthenticateAsync();
            var suffix = NewSuffix();
            var created = await CreateMaterialAsync(suffix, "Chocolate", 15, 19995, MeasureUnit.G);

            var restockResponse = await _client.PostAsJsonAsync(
                $"/api/v1/inventory/{created.Id}/restock",
                new RestockRequest(10, 10000));

            restockResponse.EnsureSuccessStatusCode();
            var restocked = (await restockResponse.Content.ReadFromJsonAsync<InventoryMaterialResponse>(JsonOptions))!;

            Assert.Equal(25, restocked.Quantity);
            Assert.Equal(1199, restocked.UnitCost);
        }
    }
}

using DeuxERP.Application.DTOs;
using DeuxERP.Tests.DTOs;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace DeuxERP.Tests.Cash
{
    public class CashFlowTests : BaseIntegrationTest
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public CashFlowTests(IntegrationTestFactory<Program> factory) : base(factory) { }

        private async Task<(Guid clientId, Guid productId)> CreateClientAndProductAsync()
        {
            var suffix = DateTime.Now.Ticks.ToString().Substring(12);

            var clientRes = await _client.PostAsJsonAsync("/api/v1/clients/new",
                new CreateClient($"Cliente Cash Test {suffix}", "12345678901"));
            clientRes.EnsureSuccessStatusCode();
            var client = await clientRes.Content.ReadFromJsonAsync<ClientResponse>(JsonOptions);

            var productForm = new MultipartFormDataContent();
            productForm.Add(new StringContent($"Produto Cash Test {suffix}"), "Name");
            productForm.Add(new StringContent("3000"), "Price");
            var productRes = await _client.PostAsync("/api/v1/products/new", productForm);
            productRes.EnsureSuccessStatusCode();
            var product = await productRes.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);

            return (client!.Id, product!.Id);
        }

        private async Task<OrderResponse> CreateOrderWithItemAsync(Guid clientId, Guid productId)
        {
            var req = new CreateOrderRequest(clientId, DateTime.UtcNow.AddDays(1),
                new List<CreateOrderItemRequest> { new(productId, 1, 3000, null, null, null) }, null);
            var res = await _client.PostAsJsonAsync("/api/v1/orders/new", req);
            res.EnsureSuccessStatusCode();
            return (await res.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions))!;
        }

        [Fact]
        public async Task PayOrder_CreatesInflowCashEntry()
        {
            await AuthenticateAsAdminAsync();
            var (clientId, productId) = await CreateClientAndProductAsync();
            var order = await CreateOrderWithItemAsync(clientId, productId);

            await _client.PatchAsync($"/api/v1/orders/{order.Id}/pay", null);

            var res = await _client.GetAsync("/api/v1/cash/entries");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var paged = await res.Content.ReadFromJsonAsync<PagedCashResponse>(JsonOptions);

            var entry = paged!.Items.FirstOrDefault(e => e.SourceId == order.Id);
            Assert.NotNull(entry);
            Assert.Equal("Inflow", entry.Type);
            Assert.Equal("OrderPayment", entry.Source);
            Assert.Equal(order.TotalPaid, entry.AmountCents);
        }

        [Fact]
        public async Task UnpayOrder_CreatesOutflowReversalEntry()
        {
            await AuthenticateAsAdminAsync();
            var (clientId, productId) = await CreateClientAndProductAsync();
            var order = await CreateOrderWithItemAsync(clientId, productId);

            await _client.PatchAsync($"/api/v1/orders/{order.Id}/pay", null);
            await _client.PatchAsJsonAsync($"/api/v1/orders/{order.Id}/unpay",
                new { Reason = "Motivo de reversão para teste" });

            var res = await _client.GetAsync($"/api/v1/cash/entries");
            var paged = await res.Content.ReadFromJsonAsync<PagedCashResponse>(JsonOptions);

            var reversal = paged!.Items.FirstOrDefault(e =>
                e.SourceId == order.Id && e.Source == "OrderReversal");
            Assert.NotNull(reversal);
            Assert.Equal("Outflow", reversal.Type);
            Assert.Equal(order.TotalPaid, reversal.AmountCents);
        }

        [Fact]
        public async Task CreateManualEntry_ReturnsCreated()
        {
            await AuthenticateAsAdminAsync();

            var res = await _client.PostAsJsonAsync("/api/v1/cash/entries", new
            {
                BillingDate = DateTime.UtcNow,
                Type = "Inflow",
                Category = "RawMaterial",
                Counterparty = "Fornecedor X",
                AmountCents = 5000
            });

            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
            var entry = await res.Content.ReadFromJsonAsync<DeuxERP.Application.DTOs.CashEntryResponse>(JsonOptions);
            Assert.Equal("Manual", entry!.Source);
            Assert.Equal(5000, entry.AmountCents);
        }

        [Fact]
        public async Task EditAutomaticEntry_ReturnsBadRequest()
        {
            await AuthenticateAsAdminAsync();
            var (clientId, productId) = await CreateClientAndProductAsync();
            var order = await CreateOrderWithItemAsync(clientId, productId);
            await _client.PatchAsync($"/api/v1/orders/{order.Id}/pay", null);

            var paged = await (await _client.GetAsync("/api/v1/cash/entries"))
                .Content.ReadFromJsonAsync<PagedCashResponse>(JsonOptions);
            var autoEntry = paged!.Items.FirstOrDefault(e => e.Source == "OrderPayment");
            Assert.NotNull(autoEntry);

            var res = await _client.PutAsJsonAsync($"/api/v1/cash/entries/{autoEntry.Id}", new
            {
                BillingDate = DateTime.UtcNow,
                Type = "Inflow",
                Category = "Order",
                Counterparty = "Tentativa edição",
                AmountCents = 1
            });

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }

        [Fact]
        public async Task DeleteAutomaticEntry_ReturnsBadRequest()
        {
            await AuthenticateAsAdminAsync();
            var (clientId, productId) = await CreateClientAndProductAsync();
            var order = await CreateOrderWithItemAsync(clientId, productId);
            await _client.PatchAsync($"/api/v1/orders/{order.Id}/pay", null);

            var paged = await (await _client.GetAsync("/api/v1/cash/entries"))
                .Content.ReadFromJsonAsync<PagedCashResponse>(JsonOptions);
            var autoEntry = paged!.Items.FirstOrDefault(e => e.Source == "OrderPayment");
            Assert.NotNull(autoEntry);

            var res = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Delete,
                $"/api/v1/cash/entries/{autoEntry.Id}")
            {
                Content = JsonContent.Create(new { Reason = "Tentativa de exclusão" })
            });

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }

        [Fact]
        public async Task SoftDeleteManualEntry_HiddenFromDefaultList()
        {
            await AuthenticateAsAdminAsync();

            var createRes = await _client.PostAsJsonAsync("/api/v1/cash/entries", new
            {
                BillingDate = DateTime.UtcNow,
                Type = "Outflow",
                Category = "Supplier",
                Counterparty = "Fornecedor para exclusão",
                AmountCents = 1000
            });
            Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
            var created = await createRes.Content.ReadFromJsonAsync<DeuxERP.Application.DTOs.CashEntryResponse>(JsonOptions);

            var deleteRes = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Delete,
                $"/api/v1/cash/entries/{created!.Id}")
            {
                Content = JsonContent.Create(new { Reason = "Exclusão para teste de soft delete" })
            });
            Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

            var listRes = await _client.GetAsync("/api/v1/cash/entries");
            var paged = await listRes.Content.ReadFromJsonAsync<PagedCashResponse>(JsonOptions);
            Assert.DoesNotContain(paged!.Items, e => e.Id == created.Id);

            var listWithDeletedRes = await _client.GetAsync("/api/v1/cash/entries?includeDeleted=true");
            var pagedWithDeleted = await listWithDeletedRes.Content.ReadFromJsonAsync<PagedCashResponse>(JsonOptions);
            Assert.Contains(pagedWithDeleted!.Items, e => e.Id == created.Id);
        }

        [Fact]
        public async Task GetSummary_TotalsMatchEntries()
        {
            await AuthenticateAsAdminAsync();

            await _client.PostAsJsonAsync("/api/v1/cash/entries", new
            {
                BillingDate = DateTime.UtcNow,
                Type = "Inflow",
                Category = "Other",
                Counterparty = "Resumo Teste Entrada",
                AmountCents = 10000
            });
            await _client.PostAsJsonAsync("/api/v1/cash/entries", new
            {
                BillingDate = DateTime.UtcNow,
                Type = "Outflow",
                Category = "Other",
                Counterparty = "Resumo Teste Saida",
                AmountCents = 4000
            });

            var res = await _client.GetAsync("/api/v1/cash/summary");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var summary = await res.Content.ReadFromJsonAsync<CashSummaryResponse>(JsonOptions);

            Assert.True(summary!.TotalInflowCents >= 10000);
            Assert.True(summary.TotalOutflowCents >= 4000);
            Assert.Equal(summary.TotalInflowCents - summary.TotalOutflowCents, summary.NetBalanceCents);
        }
    }
}

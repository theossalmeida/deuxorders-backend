using DeuxERP.Tests.DTOs;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace DeuxERP.Tests.Cash
{
    public class CashFlowSummaryTests : BaseIntegrationTest
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public CashFlowSummaryTests(IntegrationTestFactory<Program> factory) : base(factory) { }

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

            Assert.Equal(10000, summary!.TotalInflowCents);
            Assert.Equal(4000, summary.TotalOutflowCents);
            Assert.Equal(6000, summary.NetBalanceCents);
        }
    }
}

using DeuxERP.Application.DTOs;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace DeuxERP.Tests.Cash
{
    public class CashEntrySourceIdTests : BaseIntegrationTest
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public CashEntrySourceIdTests(IntegrationTestFactory<Program> factory) : base(factory) { }

        [Fact]
        public async Task CreateEntry_WithSourceId_PersistedAndRoundTripped()
        {
            await AuthenticateAsAdminAsync();
            var sourceId = Guid.NewGuid();

            var createRes = await _client.PostAsJsonAsync("/api/v1/cash/entries", new
            {
                BillingDate = DateTime.UtcNow,
                Type = "Inflow",
                Category = "Other",
                Counterparty = "Test Source Link",
                AmountCents = 2500,
                SourceId = sourceId
            });

            Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
            var created = await createRes.Content.ReadFromJsonAsync<CashEntryResponse>(JsonOptions);
            Assert.Equal(sourceId, created!.SourceId);
        }

        [Fact]
        public async Task CreateEntry_WithoutSourceId_StoredAsNull()
        {
            await AuthenticateAsAdminAsync();

            var createRes = await _client.PostAsJsonAsync("/api/v1/cash/entries", new
            {
                BillingDate = DateTime.UtcNow,
                Type = "Outflow",
                Category = "Supplier",
                Counterparty = "Test No Source",
                AmountCents = 1000
            });

            Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
            var created = await createRes.Content.ReadFromJsonAsync<CashEntryResponse>(JsonOptions);
            Assert.Null(created!.SourceId);
        }
    }
}

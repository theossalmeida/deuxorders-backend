using DeuxERP.Application.DTOs;
using DeuxERP.Domain.Cash.Enums;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DeuxERP.Tests.EndpointCoverage;

public class CashEndpointLifecycleTests : BaseIntegrationTest
{
    public CashEndpointLifecycleTests(IntegrationTestFactory<Program> factory) : base(factory) { }

    [Fact]
    [Trait("TestSet", "PaymentsAndCash")]
    public async Task ManualEntryLifecycle_CoversAuthorizationValidationGetUpdateAuditDeleteAndVisibility()
    {
        await AuthenticateAsync();
        var forbiddenCreate = await _client.PostAsJsonAsync("/api/v1/cash/entries", ValidCreateRequest());
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenCreate.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _client.GetAsync("/api/v1/cash/entries?includeDeleted=true")).StatusCode);

        await AuthenticateAsAdminAsync();
        var invalid = await _client.PostAsJsonAsync("/api/v1/cash/entries",
            ValidCreateRequest() with { AmountCents = 0, Counterparty = "" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var createdResponse = await _client.PostAsJsonAsync("/api/v1/cash/entries", ValidCreateRequest());
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = (await createdResponse.Content.ReadFromJsonAsync<CashEntryResponse>())!;

        var fetched = await _client.GetFromJsonAsync<CashEntryResponse>($"/api/v1/cash/entries/{created.Id}");
        Assert.Equal("Supplier A", fetched!.Counterparty);

        var update = await _client.PutAsJsonAsync($"/api/v1/cash/entries/{created.Id}",
            new UpdateCashEntryRequest(DateTime.UtcNow, CashFlowType.Outflow,
                CashFlowCategory.Supplier, "Supplier B", 3500, "updated"));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = (await update.Content.ReadFromJsonAsync<CashEntryResponse>())!;
        Assert.Equal("Supplier B", updated.Counterparty);
        Assert.NotNull(updated.UpdatedAt);

        using var auditResponse = await _client.GetAsync($"/api/v1/cash/audit/{created.Id}");
        auditResponse.EnsureSuccessStatusCode();
        using var audit = JsonDocument.Parse(await auditResponse.Content.ReadAsStringAsync());
        Assert.NotEmpty(audit.RootElement.EnumerateArray());

        var shortReason = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Delete,
            $"/api/v1/cash/entries/{created.Id}") { Content = JsonContent.Create(new { Reason = "no" }) });
        Assert.Equal(HttpStatusCode.BadRequest, shortReason.StatusCode);

        var deleted = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Delete,
            $"/api/v1/cash/entries/{created.Id}")
        {
            Content = JsonContent.Create(new { Reason = "Duplicate invoice" })
        });
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync($"/api/v1/cash/entries/{created.Id}")).StatusCode);

        var included = await _client.GetFromJsonAsync<CashEntryResponse>(
            $"/api/v1/cash/entries/{created.Id}?includeDeleted=true");
        Assert.NotNull(included!.DeletedAt);
    }

    private static CreateCashEntryRequest ValidCreateRequest() =>
        new(DateTime.UtcNow, CashFlowType.Outflow, CashFlowCategory.Supplier,
            "Supplier A", 2500, "invoice");
}

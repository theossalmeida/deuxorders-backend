using DeuxERP.Application.Common;
using DeuxERP.Application.DTOs;
using DeuxERP.Application.Notifications;
using DeuxERP.Domain.Cash;
using DeuxERP.Domain.Cash.Enums;
using DeuxERP.Domain.Interfaces;
using DeuxERP.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace DeuxERP.Application.Services;

public class CashFlowService
{
    private readonly ICashFlowRepository _repository;
    private readonly IAppDbContext _db;
    private readonly IPushNotificationService _push;
    private readonly ILogger<CashFlowService> _logger;

    public CashFlowService(
        ICashFlowRepository repository,
        IAppDbContext db,
        IPushNotificationService push,
        ILogger<CashFlowService> logger)
    {
        _repository = repository;
        _db = db;
        _push = push;
        _logger = logger;
    }

    public async Task<CashFlowEntry> CreateAsync(CreateCashEntryRequest req, Guid userId, string userName)
    {
        var entry = CashFlowEntry.CreateManual(
            req.BillingDate, req.Type, req.Category,
            req.Counterparty, req.AmountCents, req.Notes,
            userId, userName, req.SourceId);

        _repository.Add(entry);
        await _db.SaveChangesAsync();
        await NotifyManualCashFlowCreatedAsync(req, entry.Id);
        return entry;
    }

    public async Task<CashFlowEntry> UpdateAsync(Guid id, UpdateCashEntryRequest req, Guid userId, string userName)
    {
        var entry = await _repository.GetByIdAsync(id)
            ?? throw new InvalidOperationException("Entrada de caixa não encontrada.");

        entry.Update(req.BillingDate, req.Type, req.Category,
            req.Counterparty, req.AmountCents, req.Notes, userId, userName);

        await _db.SaveChangesAsync();
        return entry;
    }

    public async Task DeleteAsync(Guid id, string reason, Guid userId, string userName)
    {
        var entry = await _repository.GetByIdAsync(id)
            ?? throw new InvalidOperationException("Entrada de caixa não encontrada.");

        entry.SoftDelete(userId, userName, reason);
        await _db.SaveChangesAsync();
    }

    public Task<CashFlowEntry?> GetByIdAsync(Guid id, bool includeDeleted = false)
        => _repository.GetByIdAsync(id, includeDeleted);

    public Task<PagedResult<CashFlowEntry>> ListAsync(CashFlowFilter filter, int page, int size)
        => _repository.ListAsync(filter, page, size);

    public Task<CashFlowSummary> GetSummaryAsync(CashFlowFilter filter)
        => _repository.GetSummaryAsync(filter);

    public Task<IEnumerable<CashFlowEntry>> ExportAsync(CashFlowFilter filter)
        => _repository.ListForExportAsync(filter);

    public Task<IEnumerable<CashFlowAuditLog>> GetAuditLogAsync(Guid entryId)
        => _repository.GetAuditLogAsync(entryId);

    private async Task NotifyManualCashFlowCreatedAsync(CreateCashEntryRequest req, Guid entryId)
    {
        try
        {
            await _push.SendToAllAsync(
                NotificationType.ManualCashFlowCreated,
                req.Type == CashFlowType.Inflow ? "Nova entrada" : "Nova saida",
                $"{req.Counterparty} - {FormatMoney(req.AmountCents)}",
                "/cash");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to queue manual cash flow push notification for entry {EntryId}.", entryId);
        }
    }

    private static string FormatMoney(long amountCents) =>
        (amountCents / 100m).ToString("C", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
}

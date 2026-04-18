using DeuxOrders.Application.DTOs;
using DeuxOrders.Domain.Cash;
using DeuxOrders.Domain.Cash.Enums;
using DeuxOrders.Domain.Interfaces;

namespace DeuxOrders.Application.Services;

public class CashFlowService
{
    private readonly ICashFlowRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CashFlowService(ICashFlowRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CashFlowEntry> CreateAsync(CreateCashEntryRequest req, Guid userId, string userName)
    {
        var entry = CashFlowEntry.CreateManual(
            req.BillingDate, req.Type, req.Category,
            req.Counterparty, req.AmountCents, req.Notes,
            userId, userName, req.SourceId);

        _repository.Add(entry);
        await _unitOfWork.CommitAsync();
        return entry;
    }

    public async Task<CashFlowEntry> UpdateAsync(Guid id, UpdateCashEntryRequest req, Guid userId, string userName)
    {
        var entry = await _repository.GetByIdAsync(id)
            ?? throw new InvalidOperationException("Entrada de caixa não encontrada.");

        entry.Update(req.BillingDate, req.Type, req.Category,
            req.Counterparty, req.AmountCents, req.Notes, userId, userName);

        await _unitOfWork.CommitAsync();
        return entry;
    }

    public async Task DeleteAsync(Guid id, string reason, Guid userId, string userName)
    {
        var entry = await _repository.GetByIdAsync(id)
            ?? throw new InvalidOperationException("Entrada de caixa não encontrada.");

        entry.SoftDelete(userId, userName, reason);
        await _unitOfWork.CommitAsync();
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
}

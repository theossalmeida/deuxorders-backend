using DeuxERP.Domain.Cash;
using DeuxERP.Domain.Cash.Enums;

namespace DeuxERP.Domain.Interfaces;

public record CashFlowFilter(
    DateTime? From,
    DateTime? To,
    CashFlowType? Type,
    CashFlowCategory? Category,
    CashFlowSource? Source,
    bool IncludeDeleted = false);

public interface ICashFlowRepository
{
    Task<CashFlowEntry?> GetByIdAsync(Guid id, bool includeDeleted = false);
    Task<PagedResult<CashFlowEntry>> ListAsync(CashFlowFilter filter, int page, int size);
    Task<CashFlowSummary> GetSummaryAsync(CashFlowFilter filter);
    Task<IEnumerable<CashFlowEntry>> ListForExportAsync(CashFlowFilter filter);
    Task<IEnumerable<CashFlowAuditLog>> GetAuditLogAsync(Guid entryId);
    void Add(CashFlowEntry entry);
}

public record CashFlowSummary(
    long TotalInflowCents,
    long TotalOutflowCents,
    long NetBalanceCents,
    int TotalCount,
    Dictionary<string, long> InflowByCategory,
    Dictionary<string, long> OutflowByCategory);

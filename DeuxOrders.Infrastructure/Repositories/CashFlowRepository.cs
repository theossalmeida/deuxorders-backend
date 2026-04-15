using DeuxOrders.Domain.Cash;
using DeuxOrders.Domain.Cash.Enums;
using DeuxOrders.Domain.Interfaces;
using DeuxOrders.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeuxOrders.Infrastructure.Repositories;

public class CashFlowRepository : ICashFlowRepository
{
    private readonly ApplicationDbContext _context;

    public CashFlowRepository(ApplicationDbContext context)
        => _context = context;

    public async Task<CashFlowEntry?> GetByIdAsync(Guid id, bool includeDeleted = false)
    {
        var query = includeDeleted
            ? _context.CashFlowEntries.IgnoreQueryFilters()
            : _context.CashFlowEntries.AsQueryable();

        return await query.FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<PagedResult<CashFlowEntry>> ListAsync(CashFlowFilter filter, int page, int size)
    {
        var query = BuildQuery(filter);
        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.BillingDate)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return new PagedResult<CashFlowEntry>(items, total, page, size);
    }

    public async Task<CashFlowSummary> GetSummaryAsync(CashFlowFilter filter)
    {
        var query = BuildQuery(filter);

        var inflow = await query
            .Where(e => e.Type == CashFlowType.Inflow)
            .GroupBy(_ => 1)
            .Select(g => new { Total = g.Sum(e => e.AmountCents), Count = g.Count() })
            .FirstOrDefaultAsync();

        var outflow = await query
            .Where(e => e.Type == CashFlowType.Outflow)
            .GroupBy(_ => 1)
            .Select(g => new { Total = g.Sum(e => e.AmountCents), Count = g.Count() })
            .FirstOrDefaultAsync();

        var inflowByCategory = await query
            .Where(e => e.Type == CashFlowType.Inflow)
            .GroupBy(e => e.Category)
            .Select(g => new { Category = g.Key.ToString(), Total = g.Sum(e => e.AmountCents) })
            .ToListAsync();

        var outflowByCategory = await query
            .Where(e => e.Type == CashFlowType.Outflow)
            .GroupBy(e => e.Category)
            .Select(g => new { Category = g.Key.ToString(), Total = g.Sum(e => e.AmountCents) })
            .ToListAsync();

        var totalInflow = inflow?.Total ?? 0;
        var totalOutflow = outflow?.Total ?? 0;
        var totalCount = (inflow?.Count ?? 0) + (outflow?.Count ?? 0);

        return new CashFlowSummary(
            totalInflow,
            totalOutflow,
            totalInflow - totalOutflow,
            totalCount,
            inflowByCategory.ToDictionary(x => x.Category, x => x.Total),
            outflowByCategory.ToDictionary(x => x.Category, x => x.Total));
    }

    public async Task<IEnumerable<CashFlowEntry>> ListForExportAsync(CashFlowFilter filter)
        => await BuildQuery(filter)
            .OrderByDescending(e => e.BillingDate)
            .ToListAsync();

    public async Task<IEnumerable<CashFlowAuditLog>> GetAuditLogAsync(Guid entryId)
        => await _context.CashFlowAuditLogs
            .AsNoTracking()
            .Where(l => l.EntryId == entryId)
            .OrderBy(l => l.OccurredAt)
            .ToListAsync();

    public void Add(CashFlowEntry entry)
        => _context.CashFlowEntries.Add(entry);

    private IQueryable<CashFlowEntry> BuildQuery(CashFlowFilter filter)
    {
        var query = filter.IncludeDeleted
            ? _context.CashFlowEntries.IgnoreQueryFilters().AsNoTracking()
            : _context.CashFlowEntries.AsNoTracking();

        if (filter.From.HasValue)
            query = query.Where(e => e.BillingDate >= filter.From.Value);

        if (filter.To.HasValue)
            query = query.Where(e => e.BillingDate <= filter.To.Value);

        if (filter.Type.HasValue)
            query = query.Where(e => e.Type == filter.Type.Value);

        if (filter.Category.HasValue)
            query = query.Where(e => e.Category == filter.Category.Value);

        if (filter.Source.HasValue)
            query = query.Where(e => e.Source == filter.Source.Value);

        return query;
    }
}

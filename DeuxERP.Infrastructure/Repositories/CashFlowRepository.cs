using DeuxERP.Domain.Cash;
using DeuxERP.Domain.Cash.Enums;
using DeuxERP.Domain.Interfaces;
using DeuxERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeuxERP.Infrastructure.Repositories;

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
        var rows = await BuildQuery(filter)
            .GroupBy(e => new { e.Type, e.Category })
            .Select(g => new
            {
                g.Key.Type,
                g.Key.Category,
                Total = g.Sum(e => e.AmountCents),
                Count = g.Count()
            })
            .ToListAsync();

        var totalInflow = rows.Where(r => r.Type == CashFlowType.Inflow).Sum(r => r.Total);
        var totalOutflow = rows.Where(r => r.Type == CashFlowType.Outflow).Sum(r => r.Total);
        var totalCount = rows.Sum(r => r.Count);

        var inflowByCategory = rows
            .Where(r => r.Type == CashFlowType.Inflow)
            .ToDictionary(r => r.Category.ToString(), r => r.Total);

        var outflowByCategory = rows
            .Where(r => r.Type == CashFlowType.Outflow)
            .ToDictionary(r => r.Category.ToString(), r => r.Total);

        return new CashFlowSummary(
            totalInflow,
            totalOutflow,
            totalInflow - totalOutflow,
            totalCount,
            inflowByCategory,
            outflowByCategory);
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

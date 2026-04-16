using DeuxOrders.Application.DTOs;
using DeuxOrders.Application.Services;
using DeuxOrders.Domain.Cash;
using DeuxOrders.Domain.Cash.Enums;
using DeuxOrders.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

public record DeleteCashEntryRequest(string Reason);

namespace DeuxOrders.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/v1/cash")]
    public class CashController : ControllerBase
    {
        private readonly CashFlowService _service;

        public CashController(CashFlowService service)
        {
            _service = service;
        }

        [HttpGet("entries")]
        public async Task<IActionResult> GetEntries(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] CashFlowType? type,
            [FromQuery] CashFlowCategory? category,
            [FromQuery] CashFlowSource? source,
            [FromQuery] bool includeDeleted = false,
            [FromQuery] int page = 1,
            [FromQuery] int size = 20)
        {
            if (includeDeleted && !User.IsInRole("Administrator"))
                return Forbid();

            if (size > 100) size = 100;

            var utcFrom = from.HasValue ? DateTime.SpecifyKind(from.Value, DateTimeKind.Utc) : (DateTime?)null;
            var utcTo = to.HasValue ? DateTime.SpecifyKind(to.Value, DateTimeKind.Utc) : (DateTime?)null;
            var filter = new CashFlowFilter(utcFrom, utcTo, type, category, source, includeDeleted);
            var result = await _service.ListAsync(filter, page, size);

            return Ok(new
            {
                items = result.Items.Select(ToResponse),
                totalCount = result.TotalCount,
                pageNumber = result.PageNumber,
                pageSize = result.PageSize
            });
        }

        [HttpGet("entries/{id}")]
        public async Task<IActionResult> GetEntry(Guid id, [FromQuery] bool includeDeleted = false)
        {
            if (includeDeleted && !User.IsInRole("Administrator"))
                return Forbid();

            var entry = await _service.GetByIdAsync(id, includeDeleted);
            if (entry == null) return NotFound();

            return Ok(ToResponse(entry));
        }

        [HttpPost("entries")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Create([FromBody] CreateCashEntryRequest request)
        {
            var userId = Guid.Parse(User.FindFirst("id")!.Value);
            var userName = User.FindFirst("username")!.Value;

            var entry = await _service.CreateAsync(request, userId, userName);
            return CreatedAtAction(nameof(GetEntry), new { id = entry.Id }, ToResponse(entry));
        }

        [HttpPut("entries/{id}")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCashEntryRequest request)
        {
            var userId = Guid.Parse(User.FindFirst("id")!.Value);
            var userName = User.FindFirst("username")!.Value;

            var entry = await _service.UpdateAsync(id, request, userId, userName);
            return Ok(ToResponse(entry));
        }

        [HttpDelete("entries/{id}")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Delete(Guid id, [FromBody] DeleteCashEntryRequest request)
        {
            var userId = Guid.Parse(User.FindFirst("id")!.Value);
            var userName = User.FindFirst("username")!.Value;

            await _service.DeleteAsync(id, request.Reason, userId, userName);
            return NoContent();
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] CashFlowType? type,
            [FromQuery] CashFlowCategory? category,
            [FromQuery] CashFlowSource? source)
        {
            var utcFrom = from.HasValue ? DateTime.SpecifyKind(from.Value, DateTimeKind.Utc) : (DateTime?)null;
            var utcTo = to.HasValue ? DateTime.SpecifyKind(to.Value, DateTimeKind.Utc) : (DateTime?)null;
            var filter = new CashFlowFilter(utcFrom, utcTo, type, category, source);
            var summary = await _service.GetSummaryAsync(filter);
            return Ok(summary);
        }

        [HttpGet("audit/{entryId}")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> GetAuditLog(Guid entryId)
        {
            var logs = await _service.GetAuditLogAsync(entryId);
            return Ok(logs);
        }

        private static CashEntryResponse ToResponse(CashFlowEntry e) =>
            new(e.Id, e.CreatedAt, e.BillingDate,
                e.Type.ToString(), e.Category.ToString(), e.Counterparty,
                e.AmountCents, e.Notes, e.Source.ToString(), e.SourceId,
                e.AuthorUserId, e.AuthorUserName,
                e.UpdatedAt, e.DeletedAt);
    }
}

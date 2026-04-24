using DeuxERP.Application.Common;
using DeuxERP.Application.DTOs;
using DeuxERP.Application.Services;
using DeuxERP.Domain.Cash;
using DeuxERP.Domain.Cash.Enums;
using DeuxERP.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

public record DeleteCashEntryRequest(string Reason);

namespace DeuxERP.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/v1/cash")]
    public class CashController : ControllerBase
    {
        private readonly CashFlowService _service;
        private readonly ICurrentUserAccessor _currentUser;

        public CashController(CashFlowService service, ICurrentUserAccessor currentUser)
        {
            _service = service;
            _currentUser = currentUser;
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

            var (utcFrom, utcTo) = NormalizeBillingDateRange(from, to);
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
            var entry = await _service.CreateAsync(request, _currentUser.UserId, _currentUser.UserName);
            return CreatedAtAction(nameof(GetEntry), new { id = entry.Id }, ToResponse(entry));
        }

        [HttpPut("entries/{id}")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCashEntryRequest request)
        {
            var entry = await _service.UpdateAsync(id, request, _currentUser.UserId, _currentUser.UserName);
            return Ok(ToResponse(entry));
        }

        [HttpDelete("entries/{id}")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Delete(Guid id, [FromBody] DeleteCashEntryRequest request)
        {
            await _service.DeleteAsync(id, request.Reason, _currentUser.UserId, _currentUser.UserName);
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
            var (utcFrom, utcTo) = NormalizeBillingDateRange(from, to);
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

        private static (DateTime? From, DateTime? To) NormalizeBillingDateRange(DateTime? from, DateTime? to)
        {
            var utcFrom = from.HasValue
                ? DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Utc)
                : (DateTime?)null;

            var utcTo = to.HasValue
                ? DateTime.SpecifyKind(to.Value.Date.AddDays(1), DateTimeKind.Utc)
                : (DateTime?)null;

            return (utcFrom, utcTo);
        }
    }
}

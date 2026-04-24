using DeuxERP.API.Services;
using DeuxERP.Application.Services;
using DeuxERP.Domain.Sales;
using DeuxERP.Domain.Interfaces;
using DeuxERP.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeuxERP.API.Controllers
{
    [ApiController]
    [Route("api/v1/dashboard")]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private const int MaxPdfExportRows = 2000;
        private const int MaxCsvExportRows = 10000;
        private readonly DashboardService _service;
        private readonly IOrderRepository _repository;
        private readonly ExportService _exportService;

        public DashboardController(
            DashboardService service,
            IOrderRepository repository,
            ExportService exportService
            )
        {
            _service = service;
            _repository = repository;
            _exportService = exportService;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary(
            [FromQuery] DateTimeOffset? createdAtFrom,
            [FromQuery] DateTimeOffset? createdAtTo,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] OrderStatus? status)
        {
            var (utcStart, utcEnd) = NormalizeCreatedAtRange(createdAtFrom, createdAtTo, startDate, endDate);
            var result = await _service.GetSummaryAsync(utcStart, utcEnd, status);
            return Ok(result);
        }

        [HttpGet("revenue-over-time")]
        public async Task<IActionResult> GetRevenueOverTime(
            [FromQuery] DateTimeOffset? createdAtFrom,
            [FromQuery] DateTimeOffset? createdAtTo,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] OrderStatus? status)
        {
            var (utcStart, utcEnd) = NormalizeCreatedAtRange(createdAtFrom, createdAtTo, startDate, endDate);
            var result = await _service.GetRevenueOverTimeAsync(utcStart, utcEnd, status);
            return Ok(result);
        }

        [HttpGet("top-products")]
        public async Task<IActionResult> GetTopProducts(
            [FromQuery] DateTimeOffset? createdAtFrom,
            [FromQuery] DateTimeOffset? createdAtTo,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] OrderStatus? status,
            [FromQuery] int limit = 10)
        {
            var (utcStart, utcEnd) = NormalizeCreatedAtRange(createdAtFrom, createdAtTo, startDate, endDate);
            var result = await _service.GetTopProductsAsync(utcStart, utcEnd, status, limit);
            return Ok(result);
        }

        [HttpGet("top-clients")]
        public async Task<IActionResult> GetTopClients(
            [FromQuery] DateTimeOffset? createdAtFrom,
            [FromQuery] DateTimeOffset? createdAtTo,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] OrderStatus? status,
            [FromQuery] int limit = 10)
        {
            var (utcStart, utcEnd) = NormalizeCreatedAtRange(createdAtFrom, createdAtTo, startDate, endDate);
            var result = await _service.GetTopClientsAsync(utcStart, utcEnd, status, limit);
            return Ok(result);
        }

        [HttpGet("export")]
        public async Task<IActionResult> Export(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] OrderStatus? status,
            [FromQuery] string format = "csv",
            CancellationToken ct = default)
        {
            var (utcFrom, utcTo) = NormalizeDateRange(from, to);
            var filename = $"pedidos_{DateTime.UtcNow:yyyyMMdd}";
            var filter = new ExportFilter(utcFrom, utcTo, status);
            var rowCount = await _repository.CountForExportAsync(filter, ct);

            if (format.Equals("pdf", StringComparison.OrdinalIgnoreCase))
            {
                if (rowCount > MaxPdfExportRows)
                    return BadRequest($"PDF limitado a {MaxPdfExportRows} linhas. Use CSV ou reduza o intervalo.");

                var rows = await _repository.GetForExportAsync(filter, ct);
                var pdf = _exportService.GeneratePdf(rows);
                return File(pdf, "application/pdf", $"{filename}.pdf");
            }

            if (rowCount > MaxCsvExportRows)
                return BadRequest($"CSV limitado a {MaxCsvExportRows} linhas. Reduza o intervalo.");

            Response.ContentType = "text/csv; charset=utf-8";
            Response.Headers["Content-Disposition"] = $"attachment; filename=\"{filename}.csv\"";
            await _exportService.WriteCsvAsync(_repository.StreamForExportAsync(filter, ct), Response.Body, ct);
            return new EmptyResult();
        }

        private static (DateTime? Start, DateTime? End) NormalizeCreatedAtRange(
            DateTimeOffset? createdAtFrom,
            DateTimeOffset? createdAtTo,
            DateTime? legacyStart,
            DateTime? legacyEnd)
        {
            if (createdAtFrom.HasValue || createdAtTo.HasValue)
                return (createdAtFrom?.UtcDateTime, createdAtTo?.UtcDateTime);

            return NormalizeDateRange(legacyStart, legacyEnd);
        }

        private static (DateTime? Start, DateTime? End) NormalizeDateRange(DateTime? start, DateTime? end)
        {
            var utcStart = start.HasValue
                ? NormalizeDateBoundary(start.Value, false)
                : (DateTime?)null;

            var utcEnd = end.HasValue
                ? NormalizeDateBoundary(end.Value, true)
                : (DateTime?)null;

            return (utcStart, utcEnd);
        }

        private static DateTime NormalizeDateBoundary(DateTime value, bool exclusiveEnd)
        {
            if (value.Kind != DateTimeKind.Unspecified)
                return value.ToUniversalTime();

            var date = exclusiveEnd ? value.Date.AddDays(1) : value.Date;
            return DateTime.SpecifyKind(date, DateTimeKind.Utc);
        }
    }
}

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
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] OrderStatus? status)
        {
            var (utcStart, utcEnd) = NormalizeRange(startDate, endDate);
            var result = await _service.GetSummaryAsync(utcStart, utcEnd, status);
            return Ok(result);
        }

        [HttpGet("revenue-over-time")]
        public async Task<IActionResult> GetRevenueOverTime(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] OrderStatus? status)
        {
            var (utcStart, utcEnd) = NormalizeRange(startDate, endDate);
            var result = await _service.GetRevenueOverTimeAsync(utcStart, utcEnd, status);
            return Ok(result);
        }

        [HttpGet("top-products")]
        public async Task<IActionResult> GetTopProducts(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] OrderStatus? status,
            [FromQuery] int limit = 10)
        {
            var (utcStart, utcEnd) = NormalizeRange(startDate, endDate);
            var result = await _service.GetTopProductsAsync(utcStart, utcEnd, status, limit);
            return Ok(result);
        }

        [HttpGet("top-clients")]
        public async Task<IActionResult> GetTopClients(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] OrderStatus? status,
            [FromQuery] int limit = 10)
        {
            var (utcStart, utcEnd) = NormalizeRange(startDate, endDate);
            var result = await _service.GetTopClientsAsync(utcStart, utcEnd, status, limit);
            return Ok(result);
        }

        [HttpGet("export")]
        public async Task<IActionResult> Export(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] OrderStatus? status,
            [FromQuery] string format = "csv")
        {
            var (utcFrom, utcTo) = NormalizeRange(from, to);
            var rows = await _repository.GetForExportAsync(new ExportFilter(utcFrom, utcTo, status));
            var filename = $"pedidos_{DateTime.UtcNow:yyyyMMdd}";

            if (format.Equals("pdf", StringComparison.OrdinalIgnoreCase))
            {
                var pdf = _exportService.GeneratePdf(rows);
                return File(pdf, "application/pdf", $"{filename}.pdf");
            }

            var csv = _exportService.GenerateCsv(rows);
            return File(csv, "text/csv; charset=utf-8", $"{filename}.csv");
        }

        private static (DateTime? Start, DateTime? End) NormalizeRange(DateTime? start, DateTime? end)
        {
            var utcStart = start.HasValue
                ? DateTime.SpecifyKind(start.Value.Date, DateTimeKind.Utc)
                : (DateTime?)null;

            var utcEnd = end.HasValue
                ? DateTime.SpecifyKind(end.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc)
                : (DateTime?)null;

            return (utcStart, utcEnd);
        }
    }
}

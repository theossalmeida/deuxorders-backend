using DeuxERP.API.Services;
using DeuxERP.API.Models;
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
            [FromQuery] DashboardQuery query)
        {
            var (utcStart, utcEnd, dateField) = query.ResolvePeriod();
            var result = await _service.GetSummaryAsync(utcStart, utcEnd, query.Status, dateField, query.ClientId, query.IsPaid);
            return Ok(result);
        }

        [HttpGet("revenue-over-time")]
        public async Task<IActionResult> GetRevenueOverTime(
            [FromQuery] DashboardQuery query)
        {
            var (utcStart, utcEnd, dateField) = query.ResolvePeriod();
            var result = await _service.GetRevenueOverTimeAsync(utcStart, utcEnd, query.Status, dateField, query.ClientId, query.IsPaid);
            return Ok(result);
        }

        [HttpGet("top-products")]
        public async Task<IActionResult> GetTopProducts(
            [FromQuery] DashboardQuery query,
            [FromQuery] int limit = 10)
        {
            var (utcStart, utcEnd, dateField) = query.ResolvePeriod();
            var result = await _service.GetTopProductsAsync(utcStart, utcEnd, query.Status, Math.Clamp(limit, 1, 100), dateField, query.ClientId, query.IsPaid);
            return Ok(result);
        }

        [HttpGet("top-clients")]
        public async Task<IActionResult> GetTopClients(
            [FromQuery] DashboardQuery query,
            [FromQuery] int limit = 10)
        {
            var (utcStart, utcEnd, dateField) = query.ResolvePeriod();
            var result = await _service.GetTopClientsAsync(utcStart, utcEnd, query.Status, Math.Clamp(limit, 1, 100), dateField, query.ClientId, query.IsPaid);
            return Ok(result);
        }

        [HttpGet("export")]
        public async Task<IActionResult> Export(
            [FromQuery] OrderPeriodQuery query,
            [FromQuery] string format = "csv",
            CancellationToken ct = default)
        {
            var (utcFrom, utcTo, dateField) = query.ResolvePeriod();
            var filename = $"pedidos_{DateTime.UtcNow:yyyyMMdd}";
            var filter = new ExportFilter(utcFrom, utcTo, query.Status, dateField, query.ClientId, query.IsPaid);
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

    }
}

using DeuxOrders.API.Services;
using DeuxOrders.Application.Services;
using DeuxOrders.Domain.Enums;
using DeuxOrders.Domain.Interfaces;
using DeuxOrders.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeuxOrders.API.Controllers
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
            var result = await _service.GetSummaryAsync(startDate, endDate, status);
            return Ok(result);
        }

        [HttpGet("revenue-over-time")]
        public async Task<IActionResult> GetRevenueOverTime(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] OrderStatus? status)
        {
            var result = await _service.GetRevenueOverTimeAsync(startDate, endDate, status);
            return Ok(result);
        }

        [HttpGet("top-products")]
        public async Task<IActionResult> GetTopProducts(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] OrderStatus? status,
            [FromQuery] int limit = 10)
        {
            var result = await _service.GetTopProductsAsync(startDate, endDate, status, limit);
            return Ok(result);
        }

        [HttpGet("top-clients")]
        public async Task<IActionResult> GetTopClients(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] OrderStatus? status,
            [FromQuery] int limit = 10)
        {
            var result = await _service.GetTopClientsAsync(startDate, endDate, status, limit);
            return Ok(result);
        }

        [HttpGet("export")]
        public async Task<IActionResult> Export(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] OrderStatus? status,
            [FromQuery] string format = "csv")
        {
            var rows = await _repository.GetForExportAsync(new ExportFilter(from, to, status));
            var filename = $"pedidos_{DateTime.UtcNow:yyyyMMdd}";

            if (format.Equals("pdf", StringComparison.OrdinalIgnoreCase))
            {
                var pdf = _exportService.GeneratePdf(rows);
                return File(pdf, "application/pdf", $"{filename}.pdf");
            }

            var csv = _exportService.GenerateCsv(rows);
            return File(csv, "text/csv; charset=utf-8", $"{filename}.csv");
        }
    }
}

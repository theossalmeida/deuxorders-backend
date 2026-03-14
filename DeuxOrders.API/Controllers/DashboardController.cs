using DeuxOrders.Application.Services;
using DeuxOrders.Domain.Enums;
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

        public DashboardController(DashboardService service)
        {
            _service = service;
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
    }
}

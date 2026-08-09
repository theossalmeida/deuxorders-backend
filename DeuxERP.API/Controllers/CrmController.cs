using DeuxERP.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeuxERP.API.Controllers
{
    [ApiController]
    [Route("api/v1/crm")]
    [Authorize]
    public class CrmController : ControllerBase
    {
        private readonly IOrderRepository _orderRepository;

        public CrmController(IOrderRepository orderRepository)
        {
            _orderRepository = orderRepository;
        }

        [HttpGet("list")]
        public async Task<IActionResult> GetList(
            CancellationToken ct,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int size = 20)
        {
            if (page < 1) page = 1;
            if (size < 1) size = 1;
            if (size > 500) size = 500;

            var result = await _orderRepository.GetCrmSummariesAsync(page, size, search, ct);

            return Ok(new
            {
                items = result.Items,
                totalCount = result.TotalCount,
                pageNumber = result.PageNumber,
                pageSize = result.PageSize
            });
        }
    }
}

using DeuxOrders.API.Services;
using DeuxOrders.Application.DTOs;
using DeuxOrders.Application.Mapping;
using DeuxOrders.Application.Services;
using DeuxOrders.Domain.Enums;
using DeuxOrders.Domain.Interfaces;
using DeuxOrders.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeuxOrders.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/v1/orders")]
    public class OrderController : ControllerBase
    {
        private readonly IOrderRepository _repository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly OrderService _orderService;
        private readonly ExportService _exportService;

        public OrderController(
            IOrderRepository repository,
            IUnitOfWork unitOfWork,
            OrderService orderService,
            ExportService exportService)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
            _orderService = orderService;
            _exportService = exportService;
        }

        [HttpPost("new")]
        public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
        {
            var order = await _orderService.CreateOrderAsync(request);

            return CreatedAtAction(nameof(GetById), new { id = order.Id }, order.ToResponse());
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOrderRequest request)
        {
            var order = await _orderService.UpdateOrderAsync(id, request);

            return Ok(order.ToResponse(order.Client?.Name ?? "Cliente não encontrado"));
        }

        [HttpPatch("{id}/complete")]
        public async Task<IActionResult> Complete(Guid id)
        {
            var order = await _repository.GetByIdAsync(id);
            if (order == null) return NotFound();

            order.MarkAsCompleted();
            if (!await _unitOfWork.CommitAsync()) return BadRequest("Falha ao salvar no banco.");

            return Ok(order.ToResponse(order.Client?.Name ?? "Cliente não encontrado"));
        }

        [HttpPatch("{id}/cancel")]
        public async Task<IActionResult> Cancel(Guid id)
        {
            var order = await _repository.GetByIdAsync(id);
            if (order == null) return NotFound();

            order.MarkAsCanceled();
            if (!await _unitOfWork.CommitAsync()) return BadRequest("Falha ao salvar no banco.");

            return Ok(order.ToResponse(order.Client?.Name ?? "Cliente não encontrado"));
        }

        [HttpPatch("{id}/items/{productId}/cancel")]
        public async Task<IActionResult> CancelItem(Guid id, Guid productId)
        {
            var order = await _repository.GetByIdAsync(id);
            if (order == null) return NotFound("Pedido não encontrado.");

            order.CancelItem(productId);
            if (!await _unitOfWork.CommitAsync()) return BadRequest("Falha ao salvar no banco.");

            return Ok(order.ToResponse(order.Client?.Name ?? "Cliente não encontrado"));
        }

        [HttpPatch("{id}/items/{productId}/quantity")]
        public async Task<IActionResult> UpdateItemQuantity(Guid id, Guid productId, [FromBody] UpdateItemQuantityRequest request)
        {
            var order = await _repository.GetByIdAsync(id);
            if (order == null) return NotFound("Pedido não encontrado.");

            try
            {
                order.UpdateItemQuantity(productId, request.Increment);
                if (!await _unitOfWork.CommitAsync()) return BadRequest("Falha ao salvar no banco.");

                return Ok(order.ToResponse(order.Client?.Name ?? "Cliente não encontrado"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var order = await _repository.GetByIdAsync(id);
            if (order == null) return NotFound();

            return Ok(order.ToResponse());
        }

        [HttpGet("all")]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int size = 10, [FromQuery] OrderStatus? status = null)
        {
            if (size > 100) size = 100;

            var result = await _repository.GetAllAsync(page, size, status);

            var dtos = result.Items.Select(order =>
                order.ToResponse(order.Client?.Name ?? "Cliente não encontrado")
            ).ToList();

            return Ok(new
            {
                items = dtos,
                totalCount = result.TotalCount,
                pageNumber = result.PageNumber,
                pageSize = result.PageSize
            });
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

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOrder(Guid id)
        {
            var success = await _repository.DeleteAsync(id);

            if (!success)
                return NotFound(new { Message = "Pedido não encontrado." });

            return NoContent();
        }
    }
}
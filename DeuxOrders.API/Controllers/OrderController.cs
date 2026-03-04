using DeuxOrders.Application.Services;
using DeuxOrders.Domain.Enums;
using DeuxOrders.Domain.Interfaces;
using DeuxOrders.Application.DTOs;
using DeuxOrders.Application.Mapping;
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

        public OrderController(
            IOrderRepository repository,
            IUnitOfWork unitOfWork,
            OrderService orderService)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
            _orderService = orderService;
        }

        [HttpPost("new")]
        public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
        {
            var order = await _orderService.CreateOrderAsync(request);

            return CreatedAtAction(nameof(GetById), new { id = order.Id }, order.ToResponse());
        }

        [HttpPatch("{id}/complete")]
        public async Task<IActionResult> Complete(Guid id)
        {
            var order = await _repository.GetByIdAsync(id);
            if (order == null) return NotFound();

            order.MarkAsCompleted();
            if (!await _unitOfWork.CommitAsync()) return BadRequest("Falha ao salvar no banco.");

            return Ok(order);
        }

        [HttpPatch("{id}/cancel")]
        public async Task<IActionResult> Cancel(Guid id)
        {
            var order = await _repository.GetByIdAsync(id);
            if (order == null) return NotFound();

            order.MarkAsCanceled();
            if (!await _unitOfWork.CommitAsync()) return BadRequest("Falha ao salvar no banco.");

            return Ok(order);
        }

        [HttpPatch("{id}/items/{productId}/cancel")]
        public async Task<IActionResult> CancelItem(Guid id, Guid productId)
        {
            var order = await _repository.GetByIdAsync(id);
            if (order == null) return NotFound("Pedido não encontrado.");

            order.CancelItem(productId);
            if (!await _unitOfWork.CommitAsync()) return BadRequest("Falha ao salvar no banco.");

            return Ok(order);
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
                return Ok(order);
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
            return Ok(result);
        }
    }
}
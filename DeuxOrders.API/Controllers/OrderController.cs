using DeuxOrders.API.Services;
using DeuxOrders.Application.DTOs;
using DeuxOrders.Application.Mapping;
using DeuxOrders.Application.Services;
using DeuxOrders.Domain.Enums;
using DeuxOrders.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

public record PresignedUploadRequest(string FileName, string ContentType);

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
        private readonly IStorageService _storageService;

        public OrderController(
            IOrderRepository repository,
            IUnitOfWork unitOfWork,
            OrderService orderService,
            IStorageService storageService)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
            _orderService = orderService;
            _storageService = storageService;
        }

        [HttpPost("references/presigned-url")]
        public IActionResult GetPresignedUploadUrl([FromBody] PresignedUploadRequest request)
        {
            var extension = Path.GetExtension(request.FileName);
            var objectKey = $"order-references/{Guid.NewGuid()}{extension}";
            var uploadUrl = _storageService.GeneratePresignedUploadUrl(objectKey, request.ContentType);

            return Ok(new { uploadUrl, objectKey });
        }

        [HttpPost("new")]
        public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
        {
            var order = await _orderService.CreateOrderAsync(request);
            var signedUrls = _storageService.GetSignedReadUrls(order.References);

            return CreatedAtAction(nameof(GetById), new { id = order.Id }, order.ToResponse(signedReferenceUrls: signedUrls));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOrderRequest request)
        {
            var order = await _orderService.UpdateOrderAsync(id, request);
            var signedUrls = _storageService.GetSignedReadUrls(order.References);

            return Ok(order.ToResponse(order.Client?.Name ?? "Cliente não encontrado", signedUrls));
        }

        [HttpPatch("{id}/complete")]
        public async Task<IActionResult> Complete(Guid id)
        {
            var order = await _repository.GetByIdAsync(id);
            if (order == null) return NotFound();

            order.MarkAsCompleted();
            if (!await _unitOfWork.CommitAsync()) return BadRequest("Falha ao salvar no banco.");

            var signedUrls = _storageService.GetSignedReadUrls(order.References);
            return Ok(order.ToResponse(order.Client?.Name ?? "Cliente não encontrado", signedUrls));
        }

        [HttpPatch("{id}/cancel")]
        public async Task<IActionResult> Cancel(Guid id)
        {
            var order = await _repository.GetByIdAsync(id);
            if (order == null) return NotFound();

            order.MarkAsCanceled();
            if (!await _unitOfWork.CommitAsync()) return BadRequest("Falha ao salvar no banco.");

            var signedUrls = _storageService.GetSignedReadUrls(order.References);
            return Ok(order.ToResponse(order.Client?.Name ?? "Cliente não encontrado", signedUrls));
        }

        [HttpPatch("{id}/items/{productId}/cancel")]
        public async Task<IActionResult> CancelItem(Guid id, Guid productId)
        {
            var order = await _repository.GetByIdAsync(id);
            if (order == null) return NotFound("Pedido não encontrado.");

            order.CancelItem(productId);
            if (!await _unitOfWork.CommitAsync()) return BadRequest("Falha ao salvar no banco.");

            var signedUrls = _storageService.GetSignedReadUrls(order.References);
            return Ok(order.ToResponse(order.Client?.Name ?? "Cliente não encontrado", signedUrls));
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

                var signedUrls = _storageService.GetSignedReadUrls(order.References);
                return Ok(order.ToResponse(order.Client?.Name ?? "Cliente não encontrado", signedUrls));
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

            var signedUrls = _storageService.GetSignedReadUrls(order.References);
            return Ok(order.ToResponse(order.Client?.Name ?? "", signedUrls));
        }

        [HttpGet("all")]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int size = 10, [FromQuery] OrderStatus? status = null)
        {
            if (size > 100) size = 100;

            var result = await _repository.GetAllAsync(page, size, status);

            var dtos = result.Items.Select(order =>
                order.ToResponse(order.Client?.Name ?? "Cliente não encontrado", _storageService.GetSignedReadUrls(order.References))
            ).ToList();

            return Ok(new
            {
                items = dtos,
                totalCount = result.TotalCount,
                pageNumber = result.PageNumber,
                pageSize = result.PageSize
            });
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
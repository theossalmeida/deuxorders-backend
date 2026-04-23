using DeuxERP.API.Services;
using DeuxERP.Application.Common;
using DeuxERP.Application.DTOs;
using DeuxERP.Application.Mapping;
using DeuxERP.Application.Services;
using DeuxERP.Domain.Interfaces;
using DeuxERP.Domain.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

public record PresignedUploadRequest(string FileName, string ContentType);
public record RemoveReferenceRequest(string ObjectKey);
public record UnpayRequest(string Reason);

namespace DeuxERP.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/v1/orders")]
    public class OrderController : ControllerBase
    {
        private readonly IOrderRepository _repository;
        private readonly IAppDbContext _db;
        private readonly OrderService _orderService;
        private readonly InventoryService _inventoryService;
        private readonly IStorageService _storageService;
        private readonly ILogger<OrderController> _logger;

        public OrderController(
            IOrderRepository repository,
            IAppDbContext db,
            OrderService orderService,
            InventoryService inventoryService,
            IStorageService storageService,
            ILogger<OrderController> logger)
        {
            _repository = repository;
            _db = db;
            _orderService = orderService;
            _inventoryService = inventoryService;
            _storageService = storageService;
            _logger = logger;
        }

        [HttpDelete("{id}/references")]
        public async Task<IActionResult> RemoveReference(Guid id, [FromBody] RemoveReferenceRequest request)
        {
            var order = await _repository.GetByIdAsync(id);
            if (order == null) return NotFound();

            order.RemoveReference(request.ObjectKey);

            if (await _db.SaveChangesAsync() == 0) return BadRequest("Falha ao salvar no banco.");

            try
            {
                await _storageService.DeleteObjectAsync(request.ObjectKey);
            }
            catch
            {
                order.AppendReferences(new List<string> { request.ObjectKey });
                if (await _db.SaveChangesAsync() == 0)
                {
                    _logger.LogCritical(
                        "Failed to restore order reference {ObjectKey} after storage delete failure for order {OrderId}.",
                        request.ObjectKey,
                        id);
                    return StatusCode(StatusCodes.Status500InternalServerError, "Falha ao restaurar a referência do pedido após erro no armazenamento.");
                }

                return StatusCode(StatusCodes.Status502BadGateway, "Falha ao remover a referência do armazenamento. A referência foi restaurada no pedido.");
            }

            var signedUrls = _storageService.GetSignedReadUrls(order.References);
            return Ok(order.ToResponse(order.Client?.Name ?? string.Empty, signedUrls));
        }

        [HttpPost("references/presigned-url")]
        public IActionResult GetPresignedUploadUrl([FromBody] PresignedUploadRequest request)
        {
            if (!FileValidation.IsAllowedImage(request.FileName, request.ContentType))
                return BadRequest("Tipo de arquivo não permitido. Use JPG, PNG ou WebP.");

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
            var (order, warnings) = await _orderService.UpdateOrderAsync(id, request);
            var signedUrls = _storageService.GetSignedReadUrls(order.References);
            var response = order.ToResponse(order.Client?.Name ?? "Cliente não encontrado", signedUrls);

            if (warnings.Count > 0)
                return Ok(new { response, warnings });

            return Ok(response);
        }

        [HttpPatch("{id}/complete")]
        public async Task<IActionResult> Complete(Guid id)
        {
            var order = await _repository.GetByIdAsync(id);
            if (order == null) return NotFound();

            order.MarkAsCompleted();
            if (await _db.SaveChangesAsync() == 0) return BadRequest("Falha ao salvar no banco.");

            var signedUrls = _storageService.GetSignedReadUrls(order.References);
            return Ok(order.ToResponse(order.Client?.Name ?? "Cliente não encontrado", signedUrls));
        }

        [HttpPatch("{id}/cancel")]
        public async Task<IActionResult> Cancel(Guid id)
        {
            var order = await _repository.GetByIdAsync(id);
            if (order == null) return NotFound();

            var shouldRestoreInventory = order.Status == OrderStatus.Preparing
                || order.Status == OrderStatus.WaitingPickupOrDelivery;

            if (shouldRestoreInventory)
                await _inventoryService.RestoreForOrderAsync(order);

            order.MarkAsCanceled();
            if (await _db.SaveChangesAsync() == 0) return BadRequest("Falha ao salvar no banco.");

            var signedUrls = _storageService.GetSignedReadUrls(order.References);
            return Ok(order.ToResponse(order.Client?.Name ?? "Cliente não encontrado", signedUrls));
        }

        [HttpPatch("{id}/items/{productId}/cancel")]
        public async Task<IActionResult> CancelItem(Guid id, Guid productId)
        {
            var order = await _repository.GetByIdAsync(id);
            if (order == null) return NotFound("Pedido não encontrado.");

            var item = order.Items.FirstOrDefault(orderItem => orderItem.ProductId == productId);
            if (item == null) return NotFound("Item não encontrado no pedido.");

            order.CancelItem(productId);

            if (order.Status == OrderStatus.Preparing || order.Status == OrderStatus.WaitingPickupOrDelivery)
                await _inventoryService.AdjustForItemAsync(productId, -item.Quantity);

            if (await _db.SaveChangesAsync() == 0) return BadRequest("Falha ao salvar no banco.");

            var signedUrls = _storageService.GetSignedReadUrls(order.References);
            return Ok(order.ToResponse(order.Client?.Name ?? "Cliente não encontrado", signedUrls));
        }

        [HttpPatch("{id}/items/{productId}/quantity")]
        public async Task<IActionResult> UpdateItemQuantity(Guid id, Guid productId, [FromBody] UpdateItemQuantityRequest request)
        {
            var order = await _repository.GetByIdAsync(id);
            if (order == null) return NotFound("Pedido não encontrado.");

            order.UpdateItemQuantity(productId, request.Increment);

            var warnings = new List<string>();
            if (order.Status == OrderStatus.Preparing || order.Status == OrderStatus.WaitingPickupOrDelivery)
                warnings = await _inventoryService.AdjustForItemAsync(productId, request.Increment);

            if (await _db.SaveChangesAsync() == 0) return BadRequest("Falha ao salvar no banco.");

            var signedUrls = _storageService.GetSignedReadUrls(order.References);
            var response = order.ToResponse(order.Client?.Name ?? "Cliente não encontrado", signedUrls);

            if (warnings.Count > 0)
                return Ok(new { response, warnings });

            return Ok(response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var order = await _repository.GetByIdAsync(id);
            if (order == null) return NotFound();

            var signedUrls = _storageService.GetSignedReadUrls(order.References);
            return Ok(order.ToResponse(order.Client?.Name ?? string.Empty, signedUrls));
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

        [HttpPatch("{id}/pay")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> MarkAsPaid(Guid id)
        {
            var order = await _repository.GetByIdAsync(id);
            if (order == null) return NotFound();

            var userId = Guid.Parse(User.FindFirst("id")!.Value);
            var userName = User.FindFirst("email")!.Value;

            order.MarkAsPaid(userId, userName, DateTime.UtcNow);
            await _db.SaveChangesAsync();

            var signedUrls = _storageService.GetSignedReadUrls(order.References);
            return Ok(order.ToResponse(order.Client?.Name ?? string.Empty, signedUrls));
        }

        [HttpPatch("{id}/unpay")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> UnmarkAsPaid(Guid id, [FromBody] UnpayRequest request)
        {
            var order = await _repository.GetByIdAsync(id);
            if (order == null) return NotFound();

            var userId = Guid.Parse(User.FindFirst("id")!.Value);
            var userName = User.FindFirst("email")!.Value;

            order.UnmarkAsPaid(userId, userName, request.Reason);
            await _db.SaveChangesAsync();

            var signedUrls = _storageService.GetSignedReadUrls(order.References);
            return Ok(order.ToResponse(order.Client?.Name ?? string.Empty, signedUrls));
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

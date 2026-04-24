using DeuxERP.API.Services;
using DeuxERP.Application.Common;
using DeuxERP.Application.DTOs;
using DeuxERP.Application.Mapping;
using DeuxERP.Application.Services;
using DeuxERP.Domain.Interfaces;
using DeuxERP.Domain.Sales;
using DeuxERP.Domain.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public record PresignedUploadRequest(string FileName, string ContentType, Guid? OrderId);
public record RemoveReferenceRequest(string ObjectKey);
public record UnpayRequest(string Reason);

namespace DeuxERP.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/v1/orders")]
    public class OrderController : ControllerBase
    {
        private readonly IAppDbContext _db;
        private readonly IOrderRepository _repository;
        private readonly OrderService _orderService;
        private readonly IStorageService _storageService;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly ILogger<OrderController> _logger;

        public OrderController(
            IAppDbContext db,
            IOrderRepository repository,
            OrderService orderService,
            IStorageService storageService,
            ICurrentUserAccessor currentUser,
            ILogger<OrderController> logger)
        {
            _db = db;
            _repository = repository;
            _orderService = orderService;
            _storageService = storageService;
            _currentUser = currentUser;
            _logger = logger;
        }

        [HttpDelete("{id}/references")]
        public async Task<IActionResult> RemoveReference(Guid id, [FromBody] RemoveReferenceRequest request)
        {
            var order = await _orderService.LoadTrackedOrderAsync(id);
            if (order == null) return NotFound();

            await _orderService.RemoveReferenceAsync(order, request.ObjectKey);

            try
            {
                await _storageService.DeleteObjectAsync(request.ObjectKey);
            }
            catch
            {
                order.AppendReferences([request.ObjectKey]);
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
        public async Task<IActionResult> GetPresignedUploadUrl([FromBody] PresignedUploadRequest request)
        {
            if (!FileValidation.IsAllowedImage(request.FileName, request.ContentType))
                return BadRequest("Tipo de arquivo não permitido. Use JPG, PNG ou WebP.");

            var extension = Path.GetExtension(request.FileName);
            var objectKey = $"{OrderReferenceObjectKey.Prefix}{Guid.NewGuid()}{extension}";
            var uploadUrl = _storageService.GeneratePresignedUploadUrl(objectKey, request.ContentType);
            var session = new OrderReferenceUpload(
                objectKey,
                _currentUser.UserId,
                request.OrderId,
                request.ContentType,
                DateTime.UtcNow.AddMinutes(15));

            _db.OrderReferenceUploads.Add(session);
            await _db.SaveChangesAsync();

            return Ok(new { uploadUrl, objectKey });
        }

        [HttpPost("new")]
        public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
        {
            var order = await _orderService.CreateOrderAsync(request);
            var responseOrder = await _repository.GetByIdReadOnlyAsync(order.Id);
            if (responseOrder == null)
                return StatusCode(StatusCodes.Status500InternalServerError, "Pedido criado, mas não foi possível recarregar os detalhes.");

            var signedUrls = _storageService.GetSignedReadUrls(responseOrder.References);
            return CreatedAtAction(
                nameof(GetById),
                new { id = responseOrder.Id },
                responseOrder.ToResponse(responseOrder.Client?.Name ?? string.Empty, signedUrls));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOrderRequest request)
        {
            var (order, warnings) = await _orderService.UpdateOrderAsync(id, request);
            var responseOrder = await _repository.GetByIdReadOnlyAsync(order.Id);
            if (responseOrder == null) return NotFound();

            var signedUrls = _storageService.GetSignedReadUrls(responseOrder.References);
            var response = responseOrder.ToResponse(responseOrder.Client?.Name ?? "Cliente não encontrado", signedUrls);

            if (warnings.Count > 0)
                return Ok(new { response, warnings });

            return Ok(response);
        }

        [HttpPatch("{id}/complete")]
        public async Task<IActionResult> Complete(Guid id)
        {
            var order = await _orderService.LoadTrackedOrderAsync(id);
            if (order == null) return NotFound();

            order = await _orderService.CompleteAsync(order);

            var signedUrls = _storageService.GetSignedReadUrls(order.References);
            return Ok(order.ToResponse(order.Client?.Name ?? "Cliente não encontrado", signedUrls));
        }

        [HttpPatch("{id}/cancel")]
        public async Task<IActionResult> Cancel(Guid id)
        {
            var order = await _orderService.LoadTrackedOrderAsync(id);
            if (order == null) return NotFound();

            order = await _orderService.CancelAsync(order);

            var signedUrls = _storageService.GetSignedReadUrls(order.References);
            return Ok(order.ToResponse(order.Client?.Name ?? "Cliente não encontrado", signedUrls));
        }

        [HttpPatch("{id}/items/{productId}/cancel")]
        public async Task<IActionResult> CancelItem(Guid id, Guid productId)
        {
            var order = await _orderService.LoadTrackedOrderAsync(id);
            if (order == null) return NotFound("Pedido não encontrado.");

            if (!order.Items.Any(item => item.ProductId == productId))
                return NotFound("Item não encontrado no pedido.");

            order = await _orderService.CancelItemAsync(order, productId);

            var signedUrls = _storageService.GetSignedReadUrls(order.References);
            return Ok(order.ToResponse(order.Client?.Name ?? "Cliente não encontrado", signedUrls));
        }

        [HttpPatch("{id}/items/{productId}/quantity")]
        public async Task<IActionResult> UpdateItemQuantity(Guid id, Guid productId, [FromBody] UpdateItemQuantityRequest request)
        {
            var order = await _orderService.LoadTrackedOrderAsync(id);
            if (order == null) return NotFound("Pedido não encontrado.");

            if (!order.Items.Any(item => item.ProductId == productId))
                return NotFound("Item não encontrado no pedido.");

            var (updatedOrder, warnings) = await _orderService.UpdateItemQuantityAsync(order, productId, request.Increment);
            var signedUrls = _storageService.GetSignedReadUrls(updatedOrder.References);
            var response = updatedOrder.ToResponse(updatedOrder.Client?.Name ?? "Cliente não encontrado", signedUrls);

            if (warnings.Count > 0)
                return Ok(new { response, warnings });

            return Ok(response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var order = await _repository.GetByIdReadOnlyAsync(id);
            if (order == null) return NotFound();

            var signedUrls = _storageService.GetSignedReadUrls(order.References);
            return Ok(order.ToResponse(order.Client?.Name ?? string.Empty, signedUrls));
        }

        [HttpGet("all")]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int size = 10,
            [FromQuery] OrderStatus? status = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] string? search = null)
        {
            if (size > 100) size = 100;

            var utcFrom = from.HasValue
                ? DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Utc)
                : (DateTime?)null;
            var utcTo = to.HasValue
                ? DateTime.SpecifyKind(to.Value.Date, DateTimeKind.Utc)
                : (DateTime?)null;

            var result = await _repository.GetAllAsync(page, size, status, utcFrom, utcTo, search);

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
            var order = await _orderService.LoadTrackedOrderAsync(id);
            if (order == null) return NotFound();

            order = await _orderService.MarkAsPaidAsync(order);

            var signedUrls = _storageService.GetSignedReadUrls(order.References);
            return Ok(order.ToResponse(order.Client?.Name ?? string.Empty, signedUrls));
        }

        [HttpPatch("{id}/unpay")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> UnmarkAsPaid(Guid id, [FromBody] UnpayRequest request)
        {
            var order = await _orderService.LoadTrackedOrderAsync(id);
            if (order == null) return NotFound();

            order = await _orderService.UnmarkAsPaidAsync(order, request.Reason);

            var signedUrls = _storageService.GetSignedReadUrls(order.References);
            return Ok(order.ToResponse(order.Client?.Name ?? string.Empty, signedUrls));
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> DeleteOrder(Guid id)
        {
            var order = await _orderService.LoadTrackedOrderAsync(id);

            if (order == null)
                return NotFound(new { Message = "Pedido não encontrado." });

            if (order.Status != OrderStatus.Canceled)
                await _orderService.CancelAsync(order);

            return NoContent();
        }
    }
}

using DeuxOrders.Domain.Entities;
using DeuxOrders.Domain.Enums;
using DeuxOrders.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
[ApiController]
[Route("api/v1/orders")]
public class OrderController : ControllerBase
{
    private readonly IOrderRepository _repository;
    private readonly IClientRepository _clientRepository;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    public OrderController(
        IOrderRepository repository,
        IClientRepository clientRepository,
        IProductRepository productRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _clientRepository = clientRepository;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    [HttpPost("new")]
    public async Task<IActionResult> Create(CreateOrderRequest request)
    {
        var client = await _clientRepository.GetByIdAsync(request.ClientId);
        if (client == null || !client.Status)
            return BadRequest("Cliente inválido ou inativo.");

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var dbProducts = await _productRepository.GetByManyIdsAsync(productIds);

        var order = new Order(request.ClientId);

        foreach (var item in request.Items)
        {
            var product = dbProducts.FirstOrDefault(p => p.Id == item.ProductId);

            if (product == null)
                return BadRequest($"Produto {item.ProductId} não encontrado.");

            if (!product.ProductStatus)
                return BadRequest($"Produto {product.Name} está inativo.");

            order.AddItem(item.ProductId, item.Quantity, item.UnitPrice, product.Price);
        }

        _repository.Add(order);

        var success = await _unitOfWork.CommitAsync();
        if (!success)
            return BadRequest("Falha ao salvar o pedido no banco de dados.");

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    [HttpPatch("{id}/complete")]
    public async Task<IActionResult> Complete(Guid id)
    {
        var order = await _repository.GetByIdAsync(id);
        if (order == null) return NotFound();

        order.MarkAsCompleted();

        var success = await _unitOfWork.CommitAsync();
        if (!success) return BadRequest("Falha ao completar o pedido no banco de dados.");

        return Ok(order);
    }

    [HttpPatch("{id}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var order = await _repository.GetByIdAsync(id);
        if (order == null) return NotFound();

        order.MarkAsCanceled();

        var success = await _unitOfWork.CommitAsync();
        if (!success) return BadRequest("Falha ao cancelar o pedido no banco de dados.");

        return Ok(order);
    }

    [HttpPatch("{id}/items/{productId}/cancel")]
    public async Task<IActionResult> CancelItem(Guid id, Guid productId)
    {
        var order = await _repository.GetByIdAsync(id);
        if (order == null) return NotFound("Pedido não encontrado.");

        order.CancelItem(productId);

        var success = await _unitOfWork.CommitAsync();
        if (!success)
            return BadRequest("Falha ao cancelar o item do pedido no banco de dados.");

        return Ok(order);
    }

    [HttpPatch("{id}/items/{productId}/quantity")]
    public async Task<IActionResult> UpdateItemQuantity(Guid id, Guid productId, [FromBody] UpdateItemQuantityRequest request)
    {
        var order = await _repository.GetByIdAsync(id);
        if (order == null) return NotFound("Pedido não encontrado.");

        order.UpdateItemQuantity(productId, request.Increment);

        var success = await _unitOfWork.CommitAsync();
        if (!success)
            return BadRequest("Falha ao editar a quantidade do item no pedido no banco de dados.");

        return Ok(order);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var order = await _repository.GetByIdAsync(id);
        if (order == null) return NotFound();

        return Ok(order);
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int size = 10, [FromQuery] OrderStatus? status = null)
    {
        if (size > 100) size = 100;

        var result = await _repository.GetAllAsync(page, size, status);
        return Ok(result);
    }
}
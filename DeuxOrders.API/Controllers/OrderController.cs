using DeuxOrders.Domain.Entities;
using DeuxOrders.Domain.Enums;
using DeuxOrders.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/orders")]
public class OrderController : ControllerBase
{
    private readonly IOrderRepository _repository;
    private readonly IClientRepository _clientRepository;
    private readonly IProductRepository _productRepository;

    public OrderController(
        IOrderRepository repository,
        IClientRepository clientRepository,
        IProductRepository productRepository
        )
    {
        _repository = repository;
        _clientRepository = clientRepository;
        _productRepository = productRepository;
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
            // Busca o produto correspondente na lista que veio do banco
            var product = dbProducts.FirstOrDefault(p => p.Id == item.ProductId);

            if (product == null)
                return BadRequest($"Produto {item.ProductId} não encontrado.");

            if (!product.ProductStatus)
                return BadRequest($"Produto {product.Name} está inativo.");

            order.AddItem(item.ProductId, item.Quantity, item.UnitPrice);
        }

        await _repository.AddAsync(order);
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    [HttpPatch("{id}/complete")]
    public async Task<IActionResult> Complete(Guid id)
    {
        var order = await _repository.GetByIdAsync(id);
        if (order == null) return NotFound();

        try
        {
            order.MarkAsCompleted();
            await _repository.UpdateAsync(order);
            return Ok(order);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPatch("{id}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var order = await _repository.GetByIdAsync(id);
        if (order == null) return NotFound();

        try
        {
            order.MarkAsCanceled();
            await _repository.UpdateAsync(order);
            return Ok(order);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPatch("{id}/items/{productId}/cancel")]
    public async Task<IActionResult> CancelItem(Guid id, Guid productId)
    {
        var order = await _repository.GetByIdAsync(id);
        var product = await _productRepository.GetByIdAsync(productId);
        if (order == null) return NotFound("Pedido não encontrado.");
        if (product == null) return NotFound("Produto não encontrado no pedido.");

        try
        {
            
            order.CancelItem(productId);

            await _repository.UpdateAsync(order);

            return Ok(order);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPatch("{id}/items/{productId}/quantity")]
    public async Task<IActionResult> UpdateItemQuantity(Guid id, Guid productId, [FromBody] UpdateItemQuantityRequest request)
    {
        var order = await _repository.GetByIdAsync(id);
        if (order == null) return NotFound("Pedido não encontrado.");

        try
        {
            order.UpdateItemQuantity(productId, request.Increment);

            await _repository.UpdateAsync(order);
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
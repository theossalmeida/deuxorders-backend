using DeuxOrders.Domain.Entities;
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

        if (client == null)
            return BadRequest("Cliente do pedido não existe.");
        
        if (!client.Status)
            return BadRequest("Cliente do pedido está inativo.");

        var order = new Order(request.ClientId);

        foreach (var item in request.Items)
        {
            var product = await _productRepository.GetByIdAsync(item.ProductId);
            if (product == null)
                return BadRequest("Produto do pedido não existe.");

            if (!product.ProductStatus)
                return BadRequest("Produto do pedido está inativo.");
            order.AddItem(item.ProductId, item.Quantity, item.UnitPrice);
        }

        await _repository.AddAsync(order);

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var order = await _repository.GetByIdAsync(id);
        if (order == null) return NotFound();
        return Ok(order);
    }
}
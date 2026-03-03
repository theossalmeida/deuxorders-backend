using DeuxOrders.Domain.Entities;
using DeuxOrders.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
[Route("api/v1/products")]
public class ProductController : ControllerBase
{
    private readonly IProductRepository _repository;

    public ProductController(IProductRepository repository)
    {
        _repository = repository;
    }

    [HttpPost("new")]
    public async Task<IActionResult> Create([FromBody] CreateProduct request)
    {
        if (string.IsNullOrEmpty(request.Name))
        {
            return BadRequest("O nome do produto é obrigatório.");
        }

        var product = new Product(request.Name);

        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            product.SetDescription(request.Description);
        }

        await _repository.AddAsync(product);

        return CreatedAtRoute("GetProductById", new { id = product.Id }, product);
    }

    [HttpGet("{id}", Name = "GetProductById")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var product = await _repository.GetByIdAsync(id);
        if (product == null) return NotFound();
        return Ok(product);
    }
}
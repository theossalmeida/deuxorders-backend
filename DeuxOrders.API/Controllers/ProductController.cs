using DeuxOrders.Domain.Entities;
using DeuxOrders.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/products")]
public class ProductController : ControllerBase
{
    private readonly IProductRepository _repository;

    public ProductController(IProductRepository repository)
    {
        _repository = repository;
    }

    [HttpPost("new")]
    public async Task<IActionResult> Create(CreateProduct request)
    {

        var product = new Product(request.Name);
        if (request.Description != null)
        {
            product.SetDescription(request.Description);
        }

        await _repository.AddAsync(product);

        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var product = await _repository.GetByIdAsync(id);
        if (product == null) return NotFound();
        return Ok(product);
    }
}
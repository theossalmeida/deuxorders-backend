using DeuxOrders.Domain.Entities;
using DeuxOrders.Domain.Interfaces;
using DeuxOrders.Application.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
[ApiController]
[Route("api/v1/products")]
public class ProductController : ControllerBase
{
    private readonly IProductRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ProductController(IProductRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    [HttpPost("new")]
    public async Task<IActionResult> Create([FromBody] CreateProduct request)
    {
        var product = new Product(request.Name, request.Price);

        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            product.SetDescription(request.Description);
        }

        _repository.Add(product);

        var success = await _unitOfWork.CommitAsync();
        if (!success)
            return BadRequest("Falha ao salvar o produto no banco de dados.");

        return CreatedAtRoute("GetProductById", new { id = product.Id }, product);
    }

    [HttpGet("{id}", Name = "GetProductById")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var product = await _repository.GetByIdAsync(id);
        if (product == null) return NotFound();
        return Ok(product);
    }
    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        var products = await _repository.GetAllAsync();

        var response = products.Select(p => p.ToResponse());

        return Ok(response);
    }
}
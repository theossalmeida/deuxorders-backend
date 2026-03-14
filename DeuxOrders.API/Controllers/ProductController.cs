using DeuxOrders.Application.Mapping;
using DeuxOrders.Domain.Entities;
using DeuxOrders.Domain.Interfaces;
using DeuxOrders.Infrastructure.Repositories;
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

    [HttpPatch("{id}/inactive", Name = "SetProductInactive")]
    public async Task<IActionResult> DeactivateProduct(Guid id)
    {
        var product = await _repository.GetByIdAsync(id);
        if (product == null) return NotFound();
        if (!product.ProductStatus) return BadRequest("Produto já está desativado.");
        product.ChangeProductStatus();

        var success = await _unitOfWork.CommitAsync();
        if (!success)
            return BadRequest("Falha ao desativar o produto no banco de dados.");
        
        return Ok(product);
    }

    [HttpPatch("{id}/active", Name = "SetProductActive")]
    public async Task<IActionResult> ActivateProduct(Guid id)
    {
        var product = await _repository.GetByIdAsync(id);
        if (product == null) return NotFound();
        if(product.ProductStatus) return BadRequest("Produto já está ativo.");
        
        product.ChangeProductStatus();

        var success = await _unitOfWork.CommitAsync();
        if (!success)
            return BadRequest("Falha ao ativar o produto no banco de dados.");

        return Ok(product);
    }

    [HttpGet("dropdown")]
    public async Task<IActionResult> GetForDropdown([FromQuery] bool? status)
    {
        var result = await _repository.GetForDropdownAsync(status);
        return Ok(result);
    }

    [HttpGet("{id}", Name = "GetProductById")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var product = await _repository.GetByIdAsync(id);
        if (product == null) return NotFound();
        return Ok(product);
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] bool? status)
    {
        var products = await _repository.GetAllAsync(search, status);
        return Ok(products.Select(p => p.ToResponse()));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProduct(Guid id)
    {
        try
        {
            var success = await _repository.DeleteAsync(id);

            if (!success)
                return NotFound(new { Message = "Produto não encontrado." });

            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = "Não é possível deletar este produto pois ele pertence a um pedido existente." });
        }
    }
}
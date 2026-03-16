using DeuxOrders.API.Models;
using DeuxOrders.API.Services;
using DeuxOrders.Application.Mapping;
using DeuxOrders.Domain.Entities;
using DeuxOrders.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
[ApiController]
[Route("api/v1/products")]
public class ProductController : ControllerBase
{
    private readonly IProductRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStorageService _storageService;

    public ProductController(IProductRepository repository, IUnitOfWork unitOfWork, IStorageService storageService)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _storageService = storageService;
    }

    [HttpPost("new")]
    public async Task<IActionResult> Create([FromForm] CreateProductRequest request)
    {
        var product = new Product(request.Name, request.Price);

        if (!string.IsNullOrWhiteSpace(request.Description))
            product.SetDescription(request.Description);

        if (request.Image != null)
        {
            var extension = Path.GetExtension(request.Image.FileName);
            var objectKey = $"products-images/{Guid.NewGuid()}{extension}";
            using var stream = request.Image.OpenReadStream();
            await _storageService.UploadFileAsync(stream, objectKey, request.Image.ContentType);
            product.SetImage(objectKey);
        }

        _repository.Add(product);

        if (!await _unitOfWork.CommitAsync())
            return BadRequest("Falha ao salvar o produto no banco de dados.");

        var imageUrl = product.Image != null ? _storageService.GetPublicUrl(product.Image) : null;
        return CreatedAtRoute("GetProductById", new { id = product.Id }, product.ToResponse(imageUrl));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromForm] UpdateProductRequest request)
    {
        var product = await _repository.GetByIdAsync(id);
        if (product == null) return NotFound();

        string? newObjectKey = null;

        if (request.Image != null)
        {
            var extension = Path.GetExtension(request.Image.FileName);
            newObjectKey = $"products-images/{Guid.NewGuid()}{extension}";
            using var stream = request.Image.OpenReadStream();
            await _storageService.UploadFileAsync(stream, newObjectKey, request.Image.ContentType);
        }

        var oldObjectKey = product.Image;
        product.Update(request.Name, request.Price, request.Description, newObjectKey ?? product.Image);

        if (!await _unitOfWork.CommitAsync())
            return BadRequest("Falha ao atualizar o produto no banco de dados.");

        if (newObjectKey != null && oldObjectKey != null)
            await _storageService.DeleteObjectAsync(oldObjectKey);

        var imageUrl = product.Image != null ? _storageService.GetPublicUrl(product.Image) : null;
        return Ok(product.ToResponse(imageUrl));
    }

    [HttpDelete("{id}/image")]
    public async Task<IActionResult> DeleteImage(Guid id)
    {
        var product = await _repository.GetByIdAsync(id);
        if (product == null) return NotFound();
        if (product.Image == null) return BadRequest("Produto não possui imagem.");

        var objectKey = product.Image;
        product.SetImage(null);

        if (!await _unitOfWork.CommitAsync())
            return BadRequest("Falha ao remover a imagem do banco de dados.");

        await _storageService.DeleteObjectAsync(objectKey);

        return Ok(product.ToResponse());
    }

    [HttpPatch("{id}/inactive", Name = "SetProductInactive")]
    public async Task<IActionResult> DeactivateProduct(Guid id)
    {
        var product = await _repository.GetByIdAsync(id);
        if (product == null) return NotFound();
        if (!product.ProductStatus) return BadRequest("Produto já está desativado.");
        product.ChangeProductStatus();

        if (!await _unitOfWork.CommitAsync())
            return BadRequest("Falha ao desativar o produto no banco de dados.");

        var imageUrl = product.Image != null ? _storageService.GetPublicUrl(product.Image) : null;
        return Ok(product.ToResponse(imageUrl));
    }

    [HttpPatch("{id}/active", Name = "SetProductActive")]
    public async Task<IActionResult> ActivateProduct(Guid id)
    {
        var product = await _repository.GetByIdAsync(id);
        if (product == null) return NotFound();
        if (product.ProductStatus) return BadRequest("Produto já está ativo.");

        product.ChangeProductStatus();

        if (!await _unitOfWork.CommitAsync())
            return BadRequest("Falha ao ativar o produto no banco de dados.");

        var imageUrl = product.Image != null ? _storageService.GetPublicUrl(product.Image) : null;
        return Ok(product.ToResponse(imageUrl));
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
        var imageUrl = product.Image != null ? _storageService.GetPublicUrl(product.Image) : null;
        return Ok(product.ToResponse(imageUrl));
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] bool? status)
    {
        var products = await _repository.GetAllAsync(search, status);
        return Ok(products.Select(p =>
        {
            var imageUrl = p.Image != null ? _storageService.GetPublicUrl(p.Image) : null;
            return p.ToResponse(imageUrl);
        }));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProduct(Guid id)
    {
        try
        {
            var product = await _repository.GetByIdAsync(id);
            if (product == null) return NotFound(new { Message = "Produto não encontrado." });

            var objectKey = product.Image;
            var success = await _repository.DeleteAsync(id);
            if (!success) return NotFound(new { Message = "Produto não encontrado." });

            if (objectKey != null)
                await _storageService.DeleteObjectAsync(objectKey);

            return NoContent();
        }
        catch
        {
            return BadRequest(new { Message = "Não é possível deletar este produto pois ele pertence a um pedido existente." });
        }
    }
}

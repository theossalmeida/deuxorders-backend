using DeuxERP.API.Models;
using DeuxERP.API.Services;
using DeuxERP.Application.Common;
using DeuxERP.Application.DTOs;
using DeuxERP.Application.Mapping;
using DeuxERP.Domain.Interfaces;
using DeuxERP.Domain.Inventory;
using DeuxERP.Domain.Models;
using DeuxERP.Domain.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Authorize]
[ApiController]
[Route("api/v1/products")]
public class ProductController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly IOrderRepository _orderRepository;
    private readonly IStorageService _storageService;
    private readonly ILogger<ProductController> _logger;

    public ProductController(
        IAppDbContext db,
        IOrderRepository orderRepository,
        IStorageService storageService,
        ILogger<ProductController> logger)
    {
        _db = db;
        _orderRepository = orderRepository;
        _storageService = storageService;
        _logger = logger;
    }

    [HttpPost("new")]
    public async Task<IActionResult> Create([FromForm] CreateProductRequest request)
    {
        var product = new Product(request.Name, request.Price, request.Category, request.Size);
        string? uploadedObjectKey = null;

        if (!string.IsNullOrWhiteSpace(request.Description))
            product.SetDescription(request.Description);

        if (request.Image != null)
        {
            if (!FileValidation.IsAllowedImage(request.Image))
                return BadRequest("Tipo de imagem não permitido. Use JPG, PNG ou WebP.");

            if (request.Image.Length > 5 * 1024 * 1024)
                return BadRequest("A imagem não pode ser maior que 5 MB.");

            var extension = Path.GetExtension(request.Image.FileName);
            uploadedObjectKey = $"products-images/{Guid.NewGuid()}{extension}";
            using var stream = request.Image.OpenReadStream();
            await _storageService.UploadFileAsync(stream, uploadedObjectKey, request.Image.ContentType);
            product.SetImage(uploadedObjectKey);
        }

        _db.Products.Add(product);

        if (await SaveProductChangesOrCleanupAsync(uploadedObjectKey, "create commit exception") == 0)
        {
            if (uploadedObjectKey != null)
            {
                try
                {
                    await _storageService.DeleteObjectAsync(uploadedObjectKey);
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, "Failed to cleanup uploaded product image {ObjectKey} after create commit failure.", uploadedObjectKey);
                    return StatusCode(StatusCodes.Status502BadGateway, "Falha ao limpar a imagem enviada após erro ao salvar o produto.");
                }
            }

            return BadRequest("Falha ao salvar o produto no banco de dados.");
        }

        var imageUrl = product.Image != null ? _storageService.GetPublicUrl(product.Image) : null;
        return CreatedAtRoute("GetProductById", new { id = product.Id }, product.ToResponse(imageUrl));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromForm] UpdateProductRequest request)
    {
        var product = await _db.Products.FirstOrDefaultAsync(currentProduct => currentProduct.Id == id);
        if (product == null) return NotFound();

        string? newObjectKey = null;

        if (request.Image != null)
        {
            if (!FileValidation.IsAllowedImage(request.Image))
                return BadRequest("Tipo de imagem não permitido. Use JPG, PNG ou WebP.");

            if (request.Image.Length > 5 * 1024 * 1024)
                return BadRequest("A imagem não pode ser maior que 5 MB.");

            var extension = Path.GetExtension(request.Image.FileName);
            newObjectKey = $"products-images/{Guid.NewGuid()}{extension}";
            using var stream = request.Image.OpenReadStream();
            await _storageService.UploadFileAsync(stream, newObjectKey, request.Image.ContentType);
        }

        var oldObjectKey = product.Image;
        product.Update(request.Name, request.Price, request.Description, newObjectKey ?? product.Image, request.Category, request.Size);

        if (await SaveProductChangesOrCleanupAsync(newObjectKey, "update commit exception") == 0)
        {
            if (newObjectKey != null)
            {
                try
                {
                    await _storageService.DeleteObjectAsync(newObjectKey);
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, "Failed to cleanup uploaded replacement image {ObjectKey} after update commit failure.", newObjectKey);
                    return StatusCode(StatusCodes.Status502BadGateway, "Falha ao limpar a nova imagem após erro ao atualizar o produto.");
                }
            }

            return BadRequest("Falha ao atualizar o produto no banco de dados.");
        }

        if (newObjectKey != null && oldObjectKey != null)
        {
            try
            {
                await _storageService.DeleteObjectAsync(oldObjectKey);
            }
            catch
            {
                product.Update(request.Name, request.Price, request.Description, oldObjectKey, request.Category, request.Size);
                if (await _db.SaveChangesAsync() == 0)
                {
                    _logger.LogCritical(
                        "Failed to restore product {ProductId} image reference after storage cleanup failure.",
                        id);
                    return StatusCode(StatusCodes.Status500InternalServerError, "Falha ao restaurar a imagem do produto após erro no armazenamento.");
                }

                try
                {
                    await _storageService.DeleteObjectAsync(newObjectKey);
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, "Failed to cleanup replacement image {ObjectKey} after restoring product {ProductId} image reference.", newObjectKey, id);
                    return StatusCode(StatusCodes.Status502BadGateway, "Falha ao substituir a imagem do produto e a limpeza da nova imagem também falhou.");
                }

                return StatusCode(StatusCodes.Status502BadGateway, "Falha ao substituir a imagem do produto. Tente novamente.");
            }
        }

        var imageUrl = product.Image != null ? _storageService.GetPublicUrl(product.Image) : null;
        return Ok(product.ToResponse(imageUrl));
    }

    [HttpDelete("{id}/image")]
    public async Task<IActionResult> DeleteImage(Guid id)
    {
        var product = await _db.Products.FirstOrDefaultAsync(currentProduct => currentProduct.Id == id);
        if (product == null) return NotFound();
        if (product.Image == null) return BadRequest("Produto não possui imagem.");

        var objectKey = product.Image;
        product.SetImage(null);

        if (await _db.SaveChangesAsync() == 0)
            return BadRequest("Falha ao remover a imagem do banco de dados.");

        try
        {
            await _storageService.DeleteObjectAsync(objectKey);
        }
        catch
        {
            product.SetImage(objectKey);
            if (await _db.SaveChangesAsync() == 0)
            {
                _logger.LogCritical(
                    "Failed to restore product {ProductId} image reference after storage delete failure.",
                    id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Falha ao restaurar a imagem do produto após erro no armazenamento.");
            }

            return StatusCode(StatusCodes.Status502BadGateway, "Falha ao remover a imagem do armazenamento. A imagem foi restaurada no banco.");
        }

        return Ok(product.ToResponse());
    }

    [HttpPatch("{id}/inactive", Name = "SetProductInactive")]
    public async Task<IActionResult> DeactivateProduct(Guid id)
    {
        var product = await _db.Products.FirstOrDefaultAsync(currentProduct => currentProduct.Id == id);
        if (product == null) return NotFound();
        if (!product.ProductStatus) return BadRequest("Produto já está desativado.");

        product.ChangeProductStatus();

        if (await _db.SaveChangesAsync() == 0)
            return BadRequest("Falha ao desativar o produto no banco de dados.");

        var imageUrl = product.Image != null ? _storageService.GetPublicUrl(product.Image) : null;
        return Ok(product.ToResponse(imageUrl));
    }

    [HttpPatch("{id}/active", Name = "SetProductActive")]
    public async Task<IActionResult> ActivateProduct(Guid id)
    {
        var product = await _db.Products.FirstOrDefaultAsync(currentProduct => currentProduct.Id == id);
        if (product == null) return NotFound();
        if (product.ProductStatus) return BadRequest("Produto já está ativo.");

        product.ChangeProductStatus();

        if (await _db.SaveChangesAsync() == 0)
            return BadRequest("Falha ao ativar o produto no banco de dados.");

        var imageUrl = product.Image != null ? _storageService.GetPublicUrl(product.Image) : null;
        return Ok(product.ToResponse(imageUrl));
    }

    [HttpGet("dropdown")]
    public async Task<IActionResult> GetForDropdown([FromQuery] bool? status)
    {
        var query = _db.Products.AsNoTracking();

        if (status.HasValue)
            query = query.Where(product => product.ProductStatus == status.Value);

        var result = await query
            .Select(product => new ProductDropdownModel
            {
                Id = product.Id,
                Name = product.Name,
                Price = product.Price,
                Category = product.Category,
                Size = product.Size
            })
            .ToListAsync();

        return Ok(result);
    }

    [HttpGet("{id}", Name = "GetProductById")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var product = await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(currentProduct => currentProduct.Id == id);
        if (product == null) return NotFound();

        var imageUrl = product.Image != null ? _storageService.GetPublicUrl(product.Image) : null;
        return Ok(product.ToResponse(imageUrl));
    }

    [HttpPut("{id}/recipe")]
    public async Task<IActionResult> SetRecipe(Guid id, [FromBody] SetRecipeRequest request)
    {
        var product = await _db.Products
            .Include(currentProduct => currentProduct.RecipeItems)
            .AsSplitQuery()
            .FirstOrDefaultAsync(currentProduct => currentProduct.Id == id);
        if (product == null) return NotFound();

        if (request.Items.Count == 0)
        {
            product.ClearRecipe();

            if (await _db.SaveChangesAsync() == 0)
                return BadRequest("Falha ao limpar a receita do produto.");

            return Ok(new ProductRecipeResponse(false, []));
        }

        var materialIds = request.Items.Select(item => item.MaterialId).Distinct().ToList();
        var materials = await _db.InventoryMaterials
            .Where(material => materialIds.Contains(material.Id))
            .ToListAsync();
        var materialsById = materials.ToDictionary(material => material.Id);

        if (materialsById.Count != materialIds.Count)
            return BadRequest("Um ou mais materiais informados não existem.");

        var inactiveMaterial = materials.FirstOrDefault(material => !material.Status);
        if (inactiveMaterial != null)
            return BadRequest($"O material '{inactiveMaterial.Name}' está inativo e não pode ser usado na receita.");

        var existingRecipeItems = product.RecipeItems.ToDictionary(item => item.MaterialId);
        var finalRecipeItems = new List<ProductRecipeItem>(request.Items.Count);

        foreach (var item in request.Items)
        {
            if (existingRecipeItems.TryGetValue(item.MaterialId, out var existingRecipeItem))
            {
                existingRecipeItem.UpdateQuantity(item.Quantity);
                finalRecipeItems.Add(existingRecipeItem);
                continue;
            }

            finalRecipeItems.Add(new ProductRecipeItem(product.Id, item.MaterialId, item.Quantity));
        }

        product.SetRecipe(finalRecipeItems);

        if (await _db.SaveChangesAsync() == 0)
            return BadRequest("Falha ao salvar a receita do produto.");

        var responseItems = finalRecipeItems
            .Select(recipeItem =>
            {
                var material = materialsById[recipeItem.MaterialId];
                return new RecipeItemResponse(
                    recipeItem.MaterialId,
                    material.Name,
                    recipeItem.QuantityNeeded,
                    material.MeasureUnit.ToString());
            })
            .ToList();

        return Ok(new ProductRecipeResponse(true, responseItems));
    }

    [HttpGet("{id}/recipe")]
    public async Task<IActionResult> GetRecipe(Guid id)
    {
        var productExists = await _db.Products.AsNoTracking().AnyAsync(product => product.Id == id);
        if (!productExists) return NotFound();

        var recipeItems = await _db.ProductRecipeItems
            .AsNoTracking()
            .Where(recipeItem => recipeItem.ProductId == id)
            .Include(recipeItem => recipeItem.Material)
            .ToListAsync();

        var response = recipeItems
            .Select(recipeItem => new RecipeItemResponse(
                recipeItem.MaterialId,
                recipeItem.Material.Name,
                recipeItem.QuantityNeeded,
                recipeItem.Material.MeasureUnit.ToString()))
            .ToList();

        return Ok(new ProductRecipeResponse(response.Count > 0, response));
    }

    [HttpGet("{id}/recipe-options")]
    public async Task<IActionResult> GetRecipeOptions(Guid id)
    {
        var productExists = await _db.Products.AsNoTracking().AnyAsync(product => product.Id == id);
        if (!productExists) return NotFound();

        var options = await _db.ProductRecipeOptions
            .AsNoTracking()
            .Where(option => option.ProductId == id)
            .Include(option => option.Items)
                .ThenInclude(item => item.Material)
            .OrderBy(option => option.Type)
            .ThenBy(option => option.Name)
            .ToListAsync();

        return Ok(new ProductRecipeOptionsResponse(options.Select(ToRecipeOptionResponse).ToList()));
    }

    [HttpPut("{id}/recipe-options")]
    public async Task<IActionResult> SetRecipeOption(Guid id, [FromBody] SetRecipeOptionRequest request)
    {
        var productExists = await _db.Products.AsNoTracking().AnyAsync(product => product.Id == id);
        if (!productExists) return NotFound();

        var normalizedName = request.Name.Trim();
        var option = await _db.ProductRecipeOptions
            .Include(recipeOption => recipeOption.Items)
            .FirstOrDefaultAsync(recipeOption =>
                recipeOption.ProductId == id &&
                recipeOption.Type == request.Type &&
                recipeOption.Name == normalizedName);

        if (request.Items.Count == 0)
        {
            if (option != null)
            {
                _db.ProductRecipeOptions.Remove(option);
                await _db.SaveChangesAsync();
            }

            return Ok(new ProductRecipeOptionResponse(Guid.Empty, request.Type, normalizedName, false, []));
        }

        var materialIds = request.Items.Select(item => item.MaterialId).Distinct().ToList();
        var materials = await _db.InventoryMaterials
            .Where(material => materialIds.Contains(material.Id))
            .ToListAsync();
        var materialsById = materials.ToDictionary(material => material.Id);

        if (materialsById.Count != materialIds.Count)
            return BadRequest("Um ou mais materiais informados não existem.");

        var inactiveMaterial = materials.FirstOrDefault(material => !material.Status);
        if (inactiveMaterial != null)
            return BadRequest($"O material '{inactiveMaterial.Name}' está inativo e não pode ser usado na receita.");

        if (option == null)
        {
            option = new ProductRecipeOption(id, request.Type, normalizedName);
            _db.ProductRecipeOptions.Add(option);
        }
        else
        {
            _db.ProductRecipeOptionItems.RemoveRange(option.Items);
        }

        var finalItems = request.Items
            .Select(item => new ProductRecipeOptionItem(option.Id, item.MaterialId, item.Quantity))
            .ToList();

        option.SetItems(finalItems);

        await _db.SaveChangesAsync();

        return Ok(new ProductRecipeOptionResponse(
            option.Id,
            option.Type,
            option.Name,
            true,
            finalItems.Select(item =>
            {
                var material = materialsById[item.MaterialId];
                return new RecipeItemResponse(
                    item.MaterialId,
                    material.Name,
                    item.QuantityNeeded,
                    material.MeasureUnit.ToString());
            }).ToList()));
    }

    [HttpGet("{id}/order-options")]
    public async Task<IActionResult> GetOrderOptions(Guid id)
    {
        var productExists = await _db.Products.AsNoTracking().AnyAsync(product => product.Id == id);
        if (!productExists) return NotFound();

        return Ok(new ProductOrderRecipeOptionsResponse(
            RecipeOptionCatalog.CakeDoughs.ToList(),
            RecipeOptionCatalog.CakeFillings.ToList(),
            RecipeOptionCatalog.BrigadeiroFlavors.ToList(),
            RecipeOptionCatalog.CookieFlavors.ToList()));
    }

    [HttpGet("{id}/stats")]
    public async Task<IActionResult> GetStats(Guid id, [FromQuery] string month, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(month) ||
            !DateTime.TryParseExact(month, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var parsed))
        {
            return BadRequest("O parâmetro 'month' é obrigatório no formato YYYY-MM.");
        }

        var productExists = await _db.Products.AsNoTracking().AnyAsync(product => product.Id == id, ct);
        if (!productExists) return NotFound();

        var stats = await _orderRepository.GetProductStatsAsync(id, parsed.Year, parsed.Month, ct);
        return Ok(stats);
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] bool? status, [FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        if (size > 100) size = 100;

        var query = _db.Products.AsNoTracking();

        if (status.HasValue)
            query = query.Where(product => product.ProductStatus == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = _db.Database.IsNpgsql()
                ? query.Where(product => EF.Functions.ILike(product.Name, $"%{search}%"))
                : query.Where(product => product.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(product => product.Name)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return Ok(new
        {
            items = items.Select(product =>
            {
                var imageUrl = product.Image != null ? _storageService.GetPublicUrl(product.Image) : null;
                return product.ToResponse(imageUrl);
            }),
            totalCount,
            pageNumber = page,
            pageSize = size
        });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> DeleteProduct(Guid id)
    {
        try
        {
            var product = await _db.Products
                .FirstOrDefaultAsync(currentProduct => currentProduct.Id == id);
            if (product == null) return NotFound(new { Message = "Produto não encontrado." });

            if (product.Image != null)
                return BadRequest(new { Message = "Remova a imagem do produto antes de deletar o cadastro." });

            _db.Products.Remove(product);
            if (await _db.SaveChangesAsync() == 0)
                return BadRequest(new { Message = "Falha ao deletar o produto." });

            return NoContent();
        }
        catch
        {
            return BadRequest(new { Message = "Não é possível deletar este produto pois ele pertence a um pedido existente." });
        }
    }

    private async Task<int> SaveProductChangesOrCleanupAsync(string? uploadedObjectKey, string reason)
    {
        try
        {
            return await _db.SaveChangesAsync();
        }
        catch
        {
            await CleanupUploadedImageAsync(uploadedObjectKey, reason);
            throw;
        }
    }

    private async Task CleanupUploadedImageAsync(string? objectKey, string reason)
    {
        if (objectKey == null)
            return;

        try
        {
            await _storageService.DeleteObjectAsync(objectKey);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to cleanup product image {ObjectKey} after {Reason}.", objectKey, reason);
        }
    }

    private static ProductRecipeOptionResponse ToRecipeOptionResponse(ProductRecipeOption option)
    {
        return new ProductRecipeOptionResponse(
            option.Id,
            option.Type,
            option.Name,
            option.Items.Count > 0,
            option.Items.Select(item => new RecipeItemResponse(
                item.MaterialId,
                item.Material.Name,
                item.QuantityNeeded,
                item.Material.MeasureUnit.ToString())).ToList());
    }
}

using DeuxERP.API.Models;
using DeuxERP.Application.Common;
using DeuxERP.Application.Mapping;
using DeuxERP.Domain.Inventory;
using DeuxERP.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeuxERP.API.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/inventory")]
public class InventoryController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(
        IAppDbContext db,
        ILogger<InventoryController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpPost("new")]
    public async Task<IActionResult> Create([FromBody] CreateMaterialRequest request)
    {
        var material = new InventoryMaterial(request.Name, request.Quantity, request.TotalCost, request.MeasureUnit);
        _db.InventoryMaterials.Add(material);

        if (await _db.SaveChangesAsync() == 0)
        {
            _logger.LogWarning("Failed to create inventory material {MaterialName}.", request.Name);
            return BadRequest("Falha ao salvar o material no banco de dados.");
        }

        return CreatedAtAction(nameof(GetById), new { id = material.Id }, material.ToResponse());
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] bool? status, [FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        if (size > 100) size = 100;

        var query = _db.InventoryMaterials.AsNoTracking();

        if (status.HasValue)
            query = query.Where(material => material.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = _db.Database.IsNpgsql()
                ? query.Where(material => EF.Functions.ILike(material.Name, $"%{search}%"))
                : query.Where(material => material.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(material => material.Name)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return Ok(new
        {
            items = items.Select(material => material.ToResponse()),
            totalCount,
            pageNumber = page,
            pageSize = size
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var material = await _db.InventoryMaterials
            .AsNoTracking()
            .FirstOrDefaultAsync(material => material.Id == id);

        if (material == null)
        {
            _logger.LogWarning("Inventory material {MaterialId} was not found.", id);
            return NotFound();
        }

        return Ok(material.ToResponse());
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMaterialRequest request)
    {
        var material = await _db.InventoryMaterials.FirstOrDefaultAsync(material => material.Id == id);
        if (material == null)
        {
            _logger.LogWarning("Inventory material {MaterialId} was not found for update.", id);
            return NotFound();
        }

        material.Update(request.Name, request.MeasureUnit);

        if (await _db.SaveChangesAsync() == 0)
            return BadRequest("Falha ao atualizar o material no banco de dados.");

        return Ok(material.ToResponse());
    }

    [HttpPost("{id}/restock")]
    public async Task<IActionResult> Restock(Guid id, [FromBody] RestockRequest request)
    {
        var material = await _db.InventoryMaterials.FirstOrDefaultAsync(material => material.Id == id);
        if (material == null)
            return NotFound();

        material.Restock(request.Quantity, request.TotalCost);

        if (await _db.SaveChangesAsync() == 0)
            return BadRequest("Falha ao reabastecer o material no banco de dados.");

        return Ok(material.ToResponse());
    }

    [HttpPatch("{id}/active")]
    public async Task<IActionResult> Activate(Guid id)
    {
        var material = await _db.InventoryMaterials.FirstOrDefaultAsync(material => material.Id == id);
        if (material == null)
            return NotFound();

        if (material.Status)
            return BadRequest("Material já está ativo.");

        material.ChangeStatus();

        if (await _db.SaveChangesAsync() == 0)
            return BadRequest("Falha ao ativar o material no banco de dados.");

        return Ok(material.ToResponse());
    }

    [HttpPatch("{id}/inactive")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var material = await _db.InventoryMaterials.FirstOrDefaultAsync(material => material.Id == id);
        if (material == null)
            return NotFound();

        if (!material.Status)
            return BadRequest("Material já está inativo.");

        material.ChangeStatus();

        if (await _db.SaveChangesAsync() == 0)
            return BadRequest("Falha ao desativar o material no banco de dados.");

        return Ok(material.ToResponse());
    }

    [HttpGet("dropdown")]
    public async Task<IActionResult> GetForDropdown([FromQuery] bool? status)
    {
        var query = _db.InventoryMaterials.AsNoTracking();

        if (status.HasValue)
            query = query.Where(material => material.Status == status.Value);

        var items = await query
            .OrderBy(material => material.Name)
            .Select(material => new InventoryDropdownModel(
                material.Id,
                material.Name,
                material.MeasureUnit.ToString()))
            .ToListAsync();

        return Ok(items);
    }
}

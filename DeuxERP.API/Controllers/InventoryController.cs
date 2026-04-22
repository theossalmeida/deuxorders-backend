using DeuxERP.API.Models;
using DeuxERP.Application.Mapping;
using DeuxERP.Domain.Interfaces;
using DeuxERP.Domain.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeuxERP.API.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/inventory")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryMaterialRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(
        IInventoryMaterialRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<InventoryController> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    [HttpPost("new")]
    public async Task<IActionResult> Create([FromBody] CreateMaterialRequest request)
    {
        var material = new InventoryMaterial(request.Name, request.Quantity, request.TotalCost, request.MeasureUnit);
        _repository.Add(material);

        if (!await _unitOfWork.CommitAsync())
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

        var result = await _repository.GetAllAsync(search, status, page, size);
        return Ok(new
        {
            items = result.Items.Select(material => material.ToResponse()),
            totalCount = result.TotalCount,
            pageNumber = result.PageNumber,
            pageSize = result.PageSize
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var material = await _repository.GetByIdAsync(id);
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
        var material = await _repository.GetByIdAsync(id);
        if (material == null)
        {
            _logger.LogWarning("Inventory material {MaterialId} was not found for update.", id);
            return NotFound();
        }

        material.Update(request.Name, request.MeasureUnit);

        if (!await _unitOfWork.CommitAsync())
            return BadRequest("Falha ao atualizar o material no banco de dados.");

        return Ok(material.ToResponse());
    }

    [HttpPost("{id}/restock")]
    public async Task<IActionResult> Restock(Guid id, [FromBody] RestockRequest request)
    {
        var material = await _repository.GetByIdAsync(id);
        if (material == null)
            return NotFound();

        material.Restock(request.Quantity, request.TotalCost);

        if (!await _unitOfWork.CommitAsync())
            return BadRequest("Falha ao reabastecer o material no banco de dados.");

        return Ok(material.ToResponse());
    }

    [HttpPatch("{id}/active")]
    public async Task<IActionResult> Activate(Guid id)
    {
        var material = await _repository.GetByIdAsync(id);
        if (material == null)
            return NotFound();

        if (material.Status)
            return BadRequest("Material já está ativo.");

        material.ChangeStatus();

        if (!await _unitOfWork.CommitAsync())
            return BadRequest("Falha ao ativar o material no banco de dados.");

        return Ok(material.ToResponse());
    }

    [HttpPatch("{id}/inactive")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var material = await _repository.GetByIdAsync(id);
        if (material == null)
            return NotFound();

        if (!material.Status)
            return BadRequest("Material já está inativo.");

        material.ChangeStatus();

        if (!await _unitOfWork.CommitAsync())
            return BadRequest("Falha ao desativar o material no banco de dados.");

        return Ok(material.ToResponse());
    }

    [HttpGet("dropdown")]
    public async Task<IActionResult> GetForDropdown([FromQuery] bool? status)
    {
        var items = await _repository.GetForDropdownAsync(status);
        return Ok(items);
    }
}

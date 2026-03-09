using DeuxOrders.Application.Mapping;
using DeuxOrders.Domain.Entities;
using DeuxOrders.Domain.Interfaces;
using DeuxOrders.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
[ApiController]
[Route("api/v1/clients")]
public class ClientController : ControllerBase
{
    private readonly IClientRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ClientController(IClientRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    [HttpPost("new")]
    public async Task<IActionResult> Create(CreateClient request)
    {

        var client = new Client(request.Name);
        if (!string.IsNullOrWhiteSpace(request.Mobile))
        {
            client.SetMobile(request.Mobile);
        }

        _repository.Add(client);

        var success = await _unitOfWork.CommitAsync();
        if (!success)
            return BadRequest("Falha ao salvar o cliente no banco de dados.");

        return CreatedAtAction(nameof(GetById), new { id = client.Id }, client);
    }

    [HttpPatch("{id}/inactive", Name = "SetClientInactive")]
    public async Task<IActionResult> Deactivateclient(Guid id)
    {
        var client = await _repository.GetByIdAsync(id);
        if (client == null) return NotFound();
        if (!client.Status) return BadRequest("Produto já está desativado.");
        client.ChangeClientStatus();

        var success = await _unitOfWork.CommitAsync();
        if (!success)
            return BadRequest("Falha ao desativar o cliente no banco de dados.");

        return Ok(client);
    }

    [HttpPatch("{id}/active", Name = "SetClientActive")]
    public async Task<IActionResult> Activateclient(Guid id)
    {
        var client = await _repository.GetByIdAsync(id);
        if (client == null) return NotFound();
        if (client.Status) return BadRequest("Produto já está ativo.");

        client.ChangeClientStatus();

        var success = await _unitOfWork.CommitAsync();
        if (!success)
            return BadRequest("Falha ao ativar o cliente no banco de dados.");

        return Ok(client);
    }

    [HttpGet("dropdown")]
    public async Task<IActionResult> GetForDropdown([FromQuery] bool? status)
    {
        var result = await _repository.GetForDropdownAsync(status);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var client = await _repository.GetByIdAsync(id);
        if (client == null) return NotFound();
        return Ok(client);
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAll([FromQuery] bool status = true)
    {
        var clients = await _repository.GetAll();

        var response = clients.Select(c => c.ToResponse());

        return Ok(response);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteClient(Guid id)
    {
        try
        {
            var success = await _repository.DeleteAsync(id);

            if (!success)
                return NotFound(new { Message = "Cliente não encontrado." });

            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = "Não é possível deletar este cliente pois existem vínculos.", Details = ex.Message });
        }
    }
}
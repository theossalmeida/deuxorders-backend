using DeuxOrders.Domain.Entities;
using DeuxOrders.Domain.Interfaces;
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

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var client = await _repository.GetByIdAsync(id);
        if (client == null) return NotFound();
        return Ok(client);
    }
}
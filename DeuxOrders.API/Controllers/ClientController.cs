using DeuxOrders.Domain.Entities;
using DeuxOrders.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/clients")]
public class ClientController : ControllerBase
{
    private readonly IClientRepository _repository;

    public ClientController(IClientRepository repository)
    {
        _repository = repository;
    }

    [HttpPost("new")]
    public async Task<IActionResult> Create(CreateClient request)
    {

        var client = new Client(request.Name);
        if (!string.IsNullOrWhiteSpace(request.Mobile))
        {
            client.SetMobile(request.Mobile);
        }

        await _repository.AddAsync(client);

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
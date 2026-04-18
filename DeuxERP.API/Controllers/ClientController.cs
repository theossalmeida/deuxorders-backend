using DeuxERP.Application.DTOs;
using DeuxERP.Application.Mapping;
using DeuxERP.Domain.Sales;
using DeuxERP.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
[ApiController]
[Route("api/v1/clients")]
public class ClientController : ControllerBase
{
    private readonly IClientRepository _repository;
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ClientController(IClientRepository repository, IOrderRepository orderRepository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _orderRepository = orderRepository;
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

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClient request)
    {
        var client = await _repository.GetByIdAsync(id);
        if (client == null) return NotFound();

        client.Update(request.Name, request.Mobile, request.Status);

        var success = await _unitOfWork.CommitAsync();
        if (!success)
            return BadRequest("Falha ao atualizar o cliente no banco de dados.");

        return Ok(client.ToResponse());
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
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct, [FromQuery] bool orders = false, [FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        if (page < 1) page = 1;
        if (size < 1) size = 1;
        if (size > 100) size = 100;

        var client = await _repository.GetByIdAsync(id);
        if (client == null) return NotFound();

        var stats = await _orderRepository.GetClientStatsAsync(id, ct);

        PagedOrdersResponse? pagedOrders = null;
        if (orders)
        {
            var result = await _orderRepository.GetByClientAsync(id, page, size, ct);
            pagedOrders = new PagedOrdersResponse(
                result.Items.Select(o => o.ToResponse(client.Name, null)).ToList(),
                result.TotalCount,
                result.PageNumber,
                result.PageSize
            );
        }

        return Ok(new ClientDetailResponse(client.Id, client.Name, client.Mobile, client.Status, stats, pagedOrders));
    }

    [HttpGet("{id}/stats")]
    public async Task<IActionResult> GetStats(Guid id, CancellationToken ct)
    {
        var client = await _repository.GetByIdAsync(id);
        if (client == null) return NotFound();

        var stats = await _orderRepository.GetClientStatsAsync(id, ct);
        return Ok(stats);
    }

    [HttpGet("{id}/orders")]
    public async Task<IActionResult> GetOrders(Guid id, CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        if (page < 1) page = 1;
        if (size < 1) size = 1;
        if (size > 100) size = 100;

        var client = await _repository.GetByIdAsync(id);
        if (client == null) return NotFound();

        var result = await _orderRepository.GetByClientAsync(id, page, size, ct);

        return Ok(new
        {
            items = result.Items.Select(o => o.ToResponse(o.Client?.Name ?? "", null)).ToList(),
            totalCount = result.TotalCount,
            pageNumber = result.PageNumber,
            pageSize = result.PageSize
        });
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] bool? status, [FromQuery] int page = 1, [FromQuery] int size = 20, [FromQuery] bool includeTotals = false)
    {
        if (size > 100) size = 100;
        var result = await _repository.GetAll(search, status, page, size);

        Dictionary<Guid, (int TotalOrders, long TotalSpent)> totals = [];
        var clientList = result.Items.ToList();
        if (includeTotals && clientList.Count > 0)
            totals = await _orderRepository.GetTotalsForClientsAsync(clientList.Select(c => c.Id));

        var items = clientList;

        return Ok(new
        {
            items = items.Select(c =>
            {
                totals.TryGetValue(c.Id, out var t);
                return c.ToListResponse(
                    includeTotals ? t.TotalOrders : null,
                    includeTotals ? t.TotalSpent : null
                );
            }),
            totalCount = result.TotalCount,
            pageNumber = result.PageNumber,
            pageSize = result.PageSize
        });
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
            return BadRequest(new { Message = "Não é possível deletar este cliente pois existem vínculos." });
        }
    }
}
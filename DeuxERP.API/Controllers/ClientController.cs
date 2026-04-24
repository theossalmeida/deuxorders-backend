using DeuxERP.Application.Common;
using DeuxERP.Application.DTOs;
using DeuxERP.Application.Mapping;
using DeuxERP.Domain.Interfaces;
using DeuxERP.Domain.Models;
using DeuxERP.Domain.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Authorize]
[ApiController]
[Route("api/v1/clients")]
public class ClientController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly IOrderRepository _orderRepository;

    public ClientController(IAppDbContext db, IOrderRepository orderRepository)
    {
        _db = db;
        _orderRepository = orderRepository;
    }

    [HttpPost("new")]
    public async Task<IActionResult> Create(CreateClient request)
    {
        var client = new Client(request.Name);
        if (!string.IsNullOrWhiteSpace(request.Mobile))
            client.SetMobile(request.Mobile);

        _db.Clients.Add(client);

        if (await _db.SaveChangesAsync() == 0)
            return BadRequest("Falha ao salvar o cliente no banco de dados.");

        return CreatedAtAction(nameof(GetById), new { id = client.Id }, client.ToResponse());
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClient request)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(currentClient => currentClient.Id == id);
        if (client == null) return NotFound();

        client.Update(request.Name, request.Mobile, request.Status);

        if (await _db.SaveChangesAsync() == 0)
            return BadRequest("Falha ao atualizar o cliente no banco de dados.");

        return Ok(client.ToResponse());
    }

    [HttpPatch("{id}/inactive", Name = "SetClientInactive")]
    public async Task<IActionResult> Deactivateclient(Guid id)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(currentClient => currentClient.Id == id);
        if (client == null) return NotFound();
        if (!client.Status) return BadRequest("Cliente já está desativado.");

        client.ChangeClientStatus();

        if (await _db.SaveChangesAsync() == 0)
            return BadRequest("Falha ao desativar o cliente no banco de dados.");

        return Ok(client.ToResponse());
    }

    [HttpPatch("{id}/active", Name = "SetClientActive")]
    public async Task<IActionResult> Activateclient(Guid id)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(currentClient => currentClient.Id == id);
        if (client == null) return NotFound();
        if (client.Status) return BadRequest("Cliente já está ativo.");

        client.ChangeClientStatus();

        if (await _db.SaveChangesAsync() == 0)
            return BadRequest("Falha ao ativar o cliente no banco de dados.");

        return Ok(client.ToResponse());
    }

    [HttpGet("dropdown")]
    public async Task<IActionResult> GetForDropdown([FromQuery] bool? status)
    {
        var query = _db.Clients.AsNoTracking();

        if (status.HasValue)
            query = query.Where(client => client.Status == status.Value);

        var result = await query
            .Select(client => new DropdownItemModel
            {
                Id = client.Id,
                Name = client.Name
            })
            .ToListAsync();

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct, [FromQuery] bool orders = false, [FromQuery] bool includeStats = true, [FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        if (page < 1) page = 1;
        if (size < 1) size = 1;
        if (size > 100) size = 100;

        var client = await _db.Clients
            .AsNoTracking()
            .FirstOrDefaultAsync(currentClient => currentClient.Id == id, ct);
        if (client == null) return NotFound();

        ClientStats? stats = null;
        if (includeStats)
            stats = await _orderRepository.GetClientStatsAsync(id, ct);

        PagedOrdersResponse? pagedOrders = null;
        if (orders)
        {
            var result = await _orderRepository.GetByClientAsync(id, page, size, ct);
            pagedOrders = new PagedOrdersResponse(
                result.Items.Select(order => order.ToResponse(client.Name, null)).ToList(),
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
        var clientExists = await _db.Clients.AsNoTracking().AnyAsync(client => client.Id == id, ct);
        if (!clientExists) return NotFound();

        var stats = await _orderRepository.GetClientStatsAsync(id, ct);
        return Ok(stats);
    }

    [HttpGet("{id}/orders")]
    public async Task<IActionResult> GetOrders(Guid id, CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        if (page < 1) page = 1;
        if (size < 1) size = 1;
        if (size > 100) size = 100;

        var clientExists = await _db.Clients.AsNoTracking().AnyAsync(client => client.Id == id, ct);
        if (!clientExists) return NotFound();

        var result = await _orderRepository.GetByClientAsync(id, page, size, ct);

        return Ok(new
        {
            items = result.Items.Select(order => order.ToResponse(order.Client?.Name ?? "", null)).ToList(),
            totalCount = result.TotalCount,
            pageNumber = result.PageNumber,
            pageSize = result.PageSize
        });
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] bool? status, [FromQuery] int page = 1, [FromQuery] int size = 20, [FromQuery] bool includeTotals = false)
    {
        if (size > 100) size = 100;

        var query = _db.Clients.AsNoTracking();

        if (status.HasValue)
            query = query.Where(client => client.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = _db.Database.IsNpgsql()
                ? query.Where(client => EF.Functions.ILike(client.Name, $"%{search}%"))
                : query.Where(client => client.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var totalCount = await query.CountAsync();
        var clientList = await query
            .OrderBy(client => client.Name)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        Dictionary<Guid, (int TotalOrders, long TotalSpent)> totals = [];
        if (includeTotals && clientList.Count > 0)
            totals = await _orderRepository.GetTotalsForClientsAsync(clientList.Select(client => client.Id));

        return Ok(new
        {
            items = clientList.Select(client =>
            {
                totals.TryGetValue(client.Id, out var total);
                return client.ToListResponse(
                    includeTotals ? total.TotalOrders : null,
                    includeTotals ? total.TotalSpent : null
                );
            }),
            totalCount,
            pageNumber = page,
            pageSize = size
        });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> DeleteClient(Guid id)
    {
        try
        {
            var client = await _db.Clients.FirstOrDefaultAsync(currentClient => currentClient.Id == id);

            if (client == null)
                return NotFound(new { Message = "Cliente não encontrado." });

            _db.Clients.Remove(client);
            if (await _db.SaveChangesAsync() == 0)
                return BadRequest(new { Message = "Falha ao deletar o cliente." });

            return NoContent();
        }
        catch
        {
            return BadRequest(new { Message = "Não é possível deletar este cliente pois existem vínculos." });
        }
    }
}

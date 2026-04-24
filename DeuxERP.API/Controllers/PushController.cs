using DeuxERP.Application.Common;
using DeuxERP.Domain.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeuxERP.API.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/push")]
public sealed class PushController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserAccessor _currentUser;

    public PushController(IAppDbContext db, ICurrentUserAccessor currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] PushSubscriptionRequest request, CancellationToken ct)
    {
        if (!request.IsValid())
            return BadRequest("Endpoint, p256dh e auth sao obrigatorios.");

        var endpoint = request.Endpoint.Trim();
        var subscription = await _db.PushSubscriptions
            .FirstOrDefaultAsync(existing => existing.Endpoint == endpoint, ct);

        if (subscription == null)
        {
            subscription = PushSubscription.Create(
                _currentUser.UserId,
                endpoint,
                request.P256dh,
                request.Auth,
                request.DeviceLabel);

            _db.PushSubscriptions.Add(subscription);
        }
        else
        {
            subscription.Refresh(_currentUser.UserId, request.P256dh, request.Auth, request.DeviceLabel);
        }

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromBody] PushUnsubscribeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Endpoint))
            return BadRequest("Endpoint e obrigatorio.");

        var endpoint = request.Endpoint.Trim();
        var subscription = await _db.PushSubscriptions
            .FirstOrDefaultAsync(existing =>
                existing.Endpoint == endpoint &&
                existing.UserId == _currentUser.UserId,
                ct);

        if (subscription != null)
        {
            subscription.Deactivate();
            await _db.SaveChangesAsync(ct);
        }

        return NoContent();
    }
}

public sealed record PushSubscriptionRequest(
    string Endpoint,
    string P256dh,
    string Auth,
    string? DeviceLabel)
{
    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(Endpoint) &&
        !string.IsNullOrWhiteSpace(P256dh) &&
        !string.IsNullOrWhiteSpace(Auth);
}

public sealed record PushUnsubscribeRequest(string Endpoint);

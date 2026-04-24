using DeuxERP.Application.Common;
using DeuxERP.Application.Notifications;
using DeuxERP.Domain.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DeuxERP.API.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/push")]
public sealed class PushController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IPushNotificationAvailability _pushAvailability;

    public PushController(
        IAppDbContext db,
        ICurrentUserAccessor currentUser,
        IPushNotificationAvailability pushAvailability)
    {
        _db = db;
        _currentUser = currentUser;
        _pushAvailability = pushAvailability;
    }

    [HttpPost("status")]
    public async Task<IActionResult> Status([FromBody] PushStatusRequest request, CancellationToken ct)
    {
        if (!_pushAvailability.IsAvailable)
            return PushUnavailable();

        if (string.IsNullOrWhiteSpace(request.Endpoint))
            return BadRequest("Endpoint e obrigatorio.");

        var endpoint = request.Endpoint.Trim();
        var isSubscribed = await _db.PushSubscriptions
            .AnyAsync(existing =>
                existing.Endpoint == endpoint &&
                existing.UserId == _currentUser.UserId &&
                existing.IsActive,
                ct);

        return Ok(new PushStatusResponse(isSubscribed));
    }

    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] PushSubscriptionRequest request, CancellationToken ct)
    {
        if (!_pushAvailability.IsAvailable)
            return PushUnavailable();

        if (!request.IsValid())
            return BadRequest("Endpoint, p256dh e auth sao obrigatorios.");

        var endpoint = request.Endpoint.Trim();
        await UpsertSubscriptionAsync(endpoint, request, ct);
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

    private async Task UpsertSubscriptionAsync(string endpoint, PushSubscriptionRequest request, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 2; attempt += 1)
        {
            PushSubscription? addedSubscription = null;
            var subscription = await _db.PushSubscriptions
                .FirstOrDefaultAsync(existing => existing.Endpoint == endpoint, ct);

            if (subscription == null)
            {
                addedSubscription = PushSubscription.Create(
                    _currentUser.UserId,
                    endpoint,
                    request.P256dh,
                    request.Auth,
                    request.DeviceLabel);

                _db.PushSubscriptions.Add(addedSubscription);
            }
            else
            {
                subscription.Refresh(_currentUser.UserId, request.P256dh, request.Auth, request.DeviceLabel);
            }

            try
            {
                await _db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException ex) when (attempt == 0 && IsUniqueConstraintViolation(ex))
            {
                if (addedSubscription != null && _db is DbContext dbContext)
                    dbContext.Entry(addedSubscription).State = EntityState.Detached;
            }
        }
    }

    private ObjectResult PushUnavailable() =>
        StatusCode(
            StatusCodes.Status503ServiceUnavailable,
            new PushUnavailableResponse(_pushAvailability.DisabledReason ?? "Push notifications are unavailable."));

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}

public sealed record PushStatusRequest(string Endpoint);

public sealed record PushStatusResponse(bool IsSubscribed);

public sealed record PushUnavailableResponse(string Message);

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

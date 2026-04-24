using DeuxERP.Application.Notifications;
using DeuxERP.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace DeuxERP.Infrastructure.Notifications;

public sealed class NoOpPushNotificationService : IPushNotificationService
{
    private readonly ILogger<NoOpPushNotificationService> _logger;

    public NoOpPushNotificationService(ILogger<NoOpPushNotificationService> logger)
    {
        _logger = logger;
    }

    public Task SendToAllAsync(
        NotificationType type,
        string title,
        string body,
        string? actionUrl = null,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Push notification {NotificationType} skipped because push is disabled.", type);
        return Task.CompletedTask;
    }

    public Task SendToUserAsync(
        Guid userId,
        NotificationType type,
        string title,
        string body,
        string? actionUrl = null,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Push notification {NotificationType} for user {UserId} skipped because push is disabled.",
            type,
            userId);
        return Task.CompletedTask;
    }
}

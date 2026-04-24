using DeuxERP.Domain.Notifications;

namespace DeuxERP.Application.Notifications;

public interface IPushNotificationService
{
    Task SendToAllAsync(
        NotificationType type,
        string title,
        string body,
        string? actionUrl = null,
        CancellationToken ct = default);

    Task SendToUserAsync(
        Guid userId,
        NotificationType type,
        string title,
        string body,
        string? actionUrl = null,
        CancellationToken ct = default);
}

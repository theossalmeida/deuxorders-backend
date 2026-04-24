using DeuxERP.Application.Notifications;

namespace DeuxERP.Infrastructure.Notifications;

public sealed class PushNotificationAvailability : IPushNotificationAvailability
{
    public PushNotificationAvailability(bool isAvailable, string? disabledReason)
    {
        IsAvailable = isAvailable;
        DisabledReason = disabledReason;
    }

    public bool IsAvailable { get; }
    public string? DisabledReason { get; }
}

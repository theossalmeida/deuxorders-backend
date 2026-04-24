namespace DeuxERP.Application.Notifications;

public interface IPushNotificationAvailability
{
    bool IsAvailable { get; }
    string? DisabledReason { get; }
}

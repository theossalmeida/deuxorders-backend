using System.Net;
using System.Text.Json;
using DeuxERP.Application.Notifications;
using DeuxERP.Domain.Notifications;
using DeuxERP.Infrastructure.Data;
using Lib.Net.Http.WebPush;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DomainPushSubscription = DeuxERP.Domain.Notifications.PushSubscription;

namespace DeuxERP.Infrastructure.Notifications;

public sealed class WebPushNotificationService : IPushNotificationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PushServiceClient _pushClient;
    private readonly ILogger<WebPushNotificationService> _logger;

    public WebPushNotificationService(
        IServiceScopeFactory scopeFactory,
        PushServiceClient pushClient,
        ILogger<WebPushNotificationService> logger)
    {
        _scopeFactory = scopeFactory;
        _pushClient = pushClient;
        _logger = logger;
    }

    public async Task SendToAllAsync(
        NotificationType type,
        string title,
        string body,
        string? actionUrl = null,
        CancellationToken ct = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var subscriptions = await db.PushSubscriptions
                .Where(subscription => subscription.IsActive)
                .ToListAsync(ct);

            await SendBatchAsync(db, subscriptions, type, title, body, actionUrl, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send push notification {NotificationType}.", type);
        }
    }

    public async Task SendToUserAsync(
        Guid userId,
        NotificationType type,
        string title,
        string body,
        string? actionUrl = null,
        CancellationToken ct = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var subscriptions = await db.PushSubscriptions
                .Where(subscription => subscription.IsActive && subscription.UserId == userId)
                .ToListAsync(ct);

            await SendBatchAsync(db, subscriptions, type, title, body, actionUrl, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send push notification {NotificationType} to user {UserId}.", type, userId);
        }
    }

    private async Task SendBatchAsync(
        ApplicationDbContext db,
        IReadOnlyCollection<DomainPushSubscription> subscriptions,
        NotificationType type,
        string title,
        string body,
        string? actionUrl,
        CancellationToken ct)
    {
        if (subscriptions.Count == 0)
            return;

        var payload = JsonSerializer.Serialize(new
        {
            type = type.ToString(),
            title,
            body,
            url = actionUrl ?? "/dashboard"
        }, JsonOptions);

        foreach (var subscription in subscriptions)
        {
            try
            {
                var message = new PushMessage(payload)
                {
                    Topic = GetTopic(type),
                    Urgency = GetUrgency(type),
                    TimeToLive = GetTimeToLive(type)
                };

                await _pushClient.RequestPushMessageDeliveryAsync(
                    ToWebPushSubscription(subscription),
                    message,
                    ct);

                subscription.MarkUsed();
            }
            catch (PushServiceClientException ex) when (IsExpired(ex.StatusCode))
            {
                subscription.Deactivate();
                _logger.LogInformation(
                    "Deactivated expired push subscription {SubscriptionId} for user {UserId}. StatusCode: {StatusCode}.",
                    subscription.Id,
                    subscription.UserId,
                    ex.StatusCode);
            }
            catch (PushServiceClientException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Push service rejected subscription {SubscriptionId} for user {UserId}. StatusCode: {StatusCode}. ResponseBody: {ResponseBody}.",
                    subscription.Id,
                    subscription.UserId,
                    ex.StatusCode,
                    ex.Body);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to send push notification {NotificationType} to subscription {SubscriptionId}.",
                    type,
                    subscription.Id);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static Lib.Net.Http.WebPush.PushSubscription ToWebPushSubscription(DomainPushSubscription subscription) =>
        new()
        {
            Endpoint = subscription.Endpoint,
            Keys = new Dictionary<string, string>
            {
                ["p256dh"] = subscription.P256dh,
                ["auth"] = subscription.Auth
            }
        };

    private static PushMessageUrgency GetUrgency(NotificationType type) =>
        type is NotificationType.DailyDueToday or NotificationType.DailyDueTomorrow
            ? PushMessageUrgency.Low
            : PushMessageUrgency.Normal;

    private static string GetTopic(NotificationType type) =>
        type switch
        {
            NotificationType.OrderCreated => "ordr",
            NotificationType.ManualCashFlowCreated => "cash",
            NotificationType.DailyDueToday => "due0",
            NotificationType.DailyDueTomorrow => "due1",
            _ => "deux"
        };

    private static int GetTimeToLive(NotificationType type) =>
        type is NotificationType.DailyDueToday or NotificationType.DailyDueTomorrow
            ? 60 * 60 * 12
            : 60 * 60 * 24;

    private static bool IsExpired(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone;
}

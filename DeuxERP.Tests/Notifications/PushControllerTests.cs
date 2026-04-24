using DeuxERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace DeuxERP.Tests.Notifications;

public class PushControllerTests : BaseIntegrationTest
{
    public PushControllerTests(IntegrationTestFactory<Program> factory) : base(factory) { }

    [Fact]
    public async Task Subscribe_CreatesSubscriptionForAuthenticatedUser()
    {
        await AuthenticateAsync();
        var endpoint = $"https://push.example.com/{Guid.NewGuid()}";

        var response = await _client.PostAsJsonAsync("/api/v1/push/subscribe", new
        {
            Endpoint = endpoint,
            P256dh = "p256dh-key",
            Auth = "auth-key",
            DeviceLabel = "iPhone"
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var subscription = await db.PushSubscriptions.SingleAsync(s => s.Endpoint == endpoint);

        Assert.True(subscription.IsActive);
        Assert.Equal("p256dh-key", subscription.P256dh);
        Assert.Equal("auth-key", subscription.Auth);
        Assert.Equal("iPhone", subscription.DeviceLabel);
    }

    [Fact]
    public async Task Subscribe_ExistingEndpointRefreshesOwnershipAndKeys()
    {
        await AuthenticateAsync();
        var endpoint = $"https://push.example.com/{Guid.NewGuid()}";

        var firstResponse = await _client.PostAsJsonAsync("/api/v1/push/subscribe", new
        {
            Endpoint = endpoint,
            P256dh = "first-p256dh",
            Auth = "first-auth",
            DeviceLabel = "iPhone"
        });
        firstResponse.EnsureSuccessStatusCode();

        Guid firstUserId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            firstUserId = await db.PushSubscriptions
                .Where(s => s.Endpoint == endpoint)
                .Select(s => s.UserId)
                .SingleAsync();
        }

        await AuthenticateAsync();

        var secondResponse = await _client.PostAsJsonAsync("/api/v1/push/subscribe", new
        {
            Endpoint = endpoint,
            P256dh = "second-p256dh",
            Auth = "second-auth",
            DeviceLabel = "Updated iPhone"
        });

        Assert.Equal(HttpStatusCode.NoContent, secondResponse.StatusCode);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var subscription = await verifyDb.PushSubscriptions.SingleAsync(s => s.Endpoint == endpoint);

        Assert.NotEqual(firstUserId, subscription.UserId);
        Assert.Equal("second-p256dh", subscription.P256dh);
        Assert.Equal("second-auth", subscription.Auth);
        Assert.Equal("Updated iPhone", subscription.DeviceLabel);
        Assert.True(subscription.IsActive);
        Assert.Null(subscription.DeactivatedAt);
    }

    [Fact]
    public async Task Unsubscribe_OnlyDeactivatesCurrentUsersEndpoint()
    {
        await AuthenticateAsync();
        var endpoint = $"https://push.example.com/{Guid.NewGuid()}";

        var subscribeResponse = await _client.PostAsJsonAsync("/api/v1/push/subscribe", new
        {
            Endpoint = endpoint,
            P256dh = "p256dh-key",
            Auth = "auth-key",
            DeviceLabel = "iPhone"
        });
        subscribeResponse.EnsureSuccessStatusCode();

        await AuthenticateAsync();

        var response = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/v1/push/unsubscribe")
        {
            Content = JsonContent.Create(new { Endpoint = endpoint })
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var subscription = await db.PushSubscriptions.SingleAsync(s => s.Endpoint == endpoint);
        Assert.True(subscription.IsActive);
        Assert.Null(subscription.DeactivatedAt);
    }
}

using DeuxERP.Domain.Identity;
using DeuxERP.Domain.Sales;
using DeuxERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DeuxERP.Tests.EndpointCoverage;

public class PostgreSqlIntegrityTests : IClassFixture<IntegrationTestFactory<Program>>
{
    private readonly IntegrationTestFactory<Program> _factory;

    public PostgreSqlIntegrityTests(IntegrationTestFactory<Program> factory) => _factory = factory;

    [Fact]
    [Trait("TestSet", "PostgreSQLConstraints")]
    public async Task PostgreSql_EnforcesUniqueIdentityAndXminOptimisticConcurrency()
    {
        await using var setupScope = _factory.Services.CreateAsyncScope();
        var setup = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (!setup.Database.IsNpgsql())
            return;

        var suffix = Guid.NewGuid().ToString("N");
        setup.Users.Add(new User("One", $"user-{suffix}", "hash", $"same-{suffix}@example.com", UserRole.User));
        await setup.SaveChangesAsync();
        setup.Users.Add(new User("Two", $"other-{suffix}", "hash", $"same-{suffix}@example.com", UserRole.User));
        await Assert.ThrowsAsync<DbUpdateException>(() => setup.SaveChangesAsync());
        setup.ChangeTracker.Clear();

        var client = new Client($"Concurrent {suffix}");
        setup.Clients.Add(client);
        await setup.SaveChangesAsync();

        await using var firstScope = _factory.Services.CreateAsyncScope();
        await using var secondScope = _factory.Services.CreateAsyncScope();
        var firstDb = firstScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var secondDb = secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var first = await firstDb.Clients.SingleAsync(x => x.Id == client.Id);
        var second = await secondDb.Clients.SingleAsync(x => x.Id == client.Id);

        first.Update("First writer", null);
        await firstDb.SaveChangesAsync();
        second.Update("Stale writer", null);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondDb.SaveChangesAsync());
    }
}

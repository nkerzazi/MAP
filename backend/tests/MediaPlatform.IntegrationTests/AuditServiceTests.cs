using FluentAssertions;
using MediaPlatform.Infrastructure.Admin;
using MediaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace MediaPlatform.IntegrationTests;

public class AuditServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    public async Task InitializeAsync() { await _pg.StartAsync(); await using var db = NewDb(); await db.Database.MigrateAsync(); }
    public Task DisposeAsync() => _pg.DisposeAsync().AsTask();
    private AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_pg.GetConnectionString()).Options);

    [Fact]
    public async Task Log_then_list_newest_first()
    {
        var actor = Guid.NewGuid();
        await using (var db = NewDb()) await new AuditService(db).LogAsync(actor, "role.assign", "User", actor.ToString());
        await using (var db = NewDb()) await new AuditService(db).LogAsync(actor, "config.set", "Config", "theme");

        await using var read = NewDb();
        var page = await new AuditService(read).ListAsync(1);
        page.Items.Should().HaveCount(2);
        page.Items[0].Action.Should().Be("config.set"); // plus récent d'abord
    }
}

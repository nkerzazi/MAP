using FluentAssertions;
using MediaPlatform.Application.Admin;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Infrastructure.Admin;
using MediaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace MediaPlatform.IntegrationTests;

public class AdminServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    public async Task InitializeAsync() { await _pg.StartAsync(); await using var db = NewDb(); await db.Database.MigrateAsync(); }
    public Task DisposeAsync() => _pg.DisposeAsync().AsTask();
    private AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_pg.GetConnectionString()).Options);
    private AdminService Svc(AppDbContext db) => new(db, new AuditService(db));

    private async Task<Guid> NewUser()
    {
        var id = Guid.NewGuid();
        await using var db = NewDb();
        db.Users.Add(new User { Id = id, Email = $"u{id:N}@map.ma", DisplayName = "U", PasswordHash = "x" });
        await db.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task UpdateUser_deactivates_and_writes_audit()
    {
        var actor = await NewUser(); var target = await NewUser();
        await using (var db = NewDb()) await Svc(db).UpdateUserAsync(target, false, "Nouveau Nom", actor);
        await using var read = NewDb();
        var u = await read.Users.SingleAsync(x => x.Id == target);
        u.IsActive.Should().BeFalse(); u.DisplayName.Should().Be("Nouveau Nom");
        (await read.AuditLogs.AnyAsync(a => a.Action == "user.update" && a.EntityId == target.ToString())).Should().BeTrue();
    }

    [Fact]
    public async Task AssignRole_unknown_throws_and_RemoveRole_audited()
    {
        var actor = await NewUser(); var target = await NewUser();
        await using (var db = NewDb())
        {
            var act = async () => await Svc(db).AssignRoleAsync(target, "Inexistant", actor);
            await act.Should().ThrowAsync<UnknownRoleException>();
        }
        await using (var db = NewDb()) await Svc(db).AssignRoleAsync(target, "Editeur", actor);
        await using (var db = NewDb()) await Svc(db).RemoveRoleAsync(target, "Editeur", actor);
        await using var read = NewDb();
        (await read.UserRoles.CountAsync(ur => ur.UserId == target)).Should().Be(0);
        (await read.AuditLogs.CountAsync(a => a.Action == "role.remove")).Should().Be(1);
    }

    [Fact]
    public async Task Config_set_then_get_with_audit()
    {
        var actor = await NewUser();
        await using (var db = NewDb()) await Svc(db).SetConfigAsync("theme", "sombre", actor);
        await using (var db = NewDb()) await Svc(db).SetConfigAsync("theme", "clair", actor); // upsert
        await using var read = NewDb();
        var cfg = await Svc(read).GetConfigAsync();
        cfg["theme"].Should().Be("clair");
        (await read.AuditLogs.CountAsync(a => a.Action == "config.set")).Should().Be(2);
    }
}

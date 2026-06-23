using FluentAssertions;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Infrastructure.Auth;
using MediaPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace MediaPlatform.IntegrationTests;

public class AdminSeederTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    public async Task InitializeAsync() { await _pg.StartAsync(); await using var db = NewDb(); await db.Database.MigrateAsync(); }
    public Task DisposeAsync() => _pg.DisposeAsync().AsTask();
    private AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_pg.GetConnectionString()).Options);

    [Fact]
    public async Task Seeds_admin_once_and_is_idempotent()
    {
        var hasher = new PasswordHasher<User>();
        await using (var db = NewDb()) await new AdminSeeder(db, hasher).SeedAsync("admin@map.ma", "Adm1n!");
        await using (var db = NewDb()) await new AdminSeeder(db, hasher).SeedAsync("admin@map.ma", "Adm1n!");

        await using var check = NewDb();
        var admins = await check.Users.Include(u => u.Roles).ThenInclude(r => r.Role)
            .Where(u => u.Email == "admin@map.ma").ToListAsync();
        admins.Should().ContainSingle();
        admins[0].Roles.Select(r => r.Role!.Name).Should().Contain("Admin");
    }

    [Fact]
    public async Task Does_nothing_when_credentials_absent()
    {
        await using (var db = NewDb()) await new AdminSeeder(db, new PasswordHasher<User>()).SeedAsync(null, null);
        await using var check = NewDb();
        (await check.Users.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Seeds_super_admin_sa_with_admin_role()
    {
        await using (var db = NewDb())
            await new AdminSeeder(db, new PasswordHasher<User>()).SeedAsync("sa", "sa", "Super administrateur");

        await using var check = NewDb();
        var sa = await check.Users.Include(u => u.Roles).ThenInclude(r => r.Role)
            .SingleAsync(u => u.Email == "sa");
        sa.DisplayName.Should().Be("Super administrateur");
        sa.IsActive.Should().BeTrue();
        sa.Roles.Select(r => r.Role!.Name).Should().Contain("Admin");
    }
}

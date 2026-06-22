using FluentAssertions;
using MediaPlatform.Application.Auth;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Infrastructure.Auth;
using MediaPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;
using Xunit;

namespace MediaPlatform.IntegrationTests;

public class AuthServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        await using var db = NewDb();
        await db.Database.MigrateAsync();
    }
    public Task DisposeAsync() => _pg.DisposeAsync().AsTask();

    private AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(_pg.GetConnectionString()).Options);

    private static JwtTokenGenerator Jwt() => new(Options.Create(new JwtOptions
    { Key = "cle-de-test-tres-longue-pour-hmac-sha256-aaaaaaaaaaaaaaaaaaaa", Issuer = "map", Audience = "map" }));

    private AuthService NewService(AppDbContext db) =>
        new(db, new PasswordHasher<User>(), Jwt());

    [Fact]
    public async Task Register_creates_visiteur_user_with_token()
    {
        await using var db = NewDb();
        var resp = await NewService(db).RegisterAsync(new RegisterRequest("New@MAP.ma", "S3cret!", "Nour"));

        resp.Roles.Should().Equal("Visiteur");
        resp.Email.Should().Be("new@map.ma"); // normalisé
        resp.Token.Should().NotBeNullOrEmpty();

        await using var db2 = NewDb();
        var user = await db2.Users.Include(u => u.Roles).SingleAsync(u => u.Email == "new@map.ma");
        user.PasswordHash.Should().NotBe("S3cret!");
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Register_with_existing_email_throws_conflict()
    {
        await using var db = NewDb();
        var svc = NewService(db);
        await svc.RegisterAsync(new RegisterRequest("dup@map.ma", "S3cret!", "A"));

        await using var db2 = NewDb();
        var act = async () => await NewService(db2).RegisterAsync(new RegisterRequest("dup@map.ma", "x", "B"));
        await act.Should().ThrowAsync<EmailAlreadyUsedException>();
    }

    [Fact]
    public async Task Login_succeeds_with_correct_password_and_fails_otherwise()
    {
        await using (var db = NewDb())
            await NewService(db).RegisterAsync(new RegisterRequest("log@map.ma", "Good1!", "L"));

        await using var db2 = NewDb();
        var ok = await NewService(db2).LoginAsync(new LoginRequest("log@map.ma", "Good1!"));
        ok.Roles.Should().Equal("Visiteur");

        await using var db3 = NewDb();
        var bad = async () => await NewService(db3).LoginAsync(new LoginRequest("log@map.ma", "WRONG"));
        await bad.Should().ThrowAsync<InvalidCredentialsException>();

        await using var db4 = NewDb();
        var missing = async () => await NewService(db4).LoginAsync(new LoginRequest("nobody@map.ma", "x"));
        await missing.Should().ThrowAsync<InvalidCredentialsException>();
    }
}

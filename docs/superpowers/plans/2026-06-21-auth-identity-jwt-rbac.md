# Auth Identity + JWT + RBAC applicatif — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Compléter la Phase 1 avec inscription/connexion (hachage de mot de passe), émission/validation de JWT Bearer, et RBAC applicatif (`[Authorize(Roles=...)]`) sur le schéma `User`/`Role`/`UserRole` déjà migré.

**Architecture:** On conserve le schéma RBAC custom (aucune migration). Hachage via `IPasswordHasher<User>` (utilitaire d'ASP.NET Core Identity, sans le store). JWT HMAC-SHA256 émis par un `JwtTokenGenerator`, validé par le middleware `AddJwtBearer`. Découpage Domain/Application/Infrastructure/Api ; `Api` mince. Inscription publique → rôle `Visiteur` ; rôles `Editeur`/`Admin` attribués par un Admin. Admin initial seedé au boot depuis la configuration.

**Tech Stack:** ASP.NET Core 8, EF Core 8 (PostgreSQL), `Microsoft.Extensions.Identity.Core` (`IPasswordHasher`), `System.IdentityModel.Tokens.Jwt`, `Microsoft.AspNetCore.Authentication.JwtBearer` (déjà référencé par l'Api), xUnit + Testcontainers + `Microsoft.AspNetCore.Mvc.Testing`.

**Référence spec :** `docs/superpowers/specs/2026-06-21-auth-identity-jwt-rbac-design.md`

**Décision claims (critique pour l'intégration Phase 2) :** le token porte des claims **courts** `sub`, `email`, `name`, et un claim `role` par rôle. Le middleware configure `MapInboundClaims = false` (pour que `User.FindFirst("sub")` — déjà utilisé par `VideosController.Create` — fonctionne) et `TokenValidationParameters { NameClaimType = "name", RoleClaimType = "role" }` (pour que `[Authorize(Roles="...")]` matche le claim `role`).

---

## File Structure

**Application** (pur) :
- Create `backend/src/MediaPlatform.Application/Auth/RegisterRequest.cs`
- Create `backend/src/MediaPlatform.Application/Auth/LoginRequest.cs`
- Create `backend/src/MediaPlatform.Application/Auth/AuthResponse.cs`
- Create `backend/src/MediaPlatform.Application/Auth/JwtOptions.cs`
- Create `backend/src/MediaPlatform.Application/Auth/AuthExceptions.cs` (`EmailAlreadyUsedException`, `InvalidCredentialsException`)
- Create `backend/src/MediaPlatform.Application/Interfaces/IJwtTokenGenerator.cs`
- Create `backend/src/MediaPlatform.Application/Interfaces/IAuthService.cs`

**Infrastructure** :
- Create `backend/src/MediaPlatform.Infrastructure/Auth/JwtTokenGenerator.cs`
- Create `backend/src/MediaPlatform.Infrastructure/Auth/AuthService.cs`
- Create `backend/src/MediaPlatform.Infrastructure/Auth/AdminSeeder.cs`
- Modify `backend/src/MediaPlatform.Infrastructure/MediaPlatform.Infrastructure.csproj` (packages)

**Api** :
- Create `backend/src/MediaPlatform.Api/Controllers/AuthController.cs`
- Create `backend/src/MediaPlatform.Api/Controllers/UsersController.cs`
- Create `backend/src/MediaPlatform.Api/Controllers/Dtos/AssignRoleRequest.cs`
- Modify `backend/src/MediaPlatform.Api/Program.cs` (AddAuthentication/JwtBearer, DI, AdminSeeder au boot, Swagger Bearer, `public partial class Program`)
- Modify `backend/src/MediaPlatform.Api/appsettings.json` (section `Jwt` + `Seed`)

**Tests** :
- Create `backend/tests/MediaPlatform.Tests/JwtTokenGeneratorTests.cs` (unit)
- Create `backend/tests/MediaPlatform.Tests/PasswordHasherTests.cs` (unit)
- Create `backend/tests/MediaPlatform.IntegrationTests/AuthServiceTests.cs` (Postgres Testcontainer)
- Create `backend/tests/MediaPlatform.IntegrationTests/AdminSeederTests.cs` (Postgres Testcontainer)
- Create `backend/tests/MediaPlatform.IntegrationTests/AuthEndpointsTests.cs` (WebApplicationFactory + Postgres + MinIO)
- Modify `backend/tests/MediaPlatform.IntegrationTests/MediaPlatform.IntegrationTests.csproj` (add `Microsoft.AspNetCore.Mvc.Testing`, ProjectReference Api)

---

## Task 1: DTOs, options, exceptions, interfaces (Application)

**Files:** the 7 Application files listed above.

- [ ] **Step 1: Créer les DTOs et options**

`RegisterRequest.cs`:
```csharp
namespace MediaPlatform.Application.Auth;

public record RegisterRequest(string Email, string Password, string DisplayName);
```

`LoginRequest.cs`:
```csharp
namespace MediaPlatform.Application.Auth;

public record LoginRequest(string Email, string Password);
```

`AuthResponse.cs`:
```csharp
namespace MediaPlatform.Application.Auth;

public record AuthResponse(
    string Token, DateTimeOffset ExpiresAt, Guid UserId,
    string Email, string DisplayName, IReadOnlyList<string> Roles);
```

`JwtOptions.cs`:
```csharp
namespace MediaPlatform.Application.Auth;

/// <summary>Paramètres JWT (section "Jwt" de la configuration).</summary>
public class JwtOptions
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "map";
    public string Audience { get; set; } = "map";
    public int ExpiryHours { get; set; } = 8;
}
```

`AuthExceptions.cs`:
```csharp
namespace MediaPlatform.Application.Auth;

/// <summary>Email déjà utilisé lors de l'inscription → 409.</summary>
public class EmailAlreadyUsedException(string email)
    : Exception($"L'adresse « {email} » est déjà utilisée.");

/// <summary>Identifiants invalides / compte inactif → 401.</summary>
public class InvalidCredentialsException() : Exception("Identifiants invalides.");
```

- [ ] **Step 2: Créer les interfaces**

`IJwtTokenGenerator.cs`:
```csharp
using MediaPlatform.Domain.Entities;

namespace MediaPlatform.Application.Interfaces;

public interface IJwtTokenGenerator
{
    (string Token, DateTimeOffset ExpiresAt) Generate(User user, IEnumerable<string> roles);
}
```

`IAuthService.cs`:
```csharp
using MediaPlatform.Application.Auth;

namespace MediaPlatform.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
}
```

- [ ] **Step 3: Build** — `dotnet build backend/src/MediaPlatform.Application/MediaPlatform.Application.csproj --nologo` → 0 erreurs.

- [ ] **Step 4: Commit**
```
git add backend/src/MediaPlatform.Application/Auth backend/src/MediaPlatform.Application/Interfaces/IJwtTokenGenerator.cs backend/src/MediaPlatform.Application/Interfaces/IAuthService.cs
git commit -m "feat(auth): DTOs, options, exceptions et interfaces"
```

---

## Task 2: JwtTokenGenerator + packages Infrastructure + tests unitaires

**Files:** `JwtTokenGenerator.cs`, `MediaPlatform.Infrastructure.csproj`, `backend/tests/MediaPlatform.Tests/JwtTokenGeneratorTests.cs`.

- [ ] **Step 1: Ajouter les packages à `MediaPlatform.Infrastructure.csproj`** (dans le `<ItemGroup>` des packages) :
```xml
    <PackageReference Include="Microsoft.Extensions.Identity.Core" Version="8.0.*" />
    <PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.0.*" />
```

- [ ] **Step 2: Écrire le test unitaire** `backend/tests/MediaPlatform.Tests/JwtTokenGeneratorTests.cs`:
```csharp
using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using MediaPlatform.Application.Auth;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Xunit;

namespace MediaPlatform.Tests;

public class JwtTokenGeneratorTests
{
    private static JwtTokenGenerator NewGen() => new(Options.Create(new JwtOptions
    {
        Key = "une-cle-de-test-suffisamment-longue-pour-hmac-sha256-0123456789",
        Issuer = "map", Audience = "map", ExpiryHours = 8
    }));

    [Fact]
    public void Generate_emits_expected_claims_and_expiry()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "a@map.ma", DisplayName = "Alice" };
        var (token, expiresAt) = NewGen().Generate(user, new[] { "Visiteur", "Editeur" });

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims.Should().Contain(c => c.Type == "sub" && c.Value == user.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == "email" && c.Value == "a@map.ma");
        jwt.Claims.Should().Contain(c => c.Type == "name" && c.Value == "Alice");
        jwt.Claims.Where(c => c.Type == "role").Select(c => c.Value)
            .Should().BeEquivalentTo(new[] { "Visiteur", "Editeur" });
        jwt.Issuer.Should().Be("map");
        expiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddHours(8), TimeSpan.FromMinutes(1));
    }
}
```

- [ ] **Step 3: Lancer → échec** — `dotnet test backend/tests/MediaPlatform.Tests --filter JwtTokenGeneratorTests --nologo` (le type n'existe pas).

- [ ] **Step 4: Implémenter** `backend/src/MediaPlatform.Infrastructure/Auth/JwtTokenGenerator.cs`:
```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MediaPlatform.Application.Auth;
using MediaPlatform.Application.Interfaces;
using MediaPlatform.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace MediaPlatform.Infrastructure.Auth;

/// <summary>Émet un JWT HMAC-SHA256 avec claims courts (sub/email/name/role).</summary>
public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtOptions _opts;
    public JwtTokenGenerator(IOptions<JwtOptions> opts) => _opts = opts.Value;

    public (string Token, DateTimeOffset ExpiresAt) Generate(User user, IEnumerable<string> roles)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddHours(_opts.ExpiryHours);
        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("email", user.Email),
            new("name", user.DisplayName),
        };
        claims.AddRange(roles.Select(r => new Claim("role", r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _opts.Issuer, audience: _opts.Audience, claims: claims,
            expires: expiresAt.UtcDateTime, signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
```

- [ ] **Step 5: Lancer → succès** — `dotnet test backend/tests/MediaPlatform.Tests --filter JwtTokenGeneratorTests --nologo` (1 test vert).

- [ ] **Step 6: Commit**
```
git add backend/src/MediaPlatform.Infrastructure/MediaPlatform.Infrastructure.csproj backend/src/MediaPlatform.Infrastructure/Auth/JwtTokenGenerator.cs backend/tests/MediaPlatform.Tests/JwtTokenGeneratorTests.cs
git commit -m "feat(auth): JwtTokenGenerator (HMAC-SHA256) + test"
```

---

## Task 3: Hachage de mot de passe + AuthService + tests

**Files:** `AuthService.cs`, `backend/tests/MediaPlatform.Tests/PasswordHasherTests.cs`, `backend/tests/MediaPlatform.IntegrationTests/AuthServiceTests.cs`.

- [ ] **Step 1: Test unitaire du hachage** `backend/tests/MediaPlatform.Tests/PasswordHasherTests.cs`:
```csharp
using FluentAssertions;
using MediaPlatform.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace MediaPlatform.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_is_not_plaintext_and_verifies()
    {
        var hasher = new PasswordHasher<User>();
        var user = new User();
        var hash = hasher.HashPassword(user, "S3cret!");

        hash.Should().NotBe("S3cret!");
        hasher.VerifyHashedPassword(user, hash, "S3cret!").Should().Be(PasswordVerificationResult.Success);
        hasher.VerifyHashedPassword(user, hash, "mauvais").Should().Be(PasswordVerificationResult.Failed);
    }
}
```
> `PasswordHasher<>` vient de `Microsoft.AspNetCore.Identity` (assembly `Microsoft.Extensions.Identity.Core`). Le projet de tests unitaires référence l'Application ; ajouter aussi la référence projet Infrastructure n'est pas nécessaire — mais le package `Microsoft.Extensions.Identity.Core` doit être résoluble. Comme `MediaPlatform.Tests` ne référence que l'Application, ajouter dans `backend/tests/MediaPlatform.Tests/MediaPlatform.Tests.csproj` un `<PackageReference Include="Microsoft.Extensions.Identity.Core" Version="8.0.*" />`.

- [ ] **Step 2: Lancer → vert immédiat** (teste seulement la lib) — `dotnet test backend/tests/MediaPlatform.Tests --filter PasswordHasherTests --nologo`. Si vert, continuer.

- [ ] **Step 3: Écrire le test d'intégration** `backend/tests/MediaPlatform.IntegrationTests/AuthServiceTests.cs`:
```csharp
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
```

- [ ] **Step 4: Lancer → échec** — `dotnet test backend/tests/MediaPlatform.IntegrationTests --filter AuthServiceTests --nologo` (AuthService n'existe pas).

- [ ] **Step 5: Implémenter** `backend/src/MediaPlatform.Infrastructure/Auth/AuthService.cs`:
```csharp
using MediaPlatform.Application.Auth;
using MediaPlatform.Application.Interfaces;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MediaPlatform.Infrastructure.Auth;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _hasher;
    private readonly IJwtTokenGenerator _jwt;

    public AuthService(AppDbContext db, IPasswordHasher<User> hasher, IJwtTokenGenerator jwt)
    {
        _db = db; _hasher = hasher; _jwt = jwt;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            throw new EmailAlreadyUsedException(email);

        var visiteur = await _db.Roles.SingleAsync(r => r.Name == "Visiteur", ct);
        var user = new User
        {
            Id = Guid.NewGuid(), Email = email, DisplayName = request.DisplayName, IsActive = true
        };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);
        user.Roles.Add(new UserRole { UserId = user.Id, RoleId = visiteur.Id });
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return BuildResponse(user, new[] { "Visiteur" });
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users
            .Include(u => u.Roles).ThenInclude(ur => ur.Role)
            .SingleOrDefaultAsync(u => u.Email == email, ct);

        if (user is null || !user.IsActive ||
            _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            throw new InvalidCredentialsException();

        var roles = user.Roles.Select(ur => ur.Role!.Name).ToList();
        return BuildResponse(user, roles);
    }

    private AuthResponse BuildResponse(User user, IReadOnlyList<string> roles)
    {
        var (token, expiresAt) = _jwt.Generate(user, roles);
        return new AuthResponse(token, expiresAt, user.Id, user.Email, user.DisplayName, roles);
    }
}
```

- [ ] **Step 6: Lancer → succès** — `dotnet test backend/tests/MediaPlatform.IntegrationTests --filter AuthServiceTests --nologo` (3 tests verts).

- [ ] **Step 7: Commit**
```
git add backend/src/MediaPlatform.Infrastructure/Auth/AuthService.cs backend/tests/MediaPlatform.Tests/PasswordHasherTests.cs backend/tests/MediaPlatform.Tests/MediaPlatform.Tests.csproj backend/tests/MediaPlatform.IntegrationTests/AuthServiceTests.cs
git commit -m "feat(auth): AuthService (register/login + hachage) + tests"
```

---

## Task 4: AdminSeeder + test d'idempotence

**Files:** `AdminSeeder.cs`, `backend/tests/MediaPlatform.IntegrationTests/AdminSeederTests.cs`.

- [ ] **Step 1: Écrire le test** `backend/tests/MediaPlatform.IntegrationTests/AdminSeederTests.cs`:
```csharp
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
}
```

- [ ] **Step 2: Lancer → échec** — `dotnet test backend/tests/MediaPlatform.IntegrationTests --filter AdminSeederTests --nologo`.

- [ ] **Step 3: Implémenter** `backend/src/MediaPlatform.Infrastructure/Auth/AdminSeeder.cs`:
```csharp
using MediaPlatform.Domain.Entities;
using MediaPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MediaPlatform.Infrastructure.Auth;

/// <summary>Crée un compte Admin au démarrage si absent (idempotent). Aucun identifiant en dur.</summary>
public class AdminSeeder
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _hasher;
    public AdminSeeder(AppDbContext db, IPasswordHasher<User> hasher) { _db = db; _hasher = hasher; }

    public async Task SeedAsync(string? email, string? password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return; // rien à faire si non configuré

        var normalized = email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == normalized, ct))
            return; // idempotent

        var adminRole = await _db.Roles.SingleAsync(r => r.Name == "Admin", ct);
        var user = new User { Id = Guid.NewGuid(), Email = normalized, DisplayName = "Administrateur", IsActive = true };
        user.PasswordHash = _hasher.HashPassword(user, password);
        user.Roles.Add(new UserRole { UserId = user.Id, RoleId = adminRole.Id });
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 4: Lancer → succès** — `dotnet test backend/tests/MediaPlatform.IntegrationTests --filter AdminSeederTests --nologo` (2 tests verts).

- [ ] **Step 5: Commit**
```
git add backend/src/MediaPlatform.Infrastructure/Auth/AdminSeeder.cs backend/tests/MediaPlatform.IntegrationTests/AdminSeederTests.cs
git commit -m "feat(auth): AdminSeeder idempotent + tests"
```

---

## Task 5: AuthController + UsersController (Api)

**Files:** `AuthController.cs`, `UsersController.cs`, `Controllers/Dtos/AssignRoleRequest.cs`.

- [ ] **Step 1: DTO** `backend/src/MediaPlatform.Api/Controllers/Dtos/AssignRoleRequest.cs`:
```csharp
namespace MediaPlatform.Api.Controllers.Dtos;

public record AssignRoleRequest(string Role);
```

- [ ] **Step 2: `AuthController`** `backend/src/MediaPlatform.Api/Controllers/AuthController.cs`:
```csharp
using MediaPlatform.Application.Auth;
using MediaPlatform.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediaPlatform.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req, CancellationToken ct)
    {
        try { return Ok(await _auth.RegisterAsync(req, ct)); }
        catch (EmailAlreadyUsedException ex) { return Problem(statusCode: 409, detail: ex.Message); }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        try { return Ok(await _auth.LoginAsync(req, ct)); }
        catch (InvalidCredentialsException ex) { return Problem(statusCode: 401, detail: ex.Message); }
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me() => Ok(new
    {
        userId = User.FindFirst("sub")?.Value,
        email = User.FindFirst("email")?.Value,
        displayName = User.FindFirst("name")?.Value,
        roles = User.FindAll("role").Select(c => c.Value).ToArray()
    });
}
```

- [ ] **Step 3: `UsersController`** `backend/src/MediaPlatform.Api/Controllers/UsersController.cs`:
```csharp
using MediaPlatform.Api.Controllers.Dtos;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediaPlatform.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    public UsersController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var users = await _db.Users
            .Include(u => u.Roles).ThenInclude(ur => ur.Role)
            .Select(u => new
            {
                u.Id, u.Email, u.DisplayName, u.IsActive,
                roles = u.Roles.Select(ur => ur.Role!.Name).ToArray()
            })
            .ToListAsync(ct);
        return Ok(users);
    }

    [HttpPost("{id:guid}/roles")]
    public async Task<IActionResult> AssignRole(Guid id, [FromBody] AssignRoleRequest req, CancellationToken ct)
    {
        var user = await _db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return Problem(statusCode: 404, detail: "Utilisateur introuvable.");

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == req.Role, ct);
        if (role is null) return Problem(statusCode: 400, detail: $"Rôle inconnu : {req.Role}.");

        if (user.Roles.All(ur => ur.RoleId != role.Id)) // idempotent
        {
            user.Roles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
            await _db.SaveChangesAsync(ct);
        }
        return Ok(new { id, role = role.Name });
    }
}
```

- [ ] **Step 4: Build** — `dotnet build backend/MediaPlatform.sln --nologo` → 0 erreurs.

- [ ] **Step 5: Commit**
```
git add backend/src/MediaPlatform.Api/Controllers/AuthController.cs backend/src/MediaPlatform.Api/Controllers/UsersController.cs backend/src/MediaPlatform.Api/Controllers/Dtos/AssignRoleRequest.cs
git commit -m "feat(auth): AuthController (register/login/me) + UsersController (Admin)"
```

---

## Task 6: Câblage Program.cs + configuration

**Files:** `Program.cs`, `appsettings.json`.

- [ ] **Step 1: Configuration** — dans `backend/src/MediaPlatform.Api/appsettings.json`, remplacer la section `Jwt` existante et ajouter `Seed`. La section actuelle est :
```json
  "Jwt": {
    "Issuer": "map-media-platform",
    "Audience": "map-media-platform",
    "SigningKey": "CHANGE_ME_IN_PRODUCTION_USE_A_LONG_RANDOM_SECRET"
  },
```
La remplacer par :
```json
  "Jwt": {
    "Issuer": "map-media-platform",
    "Audience": "map-media-platform",
    "Key": "CHANGE_ME_IN_PRODUCTION_USE_A_LONG_RANDOM_SECRET_AT_LEAST_32_CHARS",
    "ExpiryHours": 8
  },
  "Seed": {
    "AdminEmail": "admin@map.ma",
    "AdminPassword": "ChangeMe!Admin2026"
  },
```
(Garder les autres sections — `ConnectionStrings`, `Minio`, `Redis`, etc. — inchangées.)

- [ ] **Step 2: Modifier `Program.cs`** — appliquer ces changements au fichier existant (issu de la Phase 2) :

(a) Ajouter les `using` en tête :
```csharp
using System.Text;
using MediaPlatform.Application.Auth;
using MediaPlatform.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
```

(b) Après les enregistrements DI média existants (avant `builder.Services.AddHangfire(...)`), ajouter l'enregistrement auth :
```csharp
// --- Auth (Identity hasher + JWT + RBAC) ---
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton<Microsoft.AspNetCore.Identity.IPasswordHasher<MediaPlatform.Domain.Entities.User>,
    Microsoft.AspNetCore.Identity.PasswordHasher<MediaPlatform.Domain.Entities.User>>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<AdminSeeder>();

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // conserve le claim "sub" tel quel
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = jwt.Issuer,
            ValidateAudience = true, ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            NameClaimType = "name",
            RoleClaimType = "role",
        };
    });
builder.Services.AddAuthorization();
```

(c) Dans la configuration Swagger (`AddSwaggerGen`), activer le schéma Bearer. Remplacer `builder.Services.AddSwaggerGen();` par :
```csharp
    builder.Services.AddSwaggerGen(c =>
    {
        var scheme = new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization", Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer", BearerFormat = "JWT", In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Reference = new Microsoft.OpenApi.Models.OpenApiReference
            { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" }
        };
        c.AddSecurityDefinition("Bearer", scheme);
        c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });
    });
```

(d) Dans le bloc de démarrage (le `using (var scope = ...)` qui fait `Migrate()` + buckets), ajouter APRÈS la création des buckets l'appel au seeder :
```csharp
    var seeder = sp.GetRequiredService<AdminSeeder>();
    seeder.SeedAsync(builder.Configuration["Seed:AdminEmail"], builder.Configuration["Seed:AdminPassword"])
          .GetAwaiter().GetResult();
```
> Note : `AdminSeeder` est `Scoped` ; il est résolu via `sp` qui est déjà le `ServiceProvider` du scope créé au démarrage — OK.

(e) Tout en bas du fichier, ajouter (pour que `WebApplicationFactory<Program>` puisse référencer le point d'entrée depuis les tests) :
```csharp

public partial class Program;
```

`app.UseAuthentication();` et `app.UseAuthorization();` sont **déjà présents** dans le pipeline (ordre correct : Authentication avant Authorization, avant `MapControllers`). Ne pas les dupliquer.

- [ ] **Step 3: Build** — `dotnet build backend/MediaPlatform.sln --nologo` → 0 erreurs.

- [ ] **Step 4: Commit**
```
git add backend/src/MediaPlatform.Api/Program.cs backend/src/MediaPlatform.Api/appsettings.json
git commit -m "feat(auth): cablage JwtBearer + DI + seed admin au boot + Swagger Bearer"
```

---

## Task 7: Tests d'intégration HTTP (RBAC bout-en-bout)

**Files:** `backend/tests/MediaPlatform.IntegrationTests/MediaPlatform.IntegrationTests.csproj`, `backend/tests/MediaPlatform.IntegrationTests/AuthEndpointsTests.cs`.

> Ces tests bootent l'application réelle via `WebApplicationFactory<Program>`. Comme `Program.cs` crée les buckets MinIO au démarrage, le test fournit **Postgres + MinIO** (Testcontainers) et surcharge la configuration. Ils valident le pipeline JWT + `[Authorize(Roles=...)]` — et donc que les endpoints d'upload de la Phase 2 (`VideosController`, `[Authorize(Roles="Editeur")]`) renvoient bien **401** sans token (et non plus 500).

- [ ] **Step 1: Ajouter au `MediaPlatform.IntegrationTests.csproj`** :
  - package `<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.*" />`
  - référence projet `<ProjectReference Include="..\..\src\MediaPlatform.Api\MediaPlatform.Api.csproj" />`

- [ ] **Step 2: Écrire le test** `backend/tests/MediaPlatform.IntegrationTests/AuthEndpointsTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using MediaPlatform.Application.Auth;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Infrastructure.Persistence;
using MediaPlatform.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Xunit;

namespace MediaPlatform.IntegrationTests;

public class AuthEndpointsTests : IAsyncLifetime, IClassFixture<MinioFixture>
{
    private readonly MinioFixture _minio;
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    private WebApplicationFactory<Program> _factory = default!;

    public AuthEndpointsTests(MinioFixture minio) => _minio = minio;

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:Postgres", _pg.GetConnectionString());
            b.UseSetting("Minio:Endpoint", _minio.Endpoint);
            b.UseSetting("Minio:AccessKey", _minio.AccessKey);
            b.UseSetting("Minio:SecretKey", _minio.SecretKey);
            b.UseSetting("Minio:UseSsl", "false");
            b.UseSetting("Jwt:Key", "cle-de-test-tres-longue-pour-hmac-sha256-bbbbbbbbbbbbbbbbbbbb");
            b.UseSetting("Jwt:Issuer", "map-media-platform");
            b.UseSetting("Jwt:Audience", "map-media-platform");
            b.UseSetting("Seed:AdminEmail", "");   // pas de seed auto ici
            b.UseSetting("Seed:AdminPassword", "");
        });
        // Force le démarrage (migrations + buckets) :
        _ = _factory.Services.GetRequiredService<IHost>();
    }

    public Task DisposeAsync() { _factory.Dispose(); return _pg.DisposeAsync().AsTask(); }

    private HttpClient Client() => _factory.CreateClient();

    private async Task<string> RegisterAndLogin(string email, string password, string display)
    {
        var c = Client();
        (await c.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, password, display)))
            .EnsureSuccessStatusCode();
        var resp = await (await c.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password)))
            .Content.ReadFromJsonAsync<AuthResponse>();
        return resp!.Token;
    }

    private async Task PromoteToAdmin(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.Include(u => u.Roles).SingleAsync(u => u.Email == email.ToLowerInvariant());
        var admin = await db.Roles.SingleAsync(r => r.Name == "Admin");
        if (user.Roles.All(r => r.RoleId != admin.Id))
        { user.Roles.Add(new UserRole { UserId = user.Id, RoleId = admin.Id }); await db.SaveChangesAsync(); }
    }

    [Fact]
    public async Task Users_endpoint_enforces_admin_role()
    {
        // anonyme → 401
        (await Client().GetAsync("/api/v1/users")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // visiteur → 403
        var visiteurToken = await RegisterAndLogin("visiteur@map.ma", "Pass1!", "V");
        var c1 = Client(); c1.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", visiteurToken);
        (await c1.GetAsync("/api/v1/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // admin → 200
        await RegisterAndLogin("boss@map.ma", "Pass1!", "Boss");
        await PromoteToAdmin("boss@map.ma");
        var adminToken = await RegisterAndLogin("boss@map.ma", "Pass1!", "Boss"); // re-login pour token avec rôle Admin
        var c2 = Client(); c2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        (await c2.GetAsync("/api/v1/users")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_duplicate_email_returns_409()
    {
        var c = Client();
        (await c.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest("dupe@map.ma", "Pass1!", "D")))
            .EnsureSuccessStatusCode();
        (await c.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest("dupe@map.ma", "Pass1!", "D")))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Phase2_upload_endpoint_returns_401_without_token()
    {
        // Vérifie que l'ajout de l'auth corrige le 500 observé : VideosController.Create est [Authorize(Roles=Editeur)]
        (await Client().PostAsJsonAsync("/api/v1/videos", new { title = "x" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

> Note d'implémentation : si `_factory.Services.GetRequiredService<IHost>()` ne suffit pas à déclencher le code de démarrage (`Migrate`/buckets), créer simplement un client (`_factory.CreateClient()`) dans `InitializeAsync` — la création du premier client démarre le serheur. Adapter si besoin pour garantir que les migrations sont appliquées avant les requêtes.

- [ ] **Step 3: Lancer → vert** — `dotnet test backend/tests/MediaPlatform.IntegrationTests --filter AuthEndpointsTests --nologo` (Docker : Postgres + MinIO). Si le démarrage de la factory échoue sur MinIO, vérifier que les `UseSetting("Minio:...")` correspondent bien aux clés lues par `Program.cs` (`Minio:Endpoint`, etc.).

- [ ] **Step 4: Lancer toute la suite** — `dotnet test backend/MediaPlatform.sln --nologo` → tout vert (unitaires + intégration ; smoke FFmpeg skippé).

- [ ] **Step 5: Commit**
```
git add backend/tests/MediaPlatform.IntegrationTests/MediaPlatform.IntegrationTests.csproj backend/tests/MediaPlatform.IntegrationTests/AuthEndpointsTests.cs
git commit -m "test(auth): RBAC HTTP bout-en-bout (401/403/200) + upload Phase 2 401"
```

---

## Self-Review (effectuée)

- **Couverture spec :** register/login + hachage (Task 3) · JWT (Task 2) · RBAC `[Authorize(Roles)]` (Task 5, 7) · seed admin configurable (Task 4, 6) · exceptions → 409/401 (Task 5) · GET /me (Task 5) · UsersController Admin (Task 5, 7) · câblage JwtBearer + Swagger (Task 6) · tests unitaires + intégration + HTTP (Task 2,3,4,7). Tous les points de la spec ont une tâche.
- **Intégration Phase 2 :** `MapInboundClaims=false` + claim `sub` rendent fonctionnel le `User.FindFirst("sub")` de `VideosController.Create` ; `RoleClaimType="role"` fait matcher `[Authorize(Roles="Editeur")]`. Task 7 vérifie explicitement que l'endpoint d'upload renvoie 401 (et non plus 500) sans token.
- **À surveiller à l'exécution (incertitudes signalées inline) :** déclenchement du code de démarrage par `WebApplicationFactory` (Task 7, Step 2 note) ; clés de config MinIO lues par `Program.cs`.
- **Cohérence des types :** `IJwtTokenGenerator.Generate(User, IEnumerable<string>)` ; `AuthResponse` identique entre service, controller et tests ; claims `sub/email/name/role` cohérents entre générateur, `Program.cs` (NameClaimType/RoleClaimType) et `AuthController.Me`.
- **Sécurité :** aucun secret en dur (clé JWT et identifiants admin via configuration) ; mot de passe jamais stocké en clair (PasswordHasher).
```

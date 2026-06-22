# Auth : Identity + JWT + RBAC applicatif — Design (Phase 1, complément)

> Conception détaillée du volet authentification / autorisation, complétant la
> Phase 1 (Socle) de la [spec principale](2026-06-21-plateforme-streaming-interne-design.md).
> Date : 2026-06-21.

## 1. Objectif et périmètre

Compléter la Phase 1 en ajoutant l'authentification et l'autorisation applicative :

- Inscription (`register`) et connexion (`login`) avec **hachage de mot de passe**.
- Émission et validation de **JWT (Bearer)**.
- **RBAC applicatif** via `[Authorize(Roles=...)]` sur les rôles `Admin` / `Editeur` / `Visiteur`.
- Compte **Admin initial** créé au démarrage (seed déterministe, configurable).

### Décision d'architecture

On **conserve le schéma RBAC custom déjà migré** (`User` / `Role` / `UserRole`,
avec `User.PasswordHash`). On **n'adopte pas** le store complet d'ASP.NET Core
Identity (`UserManager` / `RoleManager` / `IdentityDbContext`), qui créerait des
tables parallèles (`AspNetUsers`, `AspNetRoles`…) et imposerait une refonte du
schéma. C'est le sens de « RBAC **applicatif** ».

On réutilise uniquement l'utilitaire **`IPasswordHasher<User>`**
(`Microsoft.Extensions.Identity.Core`) pour le hachage — sans le store.

**Aucune migration de schéma n'est nécessaire** : les entités et la table existantes suffisent.

## 2. Règles métier confirmées

- **Inscription publique** : tout visiteur peut créer un compte → rôle **`Visiteur`**
  automatiquement. Les rôles `Editeur` / `Admin` sont attribués **uniquement par un Admin**.
- **Visionnage anonyme** autorisé ; authentification **requise** pour commenter / liker / partager
  (appliqué dans les phases d'engagement, hors de ce périmètre).
- **Seed Admin** : au démarrage, créer un compte Admin s'il n'existe pas, à partir de la
  configuration (`Seed:AdminEmail` / `Seed:AdminPassword`). **Aucun identifiant en dur.**
- **Jeton** : un seul **access token JWT** de durée moyenne (défaut 8 h). Pas de refresh token
  en Phase 1.

## 3. Répartition par couche

Respecte le découpage Domain / Application / Infrastructure / Api ; `Api` reste mince,
la logique vit dans les services, **pas d'accès EF depuis les controllers**.

### 3.1 Application (pur — aucune dépendance EF)

DTOs (`record`) :

- `RegisterRequest(string Email, string Password, string DisplayName)`
- `LoginRequest(string Email, string Password)`
- `AuthResponse(string Token, DateTimeOffset ExpiresAt, Guid UserId, string Email, string DisplayName, IReadOnlyList<string> Roles)`

Interfaces :

- `IAuthService` : `Task<AuthResponse> RegisterAsync(RegisterRequest, CancellationToken)` ;
  `Task<AuthResponse> LoginAsync(LoginRequest, CancellationToken)`.
- `IJwtTokenGenerator` : `(*string token, DateTimeOffset expiresAt*) Generate(User user, IEnumerable<string> roles)`.

Options :

- `JwtOptions { string Key; string Issuer; string Audience; int ExpiryHours = 8; }`
  (liée via `IOptions` depuis la section `Jwt` de la configuration).

Exceptions métier dédiées (mappées en `ProblemDetails` par l'Api) :

- `EmailAlreadyUsedException` → **409 Conflict**.
- `InvalidCredentialsException` → **401 Unauthorized**.

### 3.2 Infrastructure (EF + crypto)

- `AuthService : IAuthService`
  - dépend de `AppDbContext`, `IPasswordHasher<User>`, `IJwtTokenGenerator`.
  - **Register** : normalise l'email (trim + lowercase) ; si email déjà présent →
    `EmailAlreadyUsedException` ; crée le `User` (`IsActive = true`), hache le mot de passe,
    rattache le rôle `Visiteur` via `UserRole` ; renvoie un `AuthResponse` avec token.
  - **Login** : charge l'utilisateur (+ rôles) par email ; si introuvable, inactif, ou
    `VerifyHashedPassword` ≠ succès → `InvalidCredentialsException` ; sinon renvoie le token.
- `JwtTokenGenerator : IJwtTokenGenerator`
  - signe un JWT HMAC-SHA256 avec `JwtOptions.Key` ; claims :
    `sub` = `User.Id`, `email`, `name` = `DisplayName`, + un claim `role` par rôle ;
    `iss` / `aud` / `exp` depuis les options.
- `AdminSeeder` (classe avec méthode `Task SeedAsync(...)`)
  - exécutée au démarrage **après** `db.Database.Migrate()` ;
  - lit `Seed:AdminEmail` / `Seed:AdminPassword` ; si absent → ne fait rien (log d'avertissement) ;
  - si aucun utilisateur avec cet email → crée l'Admin (hash + rôle `Admin`) ; **idempotent**.

Nouveau package : **`Microsoft.Extensions.Identity.Core`** (Infrastructure) pour `IPasswordHasher<>`.

### 3.3 Api (mince)

`AuthController` (`[Route("api/v1/auth")]`) :

- `POST /register` → `IAuthService.RegisterAsync` → **200** `AuthResponse` (ou 409).
- `POST /login` → `IAuthService.LoginAsync` → **200** `AuthResponse` (ou 401).
- `GET /me` (`[Authorize]`) → projette les claims du JWT en `{ userId, email, displayName, roles[] }`.

`UsersController` (`[Route("api/v1/users")]`, **minimal**, prouve le RBAC bout-en-bout) :

- `GET /` (`[Authorize(Roles="Admin")]`) → liste des utilisateurs (id, email, displayName, isActive, roles).
- `POST /{id:guid}/roles` (`[Authorize(Roles="Admin")]`) → body `{ role }` ; ajoute le rôle à l'utilisateur
  (idempotent ; rôle inconnu → 400, utilisateur inconnu → 404).

`Program.cs` :

- `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)` avec
  `TokenValidationParameters` (issuer, audience, clé symétrique, `ValidateLifetime`, zéro clock skew toléré au-delà du défaut).
- `AddAuthorization()`.
- DI : `IAuthService`, `IJwtTokenGenerator`, `IPasswordHasher<User>` (`AddScoped`/`AddSingleton` selon le cas),
  `Configure<JwtOptions>(config.GetSection("Jwt"))`.
- Au boot : après `Migrate()`, appel de `AdminSeeder.SeedAsync`.
- Swagger : ajout du schéma de sécurité Bearer (pour tester depuis l'UI).

## 4. Flux

```
register/login ─▶ AuthService
                    ├─ IPasswordHasher (hash / verify)
                    ├─ AppDbContext  (User + UserRole)
                    └─ IJwtTokenGenerator ─▶ AuthResponse{ Token, ExpiresAt, Roles }

client ──Authorization: Bearer <token>──▶ Api
   middleware JWT valide la signature + claims  ─▶  [Authorize(Roles="Admin")]
```

## 5. Configuration (appsettings)

```jsonc
"Jwt": { "Key": "<secret>", "Issuer": "map", "Audience": "map", "ExpiryHours": 8 },
"Seed": { "AdminEmail": "admin@map.ma", "AdminPassword": "<secret>" }
```

En développement, valeurs par défaut dans `appsettings.Development.json` (secret factice) ;
en production, fournies via variables d'environnement / secrets. Jamais de secret commité en clair.

## 6. Tests (TDD ; Testcontainers déjà en place)

**Intégration** (`WebApplicationFactory` + PostgreSQL Testcontainers ;
ajout du package `Microsoft.AspNetCore.Mvc.Testing` au projet de tests) :

- `register` crée un utilisateur `Visiteur` et renvoie un JWT valide (signature + claims).
- `register` avec email déjà utilisé → **409**.
- `login` mot de passe correct → **200** + token ; incorrect / utilisateur inconnu → **401**.
- `GET /users` : **401** sans token, **403** avec rôle non-Admin, **200** avec rôle Admin.
- `AdminSeeder` crée l'Admin et est **idempotent** (deux exécutions → un seul Admin).

**Unitaire** :

- Roundtrip de hachage (`IPasswordHasher` : hash ≠ clair ; `VerifyHashedPassword` réussit).
- `JwtTokenGenerator` : le token décodé contient `sub`, `email`, `name`, les claims `role`,
  et une `exp` cohérente avec `ExpiryHours`.

## 7. Hors périmètre (YAGNI)

Refresh tokens, logout / révocation de jeton, reset de mot de passe, verrouillage après
échecs (lockout), CRUD utilisateurs complet, audit des connexions → phases ultérieures.

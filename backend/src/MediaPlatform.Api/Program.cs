// Point d'entrée. Mode API (défaut) ou worker Hangfire (--worker).
using System.Text;
using Hangfire;
using Hangfire.PostgreSql;
using MediaPlatform.Application.Auth;
using MediaPlatform.Application.Interfaces;
using MediaPlatform.Application.Media;
using MediaPlatform.Infrastructure.Auth;
using MediaPlatform.Infrastructure.Media;
using MediaPlatform.Infrastructure.Persistence;
using MediaPlatform.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using MediaPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var isWorker = args.Contains("--worker");
var pg = builder.Configuration.GetConnectionString("Postgres")!;

builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(pg));
builder.Services.Configure<MinioOptions>(builder.Configuration.GetSection("Minio"));
builder.Services.Configure<TranscodingOptions>(builder.Configuration.GetSection("Transcoding"));
builder.Services.AddSingleton<IObjectStorage, MinioObjectStorage>();
builder.Services.AddScoped<IUploadService, UploadService>();
builder.Services.AddScoped<IVideoTranscoder, FfmpegVideoTranscoder>();
builder.Services.AddScoped<ITranscodePipeline, TranscodePipeline>();
builder.Services.AddScoped<TranscodeVideoJob>();

// --- Auth (Identity hasher + JWT + RBAC) ---
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
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

builder.Services.AddHangfire(cfg => cfg.UsePostgreSqlStorage(o => o.UseNpgsqlConnection(pg)));
if (isWorker)
    builder.Services.AddHangfireServer();

if (!isWorker)
{
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
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
}

var app = builder.Build();

// Migrations + buckets au démarrage.
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    sp.GetRequiredService<AppDbContext>().Database.Migrate();
    var storage = sp.GetRequiredService<IObjectStorage>();
    var minio = builder.Configuration.GetSection("Minio").Get<MinioOptions>() ?? new MinioOptions();
    storage.EnsureBucketAsync(minio.OriginalsBucket).GetAwaiter().GetResult();
    storage.EnsureBucketAsync(minio.HlsBucket).GetAwaiter().GetResult();

    var seeder = sp.GetRequiredService<AdminSeeder>();
    seeder.SeedAsync(builder.Configuration["Seed:AdminEmail"], builder.Configuration["Seed:AdminPassword"])
          .GetAwaiter().GetResult();
}

if (isWorker)
{
    app.Run();           // héberge seulement le serveur Hangfire
    return;
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program;

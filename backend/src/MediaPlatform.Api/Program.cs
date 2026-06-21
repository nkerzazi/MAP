// Point d'entrée de l'API.
// Phase 1 (Socle) : persistance PostgreSQL via EF Core + application des migrations.
// À venir : Identity + JWT, Hangfire, IObjectStorage (MinIO), IVideoTranscoder (FFmpeg).

using MediaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

var app = builder.Build();

// Applique les migrations en attente au démarrage (schéma + seed RBAC).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
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

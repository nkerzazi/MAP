// Point d'entrée de l'API. Squelette — la configuration détaillée (DbContext, Identity,
// JWT, Hangfire, MinIO, CORS) sera ajoutée lors de la phase « Socle » du plan.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// TODO(phase 1): AddDbContext PostgreSQL, AddIdentity + JWT, AddHangfire,
//                enregistrer IObjectStorage (MinIO) et IVideoTranscoder (FFmpeg).

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

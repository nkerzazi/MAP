// Point d'entrée. Mode API (défaut) ou worker Hangfire (--worker).
using Hangfire;
using Hangfire.PostgreSql;
using MediaPlatform.Application.Interfaces;
using MediaPlatform.Application.Media;
using MediaPlatform.Infrastructure.Media;
using MediaPlatform.Infrastructure.Persistence;
using MediaPlatform.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

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

builder.Services.AddHangfire(cfg => cfg.UsePostgreSqlStorage(o => o.UseNpgsqlConnection(pg)));
if (isWorker)
    builder.Services.AddHangfireServer();

if (!isWorker)
{
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
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

using Hangfire;
using MediaPlatform.Api.Controllers.Dtos;
using MediaPlatform.Application.Interfaces;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Domain.Enums;
using MediaPlatform.Infrastructure.Media;
using MediaPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediaPlatform.Api.Controllers;

[ApiController]
[Route("api/v1/videos")]
public class VideosController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IUploadService _upload;
    private readonly IBackgroundJobClient _jobs;

    public VideosController(AppDbContext db, IUploadService upload, IBackgroundJobClient jobs)
    {
        _db = db; _upload = upload; _jobs = jobs;
    }

    [HttpGet]
    public IActionResult List([FromQuery] string? q, [FromQuery] int page = 1)
        => Ok(new { items = Array.Empty<object>(), page, q });

    [HttpGet("{id:guid}")]
    public IActionResult Get(Guid id) => Ok(new { id });

    [HttpGet("{id:guid}/stream")]
    public IActionResult Stream(Guid id) => Ok(new { id, manifestUrl = (string?)null, renditions = Array.Empty<object>() });

    /// <summary>Initialise une vidéo (Draft). Réservé aux éditeurs.</summary>
    [HttpPost]
    [Authorize(Roles = "Editeur")]
    public async Task<IActionResult> Create([FromBody] CreateVideoRequest req, CancellationToken ct)
    {
        var ownerId = Guid.Parse(User.FindFirst("sub")!.Value);
        var slug = $"{Slugify(req.Title)}-{Guid.NewGuid():N}";
        if (slug.Length > 320) slug = slug[..320];
        var video = new Video
        {
            Id = Guid.NewGuid(), Title = req.Title, Description = req.Description,
            CategoryId = req.CategoryId, OwnerId = ownerId, Status = VideoStatus.Draft,
            Slug = slug
        };
        _db.Videos.Add(video);
        await _db.SaveChangesAsync(ct);
        return Ok(new { id = video.Id });
    }

    /// <summary>Téléverse un chunk (octets bruts dans le corps de la requête).</summary>
    [HttpPost("{id:guid}/upload/chunk")]
    [Authorize(Roles = "Editeur")]
    public async Task<IActionResult> UploadChunk(Guid id, [FromQuery] int index, CancellationToken ct)
    {
        if (!await _db.Videos.AnyAsync(v => v.Id == id && v.Status == VideoStatus.Draft, ct))
            return Problem(statusCode: 404, detail: "Vidéo introuvable ou déjà finalisée.");
        var received = await _upload.StoreChunkAsync(id, index, Request.Body, Request.ContentLength ?? 0, ct);
        return Ok(new { received });
    }

    /// <summary>Finalise l'upload et enfile le transcodage.</summary>
    [HttpPost("{id:guid}/upload/complete")]
    [Authorize(Roles = "Editeur")]
    public async Task<IActionResult> Complete(Guid id, [FromQuery] int total, CancellationToken ct)
    {
        var video = await _db.Videos.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (video is null) return Problem(statusCode: 404, detail: "Vidéo introuvable.");
        try
        {
            video.OriginalKey = await _upload.CompleteAsync(id, total, ct);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(statusCode: 409, detail: ex.Message);
        }
        video.Status = VideoStatus.Processing;
        await _db.SaveChangesAsync(ct);
        _jobs.Enqueue<TranscodeVideoJob>(j => j.ExecuteAsync(id));
        return Ok(new { id, status = video.Status.ToString() });
    }

    private static string Slugify(string s) =>
        new string(s.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray())
            .Trim('-');
}

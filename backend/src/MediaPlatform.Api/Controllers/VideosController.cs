using Hangfire;
using MediaPlatform.Api.Controllers.Dtos;
using MediaPlatform.Application.Admin;
using MediaPlatform.Application.Catalog;
using MediaPlatform.Application.Engagement;
using MediaPlatform.Application.Interfaces;
using MediaPlatform.Application.Streaming;
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
    private readonly ICatalogService _catalog;
    private readonly IStreamingService _streaming;
    private readonly IEngagementService _engagement;
    private readonly IAnalyticsService _analytics;

    public VideosController(AppDbContext db, IUploadService upload, IBackgroundJobClient jobs,
        ICatalogService catalog, IStreamingService streaming, IEngagementService engagement, IAnalyticsService analytics)
    {
        _db = db; _upload = upload; _jobs = jobs; _catalog = catalog; _streaming = streaming;
        _engagement = engagement; _analytics = analytics;
    }

    /// <summary>Télémétrie de visionnage (anonyme autorisé).</summary>
    [HttpPost("{id:guid}/views")]
    public async Task<IActionResult> RecordView(Guid id, [FromBody] RecordViewRequest req, CancellationToken ct)
    {
        try { await _analytics.RecordViewAsync(id, req.WatchSeconds, req.SessionId, CurrentUserIdOrNull(), ct); return NoContent(); }
        catch (VideoNotFoundException) { return Problem(statusCode: 404, detail: "Vidéo introuvable."); }
    }

    // --- Engagement ---

    [HttpGet("{id:guid}/comments")]
    public async Task<IActionResult> ListComments(Guid id, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        try { return Ok(await _engagement.ListCommentsAsync(id, page, CurrentUserIdOrNull(), IsAdmin(), ct)); }
        catch (VideoNotFoundException) { return Problem(statusCode: 404, detail: "Vidéo introuvable."); }
    }

    [HttpPost("{id:guid}/comments")]
    [Authorize]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] CreateCommentRequest req, CancellationToken ct)
    {
        try { return Ok(await _engagement.AddCommentAsync(id, req.Body, CurrentUserId(), ct)); }
        catch (ArgumentException ex) { return Problem(statusCode: 400, detail: ex.Message); }
        catch (VideoNotFoundException) { return Problem(statusCode: 404, detail: "Vidéo introuvable."); }
    }

    [HttpDelete("{id:guid}/comments/{commentId:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteComment(Guid id, Guid commentId, CancellationToken ct)
    {
        try { await _engagement.DeleteCommentAsync(id, commentId, CurrentUserId(), IsAdmin(), ct); return NoContent(); }
        catch (CommentNotFoundException) { return Problem(statusCode: 404, detail: "Commentaire introuvable."); }
        catch (NotCommentAuthorException) { return Problem(statusCode: 403, detail: "Action réservée à l'auteur."); }
    }

    [HttpPost("{id:guid}/likes")]
    [Authorize]
    public async Task<IActionResult> Like(Guid id, CancellationToken ct)
    {
        try { return Ok(await _engagement.LikeAsync(id, CurrentUserId(), ct)); }
        catch (VideoNotFoundException) { return Problem(statusCode: 404, detail: "Vidéo introuvable."); }
    }

    [HttpDelete("{id:guid}/likes")]
    [Authorize]
    public async Task<IActionResult> Unlike(Guid id, CancellationToken ct)
    {
        try { return Ok(await _engagement.UnlikeAsync(id, CurrentUserId(), ct)); }
        catch (VideoNotFoundException) { return Problem(statusCode: 404, detail: "Vidéo introuvable."); }
    }

    [HttpPost("{id:guid}/shares")]
    [Authorize]
    public async Task<IActionResult> Share(Guid id, [FromBody] ShareRequest req, CancellationToken ct)
    {
        try { return Ok(new { shareCount = await _engagement.ShareAsync(id, req.Channel, CurrentUserId(), ct) }); }
        catch (ArgumentException ex) { return Problem(statusCode: 400, detail: ex.Message); }
        catch (VideoNotFoundException) { return Problem(statusCode: 404, detail: "Vidéo introuvable."); }
    }

    [HttpGet("{id:guid}/engagement")]
    public async Task<IActionResult> Engagement(Guid id, CancellationToken ct)
    {
        try { return Ok(await _engagement.GetSummaryAsync(id, CurrentUserIdOrNull(), IsAdmin(), ct)); }
        catch (VideoNotFoundException) { return Problem(statusCode: 404, detail: "Vidéo introuvable."); }
    }

    // --- Catalogue public (anonyme) ---

    /// <summary>Catalogue public (vidéos publiées), recherche ?q=, filtres et pagination.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] Guid? categoryId,
        [FromQuery] string? tag, [FromQuery] int page = 1, CancellationToken ct = default)
        => Ok(await _catalog.SearchPublishedAsync(new CatalogQuery(q, categoryId, tag, page), ct));

    /// <summary>Détail d'une vidéo (publiée pour tous ; non publiée réservée au propriétaire/Admin).</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        try { return Ok(await _catalog.GetDetailAsync(id, CurrentUserIdOrNull(), IsAdmin(), ct)); }
        catch (VideoNotFoundException) { return Problem(statusCode: 404, detail: "Vidéo introuvable."); }
    }

    /// <summary>URL du manifeste HLS + variantes (consommé par le lecteur).</summary>
    [HttpGet("{id:guid}/stream")]
    public async Task<IActionResult> Stream(Guid id, CancellationToken ct)
    {
        try
        {
            var renditions = await _streaming.GetRenditionsAsync(id, CurrentUserIdOrNull(), IsAdmin(), ct);
            return Ok(new { id, manifestUrl = $"/api/v1/videos/{id}/hls/master.m3u8", renditions });
        }
        catch (VideoNotFoundException) { return Problem(statusCode: 404, detail: "Vidéo introuvable."); }
    }

    /// <summary>Proxy HLS : streame manifeste/segments depuis MinIO.</summary>
    [HttpGet("{id:guid}/hls/{**path}")]
    public async Task<IActionResult> Hls(Guid id, string path, CancellationToken ct)
    {
        try
        {
            var obj = await _streaming.OpenHlsAsync(id, path, CurrentUserIdOrNull(), IsAdmin(), ct);
            return File(obj.Content, obj.ContentType, enableRangeProcessing: true);
        }
        catch (ArgumentException) { return Problem(statusCode: 400, detail: "Chemin HLS invalide."); }
        catch (VideoNotFoundException) { return Problem(statusCode: 404, detail: "Vidéo introuvable."); }
    }

    // --- Édition (propriétaire seul, ou Admin) ---

    /// <summary>Met à jour les métadonnées (titre, description, catégorie, tags).</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Editeur,Admin")]
    public Task<IActionResult> Update(Guid id, [FromBody] UpdateVideoRequest req, CancellationToken ct)
        => CatalogAction(() => _catalog.UpdateAsync(id, req, CurrentUserId(), IsAdmin(), ct));

    /// <summary>Publie la vidéo (Ready/Archived → Published).</summary>
    [HttpPost("{id:guid}/publish")]
    [Authorize(Roles = "Editeur,Admin")]
    public Task<IActionResult> Publish(Guid id, CancellationToken ct)
        => CatalogVoid(() => _catalog.PublishAsync(id, CurrentUserId(), IsAdmin(), ct));

    /// <summary>Archive la vidéo (Published → Archived).</summary>
    [HttpPost("{id:guid}/archive")]
    [Authorize(Roles = "Editeur,Admin")]
    public Task<IActionResult> Archive(Guid id, CancellationToken ct)
        => CatalogVoid(() => _catalog.ArchiveAsync(id, CurrentUserId(), IsAdmin(), ct));

    /// <summary>Vidéos de l'éditeur courant (tous statuts), paginées.</summary>
    [HttpGet("mine")]
    [Authorize(Roles = "Editeur,Admin")]
    public async Task<IActionResult> Mine([FromQuery] int page = 1, CancellationToken ct = default)
        => Ok(await _catalog.GetMineAsync(CurrentUserId(), page, ct));

    // --- Upload (Phase 2, éditeur) ---

    /// <summary>Initialise une vidéo (Draft). Réservé aux éditeurs.</summary>
    [HttpPost]
    [Authorize(Roles = "Editeur")]
    public async Task<IActionResult> Create([FromBody] CreateVideoRequest req, CancellationToken ct)
    {
        var ownerId = CurrentUserId();
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

    /// <summary>Téléverse un chunk (octets bruts dans le corps de la requête).
    /// Limite de taille relevée à 10 Go sur ce seul endpoint (cohérence avec Nginx) ;
    /// les autres endpoints conservent la protection Kestrel par défaut (30 Mo).</summary>
    [HttpPost("{id:guid}/upload/chunk")]
    [Authorize(Roles = "Editeur")]
    [RequestSizeLimit(10L * 1024 * 1024 * 1024)]
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

    // --- Helpers ---

    private Guid CurrentUserId() => Guid.Parse(User.FindFirst("sub")!.Value);
    private Guid? CurrentUserIdOrNull() => User.FindFirst("sub") is { } c ? Guid.Parse(c.Value) : null;
    private bool IsAdmin() => User.Identity?.IsAuthenticated == true && User.IsInRole("Admin");

    private async Task<IActionResult> CatalogAction<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (VideoNotFoundException) { return Problem(statusCode: 404, detail: "Vidéo introuvable."); }
        catch (NotVideoOwnerException) { return Problem(statusCode: 403, detail: "Action réservée au propriétaire."); }
        catch (CategoryNotFoundException ex) { return Problem(statusCode: 400, detail: ex.Message); }
        catch (InvalidVideoStateException ex) { return Problem(statusCode: 409, detail: ex.Message); }
    }

    private async Task<IActionResult> CatalogVoid(Func<Task> action)
    {
        try { await action(); return Ok(); }
        catch (VideoNotFoundException) { return Problem(statusCode: 404, detail: "Vidéo introuvable."); }
        catch (NotVideoOwnerException) { return Problem(statusCode: 403, detail: "Action réservée au propriétaire."); }
        catch (InvalidVideoStateException ex) { return Problem(statusCode: 409, detail: ex.Message); }
    }

    private static string Slugify(string s) =>
        new string(s.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray())
            .Trim('-');
}

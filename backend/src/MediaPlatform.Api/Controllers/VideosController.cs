using Microsoft.AspNetCore.Mvc;

namespace MediaPlatform.Api.Controllers;

/// <summary>Catalogue et diffusion vidéo. Squelette — les actions seront implémentées
/// dans les phases « Catalogue éditeur » et « Diffusion & viewer ».</summary>
[ApiController]
[Route("api/v1/videos")]
public class VideosController : ControllerBase
{
    /// <summary>Catalogue public (vidéos publiées), avec recherche ?q= et pagination.</summary>
    [HttpGet]
    public IActionResult List([FromQuery] string? q, [FromQuery] int page = 1)
        => Ok(new { items = Array.Empty<object>(), page, q });

    /// <summary>Détail d'une vidéo + métadonnées.</summary>
    [HttpGet("{id:guid}")]
    public IActionResult Get(Guid id) => Ok(new { id });

    /// <summary>URL du manifeste HLS + variantes — consommé par le lecteur (intégrable via API).</summary>
    [HttpGet("{id:guid}/stream")]
    public IActionResult Stream(Guid id) => Ok(new { id, manifestUrl = (string?)null, renditions = Array.Empty<object>() });
}

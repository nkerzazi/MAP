using MediaPlatform.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace MediaPlatform.Api.Controllers;

/// <summary>Tags du catalogue : liste / autocomplétion (anonyme autorisé).</summary>
[ApiController]
[Route("api/v1/tags")]
public class TagsController : ControllerBase
{
    private readonly ITagService _tags;
    public TagsController(ITagService tags) => _tags = tags;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? q, CancellationToken ct)
        => Ok(await _tags.ListAsync(q, ct));
}

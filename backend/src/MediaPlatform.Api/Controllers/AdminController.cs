using MediaPlatform.Application.Admin;
using MediaPlatform.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediaPlatform.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IAnalyticsService _analytics;
    private readonly IAuditService _audit;
    private readonly IAdminService _admin;

    public AdminController(IAnalyticsService analytics, IAuditService audit, IAdminService admin)
    {
        _analytics = analytics; _audit = audit; _admin = admin;
    }

    private Guid ActorId() => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct) => Ok(await _analytics.GetStatsAsync(ct));

    [HttpGet("audit")]
    public async Task<IActionResult> Audit([FromQuery] int page = 1, CancellationToken ct = default)
        => Ok(await _audit.ListAsync(page, ct));

    [HttpGet("config")]
    public async Task<IActionResult> GetConfig(CancellationToken ct) => Ok(await _admin.GetConfigAsync(ct));

    [HttpPut("config")]
    public async Task<IActionResult> SetConfig([FromBody] SetConfigRequest req, CancellationToken ct)
    {
        await _admin.SetConfigAsync(req.Key, req.Value, ActorId(), ct);
        return NoContent();
    }
}

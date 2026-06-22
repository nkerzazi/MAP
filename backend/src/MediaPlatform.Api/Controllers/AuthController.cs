using MediaPlatform.Application.Auth;
using MediaPlatform.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediaPlatform.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req, CancellationToken ct)
    {
        try { return Ok(await _auth.RegisterAsync(req, ct)); }
        catch (EmailAlreadyUsedException ex) { return Problem(statusCode: 409, detail: ex.Message); }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        try { return Ok(await _auth.LoginAsync(req, ct)); }
        catch (InvalidCredentialsException ex) { return Problem(statusCode: 401, detail: ex.Message); }
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me() => Ok(new
    {
        userId = User.FindFirst("sub")?.Value,
        email = User.FindFirst("email")?.Value,
        displayName = User.FindFirst("name")?.Value,
        roles = User.FindAll("role").Select(c => c.Value).ToArray()
    });
}

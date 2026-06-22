using MediaPlatform.Api.Controllers.Dtos;
using MediaPlatform.Application.Admin;
using MediaPlatform.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediaPlatform.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly IAdminService _admin;
    public UsersController(IAdminService admin) => _admin = admin;

    private Guid ActorId() => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _admin.ListUsersAsync(ct));

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest req, CancellationToken ct)
        => Run(() => _admin.UpdateUserAsync(id, req.IsActive, req.DisplayName, ActorId(), ct));

    [HttpPost("{id:guid}/roles")]
    public Task<IActionResult> AssignRole(Guid id, [FromBody] AssignRoleRequest req, CancellationToken ct)
        => Run(() => _admin.AssignRoleAsync(id, req.Role, ActorId(), ct));

    [HttpDelete("{id:guid}/roles/{role}")]
    public Task<IActionResult> RemoveRole(Guid id, string role, CancellationToken ct)
        => Run(() => _admin.RemoveRoleAsync(id, role, ActorId(), ct));

    private async Task<IActionResult> Run(Func<Task> action)
    {
        try { await action(); return NoContent(); }
        catch (UserNotFoundException) { return Problem(statusCode: 404, detail: "Utilisateur introuvable."); }
        catch (UnknownRoleException ex) { return Problem(statusCode: 400, detail: ex.Message); }
    }
}

using MediaPlatform.Api.Controllers.Dtos;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediaPlatform.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    public UsersController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var users = await _db.Users
            .Include(u => u.Roles).ThenInclude(ur => ur.Role)
            .Select(u => new
            {
                u.Id, u.Email, u.DisplayName, u.IsActive,
                roles = u.Roles.Select(ur => ur.Role!.Name).ToArray()
            })
            .ToListAsync(ct);
        return Ok(users);
    }

    [HttpPost("{id:guid}/roles")]
    public async Task<IActionResult> AssignRole(Guid id, [FromBody] AssignRoleRequest req, CancellationToken ct)
    {
        var user = await _db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return Problem(statusCode: 404, detail: "Utilisateur introuvable.");

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == req.Role, ct);
        if (role is null) return Problem(statusCode: 400, detail: $"Rôle inconnu : {req.Role}.");

        if (user.Roles.All(ur => ur.RoleId != role.Id)) // idempotent
        {
            user.Roles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
            await _db.SaveChangesAsync(ct);
        }
        return Ok(new { id, role = role.Name });
    }
}

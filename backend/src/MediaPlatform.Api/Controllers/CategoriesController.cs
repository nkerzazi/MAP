using MediaPlatform.Application.Catalog;
using MediaPlatform.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediaPlatform.Api.Controllers;

/// <summary>Catégories du catalogue : liste publique (filtres, formulaires) et CRUD réservé à l'Admin.</summary>
[ApiController]
[Route("api/v1/categories")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categories;
    public CategoriesController(ICategoryService categories) => _categories = categories;

    private Guid ActorId() => Guid.Parse(User.FindFirst("sub")!.Value);

    /// <summary>Liste des catégories (anonyme autorisé).</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _categories.ListAsync(ct));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateCategoryRequest req, CancellationToken ct)
    {
        try { return Ok(await _categories.CreateAsync(req.Name, ActorId(), ct)); }
        catch (DuplicateCategoryException ex) { return Problem(statusCode: 409, detail: ex.Message); }
        catch (ArgumentException ex) { return Problem(statusCode: 400, detail: ex.Message); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCategoryRequest req, CancellationToken ct)
    {
        try { return Ok(await _categories.UpdateAsync(id, req.Name, ActorId(), ct)); }
        catch (CategoryNotFoundException) { return Problem(statusCode: 404, detail: "Catégorie introuvable."); }
        catch (DuplicateCategoryException ex) { return Problem(statusCode: 409, detail: ex.Message); }
        catch (ArgumentException ex) { return Problem(statusCode: 400, detail: ex.Message); }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try { await _categories.DeleteAsync(id, ActorId(), ct); return NoContent(); }
        catch (CategoryNotFoundException) { return Problem(statusCode: 404, detail: "Catégorie introuvable."); }
        catch (CategoryInUseException ex) { return Problem(statusCode: 409, detail: ex.Message); }
    }
}

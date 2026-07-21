using LinguaCoach.Api.HelpContent;
using LinguaCoach.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Serves the full static help-content map (key -> HTML) to the admin SPA in one call. Content is
/// dev-authored and never edited at runtime, so the whole map is fetched once and cached
/// client-side rather than fetched per key.
/// </summary>
[ApiController]
[Route("api/admin/help-content")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminHelpContentController : ControllerBase
{
    private readonly IHelpContentProvider _helpContent;

    public AdminHelpContentController(IHelpContentProvider helpContent)
    {
        _helpContent = helpContent;
    }

    [HttpGet]
    public ActionResult<IReadOnlyDictionary<string, string>> GetAll()
    {
        return Ok(_helpContent.GetAll());
    }
}

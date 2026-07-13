using System.Security.Claims;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Phase H2 — Import Content UX v1. A product-friendly admin entry point over the existing
/// Phase E1 import pipeline (see <c>AdminResourceImportController</c>/<see cref="IContentImportService"/>):
/// paste text/CSV/JSON, choose a broad resource type + default metadata, get back pending
/// <c>ResourceCandidate</c> rows for review. Never publishes anything — see
/// <c>AdminResourceCandidateController</c> for approve/reject/publish.
/// </summary>
[ApiController]
[Route("api/admin/content-imports")]
[Authorize(Roles = "Admin")]
public sealed class AdminContentImportController : ControllerBase
{
    // Phase J5b — a null value means "Mixed": don't force a type, let ResourceImportService's
    // existing per-row field-name inference classify each row independently.
    private static readonly IReadOnlyDictionary<string, ResourceCandidateType?> SupportedResourceTypes =
        new Dictionary<string, ResourceCandidateType?>(StringComparer.OrdinalIgnoreCase)
        {
            ["vocabulary"] = ResourceCandidateType.VocabularyEntry,
            ["grammar"] = ResourceCandidateType.GrammarProfileEntry,
            ["reading"] = ResourceCandidateType.ReadingPassage,
            // Phase J5a
            ["writing"] = ResourceCandidateType.WritingPrompt,
            // Phase J5c
            ["listening"] = ResourceCandidateType.ListeningPassage,
            // Phase J5b
            ["mixed"] = null,
        };

    private static readonly IReadOnlyDictionary<string, ContentImportInputMode> SupportedInputModes =
        new Dictionary<string, ContentImportInputMode>(StringComparer.OrdinalIgnoreCase)
        {
            ["pasted_text"] = ContentImportInputMode.PastedText,
            ["csv_text"] = ContentImportInputMode.CsvText,
            ["json_text"] = ContentImportInputMode.JsonText,
        };

    private readonly IContentImportService _contentImportService;

    public AdminContentImportController(IContentImportService contentImportService)
    {
        _contentImportService = contentImportService;
    }

    // POST api/admin/content-imports
    // { sourceName, resourceType, inputMode, content, defaultCefrLevel?, defaultSkill?,
    //   defaultSubskill?, defaultContextTags?, defaultFocusTags?, defaultDifficultyBand?, notes? }
    //
    // resourceType: "vocabulary" | "grammar" | "reading" | "writing" (Phase J5a) | "listening"
    // (Phase J5c — staged here as title/transcript text; the actual audio file is uploaded
    // separately per-candidate, see AdminResourceCandidateController's audio endpoints) |
    // "mixed" (Phase J5b — no forced type, each row is classified independently by its own
    // fields) — Speaking is not yet modeled by ResourceCandidateType and is rejected here (Coming
    // soon in the UI); see docs/architecture/product-model-realignment-h0.md for the H2 scope
    // decision and the J5 roadmap entries for the phased type expansion.
    // inputMode: "pasted_text" | "csv_text" | "json_text" — file upload already exists as its own
    // flow (POST api/admin/resource-import-runs) and is out of scope for this endpoint.
    [HttpPost]
    public async Task<IActionResult> Import([FromBody] ContentImportRequestBody body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.SourceName))
            return BadRequest(new { error = "Source name is required." });

        if (!SupportedResourceTypes.TryGetValue(body.ResourceType ?? string.Empty, out var resourceType))
            return BadRequest(new { error = $"Unsupported or not-yet-implemented resource type '{body.ResourceType}'. Use vocabulary, grammar, reading, writing, listening, or mixed." });

        if (!SupportedInputModes.TryGetValue(body.InputMode ?? string.Empty, out var inputMode))
            return BadRequest(new { error = $"Unsupported input mode '{body.InputMode}'. Use pasted_text, csv_text, or json_text." });

        if (string.IsNullOrWhiteSpace(body.Content))
            return BadRequest(new { error = "Content is required." });

        if (!string.IsNullOrWhiteSpace(body.DefaultCefrLevel) && !CefrLevelConstants.IsValid(body.DefaultCefrLevel))
            return BadRequest(new { error = $"Default CEFR level '{body.DefaultCefrLevel}' is not a valid CEFR level." });

        if (body.DefaultDifficultyBand is < 1 or > 5)
            return BadRequest(new { error = "Default difficulty band must be between 1 and 5." });

        try
        {
            var result = await _contentImportService.ImportContentAsync(new ContentImportRequest(
                SourceName: body.SourceName.Trim(),
                ResourceType: resourceType,
                InputMode: inputMode,
                Content: body.Content,
                DefaultCefrLevel: body.DefaultCefrLevel,
                DefaultSkill: body.DefaultSkill,
                DefaultSubskill: body.DefaultSubskill,
                DefaultContextTags: body.DefaultContextTags,
                DefaultFocusTags: body.DefaultFocusTags,
                DefaultDifficultyBand: body.DefaultDifficultyBand,
                Notes: body.Notes,
                ImportedByUserId: GetCurrentUserId()), ct);

            return Ok(result);
        }
        catch (ResourceImportValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private Guid? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    public sealed record ContentImportRequestBody(
        string SourceName,
        string ResourceType,
        string InputMode,
        string Content,
        string? DefaultCefrLevel = null,
        string? DefaultSkill = null,
        string? DefaultSubskill = null,
        IReadOnlyList<string>? DefaultContextTags = null,
        IReadOnlyList<string>? DefaultFocusTags = null,
        int? DefaultDifficultyBand = null,
        string? Notes = null
    );
}

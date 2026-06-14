using System.Security.Claims;
using LinguaCoach.Application.Admin;
using LinguaCoach.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminController : ControllerBase
{
    private readonly ICreateStudentHandler _createStudentHandler;
    private readonly IAdminStudentQuery _studentQuery;
    private readonly IAdminPromptHandler _promptHandler;
    private readonly IAdminCurriculumHandler _curriculumHandler;
    private readonly IAdminAiConfigHandler _aiConfigHandler;
    private readonly LinguaCoach.Application.LearningPath.IStudentMemoryQuery _memoryQuery;

    public AdminController(
        ICreateStudentHandler createStudentHandler,
        IAdminStudentQuery studentQuery,
        IAdminPromptHandler promptHandler,
        IAdminCurriculumHandler curriculumHandler,
        IAdminAiConfigHandler aiConfigHandler,
        LinguaCoach.Application.LearningPath.IStudentMemoryQuery memoryQuery)
    {
        _createStudentHandler = createStudentHandler;
        _studentQuery = studentQuery;
        _promptHandler = promptHandler;
        _curriculumHandler = curriculumHandler;
        _aiConfigHandler = aiConfigHandler;
        _memoryQuery = memoryQuery;
    }

    // ── Students ──────────────────────────────────────────────────────────────

    [HttpPost("students")]
    public async Task<IActionResult> CreateStudent([FromBody] CreateStudentRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _createStudentHandler.HandleAsync(
                new CreateStudentCommand(
                    request.Email,
                    request.TemporaryPassword,
                    request.MustChangePassword,
                    request.FirstName,
                    request.LastName,
                    request.DisplayName,
                    request.CareerContext,
                    request.LearningGoal,
                    request.PreferredSessionDurationMinutes,
                    request.ProfessionalExperienceLevel,
                    request.RoleFamiliarity), ct);
            return Created($"/api/admin/students/{result.StudentProfileId}",
                new { studentProfileId = result.StudentProfileId, userId = result.UserId });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
        {
            return Conflict(new { error = ex.Message });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("students")]
    public async Task<IActionResult> ListStudents([FromQuery] bool includeArchived, CancellationToken ct)
        => Ok(await _studentQuery.ListStudentsAsync(includeArchived, ct));

    [HttpPut("students/{studentId:guid}")]
    public async Task<IActionResult> UpdateStudent(Guid studentId, [FromBody] UpdateStudentProfileRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _studentQuery.UpdateStudentAsync(
                new UpdateStudentProfileCommand(
                    studentId,
                    request.FirstName,
                    request.LastName,
                    request.DisplayName,
                    request.CareerContext,
                    request.LearningGoal,
                    request.LearningGoalDescription,
                    request.DifficultSituationsText,
                    request.PreferredSessionDurationMinutes,
                    request.ProfessionalExperienceLevel,
                    request.RoleFamiliarity),
                ct));
        }
        catch (InvalidOperationException ex) { return NotFound(new { error = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("students/{studentId:guid}/archive")]
    public async Task<IActionResult> ArchiveStudent(Guid studentId, CancellationToken ct)
    {
        try
        {
            return Ok(await _studentQuery.ArchiveStudentAsync(new ArchiveStudentCommand(studentId), ct));
        }
        catch (InvalidOperationException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpPost("students/{studentId:guid}/reset-password")]
    public async Task<IActionResult> ResetStudentPassword(Guid studentId, [FromBody] ResetStudentPasswordRequest request, CancellationToken ct)
    {
        try
        {
            await _studentQuery.ResetStudentPasswordAsync(
                new ResetStudentPasswordCommand(studentId, request.NewPassword, request.MustChangePassword), ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("students/{studentId:guid}/reset")]
    public async Task<IActionResult> ResetStudent(Guid studentId, [FromBody] ResetStudentRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { error = "Reason is required." });

        var adminId = GetCurrentUserId();
        if (adminId == Guid.Empty)
            return Unauthorized();

        var recentResetCount = await _studentQuery.CountRecentResetsAsync(adminId, TimeSpan.FromHours(1), ct);
        if (recentResetCount >= 10)
            return StatusCode(429, new { error = "Rate limit exceeded: max 10 resets per admin per hour." });

        try
        {
            var result = await _studentQuery.ResetStudentAsync(
                new ResetStudentCommand(
                    studentId,
                    adminId,
                    request.TargetStage,
                    request.ClearOnboardingAnswers,
                    request.ClearPlacementResults,
                    request.ClearCoursesAndSessions,
                    request.ClearActivityAttempts,
                    request.ClearVocabulary,
                    request.ClearLearningMemory,
                    request.ClearAudioFiles,
                    request.ClearProgressData,
                    request.Reason,
                    Guid.NewGuid().ToString()),
                ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) { return NotFound(new { error = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("students/{studentId:guid}/learning-memory")]
    public async Task<IActionResult> GetStudentMemory(Guid studentId, CancellationToken ct)
    {
        try
        {
            return Ok(await _memoryQuery.GetForStudentProfileAsync(studentId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ── Prompt templates ──────────────────────────────────────────────────────

    [HttpGet("prompts")]
    public async Task<IActionResult> ListPrompts(CancellationToken ct)
        => Ok(await _promptHandler.ListPromptsAsync(ct));

    [HttpGet("prompts/{promptId:guid}")]
    public async Task<IActionResult> GetPrompt(Guid promptId, CancellationToken ct)
    {
        try { return Ok(await _promptHandler.GetPromptAsync(promptId, ct)); }
        catch (InvalidOperationException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpPost("prompts")]
    public async Task<IActionResult> CreatePromptVersion([FromBody] CreatePromptVersionRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _promptHandler.CreateVersionAsync(
                new CreatePromptVersionCommand(request.Key, request.Content, request.MaxInputTokens, request.MaxOutputTokens), ct);
            return Created($"/api/admin/prompts/{result.Id}", result);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("prompts/{promptId:guid}/activate")]
    public async Task<IActionResult> ActivatePrompt(Guid promptId, CancellationToken ct)
    {
        try { await _promptHandler.ActivateAsync(new ActivatePromptCommand(promptId), ct); return NoContent(); }
        catch (InvalidOperationException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpPost("prompts/{promptId:guid}/deactivate")]
    public async Task<IActionResult> DeactivatePrompt(Guid promptId, CancellationToken ct)
    {
        try { await _promptHandler.DeactivateAsync(new DeactivatePromptCommand(promptId), ct); return NoContent(); }
        catch (InvalidOperationException ex) { return NotFound(new { error = ex.Message }); }
    }

    // ── Career profiles + curriculum words ───────────────────────────────────

    [HttpGet("careers")]
    public async Task<IActionResult> ListCareers(CancellationToken ct)
        => Ok(await _curriculumHandler.ListCareerProfilesAsync(ct));

    [HttpGet("careers/{careerId:guid}/words")]
    public async Task<IActionResult> ListWords(Guid careerId, [FromQuery] Guid languagePairId, CancellationToken ct)
        => Ok(await _curriculumHandler.ListWordsAsync(careerId, languagePairId, ct));

    [HttpPost("careers/{careerId:guid}/words")]
    public async Task<IActionResult> AddWord(Guid careerId, [FromBody] AddWordRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _curriculumHandler.AddWordAsync(
                new AddCurriculumWordCommand(careerId, request.LanguagePairId, request.Word,
                    request.Definition, request.ExampleSentence, request.Priority, request.Tags ?? ""), ct);
            return Created($"/api/admin/careers/{careerId}/words/{result.Id}", result);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("careers/words/{wordId:guid}")]
    public async Task<IActionResult> UpdateWord(Guid wordId, [FromBody] UpdateWordRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _curriculumHandler.UpdateWordAsync(
                new UpdateCurriculumWordCommand(wordId, request.Definition,
                    request.ExampleSentence, request.Priority, request.Tags ?? ""), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) { return NotFound(new { error = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ── AI provider config ────────────────────────────────────────────────────

    // ── AI feature routing (which provider+model handles each feature) ──────────

    // ── AI provider credentials (one key per provider, shared across features) ──

    [HttpGet("ai-providers")]
    public async Task<IActionResult> ListAiProviders(CancellationToken ct)
        => Ok(await _aiConfigHandler.ListProvidersAsync(ct));

    [HttpPut("ai-providers/{provider}/api-key")]
    public async Task<IActionResult> SetProviderApiKey(string provider, [FromBody] SetProviderApiKeyRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _aiConfigHandler.SetProviderApiKeyAsync(
                new SetProviderApiKeyCommand(provider, request.ApiKey), ct);
            return Ok(result);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("ai-providers/{provider}/endpoint")]
    public async Task<IActionResult> SetProviderEndpoint(string provider, [FromBody] SetProviderEndpointRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _aiConfigHandler.SetProviderEndpointAsync(
                new SetProviderEndpointCommand(provider, request.ApiEndpoint), ct);
            return Ok(result);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("ai-providers/{provider}/models")]
    public async Task<IActionResult> AddProviderModel(string provider, [FromBody] AddProviderModelRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _aiConfigHandler.AddProviderModelAsync(
                new AddProviderModelCommand(provider, request.ModelName), ct);
            return Ok(result);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("ai-providers/{provider}/test")]
    public async Task<IActionResult> TestProvider(string provider, CancellationToken ct)
    {
        try
        {
            var result = await _aiConfigHandler.TestProviderAsync(provider, ct);
            return Ok(result);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    [HttpPost("ai-providers/{provider}/models/test")]
    public async Task<IActionResult> TestProviderModel(string provider, [FromBody] TestProviderModelRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _aiConfigHandler.TestProviderModelAsync(provider, request.ModelName, ct);
            return Ok(result);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    // ── AI config categories (category-level provider config) ─────────────────

    [HttpGet("ai/categories")]
    public async Task<IActionResult> ListAiCategories(CancellationToken ct)
        => Ok(await _aiConfigHandler.ListCategoriesAsync(ct));

    [HttpPatch("ai/categories/{categoryKey}")]
    public async Task<IActionResult> UpdateAiCategory(string categoryKey, [FromBody] UpdateAiCategoryRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _aiConfigHandler.UpdateCategoryAsync(
                new UpdateAiConfigCategoryCommand(categoryKey, request.ProviderName, request.ModelName, request.VoiceName), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) { return NotFound(new { error = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("ai/categories/{categoryKey}/test")]
    public async Task<IActionResult> TestAiCategory(string categoryKey, CancellationToken ct)
    {
        try
        {
            var result = await _aiConfigHandler.TestCategoryAsync(categoryKey, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) { return NotFound(new { error = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    private Guid GetCurrentUserId()
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;
}

public sealed record CreateStudentRequest(
    string Email,
    string TemporaryPassword,
    bool MustChangePassword = true,
    string? FirstName = null,
    string? LastName = null,
    string? DisplayName = null,
    string? CareerContext = null,
    string? LearningGoal = null,
    int? PreferredSessionDurationMinutes = null,
    LinguaCoach.Domain.Enums.ProfessionalExperienceLevel? ProfessionalExperienceLevel = null,
    LinguaCoach.Domain.Enums.RoleFamiliarity? RoleFamiliarity = null);
public sealed record UpdateStudentProfileRequest(
    string? FirstName = null,
    string? LastName = null,
    string? DisplayName = null,
    string? CareerContext = null,
    string? LearningGoal = null,
    string? LearningGoalDescription = null,
    string? DifficultSituationsText = null,
    int? PreferredSessionDurationMinutes = null,
    LinguaCoach.Domain.Enums.ProfessionalExperienceLevel? ProfessionalExperienceLevel = null,
    LinguaCoach.Domain.Enums.RoleFamiliarity? RoleFamiliarity = null);
public sealed record ResetStudentPasswordRequest(string NewPassword, bool MustChangePassword = true);
public sealed record ResetStudentRequest(
    StudentLifecycleStage TargetStage,
    bool ClearOnboardingAnswers,
    bool ClearPlacementResults,
    bool ClearCoursesAndSessions,
    bool ClearActivityAttempts,
    bool ClearVocabulary,
    bool ClearLearningMemory,
    bool ClearAudioFiles,
    bool ClearProgressData,
    string Reason);
public sealed record CreatePromptVersionRequest(string Key, string Content, int? MaxInputTokens, int? MaxOutputTokens);
public sealed record AddWordRequest(Guid LanguagePairId, string Word, string Definition, string ExampleSentence, int Priority, string? Tags);
public sealed record UpdateWordRequest(string Definition, string ExampleSentence, int Priority, string? Tags);
public sealed record SetProviderApiKeyRequest(string? ApiKey);
public sealed record SetProviderEndpointRequest(string? ApiEndpoint);
public sealed record AddProviderModelRequest(string ModelName);
public sealed record TestProviderModelRequest(string ModelName);
public sealed record UpdateAiCategoryRequest(string? ProviderName, string? ModelName, string? VoiceName = null);

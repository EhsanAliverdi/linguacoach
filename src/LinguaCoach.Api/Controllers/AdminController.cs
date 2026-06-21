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
    private readonly IExerciseTypeCatalogService _exerciseTypes;
    private readonly LinguaCoach.Application.LearningPath.IStudentMemoryQuery _memoryQuery;

    public AdminController(
        ICreateStudentHandler createStudentHandler,
        IAdminStudentQuery studentQuery,
        IAdminPromptHandler promptHandler,
        IAdminCurriculumHandler curriculumHandler,
        IAdminAiConfigHandler aiConfigHandler,
        IExerciseTypeCatalogService exerciseTypes,
        LinguaCoach.Application.LearningPath.IStudentMemoryQuery memoryQuery)
    {
        _createStudentHandler = createStudentHandler;
        _studentQuery = studentQuery;
        _promptHandler = promptHandler;
        _curriculumHandler = curriculumHandler;
        _aiConfigHandler = aiConfigHandler;
        _exerciseTypes = exerciseTypes;
        _memoryQuery = memoryQuery;
    }


    // ── Exercise type catalog ────────────────────────────────────────────────

    [HttpGet("exercise-types")]
    public async Task<IActionResult> ListExerciseTypes(CancellationToken ct)
        => Ok(await _exerciseTypes.ListAllAsync(ct));

    [HttpPatch("exercise-types/{key}")]
    public async Task<IActionResult> UpdateExerciseType(string key, [FromBody] UpdateExerciseTypeRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _exerciseTypes.UpdateAsync(
                new UpdateExerciseTypeDefinitionCommand(
                    key,
                    request.IsEnabled,
                    request.SupportsPracticeGym,
                    request.SupportsTodayLesson,
                    request.MinItemsPerPractice,
                    request.DefaultItemsPerPractice,
                    request.MaxItemsPerPractice,
                    request.MinOptionsPerItem,
                    request.DefaultOptionsPerItem,
                    request.MaxOptionsPerItem),
                ct));
        }
        catch (InvalidOperationException ex) { return NotFound(new { error = ex.Message }); }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException) { return BadRequest(new { error = ex.Message }); }
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
    public async Task<IActionResult> ListStudents(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] bool includeArchived = false,
        [FromQuery] string? lifecycleStage = null,
        [FromQuery] string? onboardingStatus = null,
        [FromQuery] string? cefrLevel = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = null,
        CancellationToken ct = default)
    {
        var query = new StudentListQuery(
            Math.Max(1, page),
            Math.Clamp(pageSize, 1, 100),
            search,
            includeArchived,
            lifecycleStage,
            onboardingStatus,
            cefrLevel,
            sortBy,
            sortDir);
        return Ok(await _studentQuery.ListStudentsPagedAsync(query, ct));
    }

    [HttpGet("students/{studentId:guid}")]
    public async Task<IActionResult> GetStudentDetail(Guid studentId, CancellationToken ct)
    {
        var detail = await _studentQuery.GetStudentDetailAsync(studentId, ct);
        return detail is null ? NotFound(new { error = "Student not found." }) : Ok(detail);
    }

    [HttpGet("students/{studentId:guid}/audit-history")]
    public async Task<IActionResult> GetStudentAuditHistory(Guid studentId, CancellationToken ct)
    {
        var history = await _studentQuery.GetStudentAuditHistoryAsync(studentId, ct);
        return history is null ? NotFound(new { error = "Student not found." }) : Ok(history);
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
        => Ok(await _studentQuery.GetStatsAsync(ct));

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

    [HttpPost("students/{studentId:guid}/reactivate")]
    public async Task<IActionResult> ReactivateStudent(Guid studentId, CancellationToken ct)
    {
        var adminId = GetCurrentUserId();
        try
        {
            return Ok(await _studentQuery.ReactivateStudentAsync(new ReactivateStudentCommand(studentId, adminId), ct));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found")) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("students/{studentId:guid}/pause")]
    public async Task<IActionResult> PauseStudent(Guid studentId, CancellationToken ct)
    {
        var adminId = GetCurrentUserId();
        try
        {
            return Ok(await _studentQuery.PauseStudentAsync(new PauseStudentCommand(studentId, adminId), ct));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found")) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("students/{studentId:guid}/unpause")]
    public async Task<IActionResult> UnpauseStudent(Guid studentId, CancellationToken ct)
    {
        var adminId = GetCurrentUserId();
        try
        {
            return Ok(await _studentQuery.UnpauseStudentAsync(new UnpauseStudentCommand(studentId, adminId), ct));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found")) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
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

    [HttpPut("students/{studentId:guid}/cefr")]
    public async Task<IActionResult> SetStudentCefr(Guid studentId, [FromBody] SetStudentCefrRequest request, CancellationToken ct)
    {
        var adminId = GetCurrentUserId();
        try
        {
            await _studentQuery.SetStudentCefrAsync(
                new SetStudentCefrCommand(studentId, adminId, request.CefrLevel, request.Reason), ct);
            return Ok();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found")) { return NotFound(new { error = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
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

    [HttpGet("students/{studentId:guid}/activity-history")]
    public async Task<IActionResult> GetActivityHistory(Guid studentId, CancellationToken ct)
        => Ok(await _studentQuery.GetActivityHistoryAsync(studentId, ct));

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

    [HttpGet("ai/pricing")]
    public IActionResult ListAiPricing()
        => Ok(_aiConfigHandler.ListPricing());

    [HttpGet("ai/pricing/overrides")]
    public async Task<IActionResult> ListPricingOverrides(CancellationToken ct)
        => Ok(await _aiConfigHandler.ListPricingOverridesAsync(ct));

    [HttpPost("ai/pricing/overrides")]
    public async Task<IActionResult> CreatePricingOverride([FromBody] CreatePricingOverrideRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _aiConfigHandler.CreatePricingOverrideAsync(
                new CreatePricingOverrideCommand(
                    request.ProviderName, request.ModelName,
                    request.InputPricePer1KTokens, request.OutputPricePer1KTokens,
                    request.Currency ?? "USD",
                    request.EffectiveFromUtc ?? DateTime.UtcNow,
                    request.EffectiveToUtc,
                    request.Notes,
                    GetCurrentUserId()), ct);
            return Ok(result);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("ai/pricing/overrides/{id:guid}")]
    public async Task<IActionResult> UpdatePricingOverride(Guid id, [FromBody] UpdatePricingOverrideRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _aiConfigHandler.UpdatePricingOverrideAsync(
                new UpdatePricingOverrideCommand(
                    id,
                    request.InputPricePer1KTokens, request.OutputPricePer1KTokens,
                    request.Currency ?? "USD",
                    request.EffectiveFromUtc ?? DateTime.UtcNow,
                    request.EffectiveToUtc,
                    request.Notes,
                    GetCurrentUserId()), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) { return NotFound(new { error = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("ai/pricing/overrides/{id:guid}")]
    public async Task<IActionResult> DeactivatePricingOverride(Guid id, CancellationToken ct)
    {
        try
        {
            await _aiConfigHandler.DeactivatePricingOverrideAsync(
                new DeactivatePricingOverrideCommand(id, GetCurrentUserId()), ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return NotFound(new { error = ex.Message }); }
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
public sealed record SetStudentCefrRequest(string? CefrLevel, string? Reason = null);
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
public sealed record CreatePricingOverrideRequest(
    string ProviderName,
    string ModelName,
    decimal InputPricePer1KTokens,
    decimal OutputPricePer1KTokens,
    string? Currency = "USD",
    DateTime? EffectiveFromUtc = null,
    DateTime? EffectiveToUtc = null,
    string? Notes = null);
public sealed record UpdatePricingOverrideRequest(
    decimal InputPricePer1KTokens,
    decimal OutputPricePer1KTokens,
    string? Currency = "USD",
    DateTime? EffectiveFromUtc = null,
    DateTime? EffectiveToUtc = null,
    string? Notes = null);

public sealed record UpdateExerciseTypeRequest(
    bool? IsEnabled,
    bool? SupportsPracticeGym,
    bool? SupportsTodayLesson,
    int? MinItemsPerPractice = null,
    int? DefaultItemsPerPractice = null,
    int? MaxItemsPerPractice = null,
    int? MinOptionsPerItem = null,
    int? DefaultOptionsPerItem = null,
    int? MaxOptionsPerItem = null);

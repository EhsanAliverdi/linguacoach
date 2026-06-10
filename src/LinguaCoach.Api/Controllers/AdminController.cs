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

    [HttpGet("ai-config")]
    public async Task<IActionResult> ListAiConfigs(CancellationToken ct)
        => Ok(await _aiConfigHandler.ListConfigsAsync(ct));

    [HttpPut("ai-config/{configId:guid}")]
    public async Task<IActionResult> UpdateAiConfig(Guid configId, [FromBody] UpdateAiConfigRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _aiConfigHandler.UpdateConfigAsync(
                new UpdateAiProviderConfigCommand(
                    configId,
                    request.ProviderName,
                    request.ModelName,
                    request.VoiceName,
                    ClearVoiceName: request.ClearVoiceName,
                    request.FallbackProviderName,
                    request.FallbackModelName,
                    request.FallbackEnabled),
                ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) { return NotFound(new { error = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

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
public sealed record CreatePromptVersionRequest(string Key, string Content, int? MaxInputTokens, int? MaxOutputTokens);
public sealed record AddWordRequest(Guid LanguagePairId, string Word, string Definition, string ExampleSentence, int Priority, string? Tags);
public sealed record UpdateWordRequest(string Definition, string ExampleSentence, int Priority, string? Tags);
public sealed record UpdateAiConfigRequest(
    string? ProviderName,
    string? ModelName,
    string? VoiceName = null,
    bool ClearVoiceName = false,
    string? FallbackProviderName = null,
    string? FallbackModelName = null,
    bool? FallbackEnabled = null);
public sealed record SetProviderApiKeyRequest(string? ApiKey);

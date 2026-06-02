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

    public AdminController(
        ICreateStudentHandler createStudentHandler,
        IAdminStudentQuery studentQuery,
        IAdminPromptHandler promptHandler,
        IAdminCurriculumHandler curriculumHandler,
        IAdminAiConfigHandler aiConfigHandler)
    {
        _createStudentHandler = createStudentHandler;
        _studentQuery = studentQuery;
        _promptHandler = promptHandler;
        _curriculumHandler = curriculumHandler;
        _aiConfigHandler = aiConfigHandler;
    }

    // ── Students ──────────────────────────────────────────────────────────────

    [HttpPost("students")]
    public async Task<IActionResult> CreateStudent([FromBody] CreateStudentRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _createStudentHandler.HandleAsync(
                new CreateStudentCommand(request.Email, request.TemporaryPassword), ct);
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
    public async Task<IActionResult> ListStudents(CancellationToken ct)
        => Ok(await _studentQuery.ListStudentsAsync(ct));

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

    [HttpGet("ai-config/providers")]
    public async Task<IActionResult> ListAiProviders(CancellationToken ct)
        => Ok(await _aiConfigHandler.ListProvidersAsync(ct));

    [HttpGet("ai-config")]
    public async Task<IActionResult> ListAiConfigs(CancellationToken ct)
        => Ok(await _aiConfigHandler.ListConfigsAsync(ct));

    [HttpPut("ai-config/{configId:guid}")]
    public async Task<IActionResult> UpdateAiConfig(Guid configId, [FromBody] UpdateAiConfigRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _aiConfigHandler.UpdateConfigAsync(
                new UpdateAiProviderConfigCommand(configId, request.ProviderName, request.ModelName), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) { return NotFound(new { error = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }
}

public sealed record CreateStudentRequest(string Email, string TemporaryPassword);
public sealed record CreatePromptVersionRequest(string Key, string Content, int? MaxInputTokens, int? MaxOutputTokens);
public sealed record AddWordRequest(Guid LanguagePairId, string Word, string Definition, string ExampleSentence, int Priority, string? Tags);
public sealed record UpdateWordRequest(string Definition, string ExampleSentence, int Priority, string? Tags);
public sealed record UpdateAiConfigRequest(string ProviderName, string ModelName);

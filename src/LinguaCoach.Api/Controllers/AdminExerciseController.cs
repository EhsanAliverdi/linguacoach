using System.Security.Claims;
using System.Text.Json;
using LinguaCoach.Application.Exercises;
using LinguaCoach.Application.Modules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Phase H4 — Activity foundation. Reviewable, editable practice task designs generated from (or
/// manually authored about) selected published Resource Bank rows, optionally linked to a Learn
/// Item — the "Practice" half of a future Module. Distinct from the existing runtime
/// <c>LearningActivity</c> (per-student delivery record) — see <c>Exercise</c>'s doc
/// comment. The legacy <c>ActivityTemplate</c> Form.io pilot this used to be contrasted against
/// was removed in Phase I2A; see
/// docs/reviews/2026-07-10-phase-i2a-practice-gym-legacy-deletion-review.md.
/// Every create/generate action stages a pending-review row; only <see cref="Approve"/>/
/// <see cref="Reject"/> change that. Never creates a Module row, never assigns anything to a
/// student, never changes Today/Practice Gym runtime selection.
/// </summary>
[ApiController]
[Route("api/admin/exercises")]
[Authorize(Roles = "Admin")]
public sealed class AdminExerciseController : ControllerBase
{
    private readonly IAdminExerciseListQuery _listQuery;
    private readonly IAdminExerciseGetQuery _getQuery;
    private readonly IAdminCreateExerciseHandler _createHandler;
    private readonly IAdminUpdateExerciseHandler _updateHandler;
    private readonly IAdminApproveExerciseHandler _approveHandler;
    private readonly IAdminRejectExerciseHandler _rejectHandler;
    private readonly IGenerateActivityFromResourcesHandler _generateFromResourcesHandler;
    private readonly IGenerateActivityFromLessonHandler _generateFromLessonHandler;
    private readonly IGenerateActivityFromResourcesWithAiHandler _generateFromResourcesWithAiHandler;
    private readonly IGenerateActivitiesFromLessonHandler _generateActivitiesFromLessonHandler;
    private readonly IExerciseArchiveHandler _archiveHandler;
    private readonly IAdminExercisePreviewSubmitHandler _previewSubmitHandler;
    private readonly IExerciseRepairService _repairService;

    public AdminExerciseController(
        IAdminExerciseListQuery listQuery,
        IAdminExerciseGetQuery getQuery,
        IAdminCreateExerciseHandler createHandler,
        IAdminUpdateExerciseHandler updateHandler,
        IAdminApproveExerciseHandler approveHandler,
        IAdminRejectExerciseHandler rejectHandler,
        IGenerateActivityFromResourcesHandler generateFromResourcesHandler,
        IGenerateActivityFromLessonHandler generateFromLessonHandler,
        IGenerateActivityFromResourcesWithAiHandler generateFromResourcesWithAiHandler,
        IGenerateActivitiesFromLessonHandler generateActivitiesFromLessonHandler,
        IExerciseArchiveHandler archiveHandler,
        IAdminExercisePreviewSubmitHandler previewSubmitHandler,
        IExerciseRepairService repairService)
    {
        _listQuery = listQuery;
        _getQuery = getQuery;
        _createHandler = createHandler;
        _updateHandler = updateHandler;
        _approveHandler = approveHandler;
        _rejectHandler = rejectHandler;
        _generateFromResourcesHandler = generateFromResourcesHandler;
        _generateFromLessonHandler = generateFromLessonHandler;
        _generateFromResourcesWithAiHandler = generateFromResourcesWithAiHandler;
        _generateActivitiesFromLessonHandler = generateActivitiesFromLessonHandler;
        _archiveHandler = archiveHandler;
        _previewSubmitHandler = previewSubmitHandler;
        _repairService = repairService;
    }

    // GET api/admin/exercises?page=&pageSize=&status=&activityType=&rendererType=&cefrLevel=&
    //   skill=&subskill=&contextTag=&focusTag=&difficultyBand=&lessonId=&search=
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? status = null,
        [FromQuery] string? activityType = null, [FromQuery] string? rendererType = null,
        [FromQuery] string? cefrLevel = null, [FromQuery] string? skill = null, [FromQuery] string? subskill = null,
        [FromQuery] string? contextTag = null, [FromQuery] string? focusTag = null,
        [FromQuery] int? difficultyBand = null, [FromQuery] Guid? lessonId = null, [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await _listQuery.HandleAsync(new ListExercisesQuery(
            page, pageSize, status, activityType, rendererType, cefrLevel, skill, subskill,
            contextTag, focusTag, difficultyBand, lessonId, search), ct);
        return Ok(result);
    }

    // GET api/admin/exercises/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await _getQuery.HandleAsync(new GetExerciseQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    // POST api/admin/exercises
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateExerciseRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _createHandler.HandleAsync(new CreateExerciseCommand(
                body.Title, body.Instructions, body.ActivityType, body.RendererType, body.Description, body.PatternKey,
                body.FormSchemaJson, body.AnswerKeyJson, body.ScoringRulesJson, body.FeedbackPlanJson,
                body.CefrLevel, body.Skill, body.Subskill, body.ContextTags, body.FocusTags,
                body.DifficultyBand, body.EstimatedMinutes, body.LessonId, body.Links, GetCurrentUserId()), ct);
            return Ok(result);
        }
        catch (ExerciseValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/exercises/generate-from-resources
    [HttpPost("generate-from-resources")]
    public async Task<IActionResult> GenerateFromResources(
        [FromBody] GenerateActivityFromResourcesRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _generateFromResourcesHandler.HandleAsync(new GenerateActivityFromResourcesRequest(
                body.Resources, body.RequestedActivityType, body.Title, body.DefaultCefrLevel, body.DefaultSkill,
                body.DefaultSubskill, body.DefaultContextTags, body.DefaultFocusTags, body.DefaultDifficultyBand,
                body.Notes, GetCurrentUserId()), ct);
            return Ok(result);
        }
        catch (ExerciseValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/exercises/generate-from-lesson
    [HttpPost("generate-from-lesson")]
    public async Task<IActionResult> GenerateFromLesson(
        [FromBody] GenerateActivityFromLessonRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _generateFromLessonHandler.HandleAsync(new GenerateActivityFromLessonRequest(
                body.LessonId, body.RequestedActivityType, body.Title, body.Notes, GetCurrentUserId()), ct);
            return Ok(result);
        }
        catch (ExerciseValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/exercises/generate-from-lesson/batch  { lessonId, specs: [{activityType, count}], titlePrefix?, notes? }
    // Phase K5 — admin picks how many Exercises of which type(s) to generate from this Lesson
    // (e.g. 5 gap_fill + 5 multiple_choice_single = 10). Auto-creates-or-extends the Module
    // linking this Lesson to every Exercise it has generated — see
    // IModuleAutoLinkService — there is no separate "Generate Module" step anymore.
    [HttpPost("generate-from-lesson/batch")]
    public async Task<IActionResult> GenerateActivitiesFromLesson(
        [FromBody] GenerateActivitiesFromLessonRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _generateActivitiesFromLessonHandler.HandleAsync(new GenerateActivitiesFromLessonRequest(
                body.LessonId,
                body.Specs.Select(s => new ActivityGenerationSpec(s.ActivityType, s.Count)).ToList(),
                body.TitlePrefix, body.Notes, GetCurrentUserId()), ct);
            return Ok(result);
        }
        catch (ExerciseValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ModuleValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/exercises/generate-from-resources/ai
    // Phase J2b — AI-assisted alternative to the deterministic action above. A separate action:
    // the deterministic action is untouched and always available, regardless of AI availability.
    [HttpPost("generate-from-resources/ai")]
    public async Task<IActionResult> GenerateFromResourcesWithAi(
        [FromBody] GenerateActivityFromResourcesRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _generateFromResourcesWithAiHandler.HandleAsync(new GenerateActivityFromResourcesRequest(
                body.Resources, body.RequestedActivityType, body.Title, body.DefaultCefrLevel, body.DefaultSkill,
                body.DefaultSubskill, body.DefaultContextTags, body.DefaultFocusTags, body.DefaultDifficultyBand,
                body.Notes, GetCurrentUserId()), ct);
            return Ok(result);
        }
        catch (ExerciseValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // PUT api/admin/exercises/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateExerciseRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _updateHandler.HandleAsync(new UpdateExerciseCommand(
                id, body.Title, body.Instructions, body.Description, body.FormSchemaJson, body.AnswerKeyJson,
                body.ScoringRulesJson, body.FeedbackPlanJson, body.CefrLevel, body.Skill, body.Subskill,
                body.ContextTags, body.FocusTags, body.DifficultyBand, body.EstimatedMinutes), ct);
            return Ok(result);
        }
        catch (ExerciseValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/exercises/{id}/approve  { notes? }
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveExerciseRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _approveHandler.HandleAsync(new ApproveExerciseCommand(id, GetCurrentUserId(), body.Notes), ct);
            return Ok(result);
        }
        catch (ExerciseValidationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // POST api/admin/exercises/{id}/reject  { reason }
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectExerciseRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _rejectHandler.HandleAsync(new RejectExerciseCommand(id, body.Reason, GetCurrentUserId()), ct);
            return Ok(result);
        }
        catch (ExerciseValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/exercises/{id}/preview/submit  { answers: { [componentKey]: value } }
    // Phase K7 — scores a preview submission for a standalone Exercise using the same scoring
    // engine the real student runtime / Module preview uses. Never creates a LearningActivity/
    // ActivityAttempt — read/score-only.
    [HttpPost("{id:guid}/preview/submit")]
    public async Task<IActionResult> PreviewSubmit(Guid id, [FromBody] ExercisePreviewSubmitRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _previewSubmitHandler.HandleAsync(
                new ExercisePreviewSubmitRequest(id, body.Answers ?? new Dictionary<string, JsonElement>()), ct);
            return Ok(result);
        }
        catch (ExerciseValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // GET api/admin/exercises/{id}/diagnostics
    // Phase K8 — issues found on this Exercise (e.g. missing Instructions, or correctness-critical
    // gaps like a missing schema/scoring rules that are flagged but never auto-fixed).
    [HttpGet("{id:guid}/diagnostics")]
    public async Task<IActionResult> Diagnose(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _repairService.DiagnoseAsync(id, ct);
            return Ok(result);
        }
        catch (ExerciseValidationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // POST api/admin/exercises/{id}/repair
    // Phase K8 — AI-fills missing Instructions/Description only (see ExerciseRepairService).
    [HttpPost("{id:guid}/repair")]
    public async Task<IActionResult> Repair(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _repairService.RepairAsync(id, ct);
            return Ok(result);
        }
        catch (ExerciseValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/exercises/archive  { ids: [...] }
    // Phase K6 — soft-delete: hides the row(s) from the default list without breaking any
    // Module that already references them.
    [HttpPost("archive")]
    public async Task<IActionResult> Archive([FromBody] ExerciseIdsRequest request, CancellationToken ct)
    {
        var result = await _archiveHandler.ArchiveAsync(new ArchiveExercisesCommand(request.Ids), ct);
        return Ok(result);
    }

    // POST api/admin/exercises/unarchive  { ids: [...] }
    [HttpPost("unarchive")]
    public async Task<IActionResult> Unarchive([FromBody] ExerciseIdsRequest request, CancellationToken ct)
    {
        var result = await _archiveHandler.UnarchiveAsync(new UnarchiveExercisesCommand(request.Ids), ct);
        return Ok(result);
    }

    private Guid? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    // GET api/admin/exercises/issues-summary
    [HttpGet("issues-summary")]
    public async Task<IActionResult> IssuesSummary(CancellationToken ct)
    {
        var result = await _repairService.GetIssuesSummaryAsync(ct);
        return Ok(result);
    }

    // GET api/admin/exercises/with-issues
    [HttpGet("with-issues")]
    public async Task<IActionResult> ListWithIssues(CancellationToken ct)
    {
        var result = await _repairService.ListWithIssuesAsync(ct);
        return Ok(result);
    }

    // POST api/admin/exercises/repair-all
    [HttpPost("repair-all")]
    public async Task<IActionResult> RepairAll(CancellationToken ct)
    {
        var result = await _repairService.RepairAllAsync(ct);
        return Ok(result);
    }

    public sealed record ExerciseIdsRequest(IReadOnlyList<Guid> Ids);

    public sealed record CreateExerciseRequestBody(
        string Title, string Instructions, string ActivityType, string RendererType,
        string? Description = null, string? PatternKey = null, string? FormSchemaJson = null,
        string? AnswerKeyJson = null, string? ScoringRulesJson = null, string? FeedbackPlanJson = null,
        string? CefrLevel = null, string? Skill = null, string? Subskill = null,
        IReadOnlyList<string>? ContextTags = null, IReadOnlyList<string>? FocusTags = null,
        int? DifficultyBand = null, int? EstimatedMinutes = null, Guid? LessonId = null,
        IReadOnlyList<ExerciseResourceLinkInput>? Links = null
    );

    public sealed record GenerateActivityFromResourcesRequestBody(
        IReadOnlyList<ExerciseResourceLinkInput> Resources,
        string? RequestedActivityType = null, string? Title = null, string? DefaultCefrLevel = null,
        string? DefaultSkill = null, string? DefaultSubskill = null,
        IReadOnlyList<string>? DefaultContextTags = null, IReadOnlyList<string>? DefaultFocusTags = null,
        int? DefaultDifficultyBand = null, string? Notes = null
    );

    public sealed record GenerateActivityFromLessonRequestBody(
        Guid LessonId, string? RequestedActivityType = null, string? Title = null, string? Notes = null
    );

    public sealed record ActivityGenerationSpecBody(string? ActivityType, int Count);

    public sealed record GenerateActivitiesFromLessonRequestBody(
        Guid LessonId, IReadOnlyList<ActivityGenerationSpecBody> Specs,
        string? TitlePrefix = null, string? Notes = null
    );

    public sealed record UpdateExerciseRequestBody(
        string Title, string Instructions, string? Description = null, string? FormSchemaJson = null,
        string? AnswerKeyJson = null, string? ScoringRulesJson = null, string? FeedbackPlanJson = null,
        string? CefrLevel = null, string? Skill = null, string? Subskill = null,
        IReadOnlyList<string>? ContextTags = null, IReadOnlyList<string>? FocusTags = null,
        int? DifficultyBand = null, int? EstimatedMinutes = null
    );

    public sealed record ApproveExerciseRequestBody(string? Notes = null);
    public sealed record RejectExerciseRequestBody(string Reason);
    public sealed record ExercisePreviewSubmitRequestBody(Dictionary<string, JsonElement>? Answers = null);
}

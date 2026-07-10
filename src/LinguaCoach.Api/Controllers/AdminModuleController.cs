using System.Security.Claims;
using System.Text.Json;
using LinguaCoach.Application.Modules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Phase H5 — Module foundation. Reusable, reviewable learning units combining one or
/// more Lessons and Exercises plus a module-level feedback plan — the top of the
/// content-studio hierarchy (Resource Bank Item → Lesson/Exercise → Module
/// Definition). Distinct from the existing runtime <c>LearningModule</c> (a per-student thematic
/// group within a LearningPath) — see <c>Module</c>'s doc comment. Every create/generate
/// action stages a pending-review row; only <see cref="Approve"/>/<see cref="Reject"/> change
/// that. Never assigns anything to a student, never changes Today/Practice Gym runtime selection.
/// </summary>
[ApiController]
[Route("api/admin/modules")]
[Authorize(Roles = "Admin")]
public sealed class AdminModuleController : ControllerBase
{
    private readonly IAdminModuleListQuery _listQuery;
    private readonly IAdminModuleGetQuery _getQuery;
    private readonly IAdminCreateModuleHandler _createHandler;
    private readonly IAdminUpdateModuleHandler _updateHandler;
    private readonly IAdminApproveModuleHandler _approveHandler;
    private readonly IAdminRejectModuleHandler _rejectHandler;
    private readonly IGenerateModuleFromItemsHandler _generateFromItemsHandler;
    private readonly IGenerateModuleFromResourceHandler _generateFromResourceHandler;
    private readonly IGenerateModuleFromLessonHandler _generateFromLessonHandler;
    private readonly IGenerateModuleFromExerciseHandler _generateFromExerciseHandler;
    private readonly IGenerateModuleFromResourceWithAiHandler _generateFromResourceWithAiHandler;
    private readonly IAdminModulePreviewQuery _previewQuery;
    private readonly IAdminModulePreviewSubmitHandler _previewSubmitHandler;

    public AdminModuleController(
        IAdminModuleListQuery listQuery,
        IAdminModuleGetQuery getQuery,
        IAdminCreateModuleHandler createHandler,
        IAdminUpdateModuleHandler updateHandler,
        IAdminApproveModuleHandler approveHandler,
        IAdminRejectModuleHandler rejectHandler,
        IGenerateModuleFromItemsHandler generateFromItemsHandler,
        IGenerateModuleFromResourceHandler generateFromResourceHandler,
        IGenerateModuleFromLessonHandler generateFromLessonHandler,
        IGenerateModuleFromExerciseHandler generateFromExerciseHandler,
        IGenerateModuleFromResourceWithAiHandler generateFromResourceWithAiHandler,
        IAdminModulePreviewQuery previewQuery,
        IAdminModulePreviewSubmitHandler previewSubmitHandler)
    {
        _listQuery = listQuery;
        _getQuery = getQuery;
        _createHandler = createHandler;
        _updateHandler = updateHandler;
        _approveHandler = approveHandler;
        _rejectHandler = rejectHandler;
        _generateFromItemsHandler = generateFromItemsHandler;
        _generateFromResourceHandler = generateFromResourceHandler;
        _generateFromLessonHandler = generateFromLessonHandler;
        _generateFromExerciseHandler = generateFromExerciseHandler;
        _generateFromResourceWithAiHandler = generateFromResourceWithAiHandler;
        _previewQuery = previewQuery;
        _previewSubmitHandler = previewSubmitHandler;
    }

    // GET api/admin/modules?page=&pageSize=&status=&cefrLevel=&skill=&subskill=&contextTag=&
    //   focusTag=&difficultyBand=&lessonId=&exerciseId=&search=
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? status = null,
        [FromQuery] string? cefrLevel = null, [FromQuery] string? skill = null, [FromQuery] string? subskill = null,
        [FromQuery] string? contextTag = null, [FromQuery] string? focusTag = null,
        [FromQuery] int? difficultyBand = null, [FromQuery] Guid? lessonId = null,
        [FromQuery] Guid? exerciseId = null, [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await _listQuery.HandleAsync(new ListModulesQuery(
            page, pageSize, status, cefrLevel, skill, subskill, contextTag, focusTag,
            difficultyBand, lessonId, exerciseId, search), ct);
        return Ok(result);
    }

    // GET api/admin/modules/{id}/preview
    // Phase J3 — admin "preview as a learner": loads this Module's linked Lesson + Exercise for
    // rendering regardless of the Module's own review status (preview happens before approval).
    // Never exposes AnswerKeyJson/ScoringRulesJson.
    [HttpGet("{id:guid}/preview")]
    public async Task<IActionResult> Preview(Guid id, CancellationToken ct)
    {
        var result = await _previewQuery.HandleAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    // POST api/admin/modules/{id}/preview/submit  { answers: { [componentKey]: value } }
    // Phase J3 — scores a preview submission using the same scoring engine the real student
    // runtime uses. Never creates a LearningActivity/ActivityAttempt — read/score-only.
    [HttpPost("{id:guid}/preview/submit")]
    public async Task<IActionResult> PreviewSubmit(Guid id, [FromBody] ModulePreviewSubmitRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _previewSubmitHandler.HandleAsync(
                new ModulePreviewSubmitRequest(id, body.Answers ?? new Dictionary<string, JsonElement>()), ct);
            return Ok(result);
        }
        catch (ModuleValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // GET api/admin/modules/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await _getQuery.HandleAsync(new GetModuleQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    // POST api/admin/modules
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateModuleRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _createHandler.HandleAsync(new CreateModuleCommand(
                body.Title, body.LessonLinks, body.ExerciseLinks, body.Description, body.ObjectiveKey,
                body.CefrLevel, body.Skill, body.Subskill, body.ContextTags, body.FocusTags,
                body.DifficultyBand, body.EstimatedMinutes, body.FeedbackPlanJson, GetCurrentUserId()), ct);
            return Ok(result);
        }
        catch (ModuleValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/modules/generate-from-items
    [HttpPost("generate-from-items")]
    public async Task<IActionResult> GenerateFromItems([FromBody] GenerateModuleFromItemsRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _generateFromItemsHandler.HandleAsync(new GenerateModuleFromItemsRequest(
                body.LessonLinks, body.ExerciseLinks, body.Title, body.Notes, GetCurrentUserId()), ct);
            return Ok(result);
        }
        catch (ModuleValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/modules/generate-from-resource
    [HttpPost("generate-from-resource")]
    public async Task<IActionResult> GenerateFromResource([FromBody] GenerateModuleFromResourceRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _generateFromResourceHandler.HandleAsync(new GenerateModuleFromResourceRequest(
                body.ResourceType, body.ResourceId, body.Title, body.Notes, GetCurrentUserId()), ct);
            return Ok(result);
        }
        catch (ModuleValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/modules/generate-from-resource/ai
    // Phase J2c — AI-assisted alternative to the deterministic action above. A separate action:
    // the deterministic action is untouched and always available, regardless of AI availability.
    [HttpPost("generate-from-resource/ai")]
    public async Task<IActionResult> GenerateFromResourceWithAi([FromBody] GenerateModuleFromResourceRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _generateFromResourceWithAiHandler.HandleAsync(new GenerateModuleFromResourceRequest(
                body.ResourceType, body.ResourceId, body.Title, body.Notes, GetCurrentUserId()), ct);
            return Ok(result);
        }
        catch (ModuleValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/modules/generate-from-lesson
    [HttpPost("generate-from-lesson")]
    public async Task<IActionResult> GenerateFromLesson([FromBody] GenerateModuleFromLessonRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _generateFromLessonHandler.HandleAsync(new GenerateModuleFromLessonRequest(
                body.LessonId, body.Title, body.Notes, GetCurrentUserId()), ct);
            return Ok(result);
        }
        catch (ModuleValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/modules/generate-from-exercise
    [HttpPost("generate-from-exercise")]
    public async Task<IActionResult> GenerateFromExercise([FromBody] GenerateModuleFromExerciseRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _generateFromExerciseHandler.HandleAsync(new GenerateModuleFromExerciseRequest(
                body.ExerciseId, body.Title, body.Notes, GetCurrentUserId()), ct);
            return Ok(result);
        }
        catch (ModuleValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // PUT api/admin/modules/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateModuleRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _updateHandler.HandleAsync(new UpdateModuleCommand(
                id, body.Title, body.Description, body.CefrLevel, body.Skill, body.Subskill,
                body.ContextTags, body.FocusTags, body.DifficultyBand, body.EstimatedMinutes, body.FeedbackPlanJson), ct);
            return Ok(result);
        }
        catch (ModuleValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST api/admin/modules/{id}/approve  { notes? }
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveModuleRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _approveHandler.HandleAsync(new ApproveModuleCommand(id, GetCurrentUserId(), body.Notes), ct);
            return Ok(result);
        }
        catch (ModuleValidationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // POST api/admin/modules/{id}/reject  { reason }
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectModuleRequestBody body, CancellationToken ct)
    {
        try
        {
            var result = await _rejectHandler.HandleAsync(new RejectModuleCommand(id, body.Reason, GetCurrentUserId()), ct);
            return Ok(result);
        }
        catch (ModuleValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private Guid? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    public sealed record CreateModuleRequestBody(
        string Title, IReadOnlyList<ModuleLessonLinkInput> LessonLinks, IReadOnlyList<ModuleExerciseLinkInput> ExerciseLinks,
        string? Description = null, string? ObjectiveKey = null, string? CefrLevel = null, string? Skill = null,
        string? Subskill = null, IReadOnlyList<string>? ContextTags = null, IReadOnlyList<string>? FocusTags = null,
        int? DifficultyBand = null, int? EstimatedMinutes = null, string? FeedbackPlanJson = null
    );

    public sealed record GenerateModuleFromItemsRequestBody(
        IReadOnlyList<ModuleLessonLinkInput> LessonLinks, IReadOnlyList<ModuleExerciseLinkInput> ExerciseLinks,
        string? Title = null, string? Notes = null
    );

    public sealed record GenerateModuleFromResourceRequestBody(
        string ResourceType, Guid ResourceId, string? Title = null, string? Notes = null
    );

    public sealed record GenerateModuleFromLessonRequestBody(
        Guid LessonId, string? Title = null, string? Notes = null
    );

    public sealed record GenerateModuleFromExerciseRequestBody(
        Guid ExerciseId, string? Title = null, string? Notes = null
    );

    public sealed record UpdateModuleRequestBody(
        string Title, string? Description = null, string? CefrLevel = null, string? Skill = null, string? Subskill = null,
        IReadOnlyList<string>? ContextTags = null, IReadOnlyList<string>? FocusTags = null,
        int? DifficultyBand = null, int? EstimatedMinutes = null, string? FeedbackPlanJson = null
    );

    public sealed record ApproveModuleRequestBody(string? Notes = null);
    public sealed record RejectModuleRequestBody(string Reason);

    public sealed record ModulePreviewSubmitRequestBody(Dictionary<string, JsonElement>? Answers = null);
}

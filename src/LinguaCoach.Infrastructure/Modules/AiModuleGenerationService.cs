using System.Text.Json;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.Modules;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Infrastructure.Lessons;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Modules;

/// <summary>
/// Phase J2c — AI-assisted "Generate Module" composer, "from resource" entry point only. Finds the
/// same existing Approved Lesson and Approved Exercise linked to the given resource that
/// <see cref="ModuleGenerationService"/> would find — never cascade-generates a new Lesson or
/// Exercise, same hard invariant as the deterministic composer — then asks AI to write the
/// module's own descriptive framing (title/description/feedback-plan copy) referencing what the
/// selected Lesson and Exercise actually contain. There is no answer key or scoring rule at the
/// Module level, so this carries the same (low) risk profile as
/// <see cref="Lessons.AiLessonGenerationService"/>, not <see cref="Exercises.AiExerciseGenerationService"/>'s
/// answer-leak concerns. Mirrors both siblings' retry-once-then-throw failure pattern.
/// </summary>
public sealed class AiModuleGenerationService : IGenerateModuleFromResourceWithAiHandler
{
    public const string GeneratePromptKey = "module_generate_from_resource";

    private const int MaxTextVariableLength = 1500;

    private readonly LinguaCoachDbContext _db;
    private readonly IAiContextBuilder _contextBuilder;
    private readonly AiExecutionService _aiExecution;
    private readonly ILogger<AiModuleGenerationService> _logger;

    public AiModuleGenerationService(
        LinguaCoachDbContext db,
        IAiContextBuilder contextBuilder,
        AiExecutionService aiExecution,
        ILogger<AiModuleGenerationService> logger)
    {
        _db = db;
        _contextBuilder = contextBuilder;
        _aiExecution = aiExecution;
        _logger = logger;
    }

    public async Task<GenerateModuleResult> HandleAsync(
        GenerateModuleFromResourceRequest request, CancellationToken ct = default)
    {
        if (!LessonResourceLookup.TryParseResourceType(request.ResourceType, out var resourceType))
            throw new ModuleValidationException($"Unsupported resource type '{request.ResourceType}'.");

        var lesson = await _db.LessonResourceLinks
            .Where(l => l.ResourceType == resourceType && l.ResourceId == request.ResourceId)
            .Join(_db.Lessons.Where(i => i.ReviewStatus == AdminReviewStatus.Approved),
                l => l.LessonId, i => i.Id, (l, i) => i)
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync(ct)
            ?? throw new ModuleValidationException(
                "No approved Lesson is linked to this resource yet — generate and approve a Lesson first.");

        var activity = await _db.ExerciseResourceLinks
            .Where(l => l.ResourceType == resourceType && l.ResourceId == request.ResourceId)
            .Join(_db.Exercises.Where(a => a.ReviewStatus == AdminReviewStatus.Approved),
                l => l.ExerciseId, a => a.Id, (l, a) => a)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(ct)
            ?? throw new ModuleValidationException(
                "No approved Exercise is linked to this resource yet — generate and approve an Activity first.");

        var variables = new Dictionary<string, string>
        {
            ["lessonTitle"] = lesson.Title,
            ["lessonBody"] = Truncate(lesson.Body),
            ["exerciseTitle"] = activity.Title,
            ["exerciseInstructions"] = Truncate(activity.Instructions),
            ["activityType"] = activity.ActivityType,
            ["cefrLevel"] = lesson.CefrLevel ?? activity.CefrLevel ?? "",
            ["skill"] = lesson.Skill ?? activity.Skill ?? "",
            ["notes"] = request.Notes ?? "",
        };

        var correlationId = Guid.NewGuid().ToString("N")[..16];
        AiRequest aiRequest;
        try
        {
            aiRequest = await _contextBuilder.BuildAsync(GeneratePromptKey, variables, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build AI Module generation prompt. CorrelationId={CorrelationId}", correlationId);
            throw new ModuleValidationException(
                $"Could not build the AI prompt: {ex.Message} Use the deterministic Generate action instead.");
        }

        AiExecutionResult execResult;
        try
        {
            execResult = await _aiExecution.ExecuteWithMetaAsync(GeneratePromptKey, aiRequest, null, correlationId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "AI provider unavailable for AI Module generation. CorrelationId={CorrelationId}", correlationId);
            throw new ModuleValidationException(
                $"AI generation is currently unavailable: {ex.Message} Use the deterministic Generate action instead.");
        }

        var parsed = TryParseOutput(execResult.ResponseJson, out var parseError);
        if (parsed is null)
        {
            try
            {
                execResult = await _aiExecution.ExecuteWithMetaAsync(GeneratePromptKey, aiRequest, null, correlationId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "AI provider unavailable on retry for AI Module generation. CorrelationId={CorrelationId}", correlationId);
                throw new ModuleValidationException(
                    $"AI generation is currently unavailable: {ex.Message} Use the deterministic Generate action instead.");
            }

            parsed = TryParseOutput(execResult.ResponseJson, out parseError);
            if (parsed is null)
            {
                throw new ModuleValidationException(
                    $"AI response could not be parsed after retry: {parseError} Use the deterministic Generate action instead.");
            }
        }

        var resolvedTitle = !string.IsNullOrWhiteSpace(request.Title) ? request.Title!.Trim() : parsed.Title;
        var cefrLevel = lesson.CefrLevel ?? activity.CefrLevel;
        var skill = lesson.Skill ?? activity.Skill;
        var subskill = lesson.Subskill ?? activity.Subskill;
        var contextTags = MergeTagArrays(new[] { lesson.ContextTagsJson, activity.ContextTagsJson });
        var focusTags = MergeTagArrays(new[] { lesson.FocusTagsJson, activity.FocusTagsJson });
        var difficultyBand = lesson.DifficultyBand ?? activity.DifficultyBand;
        var estimatedMinutes = activity.EstimatedMinutes;

        var description = $"AI draft ({execResult.ProviderName}/{execResult.ModelName}) — review and edit before approval. "
            + parsed.Description
            + (request.Notes is not null ? $" {request.Notes.Trim()}" : string.Empty);

        var feedbackPlanJson = JsonSerializer.Serialize(new
        {
            completionMessage = parsed.CompletionMessage,
            evaluationCriteria = parsed.EvaluationCriteria,
            feedbackFocus = parsed.FeedbackFocus,
            note = $"AI-generated module-level feedback plan ({execResult.ProviderName}/{execResult.ModelName}) — review before approval.",
        });

        Module module;
        try
        {
            module = new Module(
                resolvedTitle, ModuleSourceMode.GeneratedFromResources, description, objectiveKey: null,
                cefrLevel, skill, subskill,
                JsonSerializer.Serialize(contextTags), JsonSerializer.Serialize(focusTags),
                difficultyBand, estimatedMinutes, feedbackPlanJson,
                execResult.ProviderName, execResult.ModelName, request.CreatedByUserId);
        }
        catch (ArgumentException ex)
        {
            throw new ModuleValidationException($"AI-generated content failed validation: {ex.Message}");
        }

        _db.Modules.Add(module);
        await _db.SaveChangesAsync(ct);

        var lessonLinks = new[] { new ModuleLessonLinkInput(lesson.Id, "Primary") };
        var exerciseLinks = new[] { new ModuleExerciseLinkInput(activity.Id, "PrimaryPractice") };
        var (savedLessonLinks, savedExerciseLinks) = await ModuleLinkBuilder.BuildAndAddAsync(
            _db, module.Id, lessonLinks, exerciseLinks, requireApproved: true, ct);
        await _db.SaveChangesAsync(ct);

        var dto = ModuleMappers.ToDto(module, savedLessonLinks, savedExerciseLinks);
        return new GenerateModuleResult(dto, $"/admin/modules?id={module.Id}");
    }

    private static string Truncate(string value) =>
        value.Length <= MaxTextVariableLength ? value : value[..MaxTextVariableLength];

    private static List<string> MergeTagArrays(IEnumerable<string?> jsonArrays)
    {
        var merged = new List<string>();
        foreach (var json in jsonArrays)
        {
            if (string.IsNullOrWhiteSpace(json)) continue;
            try
            {
                var tags = JsonSerializer.Deserialize<List<string>>(json);
                if (tags is null) continue;
                foreach (var tag in tags)
                    if (!string.IsNullOrWhiteSpace(tag) && !merged.Contains(tag, StringComparer.OrdinalIgnoreCase))
                        merged.Add(tag);
            }
            catch (JsonException)
            {
                // Malformed tag JSON on a source row is never fatal to generation — skip it.
            }
        }
        return merged;
    }

    private static string CleanJson(string raw)
    {
        var cleaned = raw.Trim();
        if (cleaned.StartsWith("```"))
        {
            var firstNewline = cleaned.IndexOf('\n');
            var lastFence = cleaned.LastIndexOf("```");
            if (firstNewline > 0 && lastFence > firstNewline)
                cleaned = cleaned[(firstNewline + 1)..lastFence].Trim();
        }
        return cleaned;
    }

    private sealed record AiModuleOutput(
        string Title, string Description, string CompletionMessage,
        List<string> EvaluationCriteria, string? FeedbackFocus);

    /// <summary>Fully defensive JSON-&gt;output parsing. Returns null (with
    /// <paramref name="parseError"/> set) when the response isn't parseable JSON, isn't a JSON
    /// object, or is missing a non-empty title/description/completionMessage — the minimum a
    /// usable Module draft needs. No answer-key/scoring concern at this level, unlike Exercise
    /// generation.</summary>
    private static AiModuleOutput? TryParseOutput(string rawResponse, out string? parseError)
    {
        parseError = null;
        var cleaned = CleanJson(rawResponse);

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(cleaned);
        }
        catch (JsonException ex)
        {
            parseError = $"Response is not valid JSON: {ex.Message}";
            return null;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                parseError = "Response is not a JSON object.";
                return null;
            }

            var root = doc.RootElement;
            var title = GetString(root, "title");
            var description = GetString(root, "description");

            var feedbackPlan = root.TryGetProperty("feedbackPlan", out var fp) && fp.ValueKind == JsonValueKind.Object
                ? fp
                : default;
            var completionMessage = feedbackPlan.ValueKind == JsonValueKind.Object
                ? GetString(feedbackPlan, "completionMessage")
                : null;

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description) || string.IsNullOrWhiteSpace(completionMessage))
            {
                parseError = "Response is missing a non-empty 'title', 'description', or 'feedbackPlan.completionMessage'.";
                return null;
            }

            var evaluationCriteria = feedbackPlan.ValueKind == JsonValueKind.Object
                ? GetStringArray(feedbackPlan, "evaluationCriteria").ToList()
                : new List<string>();
            var feedbackFocus = feedbackPlan.ValueKind == JsonValueKind.Object
                ? GetString(feedbackPlan, "feedbackFocus")
                : null;

            return new AiModuleOutput(title.Trim(), description.Trim(), completionMessage.Trim(), evaluationCriteria, feedbackFocus);
        }
    }

    private static string? GetString(JsonElement root, string prop) =>
        root.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    private static IReadOnlyList<string> GetStringArray(JsonElement root, string prop)
    {
        if (!root.TryGetProperty(prop, out var el) || el.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        var list = new List<string>();
        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                list.Add(item.GetString()!.Trim());
        }
        return list;
    }
}

using System.Text.Json;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.Exercises;
using LinguaCoach.Application.Onboarding;
using LinguaCoach.Application.Placement;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Infrastructure.Lessons;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Exercises;

/// <summary>
/// Phase J2b — AI-assisted "Generate Activity" composer, "from resources" entry point only. Builds
/// a pending-review <see cref="Exercise"/> draft the same way <see cref="ActivityGenerationService"/>
/// does, but AI supplies the framing content (gap-fill sentence / multiple-choice distractors /
/// comprehension question) instead of the deterministic composer's fixed templates. The correct
/// answer, scoring rule, and answer key are always deterministically derived from the resource's
/// own fields — never AI-supplied — see <see cref="IGenerateActivityFromResourcesWithAiHandler"/>'s
/// doc comment for why. Mirrors <see cref="Lessons.AiLessonGenerationService"/>'s retry-once-then-
/// throw pattern: this is a synchronous admin-triggered action the admin is actively waiting on, so
/// failure after the retry throws rather than degrading silently.
/// </summary>
public sealed class AiExerciseGenerationService : IGenerateActivityFromResourcesWithAiHandler
{
    public const string GeneratePromptKey = "exercise_generate_from_resources";

    private const int MaxResourceTextLength = 2000;
    private const int MaxDistractors = 3;
    private const string BlankMarker = "___";

    private static readonly HashSet<PublishedResourceType> DefinitionalTypes = new()
    {
        PublishedResourceType.Vocabulary, PublishedResourceType.Grammar
    };

    private readonly LinguaCoachDbContext _db;
    private readonly IFormIoSchemaValidationService _schemaValidator;
    private readonly IAiContextBuilder _contextBuilder;
    private readonly AiExecutionService _aiExecution;
    private readonly ILogger<AiExerciseGenerationService> _logger;

    public AiExerciseGenerationService(
        LinguaCoachDbContext db,
        IFormIoSchemaValidationService schemaValidator,
        IAiContextBuilder contextBuilder,
        AiExecutionService aiExecution,
        ILogger<AiExerciseGenerationService> logger)
    {
        _db = db;
        _schemaValidator = schemaValidator;
        _contextBuilder = contextBuilder;
        _aiExecution = aiExecution;
        _logger = logger;
    }

    private readonly record struct ResolvedResource(
        ExerciseResourceLinkInput Input, PublishedResourceType Type, LessonResourceRole Role, LessonResourceSnapshot Snapshot);

    public async Task<GenerateExerciseResult> HandleAsync(
        GenerateActivityFromResourcesRequest request, CancellationToken ct = default)
    {
        if (request.Resources is not { Count: > 0 })
            throw new ExerciseValidationException("At least one resource is required to generate an Activity.");
        if (request.DefaultCefrLevel is not null && !CefrLevelConstants.IsValid(request.DefaultCefrLevel))
            throw new ExerciseValidationException($"Default CEFR level '{request.DefaultCefrLevel}' is not a valid CEFR level.");
        if (request.DefaultDifficultyBand is < 1 or > 5)
            throw new ExerciseValidationException("Default difficulty band must be between 1 and 5.");

        var resolved = new List<ResolvedResource>();
        foreach (var input in request.Resources)
        {
            if (!LessonResourceLookup.TryParseResourceType(input.ResourceType, out var resourceType))
                throw new ExerciseValidationException($"Unsupported resource type '{input.ResourceType}'.");
            if (!LessonResourceLookup.TryParseRole(input.Role, out var role))
                throw new ExerciseValidationException($"Unsupported resource link role '{input.Role}'.");

            var snapshot = await LessonResourceLookup.FindAsync(_db, resourceType, input.ResourceId, ct)
                ?? throw new ExerciseValidationException(
                    $"Resource '{input.ResourceType}:{input.ResourceId}' was not found in the published Resource Bank.");

            resolved.Add(new ResolvedResource(input, resourceType, role, snapshot));
        }

        var primaryMatch = resolved.FirstOrDefault(r => r.Role == LessonResourceRole.Primary);
        var primary = primaryMatch.Snapshot is not null ? primaryMatch : resolved[0];

        var isDefinitional = DefinitionalTypes.Contains(primary.Type);
        var activityType = request.RequestedActivityType?.Trim().ToLowerInvariant()
            ?? (isDefinitional ? ActivityGenerationService.ActivityTypeGapFill : ActivityGenerationService.ActivityTypeShortAnswer);

        var supportedForCategory = isDefinitional
            ? new[] { ActivityGenerationService.ActivityTypeGapFill, ActivityGenerationService.ActivityTypeMultipleChoiceSingle }
            : new[] { ActivityGenerationService.ActivityTypeShortAnswer };
        if (!supportedForCategory.Contains(activityType))
            throw new ExerciseValidationException(
                $"Activity type '{activityType}' is not supported for resource type '{primary.Type}'. Supported: {string.Join(", ", supportedForCategory)}.");

        if (activityType == ActivityGenerationService.ActivityTypeMultipleChoiceSingle && string.IsNullOrWhiteSpace(primary.Snapshot.Body))
            throw new ExerciseValidationException(
                $"Resource '{primary.Snapshot.Title}' has no definition/description text to build a multiple-choice question from — use 'gap_fill' instead.");

        var variables = new Dictionary<string, string>
        {
            ["activityType"] = activityType,
            ["resourceTitle"] = primary.Snapshot.Title,
            ["resourceDefinition"] = Truncate(primary.Snapshot.Body ?? ""),
            ["resourceType"] = primary.Type.ToString(),
            ["cefrLevel"] = request.DefaultCefrLevel ?? primary.Snapshot.CefrLevel,
            ["skill"] = request.DefaultSkill ?? primary.Snapshot.Skill,
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
            _logger.LogWarning(ex, "Failed to build AI Exercise generation prompt. CorrelationId={CorrelationId}", correlationId);
            throw new ExerciseValidationException(
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
                "AI provider unavailable for AI Exercise generation. CorrelationId={CorrelationId}", correlationId);
            throw new ExerciseValidationException(
                $"AI generation is currently unavailable: {ex.Message} Use the deterministic Generate action instead.");
        }

        var parsed = TryParseAndValidateOutput(execResult.ResponseJson, activityType, primary.Snapshot, out var parseError);
        if (parsed is null)
        {
            try
            {
                execResult = await _aiExecution.ExecuteWithMetaAsync(GeneratePromptKey, aiRequest, null, correlationId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "AI provider unavailable on retry for AI Exercise generation. CorrelationId={CorrelationId}", correlationId);
                throw new ExerciseValidationException(
                    $"AI generation is currently unavailable: {ex.Message} Use the deterministic Generate action instead.");
            }

            parsed = TryParseAndValidateOutput(execResult.ResponseJson, activityType, primary.Snapshot, out parseError);
            if (parsed is null)
            {
                throw new ExerciseValidationException(
                    $"AI response could not be parsed after retry: {parseError} Use the deterministic Generate action instead.");
            }
        }

        var (instructions, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson) = activityType switch
        {
            ActivityGenerationService.ActivityTypeGapFill => ComposeGapFill(primary.Snapshot, parsed.PromptText!),
            ActivityGenerationService.ActivityTypeMultipleChoiceSingle => ComposeMultipleChoiceSingle(primary.Snapshot, parsed.Distractors),
            ActivityGenerationService.ActivityTypeShortAnswer => ComposeShortAnswer(primary.Snapshot, parsed.PromptText!),
            _ => throw new ExerciseValidationException($"Unsupported activity type '{activityType}'."),
        };

        var schemaCheck = _schemaValidator.ValidateSchema(formSchemaJson);
        if (!schemaCheck.IsValid)
            throw new ExerciseValidationException($"Generated Form.io schema failed validation: {schemaCheck.Error}");

        var resolvedTitle = !string.IsNullOrWhiteSpace(request.Title) ? request.Title!.Trim() : primary.Snapshot.Title;
        var cefrLevel = request.DefaultCefrLevel ?? primary.Snapshot.CefrLevel;
        var skill = request.DefaultSkill ?? primary.Snapshot.Skill;
        var subskill = request.DefaultSubskill ?? primary.Snapshot.Subskill;
        var contextTags = request.DefaultContextTags is { Count: > 0 }
            ? request.DefaultContextTags
            : MergeTagArrays(resolved.Select(r => r.Snapshot.ContextTagsJson));
        var focusTags = request.DefaultFocusTags is { Count: > 0 }
            ? request.DefaultFocusTags
            : MergeTagArrays(resolved.Select(r => r.Snapshot.FocusTagsJson));
        var difficultyBand = request.DefaultDifficultyBand ?? primary.Snapshot.DifficultyBand;

        var description = $"AI draft ({execResult.ProviderName}/{execResult.ModelName}) — review and edit before approval. "
            + $"Generated from: {string.Join(", ", resolved.Select(r => r.Snapshot.Title))}."
            + (request.Notes is not null ? $" {request.Notes.Trim()}" : string.Empty);

        Exercise activity;
        try
        {
            activity = new Exercise(
                resolvedTitle, instructions, activityType, ExerciseRendererType.Formio, ExerciseSourceMode.GeneratedFromResources,
                description, patternKey: null, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson,
                cefrLevel, skill, subskill,
                JsonSerializer.Serialize(contextTags), JsonSerializer.Serialize(focusTags),
                difficultyBand, estimatedMinutes: null, lessonId: null,
                execResult.ProviderName, execResult.ModelName, request.CreatedByUserId);
        }
        catch (ArgumentException ex)
        {
            throw new ExerciseValidationException($"AI-generated content failed validation: {ex.Message}");
        }

        _db.Exercises.Add(activity);
        await _db.SaveChangesAsync(ct);

        var links = resolved
            .Select(r => new ExerciseResourceLink(
                activity.Id, r.Type, r.Input.ResourceId, r.Role, r.Snapshot.Title, r.Snapshot.ContentFingerprint))
            .ToList();
        _db.ExerciseResourceLinks.AddRange(links);
        await _db.SaveChangesAsync(ct);

        var dto = ExerciseMappers.ToDto(activity, links);
        return new GenerateExerciseResult(dto, $"/admin/exercises?id={activity.Id}");
    }

    /// <summary>AI supplies only the sentence; the blank's answer is always the resource's own
    /// term, never AI-supplied — the correct answer can never drift from what the resource bank
    /// actually says.</summary>
    private static (string Instructions, string FormSchemaJson, string AnswerKeyJson, string ScoringRulesJson, string FeedbackPlanJson)
        ComposeGapFill(LessonResourceSnapshot primary, string aiSentence)
    {
        var term = primary.Title;
        var instructions = "Read the sentence below and type the missing word or phrase.";
        var formSchemaJson = JsonSerializer.Serialize(new
        {
            components = new object[]
            {
                new { type = "content", key = "prompt", html = $"<p>{System.Net.WebUtility.HtmlEncode(aiSentence)}</p>" },
                new { type = "textfield", key = "answer", label = "Your answer", input = true },
            }
        });
        var answerKeyJson = JsonSerializer.Serialize(new Dictionary<string, string> { ["answer"] = term });
        var scoringRulesJson = JsonSerializer.Serialize(new ScoringRulesDocument(
            new Dictionary<string, ComponentScoringRule> { ["answer"] = new(ScoringRuleKinds.TextNormalized, CorrectAnswer: term, Points: 1.0) }));
        var feedbackPlanJson = JsonSerializer.Serialize(new
        {
            correctFeedback = "Correct!",
            incorrectFeedback = $"Not quite — the answer was \"{term}\".",
        });

        return (instructions, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson);
    }

    /// <summary>AI supplies only the wrong-option distractors; the correct option's text is always
    /// the resource's own definition, verbatim — never AI-paraphrased, so the scoring key can never
    /// be wrong.</summary>
    private static (string Instructions, string FormSchemaJson, string AnswerKeyJson, string ScoringRulesJson, string FeedbackPlanJson)
        ComposeMultipleChoiceSingle(LessonResourceSnapshot primary, IReadOnlyList<string> distractors)
    {
        var correctText = primary.Body!.Trim();
        var options = new List<(string Key, string Text)> { ("opt_0", correctText) };
        for (var i = 0; i < distractors.Count; i++)
            options.Add(($"opt_{i + 1}", distractors[i]));

        var instructions = $"Choose the correct meaning of \"{primary.Title}\".";
        var formSchemaJson = JsonSerializer.Serialize(new
        {
            components = new object[]
            {
                new
                {
                    type = "radio", key = "answer", label = instructions, input = true,
                    values = options.Select(o => new { label = o.Text, value = o.Key }).ToArray(),
                },
            }
        });
        var answerKeyJson = JsonSerializer.Serialize(new Dictionary<string, string> { ["answer"] = options[0].Text });
        var scoringRulesJson = JsonSerializer.Serialize(new ScoringRulesDocument(
            new Dictionary<string, ComponentScoringRule> { ["answer"] = new(ScoringRuleKinds.SingleChoice, CorrectAnswer: options[0].Key, Points: 1.0) }));
        var feedbackPlanJson = JsonSerializer.Serialize(new
        {
            correctFeedback = "Correct!",
            incorrectFeedback = $"Not quite — the correct meaning was: \"{options[0].Text}\".",
        });

        return (instructions, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson);
    }

    /// <summary>AI supplies only the question; the excerpt shown is always the resource's own body
    /// text. Already honestly marked as requiring manual/AI evaluation — same as the deterministic
    /// composer — so there is no scoring-integrity risk here either way.</summary>
    private static (string Instructions, string FormSchemaJson, string AnswerKeyJson, string ScoringRulesJson, string FeedbackPlanJson)
        ComposeShortAnswer(LessonResourceSnapshot primary, string aiQuestion)
    {
        var excerpt = !string.IsNullOrWhiteSpace(primary.Body) ? primary.Body!.Trim() : "(no excerpt available)";
        var instructions = "Read the passage below and answer the question.";
        var formSchemaJson = JsonSerializer.Serialize(new
        {
            components = new object[]
            {
                new { type = "content", key = "passage", html = $"<p>{System.Net.WebUtility.HtmlEncode(excerpt)}</p>" },
                new { type = "textarea", key = "answer", label = aiQuestion, input = true },
            }
        });
        var answerKeyJson = JsonSerializer.Serialize(new Dictionary<string, string?> { ["answer"] = null });
        var scoringRulesJson = JsonSerializer.Serialize(new ScoringRulesDocument(
            new Dictionary<string, ComponentScoringRule> { ["answer"] = new(ScoringRuleKinds.TextNormalized, RequiresManualOrAiEvaluation: true) }));
        var feedbackPlanJson = JsonSerializer.Serialize(new
        {
            correctFeedback = (string?)null,
            incorrectFeedback = (string?)null,
            note = "Open-ended — requires manual or AI evaluation, not deterministically scored.",
        });

        return (instructions, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson);
    }

    private static string Truncate(string value) =>
        value.Length <= MaxResourceTextLength ? value : value[..MaxResourceTextLength];

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

    private sealed record AiExerciseOutput(string? PromptText, List<string> Distractors);

    /// <summary>
    /// Fully defensive JSON-&gt;output parsing plus per-activityType safety validation. Returns
    /// null (with <paramref name="parseError"/> set) on unparseable JSON, or when the output
    /// violates a type-specific safety rule — most importantly, for "gap_fill", when the AI
    /// sentence doesn't contain the blank marker or leaks the answer term outside the blank. A
    /// caller must never use an unvalidated result.
    /// </summary>
    private static AiExerciseOutput? TryParseAndValidateOutput(
        string rawResponse, string activityType, LessonResourceSnapshot primary, out string? parseError)
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
            var promptText = GetString(root, "promptText");
            var distractors = GetStringArray(root, "distractors")
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Select(d => d.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (activityType == ActivityGenerationService.ActivityTypeGapFill)
            {
                if (string.IsNullOrWhiteSpace(promptText))
                {
                    parseError = "Response is missing a non-empty 'promptText' for a gap_fill sentence.";
                    return null;
                }
                if (!promptText.Contains(BlankMarker, StringComparison.Ordinal))
                {
                    parseError = $"promptText does not contain the required blank marker '{BlankMarker}'.";
                    return null;
                }
                var withoutBlank = promptText.Replace(BlankMarker, "", StringComparison.Ordinal);
                if (withoutBlank.Contains(primary.Title, StringComparison.OrdinalIgnoreCase))
                {
                    parseError = "promptText leaks the answer term outside the blank marker.";
                    return null;
                }
                return new AiExerciseOutput(promptText.Trim(), new List<string>());
            }

            if (activityType == ActivityGenerationService.ActivityTypeMultipleChoiceSingle)
            {
                var correctText = primary.Body?.Trim() ?? "";
                var filtered = distractors
                    .Where(d => !string.Equals(d, correctText, StringComparison.OrdinalIgnoreCase))
                    .Take(MaxDistractors)
                    .ToList();
                if (filtered.Count == 0)
                {
                    parseError = "Response contained no usable distractors (empty, or all matched the correct answer).";
                    return null;
                }
                return new AiExerciseOutput(promptText, filtered);
            }

            // short_answer
            if (string.IsNullOrWhiteSpace(promptText))
            {
                parseError = "Response is missing a non-empty 'promptText' for the comprehension question.";
                return null;
            }
            return new AiExerciseOutput(promptText.Trim(), new List<string>());
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

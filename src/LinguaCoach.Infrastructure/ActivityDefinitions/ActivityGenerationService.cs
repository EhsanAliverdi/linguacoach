using System.Text.Json;
using LinguaCoach.Application.ActivityDefinitions;
using LinguaCoach.Application.Onboarding;
using LinguaCoach.Application.Placement;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.LearnItems;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ActivityDefinitions;

/// <summary>
/// Phase H4 — deterministic "Generate Activity" composer, for both entry points
/// (<see cref="IGenerateActivityFromResourcesHandler"/>/<see cref="IGenerateActivityFromLearnItemHandler"/>).
/// Builds a pending-review <see cref="ActivityDefinition"/> draft directly from the fields of one
/// or more selected published Resource Bank rows — no AI provider call, matching Phase H3's
/// <c>LearnItemGenerationService</c> decision (no existing AI service in this codebase generates a
/// scored practice exercise from source text). Never modifies the resources or Learn Item it reads
/// from, never creates a Module row, never assigns anything to a student.
///
/// Supported <c>ActivityType</c>s:
/// <list type="bullet">
/// <item><description><c>gap_fill</c> — Vocabulary/Grammar only. Shows the resource's own
/// definition/description, asks the student to type the term. Deterministically scored
/// (text_normalized).</description></item>
/// <item><description><c>multiple_choice_single</c> — Vocabulary/Grammar only. Asks "what does X
/// mean", with distractor options pulled from sibling published resources of the same type
/// already in the bank. Deterministically scored (single_choice). Requires at least one usable
/// distractor — generation is rejected (not silently degraded to a single-option "choice") when
/// none exist.</description></item>
/// <item><description><c>short_answer</c> — ReadingReference/ReadingPassage only. Open-ended
/// comprehension prompt over the resource's own excerpt/summary text, explicitly marked
/// <see cref="ComponentScoringRule.RequiresManualOrAiEvaluation"/> — reading comprehension can't be
/// deterministically graded from thin metadata, so this is honestly left ungraded rather than
/// faking a score.</description></item>
/// </list>
/// <see cref="ActivityDefinition.ScoringRulesJson"/> is serialized straight from the shared
/// <see cref="ScoringRulesDocument"/>/<see cref="ComponentScoringRule"/> types already used by
/// placement/onboarding/reorder_paragraphs scoring, so a future runtime integration can reuse
/// <see cref="LinguaCoach.Application.FormIo.ComponentAnswerScorer"/> as-is.
/// </summary>
public sealed class ActivityGenerationService : IGenerateActivityFromResourcesHandler, IGenerateActivityFromLearnItemHandler
{
    private const string GenerationProvider = "Deterministic";
    private const string GenerationModel = "activity-draft-composer-v1";
    private const int MaxDistractors = 3;

    public const string ActivityTypeGapFill = "gap_fill";
    public const string ActivityTypeMultipleChoiceSingle = "multiple_choice_single";
    public const string ActivityTypeShortAnswer = "short_answer";

    private static readonly HashSet<PublishedResourceType> DefinitionalTypes = new()
    {
        PublishedResourceType.Vocabulary, PublishedResourceType.Grammar
    };

    private readonly LinguaCoachDbContext _db;
    private readonly IFormIoSchemaValidationService _schemaValidator;

    public ActivityGenerationService(LinguaCoachDbContext db, IFormIoSchemaValidationService schemaValidator)
    {
        _db = db;
        _schemaValidator = schemaValidator;
    }

    private readonly record struct ResolvedResource(
        ActivityResourceLinkInput Input, PublishedResourceType Type, LearnItemResourceRole Role, LearnItemResourceSnapshot Snapshot);

    public async Task<GenerateActivityDefinitionResult> HandleAsync(
        GenerateActivityFromResourcesRequest request, CancellationToken ct = default)
    {
        if (request.Resources is not { Count: > 0 })
            throw new ActivityDefinitionValidationException("At least one resource is required to generate an Activity.");
        if (request.DefaultCefrLevel is not null && !CefrLevelConstants.IsValid(request.DefaultCefrLevel))
            throw new ActivityDefinitionValidationException($"Default CEFR level '{request.DefaultCefrLevel}' is not a valid CEFR level.");
        if (request.DefaultDifficultyBand is < 1 or > 5)
            throw new ActivityDefinitionValidationException("Default difficulty band must be between 1 and 5.");

        var resolved = new List<ResolvedResource>();
        foreach (var input in request.Resources)
        {
            if (!LearnItemResourceLookup.TryParseResourceType(input.ResourceType, out var resourceType))
                throw new ActivityDefinitionValidationException($"Unsupported resource type '{input.ResourceType}'.");
            if (!LearnItemResourceLookup.TryParseRole(input.Role, out var role))
                throw new ActivityDefinitionValidationException($"Unsupported resource link role '{input.Role}'.");

            var snapshot = await LearnItemResourceLookup.FindAsync(_db, resourceType, input.ResourceId, ct)
                ?? throw new ActivityDefinitionValidationException(
                    $"Resource '{input.ResourceType}:{input.ResourceId}' was not found in the published Resource Bank.");

            resolved.Add(new ResolvedResource(input, resourceType, role, snapshot));
        }

        return await ComposeAndSaveAsync(
            resolved, request.RequestedActivityType, request.Title,
            request.DefaultCefrLevel, request.DefaultSkill, request.DefaultSubskill,
            request.DefaultContextTags, request.DefaultFocusTags, request.DefaultDifficultyBand,
            request.Notes, request.CreatedByUserId, learnItemId: null, ct);
    }

    public async Task<GenerateActivityDefinitionResult> HandleAsync(
        GenerateActivityFromLearnItemRequest request, CancellationToken ct = default)
    {
        var learnItem = await _db.LearnItems.FirstOrDefaultAsync(l => l.Id == request.LearnItemId, ct)
            ?? throw new ActivityDefinitionValidationException($"Learn Item '{request.LearnItemId}' was not found.");

        var learnItemLinks = await _db.LearnItemResourceLinks
            .Where(l => l.LearnItemId == learnItem.Id).ToListAsync(ct);
        if (learnItemLinks.Count == 0)
            throw new ActivityDefinitionValidationException(
                "This Learn Item has no linked resources to generate an Activity from.");

        var resolved = new List<ResolvedResource>();
        foreach (var link in learnItemLinks)
        {
            var snapshot = await LearnItemResourceLookup.FindAsync(_db, link.ResourceType, link.ResourceId, ct)
                ?? throw new ActivityDefinitionValidationException(
                    $"Resource '{link.ResourceType}:{link.ResourceId}' linked to this Learn Item is no longer available in the published Resource Bank.");

            resolved.Add(new ResolvedResource(
                new ActivityResourceLinkInput(link.ResourceType.ToString(), link.ResourceId, link.Role.ToString()),
                link.ResourceType, link.Role, snapshot));
        }

        return await ComposeAndSaveAsync(
            resolved, request.RequestedActivityType, request.Title ?? learnItem.Title,
            learnItem.CefrLevel, learnItem.Skill, learnItem.Subskill,
            ParseTags(learnItem.ContextTagsJson), ParseTags(learnItem.FocusTagsJson), learnItem.DifficultyBand,
            request.Notes, request.CreatedByUserId, learnItemId: learnItem.Id, ct);
    }

    private async Task<GenerateActivityDefinitionResult> ComposeAndSaveAsync(
        List<ResolvedResource> resolved,
        string? requestedActivityType,
        string? title,
        string? defaultCefrLevel,
        string? defaultSkill,
        string? defaultSubskill,
        IReadOnlyList<string>? defaultContextTags,
        IReadOnlyList<string>? defaultFocusTags,
        int? defaultDifficultyBand,
        string? notes,
        Guid? createdByUserId,
        Guid? learnItemId,
        CancellationToken ct)
    {
        var primaryMatch = resolved.FirstOrDefault(r => r.Role == LearnItemResourceRole.Primary);
        var primary = primaryMatch.Snapshot is not null ? primaryMatch : resolved[0];

        var isDefinitional = DefinitionalTypes.Contains(primary.Type);
        var activityType = requestedActivityType?.Trim().ToLowerInvariant();
        if (activityType is null)
            activityType = isDefinitional ? ActivityTypeGapFill : ActivityTypeShortAnswer;

        var supportedForCategory = isDefinitional
            ? new[] { ActivityTypeGapFill, ActivityTypeMultipleChoiceSingle }
            : new[] { ActivityTypeShortAnswer };
        if (!supportedForCategory.Contains(activityType))
            throw new ActivityDefinitionValidationException(
                $"Activity type '{activityType}' is not supported for resource type '{primary.Type}'. Supported: {string.Join(", ", supportedForCategory)}.");

        var (instructions, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson) = activityType switch
        {
            ActivityTypeGapFill => ComposeGapFill(primary.Snapshot),
            ActivityTypeMultipleChoiceSingle => await ComposeMultipleChoiceSingleAsync(primary.Type, primary.Snapshot, primaryMatch.Input.ResourceId, ct),
            ActivityTypeShortAnswer => ComposeShortAnswer(primary.Snapshot),
            _ => throw new ActivityDefinitionValidationException($"Unsupported activity type '{activityType}'."),
        };

        var schemaCheck = _schemaValidator.ValidateSchema(formSchemaJson);
        if (!schemaCheck.IsValid)
            throw new ActivityDefinitionValidationException($"Generated Form.io schema failed validation: {schemaCheck.Error}");

        var resolvedTitle = !string.IsNullOrWhiteSpace(title) ? title!.Trim() : primary.Snapshot.Title;
        var cefrLevel = defaultCefrLevel ?? primary.Snapshot.CefrLevel;
        var skill = defaultSkill ?? primary.Snapshot.Skill;
        var subskill = defaultSubskill ?? primary.Snapshot.Subskill;
        var contextTags = defaultContextTags is { Count: > 0 }
            ? defaultContextTags
            : MergeTagArrays(resolved.Select(r => r.Snapshot.ContextTagsJson));
        var focusTags = defaultFocusTags is { Count: > 0 }
            ? defaultFocusTags
            : MergeTagArrays(resolved.Select(r => r.Snapshot.FocusTagsJson));
        var difficultyBand = defaultDifficultyBand ?? primary.Snapshot.DifficultyBand;

        var sourceMode = learnItemId.HasValue ? ActivitySourceMode.GeneratedFromLearnItem : ActivitySourceMode.GeneratedFromResources;
        var description = $"Deterministic draft — review and edit before approval. Generated from: "
            + string.Join(", ", resolved.Select(r => r.Snapshot.Title)) + "."
            + (notes is not null ? $" {notes.Trim()}" : string.Empty);

        ActivityDefinition activity;
        try
        {
            activity = new ActivityDefinition(
                resolvedTitle, instructions, activityType, ActivityRendererType.Formio, sourceMode,
                description, patternKey: null, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson,
                cefrLevel, skill, subskill,
                JsonSerializer.Serialize(contextTags), JsonSerializer.Serialize(focusTags),
                difficultyBand, estimatedMinutes: null, learnItemId,
                GenerationProvider, GenerationModel, createdByUserId);
        }
        catch (ArgumentException ex)
        {
            throw new ActivityDefinitionValidationException(ex.Message);
        }

        _db.ActivityDefinitions.Add(activity);
        await _db.SaveChangesAsync(ct);

        var links = resolved
            .Select(r => new ActivityResourceLink(
                activity.Id, r.Type, r.Input.ResourceId, r.Role, r.Snapshot.Title, r.Snapshot.ContentFingerprint))
            .ToList();
        _db.ActivityResourceLinks.AddRange(links);
        await _db.SaveChangesAsync(ct);

        var dto = ActivityDefinitionMappers.ToDto(activity, links);
        return new GenerateActivityDefinitionResult(dto, $"/admin/activities?id={activity.Id}");
    }

    /// <summary>Shows the resource's own definition/description, asks the student to type the
    /// term back — needs no distractor pool, so it's the safe default for any Vocabulary/Grammar
    /// resource regardless of how much other bank content exists.</summary>
    private static (string Instructions, string FormSchemaJson, string AnswerKeyJson, string ScoringRulesJson, string FeedbackPlanJson)
        ComposeGapFill(LearnItemResourceSnapshot primary)
    {
        var term = primary.Title;
        var definition = !string.IsNullOrWhiteSpace(primary.Body) ? primary.Body!.Trim() : "(no definition available)";

        var instructions = "Read the definition below and type the missing word or phrase.";
        var formSchemaJson = JsonSerializer.Serialize(new
        {
            components = new object[]
            {
                new { type = "content", key = "prompt", html = $"<p>{System.Net.WebUtility.HtmlEncode(definition)}</p>" },
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

    /// <summary>Asks "what does X mean" with distractor options pulled from sibling published
    /// resources of the same type. Rejects (throws) rather than degrading to a single-option
    /// "choice" when no usable distractor exists.</summary>
    private async Task<(string Instructions, string FormSchemaJson, string AnswerKeyJson, string ScoringRulesJson, string FeedbackPlanJson)>
        ComposeMultipleChoiceSingleAsync(PublishedResourceType type, LearnItemResourceSnapshot primary, Guid primaryId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(primary.Body))
            throw new ActivityDefinitionValidationException(
                $"Resource '{primary.Title}' has no definition/description text to build a multiple-choice question from — use 'gap_fill' instead.");

        var distractors = await FindDistractorDefinitionsAsync(type, primaryId, primary.CefrLevel, ct);
        if (distractors.Count == 0)
            throw new ActivityDefinitionValidationException(
                "No other published resources of the same type have a definition/description to use as distractors — use 'gap_fill' instead.");

        var options = new List<(string Key, string Text)> { ("opt_0", primary.Body!.Trim()) };
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

    /// <summary>Open-ended comprehension prompt, honestly marked as requiring manual/AI
    /// evaluation — reading comprehension can't be deterministically graded from a short
    /// excerpt/summary.</summary>
    private static (string Instructions, string FormSchemaJson, string AnswerKeyJson, string ScoringRulesJson, string FeedbackPlanJson)
        ComposeShortAnswer(LearnItemResourceSnapshot primary)
    {
        var excerpt = !string.IsNullOrWhiteSpace(primary.Body) ? primary.Body!.Trim() : "(no excerpt available)";

        var instructions = "Read the passage below and answer the question in one or two sentences.";
        var formSchemaJson = JsonSerializer.Serialize(new
        {
            components = new object[]
            {
                new { type = "content", key = "passage", html = $"<p>{System.Net.WebUtility.HtmlEncode(excerpt)}</p>" },
                new { type = "textarea", key = "answer", label = $"What is \"{primary.Title}\" mainly about?", input = true },
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

    private async Task<List<string>> FindDistractorDefinitionsAsync(
        PublishedResourceType type, Guid excludeId, string? cefrLevel, CancellationToken ct)
    {
        List<string?> bodies = type switch
        {
            PublishedResourceType.Vocabulary => await _db.CefrVocabularyEntries
                .Where(e => e.Id != excludeId && e.Notes != null && e.Notes != "")
                .OrderByDescending(e => e.CefrLevel == cefrLevel)
                .ThenBy(e => e.CreatedAt)
                .Take(MaxDistractors)
                .Select(e => e.Notes)
                .ToListAsync(ct),
            PublishedResourceType.Grammar => await _db.CefrGrammarProfileEntries
                .Where(e => e.Id != excludeId && e.Description != null && e.Description != "")
                .OrderByDescending(e => e.CefrLevel == cefrLevel)
                .ThenBy(e => e.CreatedAt)
                .Take(MaxDistractors)
                .Select(e => e.Description)
                .ToListAsync(ct),
            _ => new List<string?>(),
        };

        return bodies.Where(b => !string.IsNullOrWhiteSpace(b)).Select(b => b!.Trim()).Distinct().ToList();
    }

    private static List<string> ParseTags(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }

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
}

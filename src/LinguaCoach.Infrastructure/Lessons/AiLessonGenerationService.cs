using System.Text.Json;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.Lessons;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Lessons;

/// <summary>
/// Phase J2a — AI-assisted "Generate Learn" composer. Builds a pending-review <see cref="Lesson"/>
/// draft from one or more selected published Resource Bank rows, same as
/// <see cref="LessonGenerationService"/>, but generates the teaching prose (title/body/examples/
/// commonMistakes/usageNotes) via an AI provider call instead of copying the resources' own fields
/// verbatim. A deliberately separate action from the deterministic composer — see
/// <see cref="IGenerateLessonFromResourcesWithAiHandler"/>'s doc comment for why. Mirrors
/// <see cref="LinguaCoach.Infrastructure.ResourceImport.ResourceCandidateAnalysisService"/>'s
/// retry-once-on-bad-JSON pattern, but this is a synchronous admin-triggered action whose result
/// the admin is actively waiting on (like <c>ActivityTemplateInstanceGenerator</c>), so failure
/// after the retry throws rather than degrading silently — the admin sees a clear error and can
/// fall back to the deterministic action.
/// </summary>
public sealed class AiLessonGenerationService : IGenerateLessonFromResourcesWithAiHandler
{
    public const string GeneratePromptKey = "lesson_generate_from_resources";

    // Conservative truncation so a single oversized resource can't blow the prompt's token
    // budget, mirroring ResourceCandidateAnalysisService's MaxTextVariableLength discipline.
    private const int MaxResourceTextLength = 2000;

    private readonly LinguaCoachDbContext _db;
    private readonly IAiContextBuilder _contextBuilder;
    private readonly AiExecutionService _aiExecution;
    private readonly ILogger<AiLessonGenerationService> _logger;

    public AiLessonGenerationService(
        LinguaCoachDbContext db,
        IAiContextBuilder contextBuilder,
        AiExecutionService aiExecution,
        ILogger<AiLessonGenerationService> logger)
    {
        _db = db;
        _contextBuilder = contextBuilder;
        _aiExecution = aiExecution;
        _logger = logger;
    }

    public async Task<GenerateLessonFromResourcesResult> HandleAsync(
        GenerateLessonFromResourcesRequest request, CancellationToken ct = default)
    {
        if (request.Resources is not { Count: > 0 })
            throw new LessonValidationException("At least one resource is required to generate a Lesson.");
        if (request.DefaultCefrLevel is not null && !CefrLevelConstants.IsValid(request.DefaultCefrLevel))
            throw new LessonValidationException($"Default CEFR level '{request.DefaultCefrLevel}' is not a valid CEFR level.");
        if (request.DefaultDifficultyBand is < 1 or > 5)
            throw new LessonValidationException("Default difficulty band must be between 1 and 5.");

        var resolved = new List<(LessonResourceLinkInput Input, PublishedResourceType Type, LessonResourceRole Role, LessonResourceSnapshot Snapshot)>();
        foreach (var input in request.Resources)
        {
            if (!LessonResourceLookup.TryParseResourceType(input.ResourceType, out var resourceType))
                throw new LessonValidationException($"Unsupported resource type '{input.ResourceType}'.");
            if (!LessonResourceLookup.TryParseRole(input.Role, out var role))
                throw new LessonValidationException($"Unsupported resource link role '{input.Role}'.");

            var snapshot = await LessonResourceLookup.FindAsync(_db, resourceType, input.ResourceId, ct)
                ?? throw new LessonValidationException(
                    $"Resource '{input.ResourceType}:{input.ResourceId}' was not found in the published Resource Bank.");

            resolved.Add((input, resourceType, role, snapshot));
        }

        var primary = resolved.FirstOrDefault(r => r.Role == LessonResourceRole.Primary).Snapshot ?? resolved[0].Snapshot;
        var cefrLevel = request.DefaultCefrLevel ?? primary.CefrLevel;
        var skill = request.DefaultSkill ?? primary.Skill;
        var subskill = request.DefaultSubskill ?? primary.Subskill;
        var contextTags = request.DefaultContextTags is { Count: > 0 }
            ? request.DefaultContextTags
            : MergeTagArrays(resolved.Select(r => r.Snapshot.ContextTagsJson));
        var focusTags = request.DefaultFocusTags is { Count: > 0 }
            ? request.DefaultFocusTags
            : MergeTagArrays(resolved.Select(r => r.Snapshot.FocusTagsJson));
        var difficultyBand = request.DefaultDifficultyBand ?? primary.DifficultyBand;

        var variables = new Dictionary<string, string>
        {
            ["resourcesSummary"] = BuildResourcesSummary(resolved.Select(r => (r.Type, r.Snapshot.Title, r.Snapshot.Body)).ToList()),
            ["cefrLevel"] = cefrLevel,
            ["skill"] = skill,
            ["subskill"] = subskill ?? "",
            ["contextTags"] = string.Join(", ", contextTags),
            ["focusTags"] = string.Join(", ", focusTags),
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
            _logger.LogWarning(ex, "Failed to build AI Lesson generation prompt. CorrelationId={CorrelationId}", correlationId);
            throw new LessonValidationException(
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
                "AI provider unavailable for AI Lesson generation. CorrelationId={CorrelationId}", correlationId);
            throw new LessonValidationException(
                $"AI generation is currently unavailable: {ex.Message} Use the deterministic Generate action instead.");
        }

        var parsed = TryParseOutput(execResult.ResponseJson, out var parseError);
        if (parsed is null)
        {
            // Retry exactly once on bad/invalid JSON, same as ResourceCandidateAnalysisService.
            try
            {
                execResult = await _aiExecution.ExecuteWithMetaAsync(GeneratePromptKey, aiRequest, null, correlationId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "AI provider unavailable on retry for AI Lesson generation. CorrelationId={CorrelationId}", correlationId);
                throw new LessonValidationException(
                    $"AI generation is currently unavailable: {ex.Message} Use the deterministic Generate action instead.");
            }

            parsed = TryParseOutput(execResult.ResponseJson, out parseError);
            if (parsed is null)
            {
                throw new LessonValidationException(
                    $"AI response could not be parsed after retry: {parseError} Use the deterministic Generate action instead.");
            }
        }

        var usageNotes = $"AI draft ({execResult.ProviderName}/{execResult.ModelName}) — review and edit before approval. "
            + $"Generated from: {string.Join(", ", resolved.Select(r => r.Snapshot.Title))}."
            + (string.IsNullOrWhiteSpace(parsed.UsageNotes) ? "" : $" {parsed.UsageNotes.Trim()}");

        Lesson item;
        try
        {
            item = new Lesson(
                !string.IsNullOrWhiteSpace(request.Title) ? request.Title!.Trim() : parsed.Title,
                parsed.Body,
                LessonSourceMode.GeneratedFromResources,
                cefrLevel, skill, subskill,
                JsonSerializer.Serialize(contextTags), JsonSerializer.Serialize(focusTags),
                JsonSerializer.Serialize(parsed.Examples), JsonSerializer.Serialize(parsed.CommonMistakes),
                usageNotes, difficultyBand, estimatedMinutes: null,
                execResult.ProviderName, execResult.ModelName, request.CreatedByUserId);
        }
        catch (ArgumentException ex)
        {
            throw new LessonValidationException($"AI-generated content failed validation: {ex.Message}");
        }

        _db.Lessons.Add(item);
        await _db.SaveChangesAsync(ct);

        var links = resolved
            .Select(r => new LessonResourceLink(
                item.Id, r.Type, r.Input.ResourceId, r.Role, r.Snapshot.Title, r.Snapshot.ContentFingerprint))
            .ToList();
        _db.LessonResourceLinks.AddRange(links);
        await _db.SaveChangesAsync(ct);

        var dto = LessonMappers.ToDto(item, links);
        return new GenerateLessonFromResourcesResult(dto, $"/admin/lessons?id={item.Id}");
    }

    private static string BuildResourcesSummary(IReadOnlyList<(PublishedResourceType Type, string Title, string? Body)> resources)
    {
        var entries = resources.Select(r =>
            $"- [{r.Type}] {Truncate(r.Title)}"
            + (!string.IsNullOrWhiteSpace(r.Body) ? $": {Truncate(r.Body!)}" : ""));
        return string.Join("\n", entries);
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

    private sealed record AiLessonOutput(string Title, string Body, List<string> Examples, List<string> CommonMistakes, string? UsageNotes);

    /// <summary>Fully defensive JSON-&gt;output parsing. Returns null (with <paramref name="parseError"/>
    /// set) when the response isn't parseable JSON, isn't a JSON object, or is missing a non-empty
    /// title/body — those two fields are the minimum a usable Lesson draft needs.</summary>
    private static AiLessonOutput? TryParseOutput(string rawResponse, out string? parseError)
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
            var body = GetString(root, "body");

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body))
            {
                parseError = "Response is missing a non-empty 'title' or 'body'.";
                return null;
            }

            return new AiLessonOutput(
                title.Trim(), body.Trim(),
                GetStringArray(root, "examples").ToList(),
                GetStringArray(root, "commonMistakes").ToList(),
                GetString(root, "usageNotes"));
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

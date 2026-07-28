using System.Text.Json;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.SkillGraph;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Infrastructure.Ai;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.SkillGraph;

/// <inheritdoc cref="IVocabularyCategorizationService"/>
public sealed class VocabularyCategorizationService : IVocabularyCategorizationService
{
    public const string CategorizeWordsPromptKey = "vocabulary_categorize_words";

    private const int MaxDescriptionLength = 400;

    private readonly IAiContextBuilder _contextBuilder;
    private readonly AiExecutionService _aiExecution;
    private readonly ILogger<VocabularyCategorizationService> _logger;

    public VocabularyCategorizationService(
        IAiContextBuilder contextBuilder, AiExecutionService aiExecution, ILogger<VocabularyCategorizationService> logger)
    {
        _contextBuilder = contextBuilder;
        _aiExecution = aiExecution;
        _logger = logger;
    }

    public async Task<VocabularyCategorizationResult> CategorizeBatchAsync(
        VocabularyCategorizationRequest request, CancellationToken ct = default)
    {
        if (request.Words.Count == 0)
            return new VocabularyCategorizationResult(true, [], null);

        var variables = new Dictionary<string, string>
        {
            ["existingCategories"] = request.ExistingCategories.Count > 0
                ? string.Join(", ", request.ExistingCategories) : "(none yet)",
            ["words"] = string.Join("\n", request.Words.Select(w => $"{w.Headword} ({w.PartOfSpeech}, {w.CefrLevel})")),
            ["contextTags"] = string.Join(", ", CurriculumContextTagConstants.All),
        };

        var correlationId = Guid.NewGuid().ToString("N")[..16];
        AiRequest aiRequest;
        try
        {
            aiRequest = await _contextBuilder.BuildAsync(CategorizeWordsPromptKey, variables, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build vocabulary categorization prompt (non-blocking).");
            return Failure($"Could not build AI prompt: {ex.Message}");
        }

        AiExecutionResult execResult;
        try
        {
            execResult = await _aiExecution.ExecuteWithMetaAsync(CategorizeWordsPromptKey, aiRequest, null, correlationId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI provider unavailable for vocabulary categorization CorrelationId={CorrelationId} (non-blocking).", correlationId);
            return Failure($"AI provider unavailable: {ex.Message}");
        }

        var parsed = TryParseOutput(execResult.ResponseJson, request.Words, out var parseError);
        if (parsed is null)
        {
            // Retry exactly once on bad/invalid JSON, same as SkillGraphDraftingService.
            try
            {
                execResult = await _aiExecution.ExecuteWithMetaAsync(CategorizeWordsPromptKey, aiRequest, null, correlationId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI provider unavailable on retry for vocabulary categorization CorrelationId={CorrelationId} (non-blocking).", correlationId);
                return Failure($"AI provider unavailable on retry: {ex.Message}");
            }

            parsed = TryParseOutput(execResult.ResponseJson, request.Words, out parseError);
            if (parsed is null)
                return Failure($"AI response could not be parsed after retry: {parseError}");
        }

        return new VocabularyCategorizationResult(true, parsed, null);
    }

    private static VocabularyCategorizationResult Failure(string message) => new(false, [], message);

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

    /// <summary>Fully defensive: never throws. A returned (headword, pos) pair that doesn't match
    /// one of the words actually sent in this batch is dropped — the AI cannot invent a
    /// categorization for a word it wasn't given. <c>Category</c>/<c>Description</c> are trusted
    /// as-is (Category may be new, per the user's decision); ContextTags/FocusTags are dropped
    /// unless they're real values from the shared tag vocabulary.</summary>
    private static IReadOnlyList<VocabularyWordCategorization>? TryParseOutput(
        string rawResponse, IReadOnlyList<VocabularyWordRow> requestedWords, out string? parseError)
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
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("words", out var wordsEl)
                || wordsEl.ValueKind != JsonValueKind.Array)
            {
                parseError = "Response is not a JSON object with a 'words' array property.";
                return null;
            }

            var requestedKeys = new HashSet<(string Headword, string Pos)>(
                requestedWords.Select(w => (w.Headword.Trim().ToLowerInvariant(), w.PartOfSpeech.Trim().ToLowerInvariant())));

            var results = new List<VocabularyWordCategorization>();
            var seen = new HashSet<(string, string)>();

            foreach (var w in wordsEl.EnumerateArray())
            {
                if (w.ValueKind != JsonValueKind.Object) continue;

                var headword = GetString(w, "headword")?.Trim();
                var pos = GetString(w, "pos")?.Trim();
                var category = GetString(w, "category")?.Trim();
                var description = GetString(w, "description")?.Trim();
                if (string.IsNullOrWhiteSpace(headword) || string.IsNullOrWhiteSpace(pos)
                    || string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(description))
                    continue;

                var key = (headword.ToLowerInvariant(), pos.ToLowerInvariant());
                if (!requestedKeys.Contains(key)) continue; // never trust a word we didn't ask about
                if (!seen.Add(key)) continue; // drop duplicate entries within the same response

                description = Truncate(description, MaxDescriptionLength);

                var contextTags = ReadValidTags(w, "contextTags");
                var focusTags = ReadValidTags(w, "focusTags");

                results.Add(new VocabularyWordCategorization(headword, pos, category, description, contextTags, focusTags));
            }

            return results;
        }
    }

    private static List<string> ReadValidTags(JsonElement el, string propertyName)
    {
        var tags = new List<string>();
        if (el.TryGetProperty(propertyName, out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in tagsEl.EnumerateArray())
            {
                if (t.ValueKind == JsonValueKind.String && t.GetString() is { } tv && CurriculumContextTagConstants.IsValid(tv))
                    tags.Add(tv.ToLowerInvariant());
            }
        }
        return tags;
    }

    private static string? GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}

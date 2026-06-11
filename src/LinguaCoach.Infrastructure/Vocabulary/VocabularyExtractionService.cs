using System.Text.Json;
using System.Text.Json.Serialization;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.Vocabulary;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Vocabulary;

public sealed class VocabularyExtractionService : IVocabularyExtractionService
{
    private const int MaxExtractedItems = 5;
    private const int KnownTermsSampleSize = 20;
    private const int ExtractionTimeoutSeconds = 8;

    private static readonly HashSet<string> SensitivePatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "@", "http", "www", "password", "secret", "token", "api_key"
    };

    private readonly LinguaCoachDbContext _db;
    private readonly IAiContextBuilder _contextBuilder;
    private readonly AiExecutionService _aiExecution;
    private readonly ILogger<VocabularyExtractionService> _logger;

    public VocabularyExtractionService(
        LinguaCoachDbContext db,
        IAiContextBuilder contextBuilder,
        AiExecutionService aiExecution,
        ILogger<VocabularyExtractionService> logger)
    {
        _db = db;
        _contextBuilder = contextBuilder;
        _aiExecution = aiExecution;
        _logger = logger;
    }

    public async Task ExtractAsync(ExtractVocabularyCommand command, CancellationToken ct = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(ExtractionTimeoutSeconds));

            var profile = await _db.StudentProfiles
                .Include(p => p.CareerProfile)
                .FirstOrDefaultAsync(p => p.UserId == command.UserId, timeout.Token);

            if (profile is null)
            {
                _logger.LogWarning("VocabularyExtraction skipped — profile not found UserId={UserId}", command.UserId);
                return;
            }

            var activity = await _db.LearningActivities
                .FirstOrDefaultAsync(a => a.Id == command.ActivityId, timeout.Token);

            var module = command.ModuleId.HasValue
                ? await _db.LearningModules.FirstOrDefaultAsync(m => m.Id == command.ModuleId.Value, timeout.Token)
                : null;

            // Build known terms sample to avoid duplicates in the prompt
            var knownTerms = await _db.StudentVocabularyItems
                .Where(v => v.StudentProfileId == profile.Id)
                .OrderByDescending(v => v.CreatedAt)
                .Select(v => v.Term)
                .Take(KnownTermsSampleSize)
                .ToListAsync(timeout.Token);

            var contextJson = BuildExtractionContext(
                profile, activity, module,
                command.SubmittedContent, command.FeedbackJson,
                command.ImprovedVersion, knownTerms);

            var aiRequest = await _contextBuilder.BuildAsync(
                DefaultAiSeeder.VocabularyExtractFromAttemptKey,
                new Dictionary<string, string> { ["extractionContext"] = contextJson },
                timeout.Token);

            var response = await _aiExecution.ExecuteAsync(
                DefaultAiSeeder.VocabularyExtractFromAttemptKey,
                aiRequest,
                profile.Id,
                command.CorrelationId,
                timeout.Token);

            var items = ParseItems(response);
            if (items.Count == 0)
            {
                _logger.LogDebug("VocabularyExtraction returned 0 items ActivityId={ActivityId}", command.ActivityId);
                return;
            }

            await SaveItemsAsync(profile.Id, items, command.ActivityAttemptId, command.ActivityId, timeout.Token);
            _logger.LogInformation(
                "VocabularyExtraction saved {Count} items ActivityId={ActivityId} CorrelationId={CorrelationId}",
                items.Count, command.ActivityId, command.CorrelationId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "VocabularyExtraction failed ActivityId={ActivityId} CorrelationId={CorrelationId}",
                command.ActivityId, command.CorrelationId);
        }
    }

    private static string BuildExtractionContext(
        Domain.Entities.StudentProfile profile,
        LearningActivity? activity,
        LearningModule? module,
        string submittedContent,
        string feedbackJson,
        string? improvedVersion,
        IReadOnlyList<string> knownTerms)
    {
        // Parse feedback changes safely
        List<object> changes = [];
        try
        {
            using var doc = JsonDocument.Parse(feedbackJson);
            if (doc.RootElement.TryGetProperty("changes", out var changesEl)
                && changesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in changesEl.EnumerateArray())
                {
                    changes.Add(new
                    {
                        type = c.TryGetProperty("type", out var t) ? t.GetString() : null,
                        original = c.TryGetProperty("original", out var o) ? o.GetString() : null,
                        suggested = c.TryGetProperty("suggested", out var s) ? s.GetString() : null,
                        category = c.TryGetProperty("category", out var cat) ? cat.GetString() : null,
                        severity = c.TryGetProperty("severity", out var sev) ? sev.GetString() : null,
                    });
                }
            }
        }
        catch { /* ignore malformed JSON */ }

        var payload = new
        {
            studentProfile = new
            {
                cefrLevel = profile.CefrLevel ?? "B1",
                careerProfile = profile.CareerProfile?.Name ?? "General workplace",
            },
            activityTitle = activity?.Title ?? "Writing activity",
            moduleTitle = module?.Title,
            studentSubmission = submittedContent,
            feedbackChanges = changes,
            improvedVersion,
            knownTermsSample = knownTerms,
        };

        return JsonSerializer.Serialize(payload);
    }

    private static IReadOnlyList<ExtractedItem> ParseItems(string json)
    {
        try
        {
            var result = JsonSerializer.Deserialize<ExtractionResult>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (result?.Items is null or { Count: 0 }) return [];

            return result.Items
                .Where(i => !string.IsNullOrWhiteSpace(i.Term)
                         && !string.IsNullOrWhiteSpace(i.MeaningOrExplanation)
                         && !LooksSensitive(i.Term)
                         && !LooksSensitive(i.SuggestedPhrase))
                .Take(MaxExtractedItems)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static bool LooksSensitive(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return SensitivePatterns.Any(p => text.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    private async Task SaveItemsAsync(
        Guid studentProfileId,
        IReadOnlyList<ExtractedItem> items,
        Guid attemptId,
        Guid activityId,
        CancellationToken ct)
    {
        foreach (var item in items)
        {
            var normTerm = Domain.Entities.StudentVocabularyItem.NormaliseTerm(item.Term!);
            var normCat = (item.Category ?? "useful_expression").Trim().ToLowerInvariant();

            var existing = await _db.StudentVocabularyItems
                .FirstOrDefaultAsync(v =>
                    v.StudentProfileId == studentProfileId
                    && v.Term == normTerm
                    && v.Category == normCat, ct);

            if (existing is not null)
            {
                existing.RecordSeen();
            }
            else
            {
                var newItem = new StudentVocabularyItem(
                    studentProfileId: studentProfileId,
                    term: normTerm,
                    suggestedPhrase: item.SuggestedPhrase,
                    meaningOrExplanation: item.MeaningOrExplanation!,
                    exampleSentence: item.ExampleSentence,
                    category: normCat,
                    source: VocabularyItemSource.AiExtractedFromWritingAttempt,
                    sourceActivityAttemptId: attemptId,
                    sourceLearningActivityId: activityId);

                _db.StudentVocabularyItems.Add(newItem);
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    // ── JSON deserialization models ───────────────────────────────────────────

    private sealed class ExtractionResult
    {
        [JsonPropertyName("items")]
        public List<ExtractedItem>? Items { get; set; }
    }

    private sealed class ExtractedItem
    {
        [JsonPropertyName("term")] public string? Term { get; set; }
        [JsonPropertyName("suggestedPhrase")] public string? SuggestedPhrase { get; set; }
        [JsonPropertyName("meaningOrExplanation")] public string? MeaningOrExplanation { get; set; }
        [JsonPropertyName("exampleSentence")] public string? ExampleSentence { get; set; }
        [JsonPropertyName("category")] public string? Category { get; set; }
        [JsonPropertyName("reason")] public string? Reason { get; set; }
    }
}

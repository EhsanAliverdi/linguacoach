using System.Text.Json;
using LinguaCoach.Application.LearningPath;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Progress;

/// <summary>
/// Shared service for calculating student progress metrics:
/// - Distinct completed activities per module (not retry counts)
/// - Module average/latest scores
/// - Current focus area from recent feedback changes
/// </summary>
public sealed class StudentProgressService
{
    private const int CompletionThreshold = 3;
    private const double ReadyToCompleteScoreThreshold = 75.0;
    private const int FocusAreaLookbackAttempts = 5;

    private static readonly Dictionary<string, string> CategoryLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["grammar"]     = "Grammar accuracy",
        ["vocabulary"]  = "Professional vocabulary",
        ["tone"]        = "Polite workplace tone",
        ["clarity"]     = "Clear sentence meaning",
        ["structure"]   = "Message organisation",
        ["punctuation"] = "Punctuation and readability",
    };

    private readonly LinguaCoachDbContext _db;

    public StudentProgressService(LinguaCoachDbContext db) => _db = db;

    /// <summary>
    /// Returns a dictionary keyed by moduleId with enriched progress data.
    /// Uses DISTINCT LearningActivityId to avoid counting retries as separate completions.
    /// </summary>
    public async Task<Dictionary<Guid, ModuleProgressData>> GetModuleProgressAsync(
        Guid studentProfileId,
        IReadOnlyList<Guid> moduleIds,
        CancellationToken ct = default)
    {
        if (moduleIds.Count == 0)
            return new Dictionary<Guid, ModuleProgressData>();

        // Fetch all attempts for this student for activities in the given modules.
        var attempts = await _db.ActivityAttempts
            .Where(a => a.StudentProfileId == studentProfileId)
            .Join(
                _db.LearningActivities.Where(la => la.LearningModuleId.HasValue && moduleIds.Contains(la.LearningModuleId!.Value)),
                attempt => attempt.LearningActivityId,
                activity => activity.Id,
                (attempt, activity) => new
                {
                    ModuleId = activity.LearningModuleId!.Value,
                    attempt.LearningActivityId,
                    attempt.Score,
                    attempt.CreatedAt,
                })
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        var result = new Dictionary<Guid, ModuleProgressData>();

        foreach (var moduleId in moduleIds)
        {
            var moduleAttempts = attempts.Where(a => a.ModuleId == moduleId).ToList();

            // Distinct activities = count of unique LearningActivityId values
            var distinctActivityIds = moduleAttempts.Select(a => a.LearningActivityId).Distinct().ToHashSet();
            int distinctCompleted = distinctActivityIds.Count;

            // Score stats across ALL attempts (including retries — retries reflect practice effort)
            var scores = moduleAttempts.Where(a => a.Score.HasValue).Select(a => a.Score!.Value).ToList();
            double? avgScore = scores.Count > 0 ? Math.Round(scores.Average(), 0) : null;
            double? latestScore = scores.Count > 0 ? Math.Round(scores.First(), 0) : null; // already ordered desc

            bool readyToComplete = distinctCompleted >= CompletionThreshold
                && avgScore.HasValue && avgScore.Value >= ReadyToCompleteScoreThreshold;

            result[moduleId] = new ModuleProgressData(
                DistinctCompleted: distinctCompleted,
                TotalRequired: CompletionThreshold,
                AverageScore: avgScore,
                LatestScore: latestScore,
                IsReadyToComplete: readyToComplete);
        }

        return result;
    }

    /// <summary>
    /// Detects the student's current focus area by counting feedback change categories
    /// across the last N ActivityAttempt records.
    /// Returns null if insufficient data.
    /// </summary>
    public async Task<LearningFocusAreaDto?> GetCurrentFocusAreaAsync(
        Guid studentProfileId,
        CancellationToken ct = default)
    {
        var recentFeedback = await _db.ActivityAttempts
            .Where(a => a.StudentProfileId == studentProfileId && a.FeedbackJson != "{}")
            .OrderByDescending(a => a.CreatedAt)
            .Take(FocusAreaLookbackAttempts)
            .Select(a => a.FeedbackJson)
            .ToListAsync(ct);

        if (recentFeedback.Count == 0)
            return null;

        var categoryCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var json in recentFeedback)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("changes", out var changes)
                    || changes.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var change in changes.EnumerateArray())
                {
                    if (change.TryGetProperty("category", out var cat)
                        && cat.ValueKind == JsonValueKind.String
                        && cat.GetString() is { Length: > 0 } category)
                    {
                        categoryCounts.TryGetValue(category, out int count);
                        categoryCounts[category] = count + 1;
                    }
                }
            }
            catch { /* skip malformed JSON */ }
        }

        if (categoryCounts.Count == 0)
            return null;

        var topCategory = categoryCounts.OrderByDescending(kv => kv.Value).First();
        var label = CategoryLabels.TryGetValue(topCategory.Key, out var friendly)
            ? friendly
            : topCategory.Key;

        return new LearningFocusAreaDto(
            Category: topCategory.Key.ToLowerInvariant(),
            FriendlyLabel: label,
            Frequency: topCategory.Value);
    }

    /// <summary>
    /// Builds a RecentMistakesSummary string for ActivityGenerationContext from the focus area.
    /// Returns null if no focus area.
    /// </summary>
    public static string? BuildRecentMistakesSummary(LearningFocusAreaDto? focus)
    {
        if (focus is null) return null;
        return $"Student's most common issue: {focus.FriendlyLabel} ({focus.Category}). Generate an activity that helps practise this.";
    }
}

public sealed record ModuleProgressData(
    int DistinctCompleted,
    int TotalRequired,
    double? AverageScore,
    double? LatestScore,
    bool IsReadyToComplete);

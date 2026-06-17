using LinguaCoach.Application.Learning;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Infrastructure.Learning;

/// <summary>
/// Resolves learning goal context from a StudentProfile using a strict priority chain.
///
/// Priority order:
///   1. ExplicitGoalOverride (from call context)
///   2. LearningGoals list + FocusAreas list  (Phase 10G / 10I structured fields)
///   3. CustomLearningGoal / CustomFocusArea  (free-text structured fields)
///   4. LearningGoalDescription               (legacy v1 field)
///   5. LearningGoal                          (legacy v1 field)
///   6. CareerContext                         (legacy v1 field)
///   7. Fallback: "general English communication" — never workplace-only
/// </summary>
public sealed class LearningGoalContextResolver : ILearningGoalContextResolver
{
    private const int MaxSummaryLength = 200;
    private const int MaxItemLength = 80;
    private const int MaxListItems = 5;

    private static readonly string[] WorkplaceKeywords =
    [
        "workplace", "professional", "business", "office", "career",
        "work", "corporate", "industry", "job", "meeting", "presentation",
        "colleague", "client", "email"
    ];

    public ResolvedLearningGoalContext Resolve(StudentProfile profile, LearningGoalResolutionContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(profile);

        // 1. Explicit override
        if (!string.IsNullOrWhiteSpace(context?.ExplicitGoalOverride))
        {
            var summary = Truncate(context.ExplicitGoalOverride.Trim(), MaxSummaryLength);
            return new ResolvedLearningGoalContext
            {
                ContextSummary = summary,
                Source = "Explicit",
                SupportLanguageCode = profile.SupportLanguageCode,
                SupportLanguageName = profile.SupportLanguageName,
                DifficultyPreference = DescribeDifficulty(profile.DifficultyPreference),
                WorkplaceSpecific = ContainsWorkplaceKeyword(summary),
                LegacyFallbackUsed = false
            };
        }

        // 2 + 3. Structured fields (LearningGoals, FocusAreas, CustomLearningGoal, CustomFocusArea)
        var goalLabels = JoinList(profile.LearningGoals);
        var focusLabels = JoinList(profile.FocusAreas);
        var customGoal = Trim(profile.CustomLearningGoal, MaxItemLength);
        var customFocus = Trim(profile.CustomFocusArea, MaxItemLength);

        if (!string.IsNullOrEmpty(goalLabels) || !string.IsNullOrEmpty(customGoal)
            || !string.IsNullOrEmpty(focusLabels) || !string.IsNullOrEmpty(customFocus))
        {
            var parts = new List<string>(4);
            if (!string.IsNullOrEmpty(goalLabels)) parts.Add(goalLabels);
            if (!string.IsNullOrEmpty(customGoal)) parts.Add(customGoal);
            if (!string.IsNullOrEmpty(focusLabels)) parts.Add($"focus: {focusLabels}");
            if (!string.IsNullOrEmpty(customFocus)) parts.Add($"custom focus: {customFocus}");

            var summary = Truncate(string.Join("; ", parts), MaxSummaryLength);
            var primaryKey = profile.LearningGoals.FirstOrDefault(g => !string.IsNullOrWhiteSpace(g))?.Trim();
            var focusKeys = JoinList(profile.FocusAreas);

            return new ResolvedLearningGoalContext
            {
                PrimaryGoalKey = primaryKey,
                GoalLabels = goalLabels,
                CustomGoal = string.IsNullOrEmpty(customGoal) ? null : customGoal,
                FocusAreaKeys = focusKeys,
                FocusAreaLabels = focusLabels,
                CustomFocusArea = string.IsNullOrEmpty(customFocus) ? null : customFocus,
                ContextSummary = summary,
                Source = "Structured",
                SupportLanguageCode = profile.SupportLanguageCode,
                SupportLanguageName = profile.SupportLanguageName,
                DifficultyPreference = DescribeDifficulty(profile.DifficultyPreference),
                WorkplaceSpecific = ContainsWorkplaceKeyword(summary),
                LegacyFallbackUsed = false
            };
        }

        // 4-6. Legacy fields
        var legacyValue = FirstPresent(
            profile.LearningGoalDescription,
            profile.LearningGoal,
            profile.CareerContext);

        if (!string.IsNullOrEmpty(legacyValue))
        {
            var summary = Truncate(legacyValue, MaxSummaryLength);
            return new ResolvedLearningGoalContext
            {
                ContextSummary = summary,
                Source = "Legacy",
                SupportLanguageCode = profile.SupportLanguageCode,
                SupportLanguageName = profile.SupportLanguageName,
                DifficultyPreference = DescribeDifficulty(profile.DifficultyPreference),
                WorkplaceSpecific = ContainsWorkplaceKeyword(summary),
                LegacyFallbackUsed = true
            };
        }

        // 7. Generic fallback — never workplace-only
        return new ResolvedLearningGoalContext
        {
            ContextSummary = "general English communication",
            Source = "Fallback",
            SupportLanguageCode = profile.SupportLanguageCode,
            SupportLanguageName = profile.SupportLanguageName,
            DifficultyPreference = DescribeDifficulty(profile.DifficultyPreference),
            WorkplaceSpecific = false,
            LegacyFallbackUsed = false
        };
    }

    private static bool ContainsWorkplaceKeyword(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var lower = value.ToLowerInvariant();
        return WorkplaceKeywords.Any(k => lower.Contains(k));
    }

    private static string? JoinList(IReadOnlyList<string> values)
    {
        if (values is null || values.Count == 0) return null;
        var items = values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => Truncate(v.Trim(), MaxItemLength))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxListItems)
            .ToArray();
        return items.Length == 0 ? null : string.Join(", ", items);
    }

    private static string? FirstPresent(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static string? Trim(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return Truncate(value.Trim(), maxLength);
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength].TrimEnd();

    private static string? DescribeDifficulty(DifficultyPreference? preference) => preference switch
    {
        DifficultyPreference.Gentle => "gentle",
        DifficultyPreference.Balanced => "balanced",
        DifficultyPreference.Challenging => "challenging",
        null => null,
        _ => preference.ToString()
    };
}

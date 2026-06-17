using System.Text;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Infrastructure.Ai;

public static class LearnerPreferenceContextFormatter
{
    private const int MaxListItems = 5;
    private const int MaxItemLength = 80;
    private const int MaxCustomLength = 160;
    private const int MaxSectionLength = 500;
    private const int MaxGoalContextLength = 200;

    public static string Build(StudentProfile profile, string? targetLanguageName)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (!HasCapturedPreference(profile))
            return string.Empty;

        var lines = new List<string>();
        AddLine(lines, "Preferred name", profile.PreferredName);
        AddLine(lines, "Learning language", targetLanguageName);
        AddLine(lines, "Support language", profile.SupportLanguageName);

        if (profile.TranslationHelpPreference.HasValue)
            AddLine(lines, "Translation help", DescribeTranslationHelp(profile.TranslationHelpPreference.Value));

        AddList(lines, "Goals", profile.LearningGoals);
        AddLine(lines, "Custom goal", profile.CustomLearningGoal, MaxCustomLength);
        AddList(lines, "Focus areas", profile.FocusAreas);
        AddLine(lines, "Custom focus", profile.CustomFocusArea, MaxCustomLength);

        if (profile.DifficultyPreference.HasValue)
            AddLine(lines, "Difficulty preference", DescribeDifficulty(profile.DifficultyPreference.Value));

        if (!string.IsNullOrWhiteSpace(profile.CefrLevel))
            AddLine(lines, "Current level", $"{profile.CefrLevel.Trim().ToUpperInvariant()} (system-estimated)");

        if (lines.Count == 0)
            return string.Empty;

        var builder = new StringBuilder("Learner preferences:");
        foreach (var line in lines)
            builder.AppendLine().Append("- ").Append(line);

        var result = builder.ToString();
        return result.Length <= MaxSectionLength
            ? result
            : result[..MaxSectionLength].TrimEnd();
    }

    public static string? BuildLearningGoalContext(StudentProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var value = FirstPresent(
            profile.CustomLearningGoal,
            JoinList(profile.LearningGoals),
            profile.LearningGoalDescription,
            profile.LearningGoal,
            profile.CareerContext);

        if (string.IsNullOrWhiteSpace(value))
            return null;

        return Truncate(value, MaxGoalContextLength);
    }

    private static void AddLine(List<string> lines, string label, string? value, int maxLength = MaxItemLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        lines.Add($"{label}: {Truncate(value.Trim(), maxLength)}");
    }

    private static void AddList(List<string> lines, string label, IReadOnlyList<string> values)
    {
        var compact = JoinList(values);
        if (!string.IsNullOrWhiteSpace(compact))
            AddLine(lines, label, compact, MaxListItems * MaxItemLength);
    }

    private static string? JoinList(IReadOnlyList<string> values)
    {
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

    private static bool HasCapturedPreference(StudentProfile profile)
        => !string.IsNullOrWhiteSpace(profile.PreferredName)
           || !string.IsNullOrWhiteSpace(profile.SupportLanguageName)
           || profile.TranslationHelpPreference.HasValue
           || profile.LearningGoals.Any(g => !string.IsNullOrWhiteSpace(g))
           || !string.IsNullOrWhiteSpace(profile.CustomLearningGoal)
           || profile.FocusAreas.Any(f => !string.IsNullOrWhiteSpace(f))
           || !string.IsNullOrWhiteSpace(profile.CustomFocusArea)
           || profile.DifficultyPreference.HasValue;

    private static string DescribeTranslationHelp(TranslationHelpPreference preference) => preference switch
    {
        TranslationHelpPreference.Never => "never",
        TranslationHelpPreference.WhenDifficult => "when difficult",
        TranslationHelpPreference.AlwaysAvailable => "always available",
        _ => preference.ToString()
    };

    private static string DescribeDifficulty(DifficultyPreference preference) => preference switch
    {
        DifficultyPreference.Gentle => "gentle",
        DifficultyPreference.Balanced => "balanced",
        DifficultyPreference.Challenging => "challenging",
        _ => preference.ToString()
    };

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength].TrimEnd();
}

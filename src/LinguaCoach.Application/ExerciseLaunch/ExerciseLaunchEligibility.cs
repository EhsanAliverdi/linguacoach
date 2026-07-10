using System.Text.Json;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.ExerciseLaunch;

/// <summary>
/// Phase H10 — pure, read-only eligibility check for whether an <see cref="Exercise"/>
/// can be launched into a real, runnable practice attempt right now. Shared by
/// <c>IPracticeGymModuleSelectionService</c> (to precompute <c>CanLaunch</c> on suggestions
/// without a network round trip) and <c>IExerciseLaunchService</c> (to re-validate
/// fresh at the moment of launch, since approval/content can change between suggestion and
/// click). Never throws — an unparsable/malformed value degrades to "not eligible" with a
/// student-safe reason, exactly like H6/H7's selection services.
/// </summary>
public static class ExerciseLaunchEligibility
{
    /// <summary>Only these H4-generated activity types are supported for H10 launch. `short_answer`
    /// and any future speaking/long-writing/manual-graded type stay unsupported until a later
    /// phase — see <see cref="ExerciseLaunchDecision"/>'s doc comment for why.</summary>
    public static readonly IReadOnlyCollection<string> SupportedActivityTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "gap_fill", "multiple_choice_single" };

    public static ExerciseLaunchEligibilityResult Evaluate(Exercise exercise)
    {
        if (exercise.ReviewStatus != AdminReviewStatus.Approved)
            return NotEligible("This activity has not been approved yet.");

        if (exercise.RendererType != ExerciseRendererType.Formio)
            return NotEligible("This module contains an activity type that is not launchable yet.");

        if (string.IsNullOrWhiteSpace(exercise.FormSchemaJson))
            return NotEligible("This module contains an activity type that is not launchable yet.");

        if (!IsValidJson(exercise.FormSchemaJson))
            return NotEligible("This module contains an activity type that is not launchable yet.");

        if (!SupportedActivityTypes.Contains(exercise.ActivityType))
            return NotEligible("This module contains an activity type that is not launchable yet. Support for more activity types is planned for a later phase.");

        if (RequiresManualOrAiEvaluation(exercise.ScoringRulesJson))
            return NotEligible("This activity requires manual or AI-assisted review and cannot be auto-scored yet.");

        return new ExerciseLaunchEligibilityResult(true, null);
    }

    private static ExerciseLaunchEligibilityResult NotEligible(string reason) => new(false, reason);

    private static bool IsValidJson(string json)
    {
        try
        {
            using var _ = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Parses <see cref="Exercise.ScoringRulesJson"/> (same
    /// <c>Placement.ScoringRulesDocument</c> shape H4's own generation already produces) and
    /// checks whether any component is flagged as not deterministically scorable. Malformed or
    /// missing scoring rules are treated as "requires review" (fail closed — never silently
    /// launch something that can't actually be scored).</summary>
    private static bool RequiresManualOrAiEvaluation(string? scoringRulesJson)
    {
        if (string.IsNullOrWhiteSpace(scoringRulesJson))
            return true;

        try
        {
            var document = JsonSerializer.Deserialize<Placement.ScoringRulesDocument>(scoringRulesJson);
            if (document?.Components is not { Count: > 0 } components)
                return true;

            return components.Values.Any(c => c.RequiresManualOrAiEvaluation);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return true;
        }
    }
}

public sealed record ExerciseLaunchEligibilityResult(bool CanLaunch, string? UnsupportedReason);

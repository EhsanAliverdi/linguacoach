using System.Text.Json;
using LinguaCoach.Application.Activity;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Activity;

/// <summary>
/// Updates StudentSkillProfile rows for all skills affected by an activity attempt.
///
/// Weighting (configurable via constants):
///   - Primary skill:   70 % of the score signal
///   - Secondary skills share the remaining 30 % equally
///   - When no secondary skills exist, primary gets 100 %
///
/// Skill key source priority:
///   1. ExercisePatternDefinition.PrimarySkill / SecondarySkillsJson  (caller passes these in)
///   2. ActivityType fallback map
///
/// All skill keys are normalised before use. Unknown keys are ignored.
/// Exceptions are swallowed — this must never block activity submission.
/// </summary>
public sealed class MultiSkillProgressService : IMultiSkillProgressService
{
    // ── Weighting constants ────────────────────────────────────────────────────
    // Change these two values to adjust split; they must sum to ≤ 100.
    private const double PrimaryWeightPercent   = 70.0;
    private const double SecondaryWeightPercent = 30.0;

    // Score nudge scale: a delta of 1.0 maps to this many score points.
    private const double ScalePointsPerUnit = 10.0;

    // ── Skill registry ─────────────────────────────────────────────────────────
    // All valid skill keys and their display labels.
    // Must stay in sync with PatternSkillUpdateService.SkillLabels.
    // TODO: extract to a shared SkillRegistry when PatternSkillUpdateService is merged.
    internal static readonly IReadOnlyDictionary<string, string> SkillLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // General skills
            ["listening"]              = "Listening",
            ["speaking"]               = "Speaking",
            ["reading"]                = "Reading",
            ["writing"]                = "Writing",
            ["vocabulary"]             = "Vocabulary",
            ["grammar"]                = "Grammar",
            ["fluency"]                = "Fluency",
            ["pronunciation"]          = "Pronunciation",
            ["confidence"]             = "Confidence",
            // Workplace-specific skills (kept for backwards compat with PatternSkillUpdateService)
            ["grammar_accuracy"]       = "Grammar accuracy",
            ["formal_tone"]            = "Formal workplace tone",
            ["sentence_clarity"]       = "Sentence clarity",
            ["message_structure"]      = "Message structure",
            ["workplace_vocabulary"]   = "Workplace vocabulary",
            ["concise_writing"]        = "Concise writing",
            ["softening_language"]     = "Softening language",
            ["summarising_information"] = "Summarising information",
            ["clarifying_questions"]   = "Clarifying questions",
            ["escalation_language"]    = "Escalation language",
        };

    // ActivityType → (primarySkill, secondarySkills[])
    // Used only when no pattern metadata is available.
    // Do NOT default to workplace or writing for everything.
    private static readonly IReadOnlyDictionary<ActivityType, (string Primary, string[] Secondary)> ActivityTypeFallback =
        new Dictionary<ActivityType, (string, string[])>
        {
            [ActivityType.WritingScenario]        = ("writing",    ["grammar", "vocabulary"]),
            [ActivityType.ListeningComprehension] = ("listening",  ["writing"]),
            [ActivityType.VocabularyPractice]     = ("vocabulary", []),
            [ActivityType.SpeakingRolePlay]       = ("speaking",   ["fluency", "pronunciation"]),
            [ActivityType.ReadingTask]             = ("reading",    ["vocabulary", "grammar"]),
            [ActivityType.PronunciationPractice]   = ("pronunciation", ["speaking"]),
        };

    private readonly LinguaCoachDbContext _db;
    private readonly ILogger<MultiSkillProgressService> _logger;

    public MultiSkillProgressService(LinguaCoachDbContext db, ILogger<MultiSkillProgressService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ── IMultiSkillProgressService ────────────────────────────────────────────

    public async Task<MultiSkillProgressUpdateResult> ApplyAsync(
        MultiSkillProgressUpdateRequest request,
        CancellationToken ct = default)
    {
        try
        {
            if (!request.Completed)
            {
                // For incomplete attempts we do not update skill profiles.
                // Weakness signals are not written on partial/failed attempts to avoid
                // inflating negative signals from abandoned submissions.
                return new MultiSkillProgressUpdateResult([], new Dictionary<string, int>(),
                    "Skipped: attempt not completed.");
            }

            var impacts = BuildImpacts(request.PrimarySkill, request.SecondarySkills, request.NormalizedScore);
            if (impacts.Count == 0)
            {
                return new MultiSkillProgressUpdateResult([], new Dictionary<string, int>(),
                    $"No known skill keys resolved for primary='{request.PrimarySkill}'.");
            }

            var keys = impacts.Select(i => i.Key).ToList();

            var existing = await _db.StudentSkillProfiles
                .Where(x => x.StudentProfileId == request.StudentProfileId && keys.Contains(x.SkillKey))
                .ToDictionaryAsync(x => x.SkillKey, StringComparer.OrdinalIgnoreCase, ct);

            var deltaBySkill = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var impact in impacts)
            {
                var scoreDelta = (int)Math.Round(impact.Delta * ScalePointsPerUnit);
                deltaBySkill[impact.Key] = scoreDelta;

                if (existing.TryGetValue(impact.Key, out var profile))
                {
                    profile.ApplyScoreDelta(scoreDelta);
                }
                else
                {
                    _db.StudentSkillProfiles.Add(new StudentSkillProfile(
                        request.StudentProfileId,
                        impact.Key,
                        impact.Label,
                        StudentSkillProfile.DefaultScorePercent + scoreDelta));
                }
            }

            await _db.SaveChangesAsync(ct);

            var updatedKeys = impacts.Select(i => i.Key).ToList();
            var notes = BuildNotes(request, updatedKeys, deltaBySkill);

            _logger.LogInformation(
                "MultiSkillProgressService updated {Count} skill(s) StudentProfileId={ProfileId} Source={Source} Primary={Primary} IsLowerLevel={IsLower}",
                updatedKeys.Count, request.StudentProfileId, request.Source, request.PrimarySkill, request.IsLowerLevelContent);

            return new MultiSkillProgressUpdateResult(updatedKeys, deltaBySkill, notes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "MultiSkillProgressService failed — skipped StudentProfileId={ProfileId} Source={Source}",
                request.StudentProfileId, request.Source);
            return new MultiSkillProgressUpdateResult([], new Dictionary<string, int>(),
                $"Exception swallowed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public MultiSkillProgressUpdateRequest BuildRequest(
        Guid studentProfileId,
        string? exercisePatternKey,
        string? patternPrimarySkill,
        IReadOnlyList<string>? patternSecondarySkills,
        ActivityType activityType,
        double normalizedScore,
        bool completed,
        string source,
        bool isLowerLevelContent = false,
        string? routingReason = null)
    {
        string primary;
        IReadOnlyList<string> secondary;

        if (!string.IsNullOrWhiteSpace(patternPrimarySkill))
        {
            // Priority 1: pattern metadata
            primary = StudentSkillProfile.NormaliseSkillKey(patternPrimarySkill);
            secondary = (patternSecondarySkills ?? [])
                .Select(s => StudentSkillProfile.NormaliseSkillKey(s))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        else if (ActivityTypeFallback.TryGetValue(activityType, out var fallback))
        {
            // Priority 2: ActivityType fallback
            primary = fallback.Primary;
            secondary = fallback.Secondary;
        }
        else
        {
            // No mapping — use a safe empty primary that will be silently dropped
            primary = string.Empty;
            secondary = [];
        }

        return new MultiSkillProgressUpdateRequest(
            StudentProfileId: studentProfileId,
            PrimarySkill: primary,
            SecondarySkills: secondary,
            NormalizedScore: normalizedScore,
            Completed: completed,
            Source: source,
            IsLowerLevelContent: isLowerLevelContent,
            RoutingReason: routingReason);
    }

    // ── private helpers ────────────────────────────────────────────────────────

    private static List<SkillImpact> BuildImpacts(
        string primarySkill, IReadOnlyList<string> secondarySkills, double normalizedScore)
    {
        var result = new List<SkillImpact>();

        var normPrimary = StudentSkillProfile.NormaliseSkillKey(primarySkill);
        if (!SkillLabels.TryGetValue(normPrimary, out var primaryLabel))
            return result; // unknown primary — nothing to write

        // De-duplicate secondaries; exclude any that equal the primary key
        var validSecondary = secondarySkills
            .Select(s => StudentSkillProfile.NormaliseSkillKey(s))
            .Where(s => !string.IsNullOrWhiteSpace(s) && !s.Equals(normPrimary, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(s => SkillLabels.ContainsKey(s))
            .ToList();

        // Map normalizedScore (0-100) to a -1..1 delta.
        // score ≥ 60 → positive impact; < 60 → negative nudge (scaled proportionally).
        double primaryDelta;
        double secondaryDelta;

        if (validSecondary.Count == 0)
        {
            primaryDelta = ScoreToDelta(normalizedScore);
            secondaryDelta = 0;
        }
        else
        {
            // Apply weighting: primary gets 70%, secondaries share 30%
            var rawDelta = ScoreToDelta(normalizedScore);
            primaryDelta  = rawDelta * (PrimaryWeightPercent   / 100.0);
            secondaryDelta = rawDelta * (SecondaryWeightPercent / 100.0) / validSecondary.Count;
        }

        result.Add(new SkillImpact(normPrimary, primaryLabel, primaryDelta));

        foreach (var secKey in validSecondary)
        {
            result.Add(new SkillImpact(secKey, SkillLabels[secKey], secondaryDelta));
        }

        return result;
    }

    /// Maps a 0-100 score to a clamped -1..1 delta.
    /// 100 → +1.0, 60 → 0, 0 → -1.0 (linear, centred on 60).
    private static double ScoreToDelta(double scorePercent)
    {
        const double pivot = 60.0;
        double raw = scorePercent >= pivot
            ? (scorePercent - pivot) / (100.0 - pivot)   // 0..1
            : -(pivot - scorePercent) / pivot;             // -1..0
        return Math.Clamp(raw, -1.0, 1.0);
    }

    private static string BuildNotes(
        MultiSkillProgressUpdateRequest request,
        IReadOnlyList<string> updatedKeys,
        IReadOnlyDictionary<string, int> deltaBySkill)
    {
        var deltaStr = string.Join(", ", updatedKeys.Select(k => $"{k}:{(deltaBySkill.TryGetValue(k, out var d) ? d : 0):+0;-0;0}"));
        var lowerNote = request.IsLowerLevelContent ? " [lower-level/review content]" : string.Empty;
        return $"Source={request.Source}{lowerNote} Score={request.NormalizedScore:F1} Deltas=[{deltaStr}]";
    }

    private sealed record SkillImpact(string Key, string Label, double Delta);
}

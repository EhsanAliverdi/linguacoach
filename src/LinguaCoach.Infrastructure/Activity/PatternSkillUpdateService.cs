using LinguaCoach.Application.Activity;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Activity;

/// <summary>
/// Applies PatternEvaluationResult.SkillImpacts to StudentSkillProfile rows.
/// Ignores unknown skill keys and clamps deltas so malformed AI output cannot corrupt the profile.
/// </summary>
public sealed class PatternSkillUpdateService
{
    // Allowlist of skill keys that may appear in StudentSkillProfile.
    // Must stay in sync with StudentMemoryService.SkillLabels.
    private static readonly IReadOnlyDictionary<string, string> SkillLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["grammar_accuracy"]      = "Grammar accuracy",
            ["formal_tone"]           = "Formal workplace tone",
            ["sentence_clarity"]      = "Sentence clarity",
            ["message_structure"]     = "Message structure",
            ["workplace_vocabulary"]  = "Workplace vocabulary",
            ["concise_writing"]       = "Concise writing",
            ["softening_language"]    = "Softening language",
            ["summarising_information"] = "Summarising information",
            ["clarifying_questions"]  = "Clarifying questions",
            ["escalation_language"]   = "Escalation language",
        };

    // Map exercise pattern key → primary skill key for fallback when SkillImpacts is empty.
    private static readonly IReadOnlyDictionary<string, string> PatternKeyToSkillKey =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["gap_fill_workplace_phrase"]   = "workplace_vocabulary",
            ["phrase_match"]                = "workplace_vocabulary",
            ["listen_and_gap_fill"]         = "sentence_clarity",
            ["listen_and_answer"]           = "sentence_clarity",
            ["email_reply"]                 = "message_structure",
            ["teams_chat_simulation"]       = "formal_tone",
            ["spoken_response_from_prompt"] = "sentence_clarity",
            ["lesson_reflection"]           = "message_structure",
        };

    /// <summary>
    /// Returns the primary skill key associated with an exercise pattern, for use when
    /// building AI evaluation context (e.g. surfacing the student's current
    /// StudentSkillProfile.ScorePercent for that skill).
    /// </summary>
    public static string? GetPrimarySkillKey(string? exercisePatternKey) =>
        exercisePatternKey is not null && PatternKeyToSkillKey.TryGetValue(exercisePatternKey, out var key)
            ? key
            : null;

    private readonly LinguaCoachDbContext _db;
    private readonly ILogger<PatternSkillUpdateService> _logger;

    public PatternSkillUpdateService(LinguaCoachDbContext db, ILogger<PatternSkillUpdateService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Upserts StudentSkillProfile rows from SkillImpacts. Falls back to a synthetic impact when none provided.
    /// Swallows all exceptions — skill update must never fail activity submission.
    /// </summary>
    public async Task ApplyAsync(
        Guid studentProfileId,
        PatternEvaluationResult evalResult,
        string? exercisePatternKey,
        CancellationToken ct)
    {
        try
        {
            var impacts = BuildEffectiveImpacts(evalResult, exercisePatternKey);
            if (impacts.Count == 0) return;

            var keys = impacts.Select(i => i.NormalisedKey).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var existing = await _db.StudentSkillProfiles
                .Where(x => x.StudentProfileId == studentProfileId && keys.Contains(x.SkillKey))
                .ToDictionaryAsync(x => x.SkillKey, StringComparer.OrdinalIgnoreCase, ct);

            foreach (var impact in impacts)
            {
                // Delta is -1..1; scale to a +/-10 point score nudge.
                var scoreDelta = (int)Math.Round(impact.Delta * 10);

                if (existing.TryGetValue(impact.NormalisedKey, out var profile))
                {
                    profile.ApplyScoreDelta(scoreDelta);
                }
                else
                {
                    _db.StudentSkillProfiles.Add(new StudentSkillProfile(
                        studentProfileId,
                        impact.NormalisedKey,
                        impact.Label,
                        StudentSkillProfile.DefaultScorePercent + scoreDelta));
                }
            }

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "PatternSkillUpdateService updated {Count} skill(s) for StudentProfileId={StudentProfileId} PatternKey={PatternKey}",
                impacts.Count, studentProfileId, exercisePatternKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "PatternSkillUpdateService failed — skipped StudentProfileId={StudentProfileId} PatternKey={PatternKey}",
                studentProfileId, exercisePatternKey);
        }
    }

    // ── private helpers ────────────────────────────────────────────────────────

    private static List<EffectiveImpact> BuildEffectiveImpacts(
        PatternEvaluationResult evalResult, string? exercisePatternKey)
    {
        var result = new List<EffectiveImpact>();

        if (evalResult.SkillImpacts is { Count: > 0 })
        {
            foreach (var impact in evalResult.SkillImpacts)
            {
                var key = StudentSkillProfile.NormaliseSkillKey(impact.SkillKey ?? string.Empty);
                if (!SkillLabels.TryGetValue(key, out var label)) continue; // unknown key — drop
                var clampedDelta = Math.Clamp(impact.Delta, -1.0, 1.0);
                result.Add(new EffectiveImpact(key, label, clampedDelta));
            }
        }
        else if (evalResult.Completed)
        {
            // Synthesise a conservative impact from the evaluation percentage.
            // Positive = good result (not weak); negative = needs work (weak).
            var syntheticKey = exercisePatternKey is not null
                && PatternKeyToSkillKey.TryGetValue(exercisePatternKey, out var mapped)
                ? mapped
                : null;

            if (syntheticKey is not null && SkillLabels.TryGetValue(syntheticKey, out var label))
            {
                var delta = evalResult.Percentage >= 60 ? 0.5 : -0.5;
                result.Add(new EffectiveImpact(syntheticKey, label, delta));
            }
        }

        return result;
    }

    private sealed record EffectiveImpact(string NormalisedKey, string Label, double Delta);
}

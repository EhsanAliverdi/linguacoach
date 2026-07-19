using System.Text.Json;
using LinguaCoach.Application.GoalVector;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.GoalVector;

/// <summary>
/// Adaptive Curriculum Sprint 3 — reads/writes a student's weighted goal vector. Explicit sets
/// overwrite the current weight directly; implicit engagement applies a bounded EMA nudge toward
/// 1.0 (see <see cref="StudentGoalWeight.ApplyImplicitEngagement"/>) — both write to the same
/// per-(student, tag) row, which is the "explicit + implicit blend" this codebase decided on rather
/// than two separate scores to merge later.
/// </summary>
public sealed class StudentGoalVectorService : IStudentGoalVectorService
{
    /// <summary>Step size for implicit engagement drift — deliberately small and fixed (not tuned
    /// per student), per the "bounded, testable, not open-ended ML" design constraint. A single
    /// completed activity can never push a goal tag's weight by more than 10% of its remaining gap
    /// to 1.0.</summary>
    public const double ImplicitEngagementAlpha = 0.1;

    private readonly LinguaCoachDbContext _db;

    public StudentGoalVectorService(LinguaCoachDbContext db) => _db = db;

    public async Task<IReadOnlyList<StudentGoalWeightDto>> GetGoalsAsync(Guid studentId, CancellationToken ct = default)
    {
        return await _db.StudentGoalWeights.AsNoTracking()
            .Where(g => g.StudentId == studentId)
            .OrderByDescending(g => g.Weight)
            .Select(g => new StudentGoalWeightDto(g.GoalTag, g.Weight, g.Source.ToString(), g.UpdatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task SetExplicitWeightAsync(Guid studentId, string goalTag, double weight, CancellationToken ct = default)
    {
        if (!CurriculumContextTagConstants.IsGoalTag(goalTag))
            throw new ArgumentException($"'{goalTag}' is not a recognized goal tag.", nameof(goalTag));

        var existing = await _db.StudentGoalWeights
            .FirstOrDefaultAsync(g => g.StudentId == studentId && g.GoalTag == goalTag, ct);

        if (existing is null)
        {
            _db.StudentGoalWeights.Add(new StudentGoalWeight(studentId, goalTag, weight, StudentGoalWeightSource.Explicit));
        }
        else
        {
            existing.SetExplicitWeight(weight);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task RecordImplicitEngagementAsync(Guid studentId, IReadOnlyList<string> contextTags, CancellationToken ct = default)
    {
        var goalTags = contextTags.Where(CurriculumContextTagConstants.IsGoalTag).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (goalTags.Count == 0) return;

        var existing = await _db.StudentGoalWeights
            .Where(g => g.StudentId == studentId && goalTags.Contains(g.GoalTag))
            .ToListAsync(ct);
        var existingByTag = existing.ToDictionary(g => g.GoalTag, StringComparer.OrdinalIgnoreCase);

        foreach (var tag in goalTags)
        {
            if (existingByTag.TryGetValue(tag, out var row))
            {
                row.ApplyImplicitEngagement(ImplicitEngagementAlpha);
            }
            else
            {
                // First time this student has engaged with this goal tag — start at 0 then apply
                // the same nudge, so a single activity never sets a tag straight to a high weight.
                var fresh = new StudentGoalWeight(studentId, tag, 0, StudentGoalWeightSource.Implicit);
                fresh.ApplyImplicitEngagement(ImplicitEngagementAlpha);
                _db.StudentGoalWeights.Add(fresh);
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Parses a Module/Lesson/Exercise's ContextTagsJson (a plain JSON array of strings,
    /// the same shape used across this codebase) into a list. Defensive: malformed/empty JSON
    /// yields an empty list rather than throwing, since this is called from the attempt-submission
    /// path where a parsing bug must never block a student's real attempt from saving.</summary>
    public static IReadOnlyList<string> ParseContextTags(string? contextTagsJson)
    {
        if (string.IsNullOrWhiteSpace(contextTagsJson)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(contextTagsJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

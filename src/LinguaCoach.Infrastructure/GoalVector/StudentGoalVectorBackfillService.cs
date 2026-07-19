using LinguaCoach.Application.GoalVector;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.GoalVector;

/// <summary>
/// Adaptive Curriculum Sprint 3 — one-time, idempotent backfill of existing students'
/// <c>StudentProfile.LearningGoals</c> onto the new <c>StudentGoalWeight</c> vector. Never
/// overwrites an existing explicit weight (idempotent — safe to run more than once). See
/// <see cref="IStudentGoalVectorBackfillService"/>'s doc comment for why some old keys are
/// intentionally left unmapped.
/// </summary>
public sealed class StudentGoalVectorBackfillService : IStudentGoalVectorBackfillService
{
    /// <summary>Old OnboardingFlowSeeder.cs "learning_goals" key → new
    /// CurriculumContextTagConstants.GoalTags value. Keys with no entry here (pronunciation,
    /// listening_confidence, writing_confidence, exam_inspired_practice) are skill/format
    /// descriptors, not motivations, and are deliberately not mapped.</summary>
    private static readonly IReadOnlyDictionary<string, string> OldKeyToGoalTag = new Dictionary<string, string>
    {
        ["day_to_day"] = Domain.Constants.CurriculumContextTagConstants.DayToDay,
        ["travel"] = Domain.Constants.CurriculumContextTagConstants.Travel,
        ["work"] = Domain.Constants.CurriculumContextTagConstants.Workplace,
        ["study"] = Domain.Constants.CurriculumContextTagConstants.StudyAcademic,
        ["migration"] = Domain.Constants.CurriculumContextTagConstants.MigrationSettlement,
        ["job_interview"] = Domain.Constants.CurriculumContextTagConstants.JobInterviews,
        ["social"] = Domain.Constants.CurriculumContextTagConstants.SocialConversation,
    };

    /// <summary>A backfilled goal is a real signal (the student picked it), but weaker than a
    /// fresh explicit "I want this at 100%" — starts at a moderate default rather than 1.0, so the
    /// student's post-backfill "My Goals" view invites adjustment rather than looking finished.</summary>
    private const double BackfillWeight = 0.6;

    private readonly LinguaCoachDbContext _db;

    public StudentGoalVectorBackfillService(LinguaCoachDbContext db) => _db = db;

    public async Task<GoalVectorBackfillResult> BackfillFromLearningGoalsAsync(CancellationToken ct = default)
    {
        // LearningGoals is a JSON-array column — .Count on it isn't SQL-translatable, so fetch the
        // (small) id+column projection and filter client-side, same pattern already established in
        // this codebase for similarly untranslatable predicates.
        var profiles = (await _db.StudentProfiles.AsNoTracking()
            .Select(p => new { p.Id, p.LearningGoals })
            .ToListAsync(ct))
            .Where(p => p.LearningGoals.Count > 0)
            .ToList();

        var studentsWithMapped = 0;
        var created = 0;
        var skipped = 0;

        foreach (var profile in profiles)
        {
            var mappedTags = profile.LearningGoals
                .Where(OldKeyToGoalTag.ContainsKey)
                .Select(k => OldKeyToGoalTag[k])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (mappedTags.Count == 0) continue;
            studentsWithMapped++;

            var existingTags = await _db.StudentGoalWeights.AsNoTracking()
                .Where(g => g.StudentId == profile.Id && mappedTags.Contains(g.GoalTag))
                .Select(g => g.GoalTag)
                .ToListAsync(ct);
            var existingSet = new HashSet<string>(existingTags, StringComparer.OrdinalIgnoreCase);

            foreach (var tag in mappedTags)
            {
                if (existingSet.Contains(tag))
                {
                    skipped++;
                    continue;
                }

                _db.StudentGoalWeights.Add(new StudentGoalWeight(profile.Id, tag, BackfillWeight, StudentGoalWeightSource.Explicit));
                created++;
            }
        }

        await _db.SaveChangesAsync(ct);

        return new GoalVectorBackfillResult(profiles.Count, studentsWithMapped, created, skipped);
    }
}

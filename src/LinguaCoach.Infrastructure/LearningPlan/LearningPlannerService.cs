using LinguaCoach.Application.LearningPlan;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.LearningPlan;

/// <summary>
/// SQL/system-driven learning planner. Never asks AI to choose vocabulary.
/// Implements SM-2 spaced repetition for review scheduling and a structured
/// selection mix: new + weak/review + mastered-in-context.
/// </summary>
public sealed class LearningPlannerService : ILearningPlanner
{
    // Selection mix limits
    private const int NewWordsMax = 5;
    private const int WeakReviewMax = 3;
    private const int MasteredContextMax = 2;

    // Anti-repetition: exclude words seen within this window or within last N lessons
    private const int AntiRepetitionHours = 24;
    private const int AntiRepetitionLessons = 3;

    private readonly LinguaCoachDbContext _db;
    private readonly ILogger<LearningPlannerService> _logger;

    public LearningPlannerService(LinguaCoachDbContext db, ILogger<LearningPlannerService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<LessonPlan> BuildLessonPlanAsync(Guid studentProfileId, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles
            .Include(p => p.LanguagePair)
                .ThenInclude(lp => lp!.SourceLanguage)
            .Include(p => p.LanguagePair)
                .ThenInclude(lp => lp!.TargetLanguage)
            .Include(p => p.CareerProfile)
            .FirstOrDefaultAsync(p => p.Id == studentProfileId, ct)
            ?? throw new InvalidOperationException($"StudentProfile {studentProfileId} not found.");

        if (profile.LanguagePairId is null || profile.CareerProfileId is null)
            throw new InvalidOperationException("Student profile must have a language pair and career profile set.");

        var languagePairId = profile.LanguagePairId.Value;
        var careerProfileId = profile.CareerProfileId.Value;

        // Determine the student's lesson history for anti-repetition checks
        var lastLessonNumber = await _db.LessonVocabularyLogs
            .Where(l => l.StudentProfileId == studentProfileId)
            .MaxAsync(l => (int?)l.LessonNumber, ct) ?? 0;

        // Words excluded by anti-repetition (seen in last 24h OR last 3 lessons)
        var recentCutoff = DateTime.UtcNow.AddHours(-AntiRepetitionHours);
        var minExcludedLesson = Math.Max(1, lastLessonNumber - AntiRepetitionLessons + 1);

        var recentlySeenEntryIds = await _db.LessonVocabularyLogs
            .Where(l => l.StudentProfileId == studentProfileId
                && (l.OccurredAt >= recentCutoff || l.LessonNumber >= minExcludedLesson))
            .Select(l => l.VocabularyEntryId)
            .Distinct()
            .ToListAsync(ct);

        // Load all existing VocabularyEntries for this student
        var existingEntries = await _db.VocabularyEntries
            .Where(v => v.StudentProfileId == studentProfileId)
            .ToListAsync(ct);

        var existingWords = existingEntries
            .ToDictionary(v => v.Word, v => v, StringComparer.OrdinalIgnoreCase);

        var recentEntryIdSet = new HashSet<Guid>(recentlySeenEntryIds);

        // ── 1. Review words: due for spaced repetition review ─────────────────
        var reviewWords = existingEntries
            .Where(v => v.NextReviewDate.HasValue
                && v.NextReviewDate.Value <= DateTime.UtcNow
                && v.Status != VocabularyStatus.Retired
                && v.Status != VocabularyStatus.Mastered
                && !recentEntryIdSet.Contains(v.Id))
            .OrderBy(v => v.NextReviewDate)
            .Take(WeakReviewMax)
            .ToList();

        // ── 2. Weak words: status=Weak, not yet in review list ─────────────────
        var reviewEntryIds = new HashSet<Guid>(reviewWords.Select(v => v.Id));
        var weakWords = existingEntries
            .Where(v => v.Status == VocabularyStatus.Weak
                && !reviewEntryIds.Contains(v.Id)
                && !recentEntryIdSet.Contains(v.Id))
            .OrderBy(v => v.UpdatedAt)
            .Take(Math.Max(0, WeakReviewMax - reviewWords.Count))
            .ToList();

        var weakReviewWords = reviewWords.Concat(weakWords)
            .Take(WeakReviewMax)
            .ToList();

        // ── 3. Mastered-in-context: reinforce mastered words periodically ──────
        var masteredWords = existingEntries
            .Where(v => v.Status == VocabularyStatus.Mastered
                && !recentEntryIdSet.Contains(v.Id))
            .OrderBy(v => v.LastSeen ?? DateTime.MinValue)
            .Take(MasteredContextMax)
            .ToList();

        // ── 4. New words: from curriculum, not yet tracked for this student ────
        var alreadyKnownWords = new HashSet<string>(existingWords.Keys, StringComparer.OrdinalIgnoreCase);

        var curriculumWords = await _db.CurriculumWordLists
            .Where(c => c.CareerProfileId == careerProfileId
                && c.LanguagePairId == languagePairId)
            .OrderBy(c => c.Priority)
            .ToListAsync(ct);

        var newCurriculumWords = curriculumWords
            .Where(c => !alreadyKnownWords.Contains(c.Word))
            .Take(NewWordsMax)
            .ToList();

        // ── 5. Build VocabItem lists ───────────────────────────────────────────
        var targetVocab = newCurriculumWords
            .Select(c => new VocabItem(c.Word, c.Definition, c.ExampleSentence))
            .ToList();

        var reviewVocab = weakReviewWords
            .Select(v => new VocabItem(v.Word, v.Definition,
                WeaknessNote: v.Status == VocabularyStatus.Weak ? $"Previously incorrect ({v.IncorrectCount}x)" : null))
            .ToList();

        var reinforcementVocab = masteredWords
            .Select(v => new VocabItem(v.Word, v.Definition))
            .ToList();

        // ── 6. Retrieve learning summary for context ──────────────────────────
        var summary = await _db.UserLearningSummaries
            .FirstOrDefaultAsync(s => s.StudentProfileId == studentProfileId, ct);

        _logger.LogDebug(
            "LearningPlanner built plan for student {Id}: {New} new, {Review} review, {Mastered} mastered",
            studentProfileId, targetVocab.Count, reviewVocab.Count, reinforcementVocab.Count);

        return new LessonPlan(
            StudentProfileId: studentProfileId,
            LanguagePairCode: $"{profile.LanguagePair?.SourceLanguage?.Code ?? "fa"}-{profile.LanguagePair?.TargetLanguage?.Code ?? "en"}",
            CefrLevel: profile.CefrLevel ?? "B1",
            CareerContext: profile.CareerProfile?.Name ?? "Document Controller",
            LessonType: LessonType.Writing,
            TargetVocabulary: targetVocab,
            ReviewVocabulary: reviewVocab,
            ReinforcementVocabulary: reinforcementVocab,
            ScenarioTemplate: "writing.exercise.v1",
            WeaknessSummary: summary?.RecentWeaknesses ?? string.Empty,
            RecentLessonSummary: summary?.RecentProgress ?? string.Empty);
    }

    /// <summary>
    /// Applies the SM-2 algorithm to schedule the next review date after an interaction.
    /// quality: 0–5 per SM-2 spec (0–2 = failed, 3–5 = passed with varying ease).
    /// </summary>
    public static (DateTime nextReviewDate, double newEaseFactor) CalculateNextReview(
        VocabularyEntry entry, int quality)
    {
        if (quality < 0 || quality > 5) throw new ArgumentOutOfRangeException(nameof(quality), "SM-2 quality must be 0–5.");

        // Failed: reset interval to 1 day, ease factor unchanged, repetitionCount will reset
        if (quality < 3)
            return (DateTime.UtcNow.AddDays(1), entry.EaseFactor);

        // Passed: calculate interval based on persisted repetition count
        int intervalDays = entry.RepetitionCount switch
        {
            0 => 1,
            1 => 6,
            _ => (int)Math.Round(GetPreviousInterval(entry) * entry.EaseFactor)
        };

        // Update ease factor: EF' = EF + (0.1 - (5-q)*(0.08 + (5-q)*0.02))
        var newEaseFactor = entry.EaseFactor + (0.1 - (5 - quality) * (0.08 + (5 - quality) * 0.02));
        newEaseFactor = Math.Max(1.3, newEaseFactor);  // SM-2 minimum

        return (DateTime.UtcNow.AddDays(intervalDays), newEaseFactor);
    }

    /// <summary>
    /// Calculates a composite mastery score 0.0–1.0 from the entry's counters.
    /// Weighted: correct rate (50%) + status progress (30%) + usage (20%).
    /// </summary>
    public static double CalculateMasteryScore(VocabularyEntry entry)
    {
        var totalAttempts = entry.CorrectCount + entry.IncorrectCount;
        var correctRate = totalAttempts > 0 ? (double)entry.CorrectCount / totalAttempts : 0.0;

        var statusProgress = entry.Status switch
        {
            VocabularyStatus.New => 0.0,
            VocabularyStatus.Seen => 0.1,
            VocabularyStatus.Recognised => 0.25,
            VocabularyStatus.Practised => 0.4,
            VocabularyStatus.Weak => 0.2,
            VocabularyStatus.Learning => 0.65,
            VocabularyStatus.Mastered => 1.0,
            VocabularyStatus.Retired => 1.0,
            _ => 0.0
        };

        var usageScore = Math.Min(1.0, entry.UsageCount / 5.0);  // saturates at 5 usages

        return Math.Round(0.5 * correctRate + 0.3 * statusProgress + 0.2 * usageScore, 3);
    }

    /// <summary>
    /// After a lesson, persists SM-2 schedule and mastery score updates for
    /// all vocabulary entries that appeared in the lesson.
    /// Also writes LessonVocabularyLog rows for anti-repetition tracking.
    /// </summary>
    public async Task RecordLessonOutcomeAsync(
        Guid studentProfileId,
        IReadOnlyList<(Guid EntryId, int Sm2Quality)> outcomes,
        CancellationToken ct = default)
    {
        var entryIds = outcomes.Select(o => o.EntryId).ToList();
        var entries = await _db.VocabularyEntries
            .Where(v => entryIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, ct);

        var lastLesson = await _db.LessonVocabularyLogs
            .Where(l => l.StudentProfileId == studentProfileId)
            .MaxAsync(l => (int?)l.LessonNumber, ct) ?? 0;
        var lessonNumber = lastLesson + 1;

        foreach (var (entryId, quality) in outcomes)
        {
            if (!entries.TryGetValue(entryId, out var entry)) continue;

            var succeeded = quality >= 3;
            var (nextReview, newEaseFactor) = CalculateNextReview(entry, quality);
            entry.ScheduleNextReview(nextReview, newEaseFactor, succeeded);

            var masteryScore = CalculateMasteryScore(entry);
            entry.SetMasteryScore(masteryScore);

            _db.LessonVocabularyLogs.Add(new LessonVocabularyLog(studentProfileId, entryId, lessonNumber));
        }

        await _db.SaveChangesAsync(ct);
    }

    private static double GetPreviousInterval(VocabularyEntry entry)
    {
        if (!entry.NextReviewDate.HasValue) return 1.0;
        return Math.Max(1.0, (entry.NextReviewDate.Value - (entry.LastPractised ?? entry.CreatedAt)).TotalDays);
    }
}

using System.Text.Json;
using LinguaCoach.Application.Activity;
using LinguaCoach.Application.Learning;
using LinguaCoach.Application.LearningPath;
using LinguaCoach.Application.Memory;
using LinguaCoach.Application.Sessions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PatternCatalogEntry = LinguaCoach.Application.Sessions.PatternCatalogEntry;

namespace LinguaCoach.Infrastructure.Sessions;

/// <summary>
/// Deterministic session generator. Creates or retrieves today's LearningSession for a student.
///
/// Rules:
/// - Returns the existing NotStarted or InProgress session for today if one already exists.
/// - Otherwise generates a new session from the student's active module and profile.
/// - Template (exercise step count and kinds) is chosen by PreferredSessionDurationMinutes.
/// - Weak skills from StudentSkillProfile influence which step kinds are promoted.
/// - Domain complexity cap: steps are not assigned topics beyond WorkplaceSeniority + 1.
/// - AI is NOT involved in step selection — only in content generation (future Phase 3).
/// - The first exercise step is kept ready (NotStarted). No LearningActivity is generated yet
///   (that is Phase 3 work — on-demand activity generation per step).
/// </summary>
public sealed class SessionGeneratorService : ISessionGeneratorService
{
    private readonly LinguaCoachDbContext _db;
    private readonly ILearningPathGenerator _pathGenerator;
    private readonly IExerciseTypeRegistry _exerciseTypes;
    private readonly IStudentLearningLedger _ledger;
    private readonly ILearningGoalContextResolver _goalContextResolver;
    private readonly ILogger<SessionGeneratorService> _logger;

    public SessionGeneratorService(
        LinguaCoachDbContext db,
        ILearningPathGenerator pathGenerator,
        IExerciseTypeRegistry exerciseTypes,
        IStudentLearningLedger ledger,
        ILearningGoalContextResolver goalContextResolver,
        ILogger<SessionGeneratorService> logger)
    {
        _db = db;
        _pathGenerator = pathGenerator;
        _exerciseTypes = exerciseTypes;
        _ledger = ledger;
        _goalContextResolver = goalContextResolver;
        _logger = logger;
    }

    public async Task<TodaysSessionResult> GetOrCreateTodaysSessionAsync(
        GetOrCreateTodaysSessionCommand command,
        CancellationToken ct = default)
    {
        var studentProfileId = command.StudentProfileId;

        // Load student profile with related data needed for generation.
        var profile = await _db.StudentProfiles
            .Include(p => p.CareerProfile)
            .FirstOrDefaultAsync(p => p.Id == studentProfileId, ct)
            ?? throw new InvalidOperationException($"StudentProfile {studentProfileId} not found.");

        // ── 1. Return today's existing session if one already exists ──────────
        var existingSession = await FindTodaysSessionAsync(studentProfileId, ct);
        if (existingSession is not null)
        {
            _logger.LogDebug(
                "Returning existing session {SessionId} for student {StudentProfileId}",
                existingSession.Id, studentProfileId);

            return await BuildResultAsync(existingSession, isResuming: existingSession.Status == SessionStatus.InProgress, ct);
        }

        // Prefer the background-generated ready lesson buffer before creating
        // a legacy on-demand session. Page load should consume saved content.
        var readyBufferedSession = await FindNextReadyBufferedSessionAsync(studentProfileId, ct);
        if (readyBufferedSession is not null)
        {
            _logger.LogInformation(
                "Returning ready buffered session {SessionId} for student {StudentProfileId}",
                readyBufferedSession.Id, studentProfileId);

            return await BuildResultAsync(readyBufferedSession, isResuming: false, ct);
        }

        // ── 2. Resolve current module ─────────────────────────────────────────
        var currentModule = await ResolveCurrentModuleAsync(profile.UserId, studentProfileId, ct);

        // ── 3. Load full skill profile scores ─────────────────────────────────
        var skillScores = await _db.StudentSkillProfiles
            .Where(sp => sp.StudentProfileId == studentProfileId)
            .Select(sp => new { sp.SkillKey, sp.ScorePercent })
            .ToDictionaryAsync(sp => sp.SkillKey, sp => sp.ScorePercent, ct);

        // ── 4. Select duration template ───────────────────────────────────────
        var duration = SessionDurationTemplates.NormalizeDuration(profile.PreferredSessionDurationMinutes);
        var template = SessionDurationTemplates.GetTemplate(duration);

        // ── 5. Load recent pattern history (last 10 exercises across all sessions) ──
        // Use a subquery on LearningModuleId via the student's path modules to avoid
        // depending on the nullable LearningSession.StudentProfileId (only set for
        // background-generated sessions).
        var studentModuleIds = await _db.LearningPaths
            .Where(p => p.StudentProfileId == studentProfileId && p.IsActive)
            .SelectMany(p => p.Modules)
            .Select(m => m.Id)
            .ToListAsync(ct);

        IReadOnlyList<string> recentPatternKeys = studentModuleIds.Count == 0
            ? []
            : await _db.SessionExercises
                .Where(e => _db.LearningSessions
                    .Where(s => studentModuleIds.Contains(s.LearningModuleId))
                    .Select(s => s.Id)
                    .Contains(e.LearningSessionId))
                .OrderByDescending(e => e.CreatedAt)
                .Take(10)
                .Select(e => e.ExercisePatternKey)
                .ToListAsync(ct);

        // ── 6. Build catalog entries for the selector ─────────────────────────
        var todayTypes = await _exerciseTypes.GetForTodayAsync(ct);
        var catalogEntries = todayTypes
            .Select(e => new PatternCatalogEntry(
                PatternKey: e.ExercisePatternKey ?? e.Key,
                PrimarySkill: e.PrimarySkill,
                IsEnabled: e.IsEnabled,
                IsReady: string.Equals(e.ImplementationStatus, "ready", StringComparison.OrdinalIgnoreCase),
                SupportsTodayLesson: e.SupportsTodayLesson))
            .ToList();

        // ── 6b. Fetch ledger signals for the selector ─────────────────────────
        var ledgerSignals = await BuildLedgerSignalsAsync(studentProfileId, ct);

        // ── 7. Apply dynamic pattern selection per slot ───────────────────────
        var steps = ApplyDynamicPatternSelection(
            template, skillScores, recentPatternKeys, catalogEntries,
            _goalContextResolver.Resolve(profile, new LearningGoalResolutionContext { Source = "SessionGeneratorService" }).ContextSummary, profile.SkillFocus,
            ledgerSignals);

        // Filter any step whose chosen key is still not in the ready catalog.
        steps = await FilterUnavailableExerciseTypesAsync(steps, ct);
        if (steps.Count == 0)
            throw new InvalidOperationException("No enabled ready exercise types are available for today's lesson.");

        // ── 8. Build session metadata ─────────────────────────────────────────
        var (title, topic, goal, focusSkill) = BuildSessionMetadata(steps, currentModule, profile);

        var memorySnapshot = await BuildMemorySnapshotAsync(studentProfileId, ct);

        // ── 9. Determine order within module ─────────────────────────────────
        var existingSessionCount = currentModule is null ? 0
            : await _db.LearningSessions
                .CountAsync(s => s.LearningModuleId == currentModule.Id, ct);

        var session = new LearningSession(
            learningModuleId: currentModule?.Id ?? EnsureFallbackModuleId(studentProfileId),
            title: title,
            topic: topic,
            sessionGoal: goal,
            durationMinutes: duration,
            focusSkill: focusSkill,
            order: existingSessionCount,
            generatedFromMemorySnapshotJson: memorySnapshot);

        _db.LearningSessions.Add(session);
        await _db.SaveChangesAsync(ct);

        // ── 10. Create ordered SessionExercise rows ───────────────────────────
        foreach (var step in steps)
        {
            var exercise = new SessionExercise(
                learningSessionId: session.Id,
                order: step.Order,
                exercisePatternKey: step.PatternKey,
                primarySkill: step.PrimarySkill,
                instructions: step.Instructions,
                estimatedMinutes: step.EstimatedMinutes);

            _db.SessionExercises.Add(exercise);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Generated new session {SessionId} for student {StudentProfileId} duration={Duration}min steps={StepCount}",
            session.Id, studentProfileId, duration, steps.Count);

        return await BuildResultAsync(session, isResuming: false, ct);
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private async Task<LearningSession?> FindTodaysSessionAsync(Guid studentProfileId, CancellationToken ct)
    {
        // "Today's session" = any NotStarted or InProgress session on the student's
        // active module created on or after today UTC midnight.
        var todayUtc = DateTime.UtcNow.Date;

        // Find the module IDs that belong to this student's active path.
        var moduleIds = await _db.LearningPaths
            .Where(p => p.StudentProfileId == studentProfileId && p.IsActive)
            .SelectMany(p => p.Modules)
            .Select(m => m.Id)
            .ToListAsync(ct);

        if (moduleIds.Count == 0)
            return null;

        return await _db.LearningSessions
            .Where(s => moduleIds.Contains(s.LearningModuleId)
                     && s.Status != SessionStatus.Completed
                     && s.CreatedAt >= todayUtc)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<LearningSession?> FindNextReadyBufferedSessionAsync(Guid studentProfileId, CancellationToken ct)
    {
        return await _db.LearningSessions
            .Where(s => s.StudentProfileId == studentProfileId
                     && s.GenerationStatus == GenerationStatus.Ready
                     && s.Status == SessionStatus.NotStarted)
            .OrderBy(s => s.CourseSequenceNumber ?? int.MaxValue)
            .ThenBy(s => s.ReadyAtUtc ?? s.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<LearningModule?> ResolveCurrentModuleAsync(Guid userId, Guid studentProfileId, CancellationToken ct)
    {
        const int SessionsPerModule = 5;

        var path = await _db.LearningPaths
            .Include(p => p.Modules)
            .FirstOrDefaultAsync(p => p.StudentProfileId == studentProfileId && p.IsActive, ct);

        if (path is null)
        {
            // Lazy generation: student has no path yet (e.g. CourseReady via placement,
            // never visited the legacy activity flow that previously triggered this).
            _logger.LogInformation(
                "No active LearningPath for profile {ProfileId}. Generating default path lazily.",
                studentProfileId);
            await _pathGenerator.GenerateAsync(new GenerateLearningPathCommand(userId), ct);

            path = await _db.LearningPaths
                .Include(p => p.Modules)
                .FirstOrDefaultAsync(p => p.StudentProfileId == studentProfileId && p.IsActive, ct);
        }

        if (path is null || path.Modules.Count == 0)
            return null;

        var modules = path.Modules.OrderBy(m => m.Order).ToList();
        var moduleIds = modules.Select(m => m.Id).ToList();

        // Count completed sessions per module.
        var completedCounts = await _db.LearningSessions
            .Where(s => moduleIds.Contains(s.LearningModuleId) && s.Status == SessionStatus.Completed)
            .GroupBy(s => s.LearningModuleId)
            .Select(g => new { ModuleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ModuleId, x => x.Count, ct);

        // Current module = first one that hasn't yet reached the sessions-per-module threshold.
        return modules.FirstOrDefault(m =>
            completedCounts.GetValueOrDefault(m.Id, 0) < SessionsPerModule)
            ?? modules.Last();
    }


    private async Task<List<ExerciseStepTemplate>> FilterUnavailableExerciseTypesAsync(
        IReadOnlyList<ExerciseStepTemplate> steps,
        CancellationToken ct)
    {
        var todayTypes = await _exerciseTypes.GetForTodayAsync(ct);
        var availableKeys = todayTypes
            .Select(e => e.ExercisePatternKey ?? e.Key)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var filtered = steps.Where(s => availableKeys.Contains(s.PatternKey)).ToList();
        if (filtered.Count != steps.Count)
        {
            _logger.LogWarning(
                "Today generation removed {Count} disabled or unavailable exercise step(s).",
                steps.Count - filtered.Count);
        }

        return filtered;
    }

    private List<ExerciseStepTemplate> ApplyDynamicPatternSelection(
        IReadOnlyList<ExerciseStepTemplate> template,
        IReadOnlyDictionary<string, int> skillScores,
        IReadOnlyList<string> recentPatternKeys,
        IReadOnlyList<PatternCatalogEntry> catalogEntries,
        string? learningGoalContext,
        SkillFocus? skillFocus,
        LedgerSignals? ledgerSignals = null)
    {
        // Augment skill scores with skill-focus signal when profile is sparse.
        var effectiveScores = skillScores.Count > 0
            ? skillScores
            : BuildFallbackScores(skillFocus);

        var steps = new List<ExerciseStepTemplate>(template.Count);
        foreach (var step in template)
        {
            // Review slots are fixed — no dynamic selection needed.
            if (step.Kind == ExerciseKind.Review)
            {
                steps.Add(step);
                continue;
            }

            var input = new PatternSelectionInput(
                CefrLevel: null,
                SkillScores: effectiveScores,
                LearningGoalContext: learningGoalContext,
                RecentPatternKeys: recentPatternKeys,
                CandidatePatternKeys: step.GetCandidates(),
                SlotPrimarySkill: step.PrimarySkill,
                AvailableCatalog: catalogEntries,
                Ledger: ledgerSignals);

            var result = DynamicPatternSelector.Select(input);

            _logger.LogDebug(
                "DynamicPatternSelector: slot={Kind} {Reason} fallback={IsFallback}",
                step.Kind, result.Reason, result.IsFallback);

            // Keep Kind aligned with the chosen pattern when it differs from the template default.
            var resolvedKind = result.SelectedPatternKey != step.PatternKey
                ? ResolveKind(result.SelectedPatternKey)
                : step.Kind;

            steps.Add(step with
            {
                Kind = resolvedKind,
                PatternKey = result.SelectedPatternKey,
                PrimarySkill = result.TargetSkill
            });
        }

        return steps;
    }

    private static IReadOnlyDictionary<string, int> BuildFallbackScores(SkillFocus? skillFocus)
    {
        if (!skillFocus.HasValue)
            return new Dictionary<string, int>();

        var focusLabel = SkillFocusToLabel(skillFocus.Value);
        return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            // Score the focus skill low so the selector treats it as a priority.
            [focusLabel] = StudentSkillProfile.WeakThreshold - 10
        };
    }

    private static (string title, string topic, string goal, string focusSkill) BuildSessionMetadata(
        List<ExerciseStepTemplate> steps,
        LearningModule? module,
        StudentProfile profile)
    {
        // Determine dominant skill from template steps.
        var focusSkill = steps
            .GroupBy(s => s.PrimarySkill)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault() ?? "workplace communication";

        var moduleTopic = module?.Title ?? "Professional workplace communication";
        var careerContext = profile.CareerProfile?.Name ?? profile.CareerContext ?? "workplace professional";

        var topic = $"{moduleTopic} — {careerContext}";
        var title = $"{moduleTopic}";
        var goal = $"Practise {focusSkill.ToLowerInvariant()} skills in a {careerContext.ToLowerInvariant()} context.";

        return (title, topic, goal, focusSkill);
    }

    private async Task<string?> BuildMemorySnapshotAsync(Guid studentProfileId, CancellationToken ct)
    {
        try
        {
            var summary = await _db.UserLearningSummaries
                .FirstOrDefaultAsync(s => s.StudentProfileId == studentProfileId, ct);

            if (summary is null)
                return null;

            var snapshot = new
            {
                weaknesses = summary.RecentWeaknesses,
                weakSkills = summary.WeakSkillsJson,
                nextFocus = summary.NextFocusJson
            };

            return JsonSerializer.Serialize(snapshot);
        }
        catch
        {
            // Memory snapshot is advisory — never block session creation.
            return null;
        }
    }

    /// <summary>
    /// Fetches ledger-derived signals used by DynamicPatternSelector.
    /// Best-effort — returns null on any error so session generation is never blocked.
    /// When the ledger has no events for this student, returns empty-list signals
    /// (not null) so the selector still gets a non-null LedgerSignals and can
    /// fall back to 10A behaviour gracefully.
    /// </summary>
    private async Task<LedgerSignals?> BuildLedgerSignalsAsync(Guid studentProfileId, CancellationToken ct)
    {
        try
        {
            // Fetch recent pattern keys from the ledger (replaces the ad-hoc
            // SessionExercise history query for repetition avoidance).
            var recentKeys = await _ledger.GetRecentPatternKeysAsync(studentProfileId, limit: 20, ct);

            // Fetch weak events (NeedsReview / Failed outcomes).
            var weakEvents = await _ledger.GetWeakEventsAsync(studentProfileId, limit: 20, ct);
            var weakPatternKeys = weakEvents
                .Where(e => !string.IsNullOrWhiteSpace(e.PatternKey))
                .Select(e => e.PatternKey!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Fetch recent events to identify mastered patterns and goal context.
            var recentEvents = await _ledger.GetRecentAsync(studentProfileId, limit: 20, ct);
            var masteredPatternKeys = recentEvents
                .Where(e => e.Outcome == LearningEventOutcome.Mastered
                            && !string.IsNullOrWhiteSpace(e.PatternKey))
                .Select(e => e.PatternKey!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Derive goal context from the most recent event that had one set.
            // Never defaults to "workplace" — stays null if unset.
            var ledgerGoalContext = recentEvents
                .FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.LearningGoalContext))
                ?.LearningGoalContext;

            _logger.LogDebug(
                "LedgerSignals for {StudentProfileId}: recentKeys={R} weakKeys={W} masteredKeys={M} goalContext={G}",
                studentProfileId, recentKeys.Count, weakPatternKeys.Count, masteredPatternKeys.Count, ledgerGoalContext ?? "null");

            return new LedgerSignals(
                RecentPatternKeys: recentKeys,
                WeakPatternKeys: weakPatternKeys,
                MasteredPatternKeys: masteredPatternKeys,
                LedgerGoalContext: ledgerGoalContext);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build ledger signals for student {StudentProfileId}; falling back to no-ledger selection.", studentProfileId);
            return null;
        }
    }

    private async Task<TodaysSessionResult> BuildResultAsync(
        LearningSession session,
        bool isResuming,
        CancellationToken ct)
    {
        var exercises = await _db.SessionExercises
            .Where(e => e.LearningSessionId == session.Id)
            .OrderBy(e => e.Order)
            .ToListAsync(ct);

        var exerciseResults = exercises
            .Select(e => new SessionExerciseResult(
                ExerciseId: e.Id,
                Order: e.Order,
                Kind: ResolveKind(e.ExercisePatternKey),
                ExercisePatternKey: e.ExercisePatternKey,
                PrimarySkill: e.PrimarySkill,
                Instructions: e.Instructions,
                EstimatedMinutes: e.EstimatedMinutes,
                Status: e.Status,
                LearningActivityId: e.LearningActivityId))
            .ToList();

        return new TodaysSessionResult(
            SessionId: session.Id,
            Title: session.Title,
            Topic: session.Topic,
            SessionGoal: session.SessionGoal,
            DurationMinutes: session.DurationMinutes,
            FocusSkill: session.FocusSkill,
            Status: session.Status,
            IsResuming: isResuming,
            Exercises: exerciseResults);
    }

    // Maps a pattern key back to an ExerciseKind for the result DTO.
    // This is a best-effort reverse lookup for display purposes only.
    private static ExerciseKind ResolveKind(string patternKey) => patternKey switch
    {
        "phrase_match"
            or "gap_fill_workplace_phrase"                           => ExerciseKind.VocabularyWarmup,
        "listen_and_answer"
            or "listen_and_gap_fill"                                 => ExerciseKind.ListeningInput,
        "email_reply"
            or "teams_chat_simulation"
            or "writing_response"                                    => ExerciseKind.WritingTask,
        "spoken_response_from_prompt"
            or "speaking_role_play"                                  => ExerciseKind.SpeakingTask,
        "lesson_reflection"                                          => ExerciseKind.Review,
        _ when patternKey.StartsWith("listen",   StringComparison.OrdinalIgnoreCase) => ExerciseKind.ListeningInput,
        _ when patternKey.StartsWith("speaking", StringComparison.OrdinalIgnoreCase) => ExerciseKind.SpeakingTask,
        _ when patternKey.StartsWith("writing",  StringComparison.OrdinalIgnoreCase) => ExerciseKind.WritingTask,
        _ => ExerciseKind.ContextInput
    };

    private static string NormalizeSkillLabel(string skillKey)
        => skillKey.Replace("_", " ").ToLowerInvariant() switch
        {
            "writing" or "writing_scenario" => "Writing",
            "listening" or "listening_comprehension" => "Listening",
            "speaking" or "speaking_role_play" => "Speaking",
            "vocabulary" or "vocabulary_practice" => "Vocabulary",
            _ => skillKey
        };

    private static string SkillFocusToLabel(SkillFocus focus) => focus switch
    {
        SkillFocus.Writing => "Writing",
        SkillFocus.Listening => "Listening",
        SkillFocus.Speaking => "Speaking",
        SkillFocus.Vocabulary => "Vocabulary",
        _ => "Writing"
    };

    // Fallback: when there is no active learning path/module, we cannot create a session
    // without a valid LearningModuleId. Callers should ensure the path is generated first.
    // This path should not be reached in normal flow (CourseReady students have a path).
    private static Guid EnsureFallbackModuleId(Guid studentProfileId)
        => throw new InvalidOperationException(
            $"Cannot generate a session for student {studentProfileId}: no active LearningPath or modules found. " +
            "Ensure the student's LearningPath is generated before requesting a session.");
}

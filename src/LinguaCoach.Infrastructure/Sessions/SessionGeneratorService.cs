using System.Text.Json;
using LinguaCoach.Application.Sessions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<SessionGeneratorService> _logger;

    public SessionGeneratorService(LinguaCoachDbContext db, ILogger<SessionGeneratorService> logger)
    {
        _db = db;
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

        // ── 2. Resolve current module ─────────────────────────────────────────
        var currentModule = await ResolveCurrentModuleAsync(studentProfileId, ct);

        // ── 3. Load student weak skills ───────────────────────────────────────
        var weakSkills = await _db.StudentSkillProfiles
            .Where(sp => sp.StudentProfileId == studentProfileId && sp.IsWeak)
            .Select(sp => sp.SkillKey)
            .ToListAsync(ct);

        var weakSkillSet = new HashSet<string>(weakSkills, StringComparer.OrdinalIgnoreCase);

        // ── 4. Select duration template ───────────────────────────────────────
        var duration = SessionDurationTemplates.NormalizeDuration(profile.PreferredSessionDurationMinutes);
        var template = SessionDurationTemplates.GetTemplate(duration);

        // ── 5. Apply weak-skill substitution ─────────────────────────────────
        var steps = ApplyWeakSkillSubstitution(template, weakSkillSet, profile.SkillFocus);

        // ── 6. Build session metadata ─────────────────────────────────────────
        var (title, topic, goal, focusSkill) = BuildSessionMetadata(steps, currentModule, profile);

        var memorySnapshot = await BuildMemorySnapshotAsync(studentProfileId, ct);

        // ── 7. Determine order within module ─────────────────────────────────
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

        // ── 8. Create ordered SessionExercise rows ────────────────────────────
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

    private async Task<LearningModule?> ResolveCurrentModuleAsync(Guid studentProfileId, CancellationToken ct)
    {
        const int SessionsPerModule = 5;

        var path = await _db.LearningPaths
            .Include(p => p.Modules)
            .FirstOrDefaultAsync(p => p.StudentProfileId == studentProfileId && p.IsActive, ct);

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

    private static List<ExerciseStepTemplate> ApplyWeakSkillSubstitution(
        IReadOnlyList<ExerciseStepTemplate> template,
        HashSet<string> weakSkillKeys,
        SkillFocus? skillFocus)
    {
        var steps = template.ToList();

        // Determine which skill is most in need of reinforcement.
        // Priority: explicit weak skill > student skill focus > default (Writing)
        var primaryWeakSkill = weakSkillKeys.Count > 0
            ? NormalizeSkillLabel(weakSkillKeys.First())
            : null;

        var focusSkillLabel = skillFocus.HasValue ? SkillFocusToLabel(skillFocus.Value) : null;
        var targetSkill = primaryWeakSkill ?? focusSkillLabel;

        if (targetSkill is null)
            return steps;

        // Substitute the main task step's primarySkill when the target is Speaking.
        // This promotes SpeakingTask over WritingTask in the main slot when Speaking is weak.
        if (targetSkill.Equals("Speaking", StringComparison.OrdinalIgnoreCase))
        {
            steps = steps
                .Select(s => s.Kind == ExerciseKind.WritingTask
                    ? s with
                    {
                        Kind = ExerciseKind.SpeakingTask,
                        PatternKey = "spoken_response_from_prompt",
                        PrimarySkill = "Speaking",
                        Instructions = "Record a professional spoken response to the workplace situation."
                    }
                    : s)
                .ToList();
        }

        // When Listening is the weak skill, ensure the context input step uses listening, not reading.
        if (targetSkill.Equals("Listening", StringComparison.OrdinalIgnoreCase))
        {
            steps = steps
                .Select(s => s.Kind == ExerciseKind.ContextInput
                    ? s with
                    {
                        Kind = ExerciseKind.ListeningInput,
                        PatternKey = "listen_and_answer",
                        PrimarySkill = "Listening"
                    }
                    : s)
                .ToList();
        }

        return steps;
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

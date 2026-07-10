using System.Text.Json;
using LinguaCoach.Application.Admin;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.LearningPlan;
using LinguaCoach.Application.Notifications;
using LinguaCoach.Application.Speaking;
using LinguaCoach.Application.Storage;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Admin;

public sealed class AdminHandler :
    IAdminStudentQuery,
    IAdminPromptHandler,
    IAdminAiConfigHandler
{
    private readonly LinguaCoachDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAiProviderTester _tester;
    private readonly IServiceProvider _services;
    private readonly IAiProviderResolver _providerResolver;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<AdminHandler> _logger;
    private readonly IConfiguration _configuration;
    private readonly INotificationService _notifications;

    public AdminHandler(
        LinguaCoachDbContext db,
        UserManager<ApplicationUser> userManager,
        IAiProviderTester tester,
        IAiProviderResolver providerResolver,
        IServiceProvider services,
        IFileStorageService fileStorage,
        ILogger<AdminHandler> logger,
        IConfiguration configuration,
        INotificationService notifications)
    {
        _db = db;
        _userManager = userManager;
        _tester = tester;
        _providerResolver = providerResolver;
        _services = services;
        _fileStorage = fileStorage;
        _logger = logger;
        _configuration = configuration;
        _notifications = notifications;
    }

    // ── Students ──────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<StudentListItem>> ListStudentsAsync(bool includeArchived = false, CancellationToken ct = default)
    {
        var query = _db.StudentProfiles.AsQueryable();
        if (!includeArchived)
            query = query.Where(p => p.LifecycleStage != StudentLifecycleStage.Archived);

        var profiles = await query
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);

        // Batch-load all Identity users matching the student profile user IDs.
        var userIds = profiles.Select(p => p.UserId).ToList();
        var users = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);

        return profiles
            .Where(p => users.ContainsKey(p.UserId))
            .Select(p => ToStudentListItem(p, users[p.UserId].Email ?? string.Empty))
            .ToList();
    }

    public async Task<PagedResponse<StudentListItem>> ListStudentsPagedAsync(StudentListQuery listQuery, CancellationToken ct = default)
    {
        var page = Math.Max(1, listQuery.Page);
        var pageSize = Math.Min(100, Math.Max(1, listQuery.PageSize));

        // Join profiles with identity users so we can search on email.
        // We materialise a joined projection first, then apply in-memory sort.
        var profilesQ = _db.StudentProfiles.AsQueryable();

        if (!listQuery.IncludeArchived)
            profilesQ = profilesQ.Where(p => p.LifecycleStage != StudentLifecycleStage.Archived);

        if (!string.IsNullOrWhiteSpace(listQuery.LifecycleStage) &&
            Enum.TryParse<StudentLifecycleStage>(listQuery.LifecycleStage, ignoreCase: true, out var lifecycleEnum))
            profilesQ = profilesQ.Where(p => p.LifecycleStage == lifecycleEnum);

        if (!string.IsNullOrWhiteSpace(listQuery.OnboardingStatus) &&
            Enum.TryParse<OnboardingStatus>(listQuery.OnboardingStatus, ignoreCase: true, out var onboardingEnum))
            profilesQ = profilesQ.Where(p => p.OnboardingStatus == onboardingEnum);

        if (!string.IsNullOrWhiteSpace(listQuery.CefrLevel))
            profilesQ = profilesQ.Where(p => p.CefrLevel == listQuery.CefrLevel);

        // Load profiles + batch-load users so we can apply email search.
        var profiles = await profilesQ.ToListAsync(ct);
        var userIds = profiles.Select(p => p.UserId).ToList();
        var users = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);

        // Build joined items (filter out profiles with no matching identity user).
        var joined = profiles
            .Where(p => users.ContainsKey(p.UserId))
            .Select(p => (profile: p, email: users[p.UserId].Email ?? string.Empty))
            .ToList();

        // Apply search across email, displayName, firstName, lastName.
        if (!string.IsNullOrWhiteSpace(listQuery.Search))
        {
            var term = listQuery.Search.Trim().ToLowerInvariant();
            joined = joined.Where(x =>
                x.email.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (x.profile.DisplayName ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (x.profile.FirstName ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (x.profile.LastName ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        var totalCount = joined.Count;

        // Sorting.
        var sortDir = string.Equals(listQuery.SortDir, "asc", StringComparison.OrdinalIgnoreCase) ? 1 : -1;
        joined = (listQuery.SortBy?.ToLowerInvariant() switch
        {
            "student" or "name" => joined.OrderBy(x =>
                (x.profile.DisplayName ?? string.Empty + x.profile.FirstName ?? string.Empty + x.profile.LastName ?? string.Empty),
                StringComparer.OrdinalIgnoreCase),
            "email" => joined.OrderBy(x => x.email, StringComparer.OrdinalIgnoreCase),
            "onboardingstatus" => joined.OrderBy(x => x.profile.OnboardingStatus.ToString(), StringComparer.OrdinalIgnoreCase),
            "lifecyclestage" => joined.OrderBy(x => x.profile.LifecycleStage.ToString(), StringComparer.OrdinalIgnoreCase),
            "cefrlevel" => joined.OrderBy(x => x.profile.CefrLevel ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            _ => joined.OrderBy(x => x.profile.CreatedAt),
        }).ToList();

        if (sortDir == -1)
            joined = Enumerable.Reverse(joined).ToList();

        var items = joined
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToStudentListItem(x.profile, x.email))
            .ToList();

        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        return new PagedResponse<StudentListItem>(items, totalCount, page, pageSize, totalPages);
    }

    public async Task<AdminStudentDetailDto?> GetStudentDetailAsync(Guid studentProfileId, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == studentProfileId, ct);

        if (profile is null)
            return null;

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == profile.UserId, ct);

        if (user is null)
            return null;

        // Onboarding is now a one-shot Form.io submission (StudentFlowTemplate model) rather than
        // a per-step progress row — surface the latest submission's coarse status instead.
        // Avoid OrderBy on DateTimeOffset — SQLite (integration tests) does not support it.
        var submission = await _db.StudentFlowSubmissions
            .AsNoTracking()
            .Where(s => s.StudentId == profile.UserId && s.FlowKind == StudentFlowKind.Onboarding)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var submissionComplete = submission?.Status == StudentFlowSubmissionStatus.Evaluated;
        StudentOnboardingProgressInfo? progressInfo = submission is null ? null : new StudentOnboardingProgressInfo(
            null,
            Array.Empty<string>(),
            submissionComplete ? 100 : 0,
            submission.StartedAt,
            submission.EvaluatedAt,
            submissionComplete,
            profile.CefrLevel);

        // Phase 14B — learning readiness fields
        var learningReadyStages = new[]
        {
            StudentLifecycleStage.CourseReady,
            StudentLifecycleStage.InLesson,
            StudentLifecycleStage.ActiveLearning,
            StudentLifecycleStage.Paused,
        };
        var isLearningReady = learningReadyStages.Contains(profile.LifecycleStage);

        var lastPlacementCompletedAt = await _db.PlacementAssessments
            .AsNoTracking()
            .Where(a => a.StudentProfileId == profile.Id && a.Status == PlacementStatus.Completed)
            .OrderByDescending(a => a.CompletedAtUtc)
            .Select(a => a.CompletedAtUtc)
            .FirstOrDefaultAsync(ct);

        var learningPlanExists = await _db.StudentLearningPlans
            .AsNoTracking()
            .AnyAsync(p => p.StudentProfileId == profile.Id
                && (p.Status == LearningPlanStatus.Active
                    || p.Status == LearningPlanStatus.Regenerating), ct);

        return new AdminStudentDetailDto(
            profile.Id,
            profile.UserId,
            user.Email ?? string.Empty,
            profile.FirstName,
            profile.LastName,
            profile.DisplayName,
            profile.PreferredName,
            profile.LifecycleStage.ToString(),
            profile.OnboardingStatus.ToString(),
            null, // LastCompletedStep — derived from progress if needed
            profile.CefrLevel,
            profile.CareerContext,
            profile.LearningGoal,
            profile.LearningGoalDescription,
            profile.DifficultSituationsText,
            profile.PreferredSessionDurationMinutes,
            profile.ProfessionalExperienceLevel,
            profile.RoleFamiliarity,
            profile.CreatedAt,
            null, // ArchivedAt — not tracked on StudentProfile currently
            profile.SupportLanguageCode,
            profile.SupportLanguageName,
            profile.DifficultyPreference?.ToString(),
            profile.TranslationHelpPreference?.ToString(),
            profile.FocusAreas ?? [],
            profile.CustomFocusArea,
            profile.LearningGoals ?? [],
            profile.CustomLearningGoal,
            profile.LearningPreferencesUpdatedAt,
            progressInfo,
            isLearningReady,
            lastPlacementCompletedAt,
            learningPlanExists);
    }

    public async Task<AdminStatsItem> GetStatsAsync(CancellationToken ct = default)
    {
        var totalStudents = await _db.StudentProfiles
            .Where(p => p.LifecycleStage != StudentLifecycleStage.Archived)
            .CountAsync(ct);

        var onboardedStudents = await _db.StudentProfiles
            .Where(p => p.LifecycleStage != StudentLifecycleStage.Archived && p.OnboardingStatus == OnboardingStatus.Complete)
            .CountAsync(ct);

        var totalActivityAttempts = await _db.ActivityAttempts.CountAsync(ct);

        return new AdminStatsItem(totalStudents, onboardedStudents, totalActivityAttempts);
    }

    public async Task<IReadOnlyList<AdminActivityHistoryItem>> GetActivityHistoryAsync(Guid studentProfileId, CancellationToken ct = default)
    {
        return await _db.ActivityAttempts
            .Where(a => a.StudentProfileId == studentProfileId && a.DeletedAtUtc == null)
            .OrderByDescending(a => a.CreatedAt)
            .Join(_db.LearningActivities, a => a.LearningActivityId, act => act.Id, (a, act) => new AdminActivityHistoryItem(
                a.Id,
                act.Id,
                act.Title,
                act.ActivityType.ToString(),
                a.Score,
                a.Percentage,
                a.Passed,
                a.Completed,
                a.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<StudentAuditHistoryItemDto>?> GetStudentAuditHistoryAsync(Guid studentProfileId, CancellationToken ct = default)
    {
        var exists = await _db.StudentProfiles.AsNoTracking().AnyAsync(p => p.Id == studentProfileId, ct);
        if (!exists) return null;

        var auditLogs = await _db.AdminAuditLogs
            .AsNoTracking()
            .Where(l => l.TargetStudentId == studentProfileId)
            .Select(l => new StudentAuditHistoryItemDto(
                l.Id.ToString(),
                "AdminAuditLog",
                l.Action,
                l.ActorAdminUserId.ToString(),
                null,
                new DateTimeOffset(l.CreatedAt, TimeSpan.Zero),
                null,
                l.Reason,
                l.OldValueJson,
                l.NewValueJson,
                l.CorrelationId,
                null))
            .ToListAsync(ct);

        var resetLogs = await _db.StudentResetLogs
            .AsNoTracking()
            .Where(r => r.StudentProfileId == studentProfileId)
            .Select(r => new StudentAuditHistoryItemDto(
                r.Id.ToString(),
                "StudentResetLog",
                $"Reset: {r.PreviousStage} → {r.NewStage}",
                r.AdminUserId.ToString(),
                null,
                new DateTimeOffset(r.PerformedAtUtc, TimeSpan.Zero),
                null,
                r.Reason,
                null,
                null,
                r.CorrelationId,
                r.ClearedItemsJson))
            .ToListAsync(ct);

        return auditLogs
            .Concat(resetLogs)
            .OrderByDescending(x => x.Timestamp)
            .Take(50)
            .ToList();
    }

    public async Task<StudentListItem> UpdateStudentAsync(UpdateStudentProfileCommand command, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles.FirstOrDefaultAsync(p => p.Id == command.StudentProfileId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == profile.UserId, ct)
            ?? throw new InvalidOperationException("Student user not found.");

        profile.UpdateAdminProfile(
            command.FirstName,
            command.LastName,
            command.DisplayName,
            command.CareerContext,
            command.LearningGoal,
            command.LearningGoalDescription,
            command.DifficultSituationsText,
            command.PreferredSessionDurationMinutes,
            command.ProfessionalExperienceLevel,
            command.RoleFamiliarity);

        await _db.SaveChangesAsync(ct);
        return ToStudentListItem(profile, user.Email ?? string.Empty);
    }

    public async Task<StudentListItem> ArchiveStudentAsync(ArchiveStudentCommand command, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles.FirstOrDefaultAsync(p => p.Id == command.StudentProfileId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == profile.UserId, ct)
            ?? throw new InvalidOperationException("Student user not found.");

        profile.SetLifecycleStage(StudentLifecycleStage.Archived);
        user.EmailConfirmed = false;

        await _db.SaveChangesAsync(ct);
        return ToStudentListItem(profile, user.Email ?? string.Empty);
    }

    public async Task<StudentListItem> ReactivateStudentAsync(ReactivateStudentCommand command, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles.FirstOrDefaultAsync(p => p.Id == command.StudentProfileId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        if (profile.LifecycleStage != StudentLifecycleStage.Archived)
            throw new InvalidOperationException("Student is not archived.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == profile.UserId, ct)
            ?? throw new InvalidOperationException("Student user not found.");

        profile.SetLifecycleStage(StudentLifecycleStage.OnboardingRequired);
        user.EmailConfirmed = true;

        _db.AdminAuditLogs.Add(new AdminAuditLog(
            command.AdminUserId, "Reactivate", "StudentProfile",
            entityId: command.StudentProfileId.ToString(),
            targetStudentId: command.StudentProfileId));

        await _db.SaveChangesAsync(ct);
        return ToStudentListItem(profile, user.Email ?? string.Empty);
    }

    public async Task<StudentListItem> PauseStudentAsync(PauseStudentCommand command, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles.FirstOrDefaultAsync(p => p.Id == command.StudentProfileId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        if (profile.LifecycleStage == StudentLifecycleStage.Archived)
            throw new InvalidOperationException("Cannot pause an archived student.");

        if (profile.LifecycleStage == StudentLifecycleStage.Paused)
            throw new InvalidOperationException("Student is already paused.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == profile.UserId, ct)
            ?? throw new InvalidOperationException("Student user not found.");

        profile.SetLifecycleStage(StudentLifecycleStage.Paused);

        _db.AdminAuditLogs.Add(new AdminAuditLog(
            command.AdminUserId, "Pause", "StudentProfile",
            entityId: command.StudentProfileId.ToString(),
            targetStudentId: command.StudentProfileId));

        await _db.SaveChangesAsync(ct);
        return ToStudentListItem(profile, user.Email ?? string.Empty);
    }

    public async Task<StudentListItem> UnpauseStudentAsync(UnpauseStudentCommand command, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles.FirstOrDefaultAsync(p => p.Id == command.StudentProfileId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        if (profile.LifecycleStage != StudentLifecycleStage.Paused)
            throw new InvalidOperationException("Student is not paused.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == profile.UserId, ct)
            ?? throw new InvalidOperationException("Student user not found.");

        profile.SetLifecycleStage(StudentLifecycleStage.OnboardingRequired);

        _db.AdminAuditLogs.Add(new AdminAuditLog(
            command.AdminUserId, "Unpause", "StudentProfile",
            entityId: command.StudentProfileId.ToString(),
            targetStudentId: command.StudentProfileId));

        await _db.SaveChangesAsync(ct);
        return ToStudentListItem(profile, user.Email ?? string.Empty);
    }

    public async Task ResetStudentPasswordAsync(ResetStudentPasswordCommand command, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles.FirstOrDefaultAsync(p => p.Id == command.StudentProfileId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        var user = await _userManager.FindByIdAsync(profile.UserId.ToString())
            ?? throw new InvalidOperationException("Student user not found.");

        var removeResult = await _userManager.RemovePasswordAsync(user);
        if (!removeResult.Succeeded)
            throw new InvalidOperationException(string.Join("; ", removeResult.Errors.Select(e => e.Description)));

        var addResult = await _userManager.AddPasswordAsync(user, command.NewPassword);
        if (!addResult.Succeeded)
            throw new InvalidOperationException(string.Join("; ", addResult.Errors.Select(e => e.Description)));

        user.MustChangePassword = command.MustChangePassword;
        await _userManager.UpdateAsync(user);

        // Queue email notification — does not include the raw password.
        // If queueing fails, log a warning but do not fail the reset operation.
        try
        {
            await _notifications.QueueEmailAsync(
                recipientUserId: profile.UserId,
                title: "Your SpeakPath password has been reset",
                body: "An administrator has reset your password. Please log in with your new credentials. You will be prompted to change your password on first login.",
                category: NotificationCategory.Account,
                severity: NotificationSeverity.Info,
                ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to queue password-reset email notification for user {UserId}. Reset still succeeded.",
                profile.UserId);
        }
    }

    public async Task SetStudentCefrAsync(SetStudentCefrCommand command, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles.FirstOrDefaultAsync(p => p.Id == command.StudentProfileId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        var oldValue = profile.CefrLevel;

        // Validates and normalises; throws ArgumentException for invalid values.
        profile.AdminSetCefrLevel(command.CefrLevel);

        _db.AdminAuditLogs.Add(new AdminAuditLog(
            command.AdminUserId, "SetCefr", "StudentProfile",
            entityId: command.StudentProfileId.ToString(),
            targetStudentId: command.StudentProfileId,
            oldValueJson: oldValue is null ? "null" : $"\"{oldValue}\"",
            newValueJson: profile.CefrLevel is null ? "null" : $"\"{profile.CefrLevel}\"",
            reason: command.Reason));

        await _db.SaveChangesAsync(ct);

        // Phase 12D — regenerate plan when admin changes CEFR level
        if (!string.Equals(oldValue, profile.CefrLevel, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var planService = _services.GetRequiredService<ILearningPlanService>();
                await planService.RegeneratePlanAsync(profile.Id, "cefr_change", ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Plan regeneration failed after CEFR change for student {StudentProfileId}.", profile.Id);
            }
        }
    }

    public async Task<int> CountRecentResetsAsync(Guid adminUserId, TimeSpan window, CancellationToken ct = default)
    {
        var since = DateTime.UtcNow - window;
        return await _db.StudentResetLogs
            .CountAsync(l => l.AdminUserId == adminUserId && l.PerformedAtUtc >= since, ct);
    }

    public async Task<ResetStudentResponse> ResetStudentAsync(ResetStudentCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Reason))
            throw new ArgumentException("Reason is required.", nameof(command));

        var profile = await _db.StudentProfiles.FirstOrDefaultAsync(p => p.Id == command.StudentProfileId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        var previousStage = profile.LifecycleStage;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var onboardingCleared = false;
        var placementCleared = false;
        var coursesAndSessionsCleared = false;
        var attemptsCleared = false;
        var vocabularyCleared = false;
        var learningMemoryCleared = false;
        var audioFilesDeleted = 0;
        var progressDataCleared = false;

        if (command.ClearAudioFiles)
        {
            var audioKeys = new List<string>();

            audioKeys.AddRange(await _db.ActivityAttempts
                .Where(a => a.StudentProfileId == command.StudentProfileId && a.AudioStorageKey != null)
                .Select(a => a.AudioStorageKey!)
                .ToListAsync(ct));

            audioKeys.AddRange(await _db.AudioAssets
                .Where(a => a.StudentProfileId == command.StudentProfileId)
                .Select(a => a.ObjectKey)
                .ToListAsync(ct));

            foreach (var key in audioKeys.Distinct())
            {
                try
                {
                    await _fileStorage.DeleteAsync(key, ct);
                    audioFilesDeleted++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete audio file {Key} during student reset", key);
                }
            }

            await _db.AudioAssets
                .Where(a => a.StudentProfileId == command.StudentProfileId)
                .ExecuteDeleteAsync(ct);

            await _db.ActivityAttempts
                .Where(a => a.StudentProfileId == command.StudentProfileId)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.AudioStorageKey, (string?)null), ct);
        }

        if (command.ClearActivityAttempts)
        {
            var attempts = await _db.ActivityAttempts
                .Where(a => a.StudentProfileId == command.StudentProfileId)
                .ToListAsync(ct);
            foreach (var attempt in attempts)
                attempt.MarkDeleted();
            attemptsCleared = attempts.Count > 0;
        }

        if (command.ClearCoursesAndSessions)
        {
            var sessions = await _db.LearningSessions
                .Include(s => s.Exercises)
                .Where(s => s.StudentProfileId == command.StudentProfileId)
                .ToListAsync(ct);
            foreach (var session in sessions)
            {
                foreach (var exercise in session.Exercises)
                    exercise.MarkDeleted();
                session.MarkDeleted();
            }
            coursesAndSessionsCleared = sessions.Count > 0;

            var batchIds = await _db.GenerationBatches
                .Where(b => b.StudentProfileId == command.StudentProfileId)
                .Select(b => b.Id)
                .ToListAsync(ct);
            if (batchIds.Count > 0)
            {
                await _db.GenerationJobItems
                    .Where(i => batchIds.Contains(i.GenerationBatchId))
                    .ExecuteDeleteAsync(ct);
                await _db.GenerationBatches
                    .Where(b => batchIds.Contains(b.Id))
                    .ExecuteDeleteAsync(ct);
            }
        }

        if (command.ClearPlacementResults)
        {
            var assessments = await _db.PlacementAssessments
                .Where(a => a.StudentProfileId == command.StudentProfileId)
                .ToListAsync(ct);
            if (assessments.Count > 0)
            {
                var assessmentIds = assessments.Select(a => a.Id).ToList();
                // PlacementAssessmentItems cascade-delete with their parent PlacementAssessment
                // (see PlacementAssessmentItemConfiguration's OnDelete(Cascade)), so deleting the
                // assessments is sufficient.
                await _db.PlacementAssessments
                    .Where(a => assessmentIds.Contains(a.Id))
                    .ExecuteDeleteAsync(ct);
            }

            profile.ClearPlacementResult();
            placementCleared = true;
        }

        if (command.ClearVocabulary)
        {
            await _db.VocabularyEntries
                .Where(v => v.StudentProfileId == command.StudentProfileId)
                .ExecuteDeleteAsync(ct);
            await _db.StudentVocabularyItems
                .Where(v => v.StudentProfileId == command.StudentProfileId)
                .ExecuteDeleteAsync(ct);
            vocabularyCleared = true;
        }

        if (command.ClearLearningMemory)
        {
            var summary = await _db.UserLearningSummaries
                .FirstOrDefaultAsync(s => s.StudentProfileId == command.StudentProfileId, ct);
            if (summary is not null)
            {
                _db.UserLearningSummaries.Remove(summary);
                learningMemoryCleared = true;
            }

            var speakingSessionIds = await _db.SpeakingSessions
                .Where(s => s.StudentProfileId == command.StudentProfileId)
                .Select(s => s.Id)
                .ToListAsync(ct);
            if (speakingSessionIds.Count > 0)
            {
                await _db.SpeakingTurns
                    .Where(t => speakingSessionIds.Contains(t.SpeakingSessionId))
                    .ExecuteDeleteAsync(ct);
                await _db.SpeakingSessions
                    .Where(s => speakingSessionIds.Contains(s.Id))
                    .ExecuteDeleteAsync(ct);
            }

            await _db.AiUsageLogs
                .Where(l => l.StudentProfileId == command.StudentProfileId)
                .ExecuteDeleteAsync(ct);
        }

        if (command.ClearOnboardingAnswers)
        {
            profile.ResetToOnboarding();
            onboardingCleared = true;
        }

        if (command.ClearProgressData)
        {
            await _db.StudentSkillProfiles
                .Where(s => s.StudentProfileId == command.StudentProfileId)
                .ExecuteDeleteAsync(ct);
            progressDataCleared = true;
        }

        profile.SetLifecycleStage(command.TargetStage);

        var clearedItems = new ClearedItemsResult(
            onboardingCleared,
            placementCleared,
            coursesAndSessionsCleared,
            attemptsCleared,
            vocabularyCleared,
            learningMemoryCleared,
            audioFilesDeleted,
            progressDataCleared);

        var resetLog = new StudentResetLog(
            command.StudentProfileId,
            command.AdminUserId,
            previousStage,
            command.TargetStage,
            JsonSerializer.Serialize(clearedItems),
            command.Reason,
            command.CorrelationId);

        _db.StudentResetLogs.Add(resetLog);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new ResetStudentResponse(
            command.StudentProfileId,
            previousStage,
            command.TargetStage,
            clearedItems,
            resetLog.Id,
            command.AdminUserId,
            resetLog.PerformedAtUtc,
            command.CorrelationId);
    }

    private static StudentListItem ToStudentListItem(StudentProfile p, string email)
        => new(
            p.Id,
            p.UserId,
            email,
            p.FirstName,
            p.LastName,
            p.DisplayName,
            p.OnboardingStatus.ToString(),
            p.LifecycleStage.ToString(),
            p.CefrLevel,
            p.CareerContext,
            p.LearningGoal,
            p.LearningGoalDescription,
            p.DifficultSituationsText,
            p.PreferredSessionDurationMinutes,
            p.ProfessionalExperienceLevel,
            p.RoleFamiliarity,
            p.CreatedAt,
            p.PreferredName,
            p.SupportLanguageCode,
            p.SupportLanguageName,
            p.DifficultyPreference?.ToString(),
            p.TranslationHelpPreference?.ToString(),
            p.FocusAreas ?? [],
            p.CustomFocusArea,
            p.LearningGoals ?? [],
            p.CustomLearningGoal,
            p.LearningPreferencesUpdatedAt);

    // ── Prompt templates ──────────────────────────────────────────────────────

    public async Task<IReadOnlyList<PromptTemplateItem>> ListPromptsAsync(CancellationToken ct = default)
    {
        var prompts = await _db.AiPrompts
            .OrderBy(p => p.Key).ThenByDescending(p => p.Version)
            .ToListAsync(ct);

        return prompts.Select(p => new PromptTemplateItem(
            p.Id, p.Key, p.Version, p.IsActive, p.MaxInputTokens, p.MaxOutputTokens, p.CreatedAt))
            .ToList();
    }

    public async Task<PromptTemplateDetail> GetPromptAsync(Guid promptId, CancellationToken ct = default)
    {
        var p = await _db.AiPrompts.FirstOrDefaultAsync(x => x.Id == promptId, ct)
            ?? throw new InvalidOperationException("Prompt template not found.");
        return new PromptTemplateDetail(p.Id, p.Key, p.Content, p.Version, p.IsActive, p.MaxInputTokens, p.MaxOutputTokens);
    }

    public async Task<PromptTemplateDetail> CreateVersionAsync(CreatePromptVersionCommand command, CancellationToken ct = default)
    {
        var latestVersion = await _db.AiPrompts
            .Where(p => p.Key == command.Key)
            .MaxAsync(p => (int?)p.Version, ct) ?? 0;

        var newPrompt = new AiPrompt(
            command.Key,
            command.Content,
            version: latestVersion + 1,
            maxInputTokens: command.MaxInputTokens,
            maxOutputTokens: command.MaxOutputTokens);

        _db.AiPrompts.Add(newPrompt);
        await _db.SaveChangesAsync(ct);

        return new PromptTemplateDetail(
            newPrompt.Id, newPrompt.Key, newPrompt.Content,
            newPrompt.Version, newPrompt.IsActive,
            newPrompt.MaxInputTokens, newPrompt.MaxOutputTokens);
    }

    public async Task ActivateAsync(ActivatePromptCommand command, CancellationToken ct = default)
    {
        var prompt = await _db.AiPrompts.FirstOrDefaultAsync(p => p.Id == command.PromptId, ct)
            ?? throw new InvalidOperationException("Prompt template not found.");
        prompt.Activate();
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeactivateAsync(DeactivatePromptCommand command, CancellationToken ct = default)
    {
        var prompt = await _db.AiPrompts.FirstOrDefaultAsync(p => p.Id == command.PromptId, ct)
            ?? throw new InvalidOperationException("Prompt template not found.");
        prompt.Deactivate();
        await _db.SaveChangesAsync(ct);
    }

    // ── AI provider config ────────────────────────────────────────────────────

    public async Task<IReadOnlyList<AiProviderCatalogItem>> ListProvidersAsync(CancellationToken ct = default)
    {
        var credentials = await _db.AiProviderCredentials.ToListAsync(ct);
        var credByProvider = credentials.ToDictionary(c => c.ProviderName, StringComparer.OrdinalIgnoreCase);

        return AiProviderConfig.AllowedModels
            .Select(kvp =>
            {
                credByProvider.TryGetValue(kvp.Key, out var cred);
                return ToCatalogItem(kvp.Key, MergeModels(kvp.Value, cred).Order().ToList(), cred);
            })
            .OrderBy(p => p.ProviderName)
            .ToList();
    }

    // ── AI config categories ──────────────────────────────────────────────────

    public async Task<IReadOnlyList<AiConfigCategoryItem>> ListCategoriesAsync(CancellationToken ct = default)
    {
        var categories = await _db.AiConfigCategories.OrderBy(c => c.CategoryKey).ToListAsync(ct);
        return categories.Select(ToCategoryItem).ToList();
    }

    public IReadOnlyList<AiModelPricingItem> ListPricing()
    {
        var providers = new[] { "OpenAI", "Gemini", "Anthropic", "Qwen" };
        var results = new List<AiModelPricingItem>();

        foreach (var provider in providers)
        {
            var section = _configuration.GetSection($"{provider}:Pricing");
            foreach (var modelSection in section.GetChildren())
            {
                var input = modelSection.GetValue<decimal?>("InputPer1KTokens");
                var output = modelSection.GetValue<decimal?>("OutputPer1KTokens");
                var isConfigured = input.HasValue && output.HasValue && input >= 0 && output >= 0;
                results.Add(new AiModelPricingItem(
                    ProviderName: provider.ToLowerInvariant(),
                    ModelName: modelSection.Key,
                    InputPer1KTokens: input ?? 0m,
                    OutputPer1KTokens: output ?? 0m,
                    Currency: "USD",
                    Source: "Configuration",
                    IsConfigured: isConfigured));
            }
        }

        return results.OrderBy(r => r.ProviderName).ThenBy(r => r.ModelName).ToList();
    }

    public async Task<IReadOnlyList<AiModelPricingOverrideItem>> ListPricingOverridesAsync(CancellationToken ct = default)
    {
        var overrides = await _db.AiModelPricingOverrides
            .OrderBy(o => o.ProviderName).ThenBy(o => o.ModelName).ThenByDescending(o => o.EffectiveFromUtc)
            .ToListAsync(ct);
        return overrides.Select(ToPricingOverrideItem).ToList();
    }

    public async Task<AiModelPricingOverrideItem> CreatePricingOverrideAsync(CreatePricingOverrideCommand command, CancellationToken ct = default)
    {
        var entity = new AiModelPricingOverride(
            command.ProviderName,
            command.ModelName,
            command.InputPricePer1KTokens,
            command.OutputPricePer1KTokens,
            command.Currency,
            command.EffectiveFromUtc,
            command.EffectiveToUtc,
            command.Notes,
            command.AdminUserId);

        _db.AiModelPricingOverrides.Add(entity);

        _db.AdminAuditLogs.Add(new AdminAuditLog(
            command.AdminUserId, "CreatePricingOverride", "AiModelPricingOverride",
            entityId: entity.Id.ToString(),
            newValueJson: JsonSerializer.Serialize(new { command.ProviderName, command.ModelName, command.InputPricePer1KTokens, command.OutputPricePer1KTokens })));

        await _db.SaveChangesAsync(ct);
        return ToPricingOverrideItem(entity);
    }

    public async Task<AiModelPricingOverrideItem> UpdatePricingOverrideAsync(UpdatePricingOverrideCommand command, CancellationToken ct = default)
    {
        var entity = await _db.AiModelPricingOverrides.FirstOrDefaultAsync(o => o.Id == command.Id, ct)
            ?? throw new InvalidOperationException($"Pricing override '{command.Id}' not found.");

        var oldJson = JsonSerializer.Serialize(new { entity.InputPricePer1KTokens, entity.OutputPricePer1KTokens });

        entity.Update(
            command.InputPricePer1KTokens,
            command.OutputPricePer1KTokens,
            command.Currency,
            command.EffectiveFromUtc,
            command.EffectiveToUtc,
            command.Notes,
            command.AdminUserId);

        _db.AdminAuditLogs.Add(new AdminAuditLog(
            command.AdminUserId, "UpdatePricingOverride", "AiModelPricingOverride",
            entityId: entity.Id.ToString(),
            oldValueJson: oldJson,
            newValueJson: JsonSerializer.Serialize(new { command.InputPricePer1KTokens, command.OutputPricePer1KTokens })));

        await _db.SaveChangesAsync(ct);
        return ToPricingOverrideItem(entity);
    }

    public async Task DeactivatePricingOverrideAsync(DeactivatePricingOverrideCommand command, CancellationToken ct = default)
    {
        var entity = await _db.AiModelPricingOverrides.FirstOrDefaultAsync(o => o.Id == command.Id, ct)
            ?? throw new InvalidOperationException($"Pricing override '{command.Id}' not found.");

        entity.Deactivate(command.AdminUserId);

        _db.AdminAuditLogs.Add(new AdminAuditLog(
            command.AdminUserId, "DeactivatePricingOverride", "AiModelPricingOverride",
            entityId: entity.Id.ToString()));

        await _db.SaveChangesAsync(ct);
    }

    private static AiModelPricingOverrideItem ToPricingOverrideItem(AiModelPricingOverride o) =>
        new(o.Id, o.ProviderName, o.ModelName,
            o.InputPricePer1KTokens, o.OutputPricePer1KTokens,
            o.Currency, o.IsActive,
            o.EffectiveFromUtc, o.EffectiveToUtc,
            o.Notes, o.CreatedAt, o.UpdatedAtUtc,
            o.CreatedByAdminUserId, o.UpdatedByAdminUserId);

    public async Task<AiConfigCategoryItem> UpdateCategoryAsync(UpdateAiConfigCategoryCommand command, CancellationToken ct = default)
    {
        var category = await _db.AiConfigCategories
            .FirstOrDefaultAsync(c => c.CategoryKey == command.CategoryKey, ct)
            ?? throw new InvalidOperationException($"AI config category '{command.CategoryKey}' not found.");

        ValidateCategoryConfig(command.CategoryKey, command.ProviderName, command.ModelName);

        category.Update(command.ProviderName, command.ModelName);
        category.UpdateVoice(command.VoiceName);

        await _db.SaveChangesAsync(ct);
        return ToCategoryItem(category);
    }

    private static AiConfigCategoryItem ToCategoryItem(AiConfigCategory c)
        => new(c.Id, c.CategoryKey, c.DisplayName, c.ProviderName, c.ModelName, c.VoiceName);

    public async Task<AiProviderCatalogItem> SetProviderApiKeyAsync(SetProviderApiKeyCommand command, CancellationToken ct = default)
    {
        var normalised = command.ProviderName.Trim().ToLowerInvariant();
        var cred = await _db.AiProviderCredentials.FirstOrDefaultAsync(c => c.ProviderName == normalised, ct);
        if (cred is null)
        {
            cred = new AiProviderCredential(normalised);
            _db.AiProviderCredentials.Add(cred);
        }
        cred.SetApiKey(command.ApiKey);
        await _db.SaveChangesAsync(ct);

        AiProviderConfig.AllowedModels.TryGetValue(normalised, out var allowedModels);
        return ToCatalogItem(normalised, MergeModels(allowedModels, cred).Order().ToList(), cred);
    }

    public async Task<AiProviderCatalogItem> SetProviderEndpointAsync(SetProviderEndpointCommand command, CancellationToken ct = default)
    {
        var normalised = command.ProviderName.Trim().ToLowerInvariant();
        var cred = await _db.AiProviderCredentials.FirstOrDefaultAsync(c => c.ProviderName == normalised, ct);
        if (cred is null)
        {
            cred = new AiProviderCredential(normalised);
            _db.AiProviderCredentials.Add(cred);
        }
        cred.SetApiEndpoint(command.ApiEndpoint);
        await _db.SaveChangesAsync(ct);

        AiProviderConfig.AllowedModels.TryGetValue(normalised, out var allowedModels);
        return ToCatalogItem(normalised, MergeModels(allowedModels, cred).Order().ToList(), cred);
    }

    public async Task<AiProviderCatalogItem> AddProviderModelAsync(AddProviderModelCommand command, CancellationToken ct = default)
    {
        var normalised = command.ProviderName.Trim().ToLowerInvariant();
        if (!AiProviderConfig.AllowedModels.ContainsKey(normalised))
            throw new ArgumentException($"Unsupported provider '{normalised}'.", nameof(command.ProviderName));

        var cred = await _db.AiProviderCredentials.FirstOrDefaultAsync(c => c.ProviderName == normalised, ct);
        if (cred is null)
        {
            cred = new AiProviderCredential(normalised);
            _db.AiProviderCredentials.Add(cred);
        }

        cred.AddModel(command.ModelName);
        await _db.SaveChangesAsync(ct);

        AiProviderConfig.AllowedModels.TryGetValue(normalised, out var allowedModels);
        return ToCatalogItem(normalised, MergeModels(allowedModels, cred).Order().ToList(), cred);
    }

    public async Task<AiProviderCatalogItem> TestProviderAsync(string providerName, CancellationToken ct = default)
    {
        var normalised = providerName.Trim().ToLowerInvariant();
        var cred = await _db.AiProviderCredentials.FirstOrDefaultAsync(c => c.ProviderName == normalised, ct);

        AiProviderConfig.AllowedModels.TryGetValue(normalised, out var allowedSet);
        var models = MergeModels(allowedSet, cred).Order().ToList();

        // TTS-only models cannot be tested via chat completion — skip them.
        var testableModels = models.Where(m => !IsTtsOnlyModel(m)).ToList();
        var outcomes = await _tester.TestAllModelsAsync(normalised, testableModels, cred?.ApiKey, ct);

        if (cred is null)
        {
            cred = new AiProviderCredential(normalised);
            _db.AiProviderCredentials.Add(cred);
        }
        foreach (var o in outcomes)
            cred.RecordModelTest(o.ModelName, o.Ok, o.LatencyMs, o.Error);

        await _db.SaveChangesAsync(ct);
        return ToCatalogItem(normalised, models, cred);
    }

    public async Task<AiProviderCatalogItem> TestProviderModelAsync(string providerName, string modelName, CancellationToken ct = default)
    {
        var normalised = providerName.Trim().ToLowerInvariant();
        if (!AiProviderConfig.AllowedModels.ContainsKey(normalised))
            throw new ArgumentException($"Unsupported provider '{normalised}'.", nameof(providerName));

        var model = modelName.Trim();
        var cred = await _db.AiProviderCredentials.FirstOrDefaultAsync(c => c.ProviderName == normalised, ct);

        var outcomes = await _tester.TestAllModelsAsync(normalised, [model], cred?.ApiKey, ct);
        if (cred is null)
        {
            cred = new AiProviderCredential(normalised);
            _db.AiProviderCredentials.Add(cred);
        }

        foreach (var outcome in outcomes)
            cred.RecordModelTest(outcome.ModelName, outcome.Ok, outcome.LatencyMs, outcome.Error);

        await _db.SaveChangesAsync(ct);
        AiProviderConfig.AllowedModels.TryGetValue(normalised, out var allowedModels);
        return ToCatalogItem(normalised, MergeModels(allowedModels, cred).Order().ToList(), cred);
    }

    public async Task<CategoryTestResult> TestCategoryAsync(string categoryKey, CancellationToken ct = default)
    {
        var category = await _db.AiConfigCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CategoryKey == categoryKey, ct)
            ?? throw new InvalidOperationException($"AI config category '{categoryKey}' not found.");

        var resolved = await ResolveCategoryForTestAsync(category, ct);
        if (resolved.ProviderName is null)
        {
            return new CategoryTestResult(categoryKey, "", resolved.ModelName, resolved.VoiceName, false, 0,
                "No provider/model is configured for this category.");
        }

        if (categoryKey.StartsWith("tts.", StringComparison.OrdinalIgnoreCase))
            return await TestTtsCategoryAsync(categoryKey, ct);

        return await TestLlmCategoryAsync(categoryKey, resolved.ProviderName, resolved.ModelName, resolved.VoiceName, ct);
    }

    private static bool IsTtsOnlyModel(string modelName)
    {
        var lower = modelName.ToLowerInvariant();
        return lower.Contains("-tts") || lower == "cosyvoice-v2" || lower.StartsWith("tts-");
    }

    private static bool IsProviderTtsModel(string providerName, string modelName)
    {
        var provider = providerName.Trim().ToLowerInvariant();
        var lower = modelName.Trim().ToLowerInvariant();
        return provider switch
        {
            "openai" => lower.StartsWith("tts-", StringComparison.OrdinalIgnoreCase),
            "gemini" => lower.Contains("-tts", StringComparison.OrdinalIgnoreCase),
            "qwen" => lower == "cosyvoice-v2",
            _ => false
        };
    }

    private async Task<CategoryTestResult> TestLlmCategoryAsync(
        string categoryKey,
        string providerName,
        string? modelName,
        string? voiceName,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(modelName))
            return new CategoryTestResult(categoryKey, providerName, modelName, voiceName, false, 0, "Model is not configured.");

        if (IsTtsOnlyModel(modelName))
            return new CategoryTestResult(categoryKey, providerName, modelName, voiceName, false, 0, "This is a TTS-only model.");

        var cred = await _db.AiProviderCredentials.FirstOrDefaultAsync(c => c.ProviderName == providerName, ct);
        var outcome = (await _tester.TestAllModelsAsync(providerName, [modelName], cred?.ApiKey, ct)).Single();
        if (cred is null)
        {
            cred = new AiProviderCredential(providerName);
            _db.AiProviderCredentials.Add(cred);
        }

        cred.RecordModelTest(outcome.ModelName, outcome.Ok, outcome.LatencyMs, outcome.Error);
        await _db.SaveChangesAsync(ct);

        return new CategoryTestResult(categoryKey, providerName, modelName, voiceName,
            outcome.Ok, outcome.LatencyMs, outcome.Error);
    }

    private async Task<CategoryTestResult> TestTtsCategoryAsync(string categoryKey, CancellationToken ct)
    {
        var selection = _providerResolver.ResolveTts(categoryKey, categoryKey);
        ITextToSpeechService service = selection.ProviderName.ToLowerInvariant() switch
        {
            "openai" => _services.GetRequiredService<LinguaCoach.Infrastructure.Speaking.OpenAiTextToSpeechService>(),
            "gemini" => _services.GetRequiredService<LinguaCoach.Infrastructure.Speaking.GeminiTextToSpeechService>(),
            "qwen" => _services.GetRequiredService<LinguaCoach.Infrastructure.Speaking.QwenTextToSpeechService>(),
            _ => throw new ArgumentException($"Unsupported TTS provider '{selection.ProviderName}'.")
        };

        var started = DateTime.UtcNow;
        var result = await service.GenerateSpeechAsync(
            "This is a SpeakPath audio configuration test.",
            new TextToSpeechOptions(
                "en",
                selection.VoiceName,
                selection.ModelName,
                selection.ApiKeyOverride,
                selection.EndpointOverride),
            ct);

        return new CategoryTestResult(
            categoryKey,
            selection.ProviderName,
            selection.ModelName,
            selection.VoiceName,
            result.Success,
            (int)Math.Clamp((DateTime.UtcNow - started).TotalMilliseconds, 0, int.MaxValue),
            result.FailureReason);
    }

    private async Task<(string? ProviderName, string? ModelName, string? VoiceName)> ResolveCategoryForTestAsync(
        AiConfigCategory category,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(category.ProviderName)
            && !string.Equals(category.ProviderName, "fake", StringComparison.OrdinalIgnoreCase))
        {
            return (category.ProviderName, category.ModelName, category.VoiceName);
        }

        if (category.CategoryKey.StartsWith("tts.", StringComparison.OrdinalIgnoreCase))
            return (category.ProviderName, category.ModelName, category.VoiceName);

        if (!string.Equals(category.CategoryKey, "llm.default", StringComparison.OrdinalIgnoreCase))
        {
            var defaultCategory = await _db.AiConfigCategories.AsNoTracking()
                .FirstOrDefaultAsync(c => c.CategoryKey == "llm.default", ct);
            if (defaultCategory is not null)
                return (defaultCategory.ProviderName, defaultCategory.ModelName, defaultCategory.VoiceName);
        }

        return (category.ProviderName, category.ModelName, category.VoiceName);
    }

    private static void ValidateCategoryConfig(string categoryKey, string? providerName, string? modelName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            return;

        var provider = providerName.Trim().ToLowerInvariant();
        if (provider == "fake")
            return;

        if (!AiProviderConfig.AllowedModels.ContainsKey(provider))
            throw new ArgumentException($"Unsupported provider '{provider}'.", nameof(providerName));

        if (categoryKey.StartsWith("llm.", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(modelName))
            throw new ArgumentException("ModelName is required when an LLM provider is configured.", nameof(modelName));

        if (categoryKey.StartsWith("tts.", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(modelName))
                throw new ArgumentException("ModelName is required when a TTS provider is configured.", nameof(modelName));

            if (!IsProviderTtsModel(provider, modelName))
                throw new ArgumentException(
                    $"Model '{modelName}' is not a TTS model for provider '{provider}'.",
                    nameof(modelName));
        }
    }

    private static IEnumerable<string> MergeModels(IReadOnlySet<string>? allowedModels, AiProviderCredential? cred)
    {
        var models = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (allowedModels is not null)
        {
            foreach (var model in allowedModels)
                models.Add(model);
        }

        if (cred is not null)
        {
            foreach (var model in cred.ModelTests.Keys)
                models.Add(model);
        }

        return models;
    }

    private static AiProviderCatalogItem ToCatalogItem(
        string providerName,
        IReadOnlyList<string> models,
        AiProviderCredential? cred)
    {
        var tests = models.Select(m =>
        {
            if (cred?.ModelTests.TryGetValue(m, out var r) == true)
                return new ModelTestStatus(m, r.Ok, r.LatencyMs, r.Error, r.TestedAt);
            return new ModelTestStatus(m, false, 0, null, default);
        }).ToList();

        return new AiProviderCatalogItem(
            providerName,
            models,
            HasApiKey: cred?.ApiKey is not null,
            ModelTests: tests,
            ApiEndpoint: cred?.ApiEndpoint);
    }
}

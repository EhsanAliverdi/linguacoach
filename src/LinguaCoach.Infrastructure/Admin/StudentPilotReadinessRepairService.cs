using LinguaCoach.Application.Admin.StudentReadiness;
using LinguaCoach.Application.LearningPlan;
using LinguaCoach.Application.ReadinessPool;
using LinguaCoach.Application.Sessions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Admin;

/// <summary>
/// Phase 20D: explicit, admin-triggered repair actions for one student's pilot readiness.
/// Every action reuses an existing, already-safe service method or entity mutator — nothing
/// here invents new generation/mutation logic. Never deletes attempts/submissions/evaluations.
/// </summary>
public sealed class StudentPilotReadinessRepairService : IStudentPilotReadinessRepairService
{
    private static readonly string[] CefrOrder = ["A1", "A2", "B1", "B2", "C1", "C2"];

    private readonly LinguaCoachDbContext _db;
    private readonly ILearningPlanService _learningPlan;
    private readonly IGetTodaysSessionHandler _todaysSessionHandler;
    private readonly IEffectiveReadinessPoolSettingsProvider _settingsProvider;
    private readonly ILogger<StudentPilotReadinessRepairService> _logger;

    public StudentPilotReadinessRepairService(
        LinguaCoachDbContext db,
        ILearningPlanService learningPlan,
        IGetTodaysSessionHandler todaysSessionHandler,
        IEffectiveReadinessPoolSettingsProvider settingsProvider,
        ILogger<StudentPilotReadinessRepairService> logger)
    {
        _db = db;
        _learningPlan = learningPlan;
        _todaysSessionHandler = todaysSessionHandler;
        _settingsProvider = settingsProvider;
        _logger = logger;
    }

    public async Task<StudentReadinessRepairResultDto> RepairAsync(
        Guid studentProfileId, Guid adminUserId, StudentReadinessRepairRequestDto request, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles.FirstOrDefaultAsync(p => p.Id == studentProfileId, ct)
            ?? throw new KeyNotFoundException($"Unknown student profile '{studentProfileId}'.");

        var def = StudentReadinessRepairActions.Find(request.ActionKey)
            ?? throw new KeyNotFoundException($"Unknown repair action '{request.ActionKey}'.");

        if (!def.IsImplemented)
            throw new InvalidOperationException($"'{def.DisplayName}' is not implemented yet.");

        if (!request.DryRun && string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("A reason is required to run a real (non-dry-run) repair action.");

        if (request.ActionKey == StudentReadinessRepairActions.RunAllSafeRepairs)
        {
            var all = await RunAllSafeRepairsAsync(studentProfileId, adminUserId, request.Reason, request.DryRun, ct);
            return new StudentReadinessRepairResultDto
            {
                ActionKey = StudentReadinessRepairActions.RunAllSafeRepairs,
                DryRun = request.DryRun,
                ChangedCount = all.Sum(r => r.ChangedCount),
                SkippedCount = all.Sum(r => r.SkippedCount),
                Warnings = all.SelectMany(r => r.Warnings).ToList(),
                Errors = all.SelectMany(r => r.Errors).ToList(),
                BeforeSummary = string.Join("; ", all.Select(r => $"{r.ActionKey}: {r.BeforeSummary}")),
                AfterSummary = string.Join("; ", all.Select(r => $"{r.ActionKey}: {r.AfterSummary}")),
            };
        }

        var result = request.ActionKey switch
        {
            StudentReadinessRepairActions.GenerateLearningPlanIfMissing =>
                await RepairGenerateLearningPlanAsync(profile, request.DryRun, ct),
            StudentReadinessRepairActions.RefillTodayLessonIfEmpty =>
                await RepairRefillTodayLessonAsync(profile, request.DryRun, ct),
            StudentReadinessRepairActions.ExpireInvalidReadinessItems =>
                await RepairExpireInvalidReadinessItemsAsync(profile, request.DryRun, ct),
            StudentReadinessRepairActions.ExpireStaleReservedItems =>
                await RepairExpireStaleReservedItemsAsync(profile, request.DryRun, ct),
            _ => throw new InvalidOperationException($"'{def.DisplayName}' has no repair implementation wired up."),
        };

        if (!request.DryRun)
        {
            var auditLog = new AdminAuditLog(
                adminUserId, "RepairStudentReadiness", "StudentReadinessRepair",
                entityId: request.ActionKey,
                targetStudentId: studentProfileId,
                oldValueJson: result.BeforeSummary is null ? null : $"\"{result.BeforeSummary}\"",
                newValueJson: result.AfterSummary is null ? null : $"\"{result.AfterSummary}\"",
                reason: request.Reason);
            _db.AdminAuditLogs.Add(auditLog);
            await _db.SaveChangesAsync(ct);
            result = result with { AuditLogId = auditLog.Id };
        }

        return result;
    }

    public async Task<IReadOnlyList<StudentReadinessRepairResultDto>> RunAllSafeRepairsAsync(
        Guid studentProfileId, Guid adminUserId, string? reason, bool dryRun, CancellationToken ct = default)
    {
        if (!await _db.StudentProfiles.AnyAsync(p => p.Id == studentProfileId, ct))
            throw new KeyNotFoundException($"Unknown student profile '{studentProfileId}'.");

        if (!dryRun && string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A reason is required to run real (non-dry-run) repairs.");

        var safeActionKeys = new[]
        {
            StudentReadinessRepairActions.GenerateLearningPlanIfMissing,
            StudentReadinessRepairActions.RefillTodayLessonIfEmpty,
            StudentReadinessRepairActions.ExpireInvalidReadinessItems,
            StudentReadinessRepairActions.ExpireStaleReservedItems,
        };

        var results = new List<StudentReadinessRepairResultDto>();
        foreach (var actionKey in safeActionKeys)
        {
            var result = await RepairAsync(
                studentProfileId, adminUserId,
                new StudentReadinessRepairRequestDto { ActionKey = actionKey, Reason = reason, DryRun = dryRun }, ct);
            results.Add(result);
        }

        return results;
    }

    // --- individual repair actions ---

    private async Task<StudentReadinessRepairResultDto> RepairGenerateLearningPlanAsync(
        StudentProfile profile, bool dryRun, CancellationToken ct)
    {
        var existsBefore = await _db.StudentLearningPlans.AsNoTracking()
            .AnyAsync(p => p.StudentProfileId == profile.Id
                && (p.Status == LearningPlanStatus.Active || p.Status == LearningPlanStatus.Regenerating), ct);

        if (existsBefore)
        {
            return new StudentReadinessRepairResultDto
            {
                ActionKey = StudentReadinessRepairActions.GenerateLearningPlanIfMissing,
                DryRun = dryRun,
                ChangedCount = 0,
                SkippedCount = 1,
                Warnings = [],
                Errors = [],
                BeforeSummary = "Learning Plan already exists",
                AfterSummary = "Learning Plan already exists (no-op)",
            };
        }

        if (dryRun)
        {
            return new StudentReadinessRepairResultDto
            {
                ActionKey = StudentReadinessRepairActions.GenerateLearningPlanIfMissing,
                DryRun = true,
                ChangedCount = 1,
                SkippedCount = 0,
                Warnings = [],
                Errors = [],
                BeforeSummary = "No Learning Plan",
                AfterSummary = "Would generate a new Learning Plan",
            };
        }

        try
        {
            await _learningPlan.GetOrCreatePlanAsync(profile.Id, ct);
            return new StudentReadinessRepairResultDto
            {
                ActionKey = StudentReadinessRepairActions.GenerateLearningPlanIfMissing,
                DryRun = false,
                ChangedCount = 1,
                SkippedCount = 0,
                Warnings = [],
                Errors = [],
                BeforeSummary = "No Learning Plan",
                AfterSummary = "Learning Plan generated",
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Readiness repair: Learning Plan generation failed for student {StudentId}.", profile.Id);
            return new StudentReadinessRepairResultDto
            {
                ActionKey = StudentReadinessRepairActions.GenerateLearningPlanIfMissing,
                DryRun = false,
                ChangedCount = 0,
                SkippedCount = 0,
                Warnings = [],
                Errors = [ex.Message],
                BeforeSummary = "No Learning Plan",
                AfterSummary = "Learning Plan generation failed",
            };
        }
    }

    private async Task<StudentReadinessRepairResultDto> RepairRefillTodayLessonAsync(
        StudentProfile profile, bool dryRun, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var hasUsableSession = await _db.LearningSessions.AsNoTracking()
            .AnyAsync(s => s.StudentProfileId == profile.Id
                && s.DeletedAtUtc == null
                && s.CreatedAt >= today
                && (s.Status == SessionStatus.NotStarted || s.Status == SessionStatus.InProgress)
                && s.GenerationStatus == GenerationStatus.Ready, ct);

        if (hasUsableSession)
        {
            return new StudentReadinessRepairResultDto
            {
                ActionKey = StudentReadinessRepairActions.RefillTodayLessonIfEmpty,
                DryRun = dryRun,
                ChangedCount = 0,
                SkippedCount = 1,
                Warnings = [],
                Errors = [],
                BeforeSummary = "A usable Today session already exists",
                AfterSummary = "A usable Today session already exists (no-op)",
            };
        }

        if (dryRun)
        {
            return new StudentReadinessRepairResultDto
            {
                ActionKey = StudentReadinessRepairActions.RefillTodayLessonIfEmpty,
                DryRun = true,
                ChangedCount = 1,
                SkippedCount = 0,
                Warnings = [],
                Errors = [],
                BeforeSummary = "No usable Today session",
                AfterSummary = "Would attempt on-demand Today-lesson generation",
            };
        }

        try
        {
            await _todaysSessionHandler.HandleAsync(new GetTodaysSessionQuery(profile.UserId), ct);
            return new StudentReadinessRepairResultDto
            {
                ActionKey = StudentReadinessRepairActions.RefillTodayLessonIfEmpty,
                DryRun = false,
                ChangedCount = 1,
                SkippedCount = 0,
                Warnings = [],
                Errors = [],
                BeforeSummary = "No usable Today session",
                AfterSummary = "Today session generated on demand",
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Readiness repair: Today-lesson generation failed for student {StudentId}.", profile.Id);
            return new StudentReadinessRepairResultDto
            {
                ActionKey = StudentReadinessRepairActions.RefillTodayLessonIfEmpty,
                DryRun = false,
                ChangedCount = 0,
                SkippedCount = 1,
                Warnings = [ex.Message],
                Errors = [],
                BeforeSummary = "No usable Today session",
                AfterSummary = "Could not generate a Today session — see warning for the exact reason",
            };
        }
    }

    private async Task<StudentReadinessRepairResultDto> RepairExpireInvalidReadinessItemsAsync(
        StudentProfile profile, bool dryRun, CancellationToken ct)
    {
        if (profile.CefrLevel is null)
        {
            return new StudentReadinessRepairResultDto
            {
                ActionKey = StudentReadinessRepairActions.ExpireInvalidReadinessItems,
                DryRun = dryRun,
                ChangedCount = 0,
                SkippedCount = 0,
                Warnings = [],
                Errors = [],
                BeforeSummary = "No CEFR level set — nothing to compare",
                AfterSummary = "No CEFR level set — nothing to compare",
            };
        }

        var candidates = await _db.StudentActivityReadinessItems
            .Where(i => i.StudentId == profile.Id
                && (i.Status == ReadinessPoolStatus.Ready || i.Status == ReadinessPoolStatus.Reserved)
                && i.RoutingReason == RoutingReason.Normal
                && !i.IsLowerLevelContent)
            .ToListAsync(ct);

        var mismatched = candidates.Where(i => IsBelowCurrentLevel(i.TargetCefrLevel, profile.CefrLevel)).ToList();

        if (mismatched.Count == 0)
        {
            return new StudentReadinessRepairResultDto
            {
                ActionKey = StudentReadinessRepairActions.ExpireInvalidReadinessItems,
                DryRun = dryRun,
                ChangedCount = 0,
                SkippedCount = 0,
                Warnings = [],
                Errors = [],
                BeforeSummary = "No CEFR-mismatched readiness items",
                AfterSummary = "No CEFR-mismatched readiness items",
            };
        }

        if (dryRun)
        {
            return new StudentReadinessRepairResultDto
            {
                ActionKey = StudentReadinessRepairActions.ExpireInvalidReadinessItems,
                DryRun = true,
                ChangedCount = mismatched.Count,
                SkippedCount = 0,
                Warnings = [],
                Errors = [],
                BeforeSummary = $"{mismatched.Count} CEFR-mismatched item(s) found",
                AfterSummary = $"Would mark {mismatched.Count} item(s) Stale",
            };
        }

        foreach (var item in mismatched)
            item.MarkStale($"Readiness audit repair: target level {item.TargetCefrLevel} below current level {profile.CefrLevel}.");
        await _db.SaveChangesAsync(ct);

        return new StudentReadinessRepairResultDto
        {
            ActionKey = StudentReadinessRepairActions.ExpireInvalidReadinessItems,
            DryRun = false,
            ChangedCount = mismatched.Count,
            SkippedCount = 0,
            Warnings = [],
            Errors = [],
            BeforeSummary = $"{mismatched.Count} CEFR-mismatched item(s) found",
            AfterSummary = $"Marked {mismatched.Count} item(s) Stale",
        };
    }

    private async Task<StudentReadinessRepairResultDto> RepairExpireStaleReservedItemsAsync(
        StudentProfile profile, bool dryRun, CancellationToken ct)
    {
        var effective = await _settingsProvider.GetEffectiveAsync(ct);
        var cutoff = DateTime.UtcNow.AddHours(-effective.ReservedItemExpiryHours);

        var candidates = await _db.StudentActivityReadinessItems
            .Where(i => i.StudentId == profile.Id
                && i.Status == ReadinessPoolStatus.Reserved
                && i.ReservedAt != null && i.ReservedAt < cutoff)
            .ToListAsync(ct);

        if (candidates.Count == 0)
        {
            return new StudentReadinessRepairResultDto
            {
                ActionKey = StudentReadinessRepairActions.ExpireStaleReservedItems,
                DryRun = dryRun,
                ChangedCount = 0,
                SkippedCount = 0,
                Warnings = [],
                Errors = [],
                BeforeSummary = "No stale reserved items",
                AfterSummary = "No stale reserved items",
            };
        }

        if (dryRun)
        {
            return new StudentReadinessRepairResultDto
            {
                ActionKey = StudentReadinessRepairActions.ExpireStaleReservedItems,
                DryRun = true,
                ChangedCount = candidates.Count,
                SkippedCount = 0,
                Warnings = [],
                Errors = [],
                BeforeSummary = $"{candidates.Count} stale reserved item(s) found",
                AfterSummary = $"Would expire {candidates.Count} item(s)",
            };
        }

        foreach (var item in candidates)
            item.Expire("Readiness audit repair: reservation expired past the effective expiry window.");
        await _db.SaveChangesAsync(ct);

        return new StudentReadinessRepairResultDto
        {
            ActionKey = StudentReadinessRepairActions.ExpireStaleReservedItems,
            DryRun = false,
            ChangedCount = candidates.Count,
            SkippedCount = 0,
            Warnings = [],
            Errors = [],
            BeforeSummary = $"{candidates.Count} stale reserved item(s) found",
            AfterSummary = $"Expired {candidates.Count} item(s)",
        };
    }

    private static bool IsBelowCurrentLevel(string targetLevel, string currentLevel)
    {
        var targetCore = targetLevel.TrimEnd('+', '-');
        var currentCore = currentLevel.TrimEnd('+', '-');
        var targetIndex = Array.IndexOf(CefrOrder, targetCore.ToUpperInvariant());
        var currentIndex = Array.IndexOf(CefrOrder, currentCore.ToUpperInvariant());
        return targetIndex >= 0 && currentIndex >= 0 && targetIndex < currentIndex;
    }
}

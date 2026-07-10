using System.Text.Json;
using LinguaCoach.Application.PracticeGym;
using LinguaCoach.Application.PracticeGymModules;
using LinguaCoach.Application.ReadinessPool;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.PracticeGym;

/// <summary>
/// Phase I2A (legacy fallback deletion): SuggestedItems/ContinueItems/ReviewItems no longer read
/// the student readiness pool for Practice-Gym-sourced (<see cref="ReadinessPoolSource.PracticeGym"/>)
/// rows — that generation path (readiness-pool-backed Practice Gym content, including the
/// review/scaffold pilot described below) was removed. Those three lists are now always empty.
/// <see cref="PracticeGymSuggestionsDto.ModuleSuggestions"/> (Phase H7, computed via
/// <see cref="IPracticeGymModuleSelectionService"/>) is unaffected — it was already independent
/// of the readiness pool and is now the sole real content in this DTO. See
/// docs/reviews/2026-07-10-phase-i2a-practice-gym-legacy-deletion-review.md.
///
/// StartSuggestionAsync/TryMarkConsumedAsync are unchanged — Today still writes/reserves
/// readiness-pool items directly, and these methods operate generically on a given
/// readinessItemId (no PracticeGym-source filter of their own), out of scope for this pass.
///
/// The doc comment below describes the pre-I2A ranking/guardrail behaviour for historical
/// context; it no longer applies to Practice Gym now that the three lists are always empty:
///   Ranking order: focus-area match → goal/context match → routing priority → expiry urgency → FIFO.
///   Guardrails (defence-in-depth):
///     - Consumed / expired / failed / stale / queued / generating items excluded entirely.
///     - Items with RequiresAdminReview=true are excluded unless per-item AdminReviewStatus=Approved
///       (Phase 19B). PendingReview and Rejected items are excluded entirely.
///     - ReviewOnly status items go to ReviewItems only, never SuggestedItems.
///     - RoutingReason.Review / Scaffold / Remediation items with Ready status
///       go to ReviewItems if IsLowerLevelContent, otherwise may appear in Suggested.
///     - Reserved valid items go to ContinueItems.
///     - general_english is fallback context — workplace is never default.
///   Phase 19C pilot gate:
///     - Approved review/scaffold items (RequiresAdminReview=true) are further gated on
///       PracticeGymPilotEnabled. When the pilot is off, they are excluded from ReviewItems
///       entirely — even though generation and admin approval (19A/19B) may already be running.
///     - When the pilot is on, visible scaffold items are capped at MaxStudentVisibleScaffoldSuggestions
///       and their CallToAction/Explanation are replaced with the configured friendly pilot
///       label/reason so wording stays non-negative regardless of RoutingReason.
/// </summary>
public sealed class PracticeGymSuggestionService : IPracticeGymSuggestionService
{
    private readonly LinguaCoachDbContext _db;
    private readonly IReadinessPoolReplenishmentService _replenishment;
    private readonly IPracticeGymModuleSelectionService _moduleSelector;
    private readonly IPracticeGymModuleAssignmentRecorder _moduleAssignmentRecorder;
    private readonly ILogger<PracticeGymSuggestionService> _logger;

    public PracticeGymSuggestionService(
        LinguaCoachDbContext db,
        IReadinessPoolReplenishmentService replenishment,
        IPracticeGymModuleSelectionService moduleSelector,
        IPracticeGymModuleAssignmentRecorder moduleAssignmentRecorder,
        ILogger<PracticeGymSuggestionService> logger)
    {
        _db = db;
        _replenishment = replenishment;
        _moduleSelector = moduleSelector;
        _moduleAssignmentRecorder = moduleAssignmentRecorder;
        _logger = logger;
    }

    public async Task<PracticeGymSuggestionsDto> GetSuggestionsForStudentAsync(
        Guid studentId,
        CancellationToken ct = default)
    {
        var (profile, profileId) = await ResolveProfileAsync(studentId, ct);

        var focusTags   = profile?.FocusAreas   ?? [];
        var contextTags = profile?.LearningGoals ?? [];

        // Phase I2A: SuggestedItems/ContinueItems/ReviewItems no longer read the readiness pool
        // for Practice-Gym-sourced rows — that generation path was removed. They are always
        // empty now; ModuleSuggestions (below) is the sole real content in this DTO. Health is
        // still queried (ReadyCount/ReviewOnlyCount/IsReplenishmentRecommended) since it reports
        // on pool state generically and wasn't part of this pass's scope to change.
        IReadOnlyList<PracticeGymSuggestionItemDto> continueItems = [];
        IReadOnlyList<PracticeGymSuggestionItemDto> reviewItems = [];
        IReadOnlyList<PracticeGymSuggestionItemDto> ranked = [];

        var health = await _replenishment.GetHealthAsync(profileId, ReadinessPoolSource.PracticeGym, ct);

        // Phase H7 — additive, best-effort: a Practice Gym module selection failure must never
        // break the existing readiness-pool-backed suggestions above, so it is wrapped separately.
        PracticeGymModuleSelectionResult? moduleSuggestions = null;
        try
        {
            moduleSuggestions = await _moduleSelector.SelectAsync(
                new PracticeGymModuleSelectionRequest(
                    StudentId: profileId,
                    CefrLevel: profile?.CefrLevel,
                    FocusAreas: focusTags,
                    ContextTags: contextTags),
                ct);

            await _moduleAssignmentRecorder.RecordAsync(profileId, moduleSuggestions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Practice Gym module selection failed for student {StudentProfileId}; ModuleSuggestions will be empty for this request.",
                profileId);
        }

        var dto = new PracticeGymSuggestionsDto
        {
            SuggestedItems             = ranked,
            ContinueItems              = continueItems,
            ReviewItems                = reviewItems,
            ReadyCount                 = health.ReadyCount,
            ReviewOnlyCount            = health.ReviewOnlyCount,
            ReservedCount              = 0,
            IsReplenishmentRecommended = health.NeedsReplenishment,
            GeneratedAtUtc             = DateTime.UtcNow,
            ModuleSuggestions          = moduleSuggestions
        };

        _logger.LogDebug(
            "PracticeGymSuggestions: student={StudentId} suggested={S} continue={C} review={R} replenish={Rep}",
            profileId, dto.SuggestedItems.Count, dto.ContinueItems.Count,
            dto.ReviewItems.Count, dto.IsReplenishmentRecommended);

        return dto;
    }

    public async Task<StartSuggestionResult> StartSuggestionAsync(
        Guid studentId,
        Guid readinessItemId,
        CancellationToken ct = default)
    {
        var (_, profileId) = await ResolveProfileAsync(studentId, ct);

        var item = await _db.StudentActivityReadinessItems
            .FirstOrDefaultAsync(i => i.Id == readinessItemId && i.StudentId == profileId, ct);

        if (item is null)
        {
            _logger.LogWarning(
                "StartSuggestion: item {ItemId} not found for student {StudentId}", readinessItemId, profileId);
            return new StartSuggestionResult { Success = false, FailureReason = "Item not found." };
        }

        // Idempotent: already reserved.
        if (item.Status == ReadinessPoolStatus.Reserved)
        {
            return new StartSuggestionResult
            {
                Success            = true,
                AlreadyReserved    = true,
                LearningActivityId = item.LearningActivityId,
                LearningSessionId  = item.LearningSessionId,
                SessionExerciseId  = item.SessionExerciseId
            };
        }

        if (item.Status is not ReadinessPoolStatus.Ready and not ReadinessPoolStatus.ReviewOnly)
        {
            return new StartSuggestionResult
            {
                Success       = false,
                FailureReason = $"Item is not available (status: {item.Status})."
            };
        }

        if (item.ExpiresAt.HasValue && item.ExpiresAt.Value <= DateTime.UtcNow)
            return new StartSuggestionResult { Success = false, FailureReason = "Item has expired." };

        try
        {
            item.Reserve();
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.Entry(item).State = EntityState.Detached;
            item = await _db.StudentActivityReadinessItems
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == readinessItemId, ct);

            if (item?.Status == ReadinessPoolStatus.Reserved)
            {
                return new StartSuggestionResult
                {
                    Success            = true,
                    AlreadyReserved    = true,
                    LearningActivityId = item.LearningActivityId,
                    LearningSessionId  = item.LearningSessionId,
                    SessionExerciseId  = item.SessionExerciseId
                };
            }
            return new StartSuggestionResult { Success = false, FailureReason = "Reservation conflict." };
        }

        _logger.LogInformation(
            "StartSuggestion: reserved item {ItemId} for student {StudentId} activityId={ActivityId}",
            readinessItemId, profileId, item.LearningActivityId);

        return new StartSuggestionResult
        {
            Success            = true,
            LearningActivityId = item.LearningActivityId,
            LearningSessionId  = item.LearningSessionId,
            SessionExerciseId  = item.SessionExerciseId
        };
    }

    public async Task TryMarkConsumedAsync(Guid studentId, Guid readinessItemId, CancellationToken ct = default)
    {
        var (_, profileId) = await ResolveProfileAsync(studentId, ct);

        var item = await _db.StudentActivityReadinessItems
            .FirstOrDefaultAsync(i => i.Id == readinessItemId && i.StudentId == profileId, ct);

        if (item is null || item.Status != ReadinessPoolStatus.Reserved)
            return;

        try
        {
            item.MarkConsumed();
            await _db.SaveChangesAsync(ct);
            _logger.LogDebug("TryMarkConsumed: consumed item {ItemId} for student {StudentId}", readinessItemId, profileId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "TryMarkConsumed: could not consume item {ItemId} — ignored (best-effort).", readinessItemId);
        }
    }

    // --- Helpers ---

    private async Task<(StudentProfile? profile, Guid profileId)> ResolveProfileAsync(
        Guid studentId, CancellationToken ct)
    {
        var profile = await _db.StudentProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == studentId || p.Id == studentId, ct);

        var profileId = profile?.Id ?? studentId;
        return (profile, profileId);
    }
}

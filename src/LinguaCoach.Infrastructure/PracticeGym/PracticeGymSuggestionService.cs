using LinguaCoach.Application.PracticeGym;
using LinguaCoach.Application.PracticeGymModules;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.PracticeGym;

/// <summary>
/// Phase I2A (legacy fallback deletion): SuggestedItems/ContinueItems/ReviewItems no longer read
/// the student readiness pool for Practice-Gym-sourced rows — that generation path (readiness-
/// pool-backed Practice Gym content, including the review/scaffold pilot) was removed. Those
/// three lists are now always empty. See
/// docs/reviews/2026-07-10-phase-i2a-practice-gym-legacy-deletion-review.md.
///
/// Phase I2C: the readiness pool itself (StudentActivityReadinessItem,
/// IReadinessPoolReplenishmentService) was deleted. StartSuggestionAsync/TryMarkConsumedAsync are
/// now permanently no-ops — with the three lists above always empty, no readinessItemId a
/// frontend could pass in ever refers to a real row. They are kept (rather than removed from the
/// interface/controller) purely for API-contract stability; PracticeGymSuggestionsController's
/// /start and /complete routes and the Angular practice-gym-suggestions.service.ts calls are
/// unchanged. Flagged as a residual for a future cleanup pass. Health counts (ReadyCount/
/// ReviewOnlyCount/IsReplenishmentRecommended) are hardcoded to 0/false — there is no pool to
/// report on. <see cref="PracticeGymSuggestionsDto.ModuleSuggestions"/> (Phase H7, computed via
/// <see cref="IPracticeGymModuleSelectionService"/>) is unaffected — it was already independent
/// of the readiness pool and is now the sole real content in this DTO. See
/// docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md.
/// </summary>
public sealed class PracticeGymSuggestionService : IPracticeGymSuggestionService
{
    private readonly LinguaCoachDbContext _db;
    private readonly IPracticeGymModuleSelectionService _moduleSelector;
    private readonly IPracticeGymModuleAssignmentRecorder _moduleAssignmentRecorder;
    private readonly ILogger<PracticeGymSuggestionService> _logger;

    public PracticeGymSuggestionService(
        LinguaCoachDbContext db,
        IPracticeGymModuleSelectionService moduleSelector,
        IPracticeGymModuleAssignmentRecorder moduleAssignmentRecorder,
        ILogger<PracticeGymSuggestionService> logger)
    {
        _db = db;
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
        // empty now; ModuleSuggestions (below) is the sole real content in this DTO.
        // Phase I2C: the readiness pool itself is gone — ReadyCount/ReviewOnlyCount/
        // IsReplenishmentRecommended are hardcoded to 0/false; there is no pool to report on.
        IReadOnlyList<PracticeGymSuggestionItemDto> continueItems = [];
        IReadOnlyList<PracticeGymSuggestionItemDto> reviewItems = [];
        IReadOnlyList<PracticeGymSuggestionItemDto> ranked = [];

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
            ReadyCount                 = 0,
            ReviewOnlyCount            = 0,
            ReservedCount              = 0,
            IsReplenishmentRecommended = false,
            GeneratedAtUtc             = DateTime.UtcNow,
            ModuleSuggestions          = moduleSuggestions
        };

        _logger.LogDebug(
            "PracticeGymSuggestions: student={StudentId} suggested={S} continue={C} review={R} replenish={Rep}",
            profileId, dto.SuggestedItems.Count, dto.ContinueItems.Count,
            dto.ReviewItems.Count, dto.IsReplenishmentRecommended);

        return dto;
    }

    /// <summary>
    /// Phase I2C: permanently a no-op — the readiness pool is gone, and SuggestedItems/
    /// ContinueItems/ReviewItems have been empty since Phase I2A, so no readinessItemId a
    /// frontend could pass in ever refers to a real row. Kept for API-contract stability. See
    /// docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md.
    /// </summary>
    public Task<StartSuggestionResult> StartSuggestionAsync(
        Guid studentId,
        Guid readinessItemId,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "StartSuggestion: no-op — readiness pool removed (Phase I2C). item={ItemId} student={StudentId}",
            readinessItemId, studentId);
        return Task.FromResult(new StartSuggestionResult { Success = false, FailureReason = "Item not found." });
    }

    /// <summary>
    /// Phase I2C: permanently a no-op — see <see cref="StartSuggestionAsync"/>.
    /// </summary>
    public Task TryMarkConsumedAsync(Guid studentId, Guid readinessItemId, CancellationToken ct = default) =>
        Task.CompletedTask;

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

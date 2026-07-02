using System.Text.Json;
using LinguaCoach.Application.PracticeGym;
using LinguaCoach.Application.ReadinessPool;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.PracticeGym;

/// <summary>
/// Builds personalised Practice Gym suggestion lists from the student readiness pool.
///
/// Ranking order: focus-area match → goal/context match → routing priority → expiry urgency → FIFO.
///
/// Guardrails (defence-in-depth):
///   - Consumed / expired / failed / stale / queued / generating items excluded entirely.
///   - Items with RequiresAdminReview=true are excluded unless per-item AdminReviewStatus=Approved
///     (Phase 19B). PendingReview and Rejected items are excluded entirely.
///   - ReviewOnly status items go to ReviewItems only, never SuggestedItems.
///   - RoutingReason.Review / Scaffold / Remediation items with Ready status
///     go to ReviewItems if IsLowerLevelContent, otherwise may appear in Suggested.
///   - Reserved valid items go to ContinueItems.
///   - general_english is fallback context — workplace is never default.
///
/// Phase 19C pilot gate:
///   - Approved review/scaffold items (RequiresAdminReview=true) are further gated on
///     PracticeGymPilotEnabled. When the pilot is off, they are excluded from ReviewItems
///     entirely — even though generation and admin approval (19A/19B) may already be running.
///   - When the pilot is on, visible scaffold items are capped at MaxStudentVisibleScaffoldSuggestions
///     and their CallToAction/Explanation are replaced with the configured friendly pilot
///     label/reason so wording stays non-negative regardless of RoutingReason.
/// </summary>
public sealed class PracticeGymSuggestionService : IPracticeGymSuggestionService
{
    private readonly LinguaCoachDbContext _db;
    private readonly IReadinessPoolReplenishmentService _replenishment;
    private readonly IEffectiveReadinessPoolSettingsProvider _settingsProvider;

    // Resolved fresh at the top of GetSuggestionsForStudentAsync — see below.
    private ReadinessPoolReplenishmentOptions _opts = new();
    private readonly ILogger<PracticeGymSuggestionService> _logger;

    private const int MaxSuggested = 6;
    private const int MaxContinue  = 3;
    private const int MaxReview    = 4;

    public PracticeGymSuggestionService(
        LinguaCoachDbContext db,
        IReadinessPoolReplenishmentService replenishment,
        IEffectiveReadinessPoolSettingsProvider settingsProvider,
        ILogger<PracticeGymSuggestionService> logger)
    {
        _db = db;
        _replenishment = replenishment;
        _settingsProvider = settingsProvider;
        _logger = logger;
    }

    public async Task<PracticeGymSuggestionsDto> GetSuggestionsForStudentAsync(
        Guid studentId,
        CancellationToken ct = default)
    {
        _opts = await _settingsProvider.GetEffectiveAsync(ct);

        var (profile, profileId) = await ResolveProfileAsync(studentId, ct);

        var focusTags   = profile?.FocusAreas   ?? [];
        var contextTags = profile?.LearningGoals ?? [];

        var rawItems = await _db.StudentActivityReadinessItems
            .AsNoTracking()
            .Where(i => i.StudentId == profileId
                     && i.Source == ReadinessPoolSource.PracticeGym
                     && i.Status != ReadinessPoolStatus.Consumed
                     && i.Status != ReadinessPoolStatus.Expired
                     && i.Status != ReadinessPoolStatus.Failed
                     && i.Status != ReadinessPoolStatus.Stale
                     && i.Status != ReadinessPoolStatus.Skipped
                     && i.Status != ReadinessPoolStatus.Queued
                     && i.Status != ReadinessPoolStatus.Generating
                     && (!i.RequiresAdminReview || i.AdminReviewStatus == AdminReviewStatus.Approved))
            .ToListAsync(ct);

        // ContinueItems: reserved, not expired. Scaffold items stay pilot-gated so rollback
        // (PracticeGymPilotEnabled=false) hides approved-but-unconsumed scaffold items too.
        // Deduped by underlying activity/objective identity — Continue is populated first so it
        // always wins ties against Suggested/Review below.
        var continueCandidates = DedupeByIdentity(rawItems
            .Where(i => i.Status == ReadinessPoolStatus.Reserved
                     && (i.ExpiresAt == null || i.ExpiresAt > DateTime.UtcNow)
                     && (!i.RequiresAdminReview || _opts.PracticeGymPilotEnabled)));

        var continueItems = continueCandidates
            .Take(MaxContinue)
            .Select(ToDto)
            .ToList();

        var claimedKeys = continueCandidates.Select(ItemIdentityKey).ToHashSet();

        // ReviewItems: ReviewOnly status OR Ready+lower-level with review/scaffold/remediation reason.
        // Phase 19C: items that required admin review (i.e. came through the review/scaffold
        // generation pipeline) are pilot-gated on top of the existing per-item approval gate —
        // they only reach students when PracticeGymPilotEnabled=true, and are capped separately.
        var reviewCandidates = DedupeByIdentity(rawItems
            .Where(i => i.Status == ReadinessPoolStatus.ReviewOnly
                     || (i.Status == ReadinessPoolStatus.Ready
                         && i.IsLowerLevelContent
                         && i.RoutingReason != RoutingReason.Normal
                         && i.RoutingReason != RoutingReason.Fallback))
            .Where(i => !i.RequiresAdminReview || _opts.PracticeGymPilotEnabled)
            .Where(i => !claimedKeys.Contains(ItemIdentityKey(i))));

        var nonScaffoldReview = reviewCandidates.Where(i => !i.RequiresAdminReview);
        var scaffoldReview = reviewCandidates
            .Where(i => i.RequiresAdminReview)
            .Take(_opts.MaxStudentVisibleScaffoldSuggestions);

        var reviewKept = nonScaffoldReview
            .Concat(scaffoldReview)
            .Take(MaxReview)
            .ToList();

        var reviewItems = reviewKept.Select(ToDto).ToList();

        claimedKeys.UnionWith(reviewKept.Select(ItemIdentityKey));

        // SuggestedItems: Ready, not lower-level-only review content. Defence-in-depth:
        // scaffold-origin items never appear here even if a future routing change stops
        // pairing them with IsLowerLevelContent — the pilot gate still applies. Also excludes
        // anything already placed in Continue/Review so a single item never appears twice.
        var suggestable = DedupeByIdentity(rawItems
            .Where(i => i.Status == ReadinessPoolStatus.Ready
                     && !(i.IsLowerLevelContent && i.RoutingReason != RoutingReason.Normal && i.RoutingReason != RoutingReason.Fallback)
                     && (!i.RequiresAdminReview || _opts.PracticeGymPilotEnabled))
            .Where(i => !claimedKeys.Contains(ItemIdentityKey(i))));

        var ranked = RankSuggestions(suggestable, focusTags, contextTags)
            .Take(MaxSuggested)
            .Select(ToDto)
            .ToList();

        var health = await _replenishment.GetHealthAsync(profileId, ReadinessPoolSource.PracticeGym, ct);

        var dto = new PracticeGymSuggestionsDto
        {
            SuggestedItems             = ranked,
            ContinueItems              = continueItems,
            ReviewItems                = reviewItems,
            ReadyCount                 = health.ReadyCount,
            ReviewOnlyCount            = health.ReviewOnlyCount,
            ReservedCount              = rawItems.Count(i => i.Status == ReadinessPoolStatus.Reserved),
            IsReplenishmentRecommended = health.NeedsReplenishment,
            GeneratedAtUtc             = DateTime.UtcNow
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

    // Identity used to dedupe suggestion cards, in priority order:
    //   1. objective key + pattern key + activity type — the strongest signal for what a student
    //      actually sees as "the same card": title/CTA/explanation are all derived from these
    //      fields (see ToDto/BuildTitle/BuildCallToAction below), so several readiness-pool rows
    //      queued for one objective (e.g. before ReadinessPoolReplenishmentService.DuplicateKey's
    //      own dedup fix caught up) read as identical cards even though each has a distinct
    //      LearningActivityId — they must collapse to one visible suggestion. This also subsumes
    //      the same-activity case (same activity implies same objective/pattern/type).
    //   2. otherwise materialized activity id, if objective/pattern metadata is missing but the
    //      item is materialized — still a meaningful identity to dedupe on.
    //   3. otherwise the readiness item's own id — no stronger signal to group on, so it stands
    //      alone (never over-dedupe genuinely unrelated content just because metadata is sparse).
    private static string ItemIdentityKey(StudentActivityReadinessItem i)
    {
        if (i.CurriculumObjectiveKey is { Length: > 0 } objectiveKey && i.PatternKey is { Length: > 0 } patternKey)
            return $"objective:{objectiveKey}|{patternKey}|{i.ActivityType}";
        if (i.LearningActivityId is { } activityId)
            return $"activity:{activityId}";
        return $"item:{i.Id}";
    }

    // Removes items that share an identity key, keeping the first occurrence (callers pass
    // already-priority-ordered sequences, e.g. reserved-before-others, oldest-first).
    private static List<StudentActivityReadinessItem> DedupeByIdentity(IEnumerable<StudentActivityReadinessItem> items)
    {
        var seen = new HashSet<string>();
        var result = new List<StudentActivityReadinessItem>();
        foreach (var item in items)
        {
            if (seen.Add(ItemIdentityKey(item)))
                result.Add(item);
        }
        return result;
    }

    private static IEnumerable<StudentActivityReadinessItem> RankSuggestions(
        IEnumerable<StudentActivityReadinessItem> items,
        IReadOnlyList<string> focusTags,
        IReadOnlyList<string> contextTags)
    {
        return items
            .OrderByDescending(i =>
            {
                var itemFocus   = ParseJsonStringArray(i.FocusTagsJson);
                var itemContext = ParseJsonStringArray(i.ContextTagsJson);
                int focusScore   = itemFocus.Count(t => focusTags.Contains(t, StringComparer.OrdinalIgnoreCase));
                int contextScore = itemContext.Count(t => contextTags.Contains(t, StringComparer.OrdinalIgnoreCase));
                return focusScore * 100 + contextScore * 10;
            })
            .ThenBy(i => i.Priority)
            .ThenBy(i => i.ExpiresAt ?? DateTime.MaxValue)
            .ThenBy(i => i.CreatedAt);
    }

    private PracticeGymSuggestionItemDto ToDto(StudentActivityReadinessItem i)
    {
        var (callToAction, explanation) = i.RequiresAdminReview
            ? (_opts.PracticeGymPilotLabel, _opts.PracticeGymPilotReason)
            : BuildCallToAction(i.RoutingReason, i.IsLowerLevelContent, i.PrimarySkill, i.CurriculumObjectiveTitle);

        return new PracticeGymSuggestionItemDto
        {
            ReadinessItemId          = i.Id,
            Title                    = BuildTitle(i.PrimarySkill, i.ActivityType, i.CurriculumObjectiveTitle),
            Description              = explanation,
            PrimarySkill             = i.PrimarySkill,
            SecondarySkills          = ParseJsonStringArray(i.SecondarySkillsJson),
            PatternKey               = i.PatternKey,
            ActivityType             = i.ActivityType,
            TargetCefrLevel          = i.TargetCefrLevel,
            StudentCefrLevelSnapshot = i.OriginalCefrLevelSnapshot,
            CurriculumObjectiveKey   = i.CurriculumObjectiveKey,
            CurriculumObjectiveTitle = i.CurriculumObjectiveTitle,
            ContextTags              = ParseJsonStringArray(i.ContextTagsJson),
            FocusTags                = ParseJsonStringArray(i.FocusTagsJson),
            RoutingReason            = i.RoutingReason.ToString(),
            IsLowerLevelContent      = i.IsLowerLevelContent,
            DifficultyBand           = i.DifficultyBand,
            EstimatedDurationMinutes = i.PreferredSessionDurationMinutes,
            SupportLanguageName      = i.SupportLanguageName,
            Status                   = i.Status.ToString(),
            CallToAction             = callToAction,
            Explanation              = explanation,
            LinkedLearningActivityId = i.LearningActivityId,
            LinkedLearningSessionId  = i.LearningSessionId,
            LinkedSessionExerciseId  = i.SessionExerciseId
        };
    }

    private static string BuildTitle(string? skill, string? activityType, string? objectiveTitle)
    {
        if (!string.IsNullOrWhiteSpace(objectiveTitle))
            return objectiveTitle;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(skill))      parts.Add(Capitalise(skill));
        if (!string.IsNullOrWhiteSpace(activityType)) parts.Add(Capitalise(activityType));
        return parts.Count > 0 ? string.Join(" — ", parts) : "Practice activity";
    }

    private static (string callToAction, string explanation) BuildCallToAction(
        RoutingReason reason, bool isLower, string? skill, string? objectiveTitle)
    {
        var skillLabel = string.IsNullOrWhiteSpace(skill) ? "this skill" : Capitalise(skill);
        return reason switch
        {
            RoutingReason.Normal      => ("Recommended for your current goal",
                                         string.IsNullOrWhiteSpace(objectiveTitle)
                                             ? $"Practice {skillLabel} at your current level."
                                             : $"Practice: {objectiveTitle}"),
            RoutingReason.Review      => ("Review",
                                         $"Revisit {skillLabel} to reinforce what you've learned."),
            RoutingReason.Scaffold    => ("Step back to strengthen basics",
                                         $"Build a stronger foundation in {skillLabel} before moving up."),
            RoutingReason.Remediation => ("Targeted fix",
                                         $"Address a specific gap in {skillLabel}."),
            RoutingReason.Fallback    => ("General practice",
                                         $"General {skillLabel} practice activity."),
            _                         => ("Practice", $"Practice {skillLabel}.")
        };
    }

    private static string Capitalise(string s) =>
        string.IsNullOrWhiteSpace(s) ? s :
        char.ToUpperInvariant(s[0]) + s[1..].Replace('_', ' ');

    private static IReadOnlyList<string> ParseJsonStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
            return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }
}

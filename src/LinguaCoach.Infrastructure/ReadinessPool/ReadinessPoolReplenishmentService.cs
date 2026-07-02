using LinguaCoach.Application.Curriculum;
using LinguaCoach.Application.Learning;
using LinguaCoach.Application.Mastery;
using LinguaCoach.Application.Memory;
using LinguaCoach.Application.ReadinessPool;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Curriculum;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.ReadinessPool;

/// <summary>
/// Maintains readiness pool health for all active students.
///
/// Per-run responsibilities:
///   1. Sweep expired ready items (past ReadyItemExpiryDays).
///   2. Sweep expired reserved items (past ReservedItemExpiryHours).
///   3. Recover orphaned generating items (past GeneratingTimeoutMinutes → failed).
///   4. Retry eligible failed items (attempts &lt; MaxGenerationAttempts, delay elapsed).
///   5. For each active student × source below target: queue new items up to MaxItemsGeneratedPerRun.
///   6. Duplicate prevention: skip if same (student, source, objectiveKey, patternKey, cefrLevel) already queued/generating/ready.
///
/// Review/scaffold rule:
///   AllowReviewOrScaffold is enabled only when GetWeakEventsAsync returns at least one
///   relevant weak event for the student. Otherwise strict same-level routing is used.
///   B2 students will never silently receive B1 content as Normal.
/// </summary>
public sealed class ReadinessPoolReplenishmentService : IReadinessPoolReplenishmentService
{
    private readonly LinguaCoachDbContext _db;
    private readonly IStudentActivityReadinessPoolService _pool;
    private readonly IStudentLearningLedger _ledger;
    private readonly IStudentMasteryEvaluationService _mastery;
    private readonly ILearningGoalContextResolver _goalResolver;
    private readonly ICurriculumRoutingService _routing;
    private readonly IEffectiveReadinessPoolSettingsProvider _settingsProvider;

    // Resolved fresh (from appsettings + any active admin override) at the top of every
    // public entry point — see RunAsync/GetHealthAsync. Defaults to safe class defaults
    // until first resolved.
    private ReadinessPoolReplenishmentOptions _opts = new();
    private readonly ILogger<ReadinessPoolReplenishmentService> _logger;

    public ReadinessPoolReplenishmentService(
        LinguaCoachDbContext db,
        IStudentActivityReadinessPoolService pool,
        IStudentLearningLedger ledger,
        IStudentMasteryEvaluationService mastery,
        ILearningGoalContextResolver goalResolver,
        ICurriculumRoutingService routing,
        IEffectiveReadinessPoolSettingsProvider settingsProvider,
        ILogger<ReadinessPoolReplenishmentService> logger)
    {
        _db = db;
        _pool = pool;
        _ledger = ledger;
        _mastery = mastery;
        _goalResolver = goalResolver;
        _routing = routing;
        _settingsProvider = settingsProvider;
        _logger = logger;
    }

    public async Task<ReplenishmentRunSummary> RunAsync(CancellationToken ct = default)
    {
        _opts = await _settingsProvider.GetEffectiveAsync(ct);

        var started = DateTime.UtcNow;
        var totalQueued = 0;
        var totalExpired = 0;
        var totalRecovered = 0;
        var totalRetryQueued = 0;
        var totalStale = 0;
        var totalSkipped = 0;
        var totalSkippedAtMaxBuffer = 0;
        var totalSkippedDailyCap = 0;
        var hitLimit = false;

        _logger.LogInformation("ReadinessPool replenishment run starting.");

        // Step 1+2: expire ready items past age and reserved items past timeout.
        var expired = await SweepExpiredItemsAsync(ct);
        totalExpired += expired;

        // Step 2b: mark stale items whose CEFR target no longer matches the student's level.
        var staled = await SweepCefrMismatchedItemsAsync(ct);
        totalStale += staled;

        // Step 3: recover orphaned generating items.
        var recovered = await RecoverOrphanedGeneratingAsync(ct);
        totalRecovered += recovered;

        // Step 4+5: retry failed + fill shortfalls per active student.
        var activeStudents = await GetActiveStudentProfilesAsync(ct);
        _logger.LogInformation(
            "ReadinessPool: processing {Count} active students.", activeStudents.Count);

        var generatedThisRun = 0;

        foreach (var profile in activeStudents)
        {
            if (ct.IsCancellationRequested) break;

            var (retried, skippedR) = await RetryFailedItemsAsync(profile, generatedThisRun, ct);
            totalRetryQueued += retried;
            totalSkipped += skippedR;
            generatedThisRun += retried;

            if (generatedThisRun >= _opts.MaxItemsGeneratedPerRun)
            {
                hitLimit = true;
                break;
            }

            foreach (var source in new[] { ReadinessPoolSource.TodayLesson, ReadinessPoolSource.PracticeGym })
            {
                var health = await GetHealthAsync(profile.Id, source, ct);
                if (!health.NeedsReplenishment) continue;

                var toCreate = Math.Min(
                    health.ShortfallCount,
                    _opts.MaxItemsGeneratedPerRun - generatedThisRun);

                if (toCreate <= 0)
                {
                    hitLimit = true;
                    break;
                }

                var (queued, skipped, skippedBuffer, skippedDailyCap) = await FillShortfallAsync(profile, source, toCreate, ct);
                totalQueued += queued;
                totalSkipped += skipped;
                totalSkippedAtMaxBuffer += skippedBuffer;
                totalSkippedDailyCap += skippedDailyCap;
                generatedThisRun += queued;

                if (generatedThisRun >= _opts.MaxItemsGeneratedPerRun)
                {
                    hitLimit = true;
                    break;
                }
            }

            if (hitLimit) break;
        }

        var completedAt = DateTime.UtcNow;
        var summary = new ReplenishmentRunSummary
        {
            StartedAt = started,
            CompletedAt = completedAt,
            StudentsProcessed = activeStudents.Count,
            ItemsQueued = totalQueued,
            ItemsExpired = totalExpired,
            ItemsRecoveredFromGenerating = totalRecovered,
            ItemsRetryQueued = totalRetryQueued,
            ItemsMarkedStale = totalStale,
            SkippedDuplicates = totalSkipped,
            SkippedAtMaxBuffer = totalSkippedAtMaxBuffer,
            SkippedDailyCapReached = totalSkippedDailyCap,
            HitMaxItemsLimit = hitLimit
        };

        _logger.LogInformation(
            "ReadinessPool replenishment complete. queued={Queued} expired={Expired} recovered={Recovered} retried={Retried} skippedDupes={Dupes} skippedMaxBuffer={MaxBuf} stale={Stale} limitHit={Limit} elapsedMs={Elapsed} successRate={Rate:P0}",
            summary.ItemsQueued, summary.ItemsExpired, summary.ItemsRecoveredFromGenerating,
            summary.ItemsRetryQueued, summary.SkippedDuplicates, summary.SkippedAtMaxBuffer,
            summary.ItemsMarkedStale, summary.HitMaxItemsLimit,
            summary.ElapsedMs, summary.GenerationSuccessRate);

        return summary;
    }

    public async Task<PoolHealthSummary> GetHealthAsync(
        Guid studentId,
        ReadinessPoolSource source,
        CancellationToken ct = default)
    {
        _opts = await _settingsProvider.GetEffectiveAsync(ct);

        var target = source == ReadinessPoolSource.TodayLesson
            ? _opts.TodayLessonPoolTargetCount
            : _opts.PracticeGymPoolTargetCount;

        var counts = await _db.StudentActivityReadinessItems
            .Where(i => i.StudentId == studentId && i.Source == source)
            .GroupBy(i => i.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int Get(ReadinessPoolStatus s) => counts.FirstOrDefault(c => c.Status == s)?.Count ?? 0;

        return new PoolHealthSummary
        {
            StudentId = studentId,
            Source = source,
            TargetCount = target,
            ReadyCount = Get(ReadinessPoolStatus.Ready),
            ReservedCount = Get(ReadinessPoolStatus.Reserved),
            // Queued and Generating count toward "in-flight" — do not over-generate.
            QueuedOrGeneratingCount = Get(ReadinessPoolStatus.Queued) + Get(ReadinessPoolStatus.Generating),
            FailedCount = Get(ReadinessPoolStatus.Failed),
            StaleCount = Get(ReadinessPoolStatus.Stale),
            ExpiredCount = Get(ReadinessPoolStatus.Expired),
            SkippedCount = Get(ReadinessPoolStatus.Skipped),
            ReviewOnlyCount = Get(ReadinessPoolStatus.ReviewOnly)
        };
    }

    // --- private helpers ---

    private async Task<List<StudentProfile>> GetActiveStudentProfilesAsync(CancellationToken ct)
    {
        return await _db.StudentProfiles
            .Include(p => p.CareerProfile)
            .Include(p => p.LanguagePair!).ThenInclude(lp => lp.SourceLanguage)
            .Include(p => p.LanguagePair!).ThenInclude(lp => lp.TargetLanguage)
            .Where(p => p.LifecycleStage != StudentLifecycleStage.Archived
                     && p.LifecycleStage >= StudentLifecycleStage.CourseReady
                     && p.OnboardingStatus == OnboardingStatus.Complete)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    private async Task<int> SweepExpiredItemsAsync(CancellationToken ct)
    {
        var readyExpiryCutoff = DateTime.UtcNow.AddDays(-_opts.ReadyItemExpiryDays);
        var reservedExpiryCutoff = DateTime.UtcNow.AddHours(-_opts.ReservedItemExpiryHours);
        var count = 0;

        // Ready items past age.
        var expiredReady = await _db.StudentActivityReadinessItems
            .Where(i => i.Status == ReadinessPoolStatus.Ready
                     && i.UpdatedAt < readyExpiryCutoff)
            .ToListAsync(ct);

        foreach (var item in expiredReady)
        {
            item.Expire("Expired: past ReadyItemExpiryDays.");
            count++;
        }

        // Reserved items stuck past timeout.
        var expiredReserved = await _db.StudentActivityReadinessItems
            .Where(i => i.Status == ReadinessPoolStatus.Reserved
                     && i.ReservedAt != null
                     && i.ReservedAt < reservedExpiryCutoff)
            .ToListAsync(ct);

        foreach (var item in expiredReserved)
        {
            item.Expire("Expired: reserved item past ReservedItemExpiryHours.");
            count++;
        }

        if (count > 0)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("ReadinessPool sweep: expired {Count} items.", count);
        }

        return count;
    }

    /// <summary>
    /// Marks Ready/Reserved items stale when the student's current CEFR level has advanced
    /// beyond the item's target level. A student at B2 should not be served B1 Normal items.
    /// Only applies to Normal routing — review/scaffold/remediation lower-level items are intentional.
    /// </summary>
    private async Task<int> SweepCefrMismatchedItemsAsync(CancellationToken ct)
    {
        // Load (studentId, currentCefrLevel) for all active students with a known CEFR level.
        var studentLevels = await _db.StudentProfiles
            .Where(p => p.LifecycleStage != StudentLifecycleStage.Archived
                     && p.LifecycleStage >= StudentLifecycleStage.CourseReady
                     && p.OnboardingStatus == OnboardingStatus.Complete
                     && p.CefrLevel != null)
            .Select(p => new { p.Id, p.CefrLevel })
            .ToListAsync(ct);

        if (studentLevels.Count == 0) return 0;

        var studentIds = studentLevels.Select(s => s.Id).ToList();

        // Fetch all Ready/Reserved Normal-routing items for active students.
        var candidates = await _db.StudentActivityReadinessItems
            .Where(i => studentIds.Contains(i.StudentId)
                     && (i.Status == ReadinessPoolStatus.Ready || i.Status == ReadinessPoolStatus.Reserved)
                     && i.RoutingReason == RoutingReason.Normal
                     && !i.IsLowerLevelContent)
            .ToListAsync(ct);

        if (candidates.Count == 0) return 0;

        var levelMap = studentLevels.ToDictionary(s => s.Id, s => s.CefrLevel!);
        var count = 0;

        foreach (var item in candidates)
        {
            if (!levelMap.TryGetValue(item.StudentId, out var currentLevel)) continue;

            // Compare CEFR levels. If student has advanced, item target is below current level.
            if (IsBelowCurrentLevel(item.TargetCefrLevel, currentLevel))
            {
                item.MarkStale($"CEFR mismatch: item targets {item.TargetCefrLevel}, student is now {currentLevel}.");
                item.RecordEvaluation();
                count++;
                _logger.LogInformation(
                    "ReadinessPool: marked stale (CEFR mismatch) item {Id} student {StudentId} target={Target} current={Current}.",
                    item.Id, item.StudentId, item.TargetCefrLevel, currentLevel);
            }
            else
            {
                item.RecordEvaluation();
            }
        }

        if (count > 0)
            await _db.SaveChangesAsync(ct);

        return count;
    }

    /// <summary>
    /// Returns true when <paramref name="itemCefr"/> is strictly below <paramref name="studentCefr"/>.
    /// Order: A1 &lt; A2 &lt; B1 &lt; B2 &lt; C1 &lt; C2.
    /// </summary>
    private static bool IsBelowCurrentLevel(string itemCefr, string studentCefr)
    {
        var order = new[] { "A1", "A2", "B1", "B2", "C1", "C2" };
        var itemIdx = Array.IndexOf(order, itemCefr.ToUpperInvariant().Trim());
        var studentIdx = Array.IndexOf(order, studentCefr.ToUpperInvariant().Trim());
        // If either level is unknown/custom, don't demote.
        if (itemIdx < 0 || studentIdx < 0) return false;
        return itemIdx < studentIdx;
    }

    private async Task<int> RecoverOrphanedGeneratingAsync(CancellationToken ct)
    {
        var timeoutCutoff = DateTime.UtcNow.AddMinutes(-_opts.GeneratingTimeoutMinutes);

        var orphaned = await _db.StudentActivityReadinessItems
            .Where(i => i.Status == ReadinessPoolStatus.Generating
                     && i.UpdatedAt < timeoutCutoff)
            .ToListAsync(ct);

        if (orphaned.Count == 0) return 0;

        foreach (var item in orphaned)
        {
            // If under attempt limit, mark failed so retry logic can pick it up.
            // If at limit, keep failed (no retry).
            item.MarkFailed("TIMEOUT", $"Generating timed out after {_opts.GeneratingTimeoutMinutes} minutes.");
            _logger.LogWarning(
                "ReadinessPool: recovered orphaned generating item {Id} for student {StudentId} (attempt {A}).",
                item.Id, item.StudentId, item.AttemptCount);
        }

        await _db.SaveChangesAsync(ct);
        return orphaned.Count;
    }

    private async Task<(int retried, int skipped)> RetryFailedItemsAsync(
        StudentProfile profile,
        int generatedThisRun,
        CancellationToken ct)
    {
        var retryCutoff = DateTime.UtcNow.AddMinutes(-_opts.FailedRetryDelayMinutes);
        var retried = 0;
        var skipped = 0;

        var failedItems = await _db.StudentActivityReadinessItems
            .Where(i => i.StudentId == profile.Id
                     && i.Status == ReadinessPoolStatus.Failed
                     && i.AttemptCount < _opts.MaxGenerationAttempts
                     && i.UpdatedAt < retryCutoff)
            .OrderBy(i => i.UpdatedAt)
            .ToListAsync(ct);

        foreach (var failed in failedItems)
        {
            if (generatedThisRun + retried >= _opts.MaxItemsGeneratedPerRun) break;

            // Create a fresh queued item from the failed item's routing snapshot.
            var req = new CreateReadinessItemRequest
            {
                StudentId = failed.StudentId,
                Source = failed.Source,
                TargetCefrLevel = failed.TargetCefrLevel,
                RoutingReason = failed.RoutingReason,
                IsLowerLevelContent = failed.IsLowerLevelContent,
                CurriculumObjectiveKey = failed.CurriculumObjectiveKey,
                CurriculumObjectiveTitle = failed.CurriculumObjectiveTitle,
                PrimarySkill = failed.PrimarySkill,
                SecondarySkillsJson = failed.SecondarySkillsJson,
                ContextTagsJson = failed.ContextTagsJson,
                FocusTagsJson = failed.FocusTagsJson,
                PatternKey = failed.PatternKey,
                ActivityType = failed.ActivityType,
                DifficultyBand = failed.DifficultyBand,
                OriginalCefrLevelSnapshot = failed.OriginalCefrLevelSnapshot,
                RoutingExplanation = failed.RoutingExplanation,
                PreferredSessionDurationMinutes = failed.PreferredSessionDurationMinutes,
                DifficultyPreference = failed.DifficultyPreference,
                SupportLanguageCode = failed.SupportLanguageCode,
                SupportLanguageName = failed.SupportLanguageName,
                TranslationHelpPreference = failed.TranslationHelpPreference,
                GeneratedBy = "ReadinessPoolReplenishment:Retry",
                ExpiresAt = DateTime.UtcNow.AddDays(_opts.ReadyItemExpiryDays)
            };

            await _pool.CreateQueuedAsync(req, ct);
            retried++;

            _logger.LogInformation(
                "ReadinessPool: queued retry for failed item {Id} student {StudentId} source={Source} attempt={A}.",
                failed.Id, failed.StudentId, failed.Source, failed.AttemptCount);
        }

        return (retried, skipped);
    }

    private async Task<(int queued, int skipped, int skippedAtMaxBuffer, int skippedDailyCap)> FillShortfallAsync(
        StudentProfile profile,
        ReadinessPoolSource source,
        int toCreate,
        CancellationToken ct)
    {
        var queued = 0;
        var skipped = 0;
        var skippedAtMaxBuffer = 0;
        var skippedDailyCap = 0;

        // Fetch mastered objective keys so routing can exclude them from new-learning slots.
        // Also used below to derive the deterministic confidence band for review/scaffold gating.
        var masteryReport = await _mastery.EvaluateStudentAsync(
            profile.Id, MasteryEvaluationReason.BeforeReplenishment, ct);
        var masteredKeys = (IReadOnlyList<string>)masteryReport.MasteredObjectiveKeys;

        // Determine if review/scaffold is safe for this source based on weak ledger signals,
        // source allow-list, confidence banding, and the per-student daily cap.
        var allowReviewOrScaffold = false;
        var itemRequiresAdminReview = false;
        if (_opts.EnableReviewScaffoldGeneration)
        {
            var sourceAllowed = _opts.ScaffoldAllowedSources.Contains(source.ToString(), StringComparer.OrdinalIgnoreCase)
                && (source != ReadinessPoolSource.TodayLesson || _opts.AllowTodayLessonInsertion);

            if (sourceAllowed)
            {
                var weakEvents = await _ledger.GetWeakEventsAsync(profile.Id, limit: 5, ct);
                if (weakEvents.Count > 0)
                {
                    var confidence = masteryReport.AtRiskObjectiveKeys.Count > 0
                        ? ReviewNeedConfidence.High
                        : masteryReport.WeakObjectiveKeys.Count > 0
                            ? ReviewNeedConfidence.Medium
                            : ReviewNeedConfidence.Low;

                    var minimumConfidence = Enum.TryParse<ReviewNeedConfidence>(
                        _opts.MinimumConfidenceForReviewNeed, ignoreCase: true, out var parsed)
                        ? parsed
                        : ReviewNeedConfidence.Medium;

                    if (confidence >= minimumConfidence)
                    {
                        var todayScaffoldCount = await _db.StudentActivityReadinessItems
                            .CountAsync(i => i.StudentId == profile.Id
                                          && i.CreatedAt >= DateTime.UtcNow.Date
                                          && i.RoutingReason != RoutingReason.Normal
                                          && i.GeneratedBy != null
                                          && i.GeneratedBy.StartsWith("ReadinessPoolReplenishment"), ct);

                        if (todayScaffoldCount >= _opts.MaxScaffoldItemsPerStudentPerDay)
                        {
                            skippedDailyCap++;
                        }
                        else
                        {
                            allowReviewOrScaffold = true;
                            itemRequiresAdminReview = _opts.RequireAdminReview;
                        }
                    }
                }
            }
        }

        var resolvedGoalContext = _goalResolver.Resolve(
            profile, new LearningGoalResolutionContext { Source = "ReadinessPoolReplenishment" });

        // Enforce MaxBufferCount: count current active (non-terminal, non-stale) items.
        var activeCount = await _db.StudentActivityReadinessItems
            .CountAsync(i => i.StudentId == profile.Id
                          && i.Source == source
                          && (i.Status == ReadinessPoolStatus.Queued
                           || i.Status == ReadinessPoolStatus.Generating
                           || i.Status == ReadinessPoolStatus.Ready
                           || i.Status == ReadinessPoolStatus.Reserved), ct);

        if (activeCount >= _opts.MaxBufferCount)
        {
            _logger.LogDebug(
                "ReadinessPool: student {StudentId} source={Source} already at MaxBufferCount ({Count}/{Max}), skipping fill.",
                profile.Id, source, activeCount, _opts.MaxBufferCount);
            return (0, 0, toCreate, 0);
        }

        // Cap toCreate so we never exceed MaxBufferCount.
        var canCreate = Math.Min(toCreate, _opts.MaxBufferCount - activeCount);
        if (canCreate < toCreate)
        {
            skippedAtMaxBuffer += toCreate - canCreate;
            toCreate = canCreate;
        }

        // Build a set of existing active item keys to prevent duplicates. PatternKey is only
        // assigned during materialization (PracticeGymGenerationJob), well after an item is
        // queued here — so it is always null at queue time. Keying on PatternKey would let this
        // dedup check compare (Obj, null, Cefr) against a materialized item's (Obj, "actual_pattern",
        // Cefr) and never match, silently re-queuing duplicates for the same objective/level
        // forever. Dedup on objective + CEFR level only.
        var existingKeys = await _db.StudentActivityReadinessItems
            .Where(i => i.StudentId == profile.Id
                     && i.Source == source
                     && (i.Status == ReadinessPoolStatus.Queued
                      || i.Status == ReadinessPoolStatus.Generating
                      || i.Status == ReadinessPoolStatus.Ready
                      || i.Status == ReadinessPoolStatus.Reserved))
            .Select(i => new DuplicateKey(
                i.CurriculumObjectiveKey,
                i.TargetCefrLevel))
            .ToListAsync(ct);

        var existingKeySet = existingKeys.ToHashSet();

        // Determine primary skills to target based on source.
        var skills = source == ReadinessPoolSource.TodayLesson
            ? new[] { "writing", "listening", "speaking", "vocabulary" }
            : new[] { "writing", "listening", "speaking", "vocabulary", "reading" };

        var skillIndex = 0;

        for (var i = 0; i < toCreate; i++)
        {
            // Rotate through skills for variety.
            var skill = skills[skillIndex % skills.Length];
            skillIndex++;

            var routingMode = allowReviewOrScaffold ? RoutingMode.Review : RoutingMode.NewLearning;
            var routingRequest = CurriculumRoutingRequestFactory.Build(
                profile, resolvedGoalContext,
                source: $"ReadinessPoolReplenishment:{source}",
                primarySkill: skill,
                allowReviewOrScaffold: allowReviewOrScaffold,
                masteredObjectiveKeys: masteredKeys,
                allowReviewOfMastered: allowReviewOrScaffold,
                mode: routingMode);

            CurriculumRoutingRecommendation routing;
            try
            {
                routing = await _routing.RecommendAsync(routingRequest, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "ReadinessPool: routing failed for student {Id} skill={Skill}. Skipping slot.",
                    profile.Id, skill);
                skipped++;
                continue;
            }

            // Duplicate check.
            var key = new DuplicateKey(
                routing.CurriculumObjectiveKey,
                routing.TargetCefrLevel);

            if (existingKeySet.Contains(key))
            {
                skipped++;
                continue;
            }

            existingKeySet.Add(key);

            // DryRunOnly: review/scaffold items are computed (routing, dedup, caps all still
            // apply) but never persisted. Normal new-learning items are unaffected regardless
            // of DryRunOnly — this flag only gates the review/scaffold generation path.
            var isScaffoldItem = allowReviewOrScaffold && routing.RoutingReason != RoutingReason.Normal;
            if (isScaffoldItem && _opts.DryRunOnly)
            {
                skipped++;
                _logger.LogDebug(
                    "ReadinessPool: DryRunOnly active — simulated (not persisted) scaffold item for student {StudentId} source={Source} obj={Obj}.",
                    profile.Id, source, routing.CurriculumObjectiveKey);
                continue;
            }

            var req = ReadinessItemRequestBuilder.FromRoutingRecommendation(
                studentId: profile.Id,
                source: source,
                recommendation: routing,
                originalCefrLevelSnapshot: profile.CefrLevel,
                preferredSessionDurationMinutes: profile.PreferredSessionDurationMinutes,
                difficultyPreference: profile.DifficultyPreference?.ToString(),
                supportLanguageCode: profile.LanguagePair?.SourceLanguage?.Code,
                supportLanguageName: profile.LanguagePair?.SourceLanguage?.Name,
                translationHelpPreference: profile.TranslationHelpPreference?.ToString(),
                generatedBy: "ReadinessPoolReplenishment",
                expiresAt: DateTime.UtcNow.AddDays(_opts.ReadyItemExpiryDays),
                requiresAdminReview: itemRequiresAdminReview && routing.RoutingReason != RoutingReason.Normal);

            await _pool.CreateQueuedAsync(req, ct);
            queued++;

            _logger.LogDebug(
                "ReadinessPool: queued item for student {StudentId} source={Source} cefr={Cefr} skill={Skill} obj={Obj}.",
                profile.Id, source, routing.TargetCefrLevel, skill, routing.CurriculumObjectiveKey);
        }

        return (queued, skipped, skippedAtMaxBuffer, skippedDailyCap);
    }

    // Value type for duplicate detection (objective key + cefr level). PatternKey is
    // deliberately excluded — it is only known after materialization, so including it here
    // would make queue-time and materialized-item keys never match. See usage above.
    private readonly record struct DuplicateKey(
        string? ObjectiveKey,
        string CefrLevel);
}

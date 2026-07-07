using System.Text.Json;
using LinguaCoach.Application.LearningPlan;
using LinguaCoach.Application.Placement;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinguaCoach.Infrastructure.Placement;

/// <summary>
/// Deterministic adaptive placement assessment service (Phase 13B).
/// Real response submission, deterministic scoring, adaptive item selection,
/// confidence-based completion. No AI calls, no simulation.
///
/// Form.io-native migration: every item is authored via FormIoSchemaJson/ScoringRulesJson.
/// The adaptive selection/confidence/completion algorithm below is unchanged from the prior
/// QuestionContent-based phase — only the schema/scoring/submission seam changed.
/// </summary>
public sealed class PlacementAssessmentService : IPlacementAssessmentService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly LinguaCoachDbContext _db;
    private readonly ILearningPlanService _learningPlan;
    private readonly IPlacementScoringService _scoring;
    private readonly IPlacementSpeakingScorer _speakingScorer;
    private readonly PlacementAssessmentOptions _opts;
    private readonly ILogger<PlacementAssessmentService> _logger;

    public PlacementAssessmentService(
        LinguaCoachDbContext db,
        ILearningPlanService learningPlan,
        IPlacementScoringService scoring,
        IPlacementSpeakingScorer speakingScorer,
        IOptions<PlacementAssessmentOptions> opts,
        ILogger<PlacementAssessmentService> logger)
    {
        _db = db;
        _learningPlan = learningPlan;
        _scoring = scoring;
        _speakingScorer = speakingScorer;
        _opts = opts.Value;
        _logger = logger;
    }

    // ── Item bank (Phase 20I-4: admin-configurable, loaded from PlacementItemDefinition) ───

    private record PlacementItemTemplate(
        Guid DefinitionId, string Skill, string CefrLevel,
        string? FormIoSchemaJson, string? ScoringRulesJson, int ScoringRulesVersion);

    /// <summary>Loads the enabled item bank once per outer call — replaces the old hardcoded static list.</summary>
    private async Task<List<PlacementItemTemplate>> LoadItemBankAsync(CancellationToken ct)
    {
        var rows = await _db.PlacementItemDefinitions
            .Where(i => i.IsEnabled)
            .OrderBy(i => i.ItemOrder)
            .ToListAsync(ct);

        return rows.Select(i => new PlacementItemTemplate(
            i.Id, i.Skill, i.CefrLevel,
            i.FormIoSchemaJson, i.ScoringRulesJson, i.ScoringRulesVersion)).ToList();
    }

    /// <summary>Dedup identity for "has this item already been issued in this assessment" — the
    /// stable PlacementItemDefinition FK (SourceItemDefinitionId), populated for every Form.io-authored
    /// item since the template side no longer carries a Prompt to fall back to.</summary>
    private static bool IsUsed(PlacementItemTemplate template, IReadOnlyCollection<PlacementAssessmentItem> issuedItems) =>
        issuedItems.Any(issued => issued.SourceItemDefinitionId == template.DefinitionId);

    private static readonly string[] CefrLevels = ["A1", "A2", "B1", "B2"];

    private static readonly Dictionary<string, string> SkillLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["grammar"] = "Grammar",
        ["vocabulary"] = "Vocabulary",
        ["listening"] = "Listening",
        ["reading"] = "Reading",
        ["writing"] = "Writing",
        ["speaking"] = "Speaking",
    };

    // ── Confidence model ────────────────────────────────────────────────────────

    private record SkillConfidenceState(
        string EstimatedLevel,
        double Confidence,
        int EvidenceCount,
        int ConsecutiveSuccesses,
        int ConsecutiveFailures);

    private static SkillConfidenceState ComputeSkillConfidence(
        IEnumerable<PlacementAssessmentItem> allItems,
        string skill,
        string fallbackLevel)
    {
        var answered = allItems
            .Where(i => i.Skill == skill && i.IsCorrect.HasValue)
            .OrderBy(i => i.ItemOrder)
            .ToList();

        if (answered.Count == 0)
            return new SkillConfidenceState(fallbackLevel, 0.0, 0, 0, 0);

        // Find highest CEFR level where pass rate >= 70%
        var byLevel = answered
            .GroupBy(i => i.TargetCefrLevel)
            .ToDictionary(g => g.Key, g => (Total: g.Count(), Correct: g.Count(x => x.IsCorrect == true)));

        string? highestPassed = null;
        foreach (var level in CefrLevels)
        {
            if (!byLevel.TryGetValue(level, out var stats) || stats.Total == 0) continue;
            var rate = (double)stats.Correct / stats.Total;
            if (rate >= 0.70)
                highestPassed = level;
            else if (rate < 0.40)
                break;
        }

        var estimated = highestPassed ?? fallbackLevel;
        var avgScore = answered.Average(i => i.Score ?? 0.0);

        // Confidence = weighted blend of evidence depth and quality
        var evidenceWeight = Math.Min(answered.Count / 6.0, 1.0);
        var confidence = (evidenceWeight * 0.6) + (avgScore * 0.4);

        // Count trailing consecutive successes and failures
        var consecutiveSuccesses = 0;
        for (var i = answered.Count - 1; i >= 0; i--)
        {
            if (answered[i].IsCorrect == true) consecutiveSuccesses++;
            else break;
        }
        var consecutiveFailures = 0;
        for (var i = answered.Count - 1; i >= 0; i--)
        {
            if (answered[i].IsCorrect == false) consecutiveFailures++;
            else break;
        }

        if (consecutiveSuccesses >= 3) confidence = Math.Min(confidence + 0.10, 1.0);
        if (consecutiveFailures >= 3) confidence = Math.Max(confidence - 0.15, 0.0);

        return new SkillConfidenceState(estimated, Math.Round(confidence, 3), answered.Count, consecutiveSuccesses, consecutiveFailures);
    }

    // ── Adaptive next-item selection ────────────────────────────────────────────

    private static PlacementItemTemplate? SelectNextTemplate(
        IReadOnlyCollection<PlacementAssessmentItem> allItems,
        string skill,
        SkillConfidenceState state,
        IReadOnlyList<PlacementItemTemplate> itemBank)
    {
        // Determine target level from last answered item for this skill
        var lastForSkill = allItems
            .Where(i => i.Skill == skill && i.IsCorrect.HasValue)
            .OrderByDescending(i => i.ItemOrder)
            .FirstOrDefault();

        string targetLevel;
        if (lastForSkill is null)
        {
            targetLevel = state.EstimatedLevel;
        }
        else
        {
            var lastIdx = Array.IndexOf(CefrLevels, lastForSkill.TargetCefrLevel);
            var lastScore = lastForSkill.Score ?? 0.0;

            if (lastScore >= 0.8 && lastIdx < CefrLevels.Length - 1)
                targetLevel = CefrLevels[lastIdx + 1]; // harder
            else if (lastScore < 0.4 && lastIdx > 0)
                targetLevel = CefrLevels[lastIdx - 1]; // easier
            else
                targetLevel = lastForSkill.TargetCefrLevel; // same
        }

        // Try target level, then adjacent levels
        var targetIdx = Array.IndexOf(CefrLevels, targetLevel);
        if (targetIdx < 0) targetIdx = 1;

        var levelsToTry = new List<string> { CefrLevels[targetIdx] };
        if (targetIdx + 1 < CefrLevels.Length) levelsToTry.Add(CefrLevels[targetIdx + 1]);
        if (targetIdx - 1 >= 0) levelsToTry.Add(CefrLevels[targetIdx - 1]);

        foreach (var level in levelsToTry)
        {
            var candidate = itemBank.FirstOrDefault(
                t => t.Skill == skill && t.CefrLevel == level && !IsUsed(t, allItems));
            if (candidate is not null) return candidate;
        }

        return null;
    }

    // ── Completion check ────────────────────────────────────────────────────────

    private bool ShouldComplete(
        IReadOnlyCollection<PlacementAssessmentItem> items,
        Dictionary<string, SkillConfidenceState> states,
        IReadOnlyList<PlacementItemTemplate> itemBank,
        out string completionReason)
    {
        var answeredCount = items.Count(i => i.IsCorrect.HasValue);

        if (answeredCount >= _opts.MaxItems)
        {
            completionReason = "max_items_reached";
            return true;
        }

        var allConfident = _opts.SkillsToAssess.All(skill =>
            states.TryGetValue(skill, out var s) && s.Confidence >= _opts.ConfidenceThreshold);

        if (allConfident)
        {
            completionReason = "confidence_threshold_reached";
            return true;
        }

        // Check whether all item bank slots are exhausted for pending skills
        var allExhausted = _opts.SkillsToAssess
            .Where(skill => !states.TryGetValue(skill, out var s) || s.Confidence < _opts.ConfidenceThreshold)
            .All(skill => !itemBank.Any(t => t.Skill == skill && !IsUsed(t, items)));

        if (allExhausted)
        {
            completionReason = "items_exhausted";
            return true;
        }

        completionReason = string.Empty;
        return false;
    }

    // ── Skill selection (assess least-evidenced skill first) ────────────────────

    private string? SelectNextSkill(
        IReadOnlyCollection<PlacementAssessmentItem> items,
        Dictionary<string, SkillConfidenceState> states,
        IReadOnlyList<PlacementItemTemplate> itemBank)
    {
        return _opts.SkillsToAssess
            .Where(skill =>
            {
                if (states.TryGetValue(skill, out var s) && s.Confidence >= _opts.ConfidenceThreshold)
                    return false; // already confident enough
                return itemBank.Any(t => t.Skill == skill && !IsUsed(t, items));
            })
            .OrderBy(skill => states.TryGetValue(skill, out var s) ? s.EvidenceCount : 0)
            .FirstOrDefault();
    }

    // ── Per-skill final results ─────────────────────────────────────────────────

    private List<PlacementSkillResult> BuildSkillResults(Guid assessmentId, IReadOnlyCollection<PlacementAssessmentItem> items)
    {
        var results = new List<PlacementSkillResult>();
        foreach (var skill in _opts.SkillsToAssess)
        {
            var state = ComputeSkillConfidence(items, skill, _opts.StartingLevelFallback);
            results.Add(PlacementSkillResult.Create(
                assessmentId,
                skill,
                state.EstimatedLevel,
                state.Confidence,
                state.EvidenceCount,
                state.EvidenceCount >= 2 ? $"Demonstrated {state.EstimatedLevel} competency in {skill}." : null,
                state.EvidenceCount < 2 ? $"Insufficient evidence for {skill}." : null));
        }
        return results;
    }

    private static string ComputeOverallCefr(IEnumerable<PlacementSkillResult> results, string fallback)
    {
        var indices = results
            .Select(r => Array.IndexOf(CefrLevels, r.EstimatedCefrLevel))
            .Where(i => i >= 0)
            .ToList();
        return indices.Count == 0 ? fallback : CefrLevels[indices.Min()];
    }

    // ── DTO builders ─────────────────────────────────────────────────────────────

    private static PlacementAssessmentSummaryDto ToSummaryDto(
        PlacementAssessment assessment,
        IReadOnlyList<PlacementSkillResult> skillResults,
        bool learningPlanRegenerated = false,
        string? learningPlanWarning = null)
    {
        var skillDtos = skillResults.Select(r => new PlacementSkillResultDto(
            r.Skill,
            r.EstimatedCefrLevel,
            r.Confidence,
            r.EvidenceCount,
            r.Strengths,
            r.Weaknesses,
            (r.RecommendedStartingObjectiveKeys
                ?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim())
                .ToList()
                ?? new List<string>()) as IReadOnlyList<string>
        )).ToList();

        return new PlacementAssessmentSummaryDto(
            assessment.Id,
            assessment.StudentProfileId,
            assessment.Status.ToString(),
            assessment.StartedAtUtc,
            assessment.CompletedAtUtc,
            assessment.ExpiredAtUtc,
            assessment.OverallEstimatedLevel,
            assessment.OverallConfidence,
            assessment.IsProvisional,
            assessment.ResultSummary,
            assessment.Source,
            skillDtos,
            learningPlanRegenerated,
            learningPlanWarning,
            assessment.Items.Count);
    }

    /// <summary>
    /// Used when the profile's lifecycle stage says placement is complete but no completed
    /// adaptive assessment row exists for this student (e.g. placed via the legacy flow).
    /// Avoids starting a new, never-completable adaptive assessment for an already-placed student.
    /// </summary>
    private static PlacementAssessmentSummaryDto ToSyntheticCompletedSummaryDto(StudentProfile profile) =>
        new(
            AssessmentId: Guid.Empty,
            StudentProfileId: profile.Id,
            Status: PlacementStatus.Completed.ToString(),
            StartedAtUtc: null,
            CompletedAtUtc: null,
            ExpiredAtUtc: null,
            OverallCefrLevel: profile.CefrLevel,
            OverallConfidence: null,
            IsProvisional: false,
            ResultSummary: "Placement was completed through an earlier process.",
            Source: "LifecycleStage",
            SkillResults: [],
            LearningPlanRegenerated: false,
            LearningPlanRegenerationWarning: null,
            ItemCount: 0);

    private static PlacementHistoryItemDto ToHistoryDto(PlacementAssessment a) =>
        new(a.Id, a.Status.ToString(), a.StartedAtUtc, a.CompletedAtUtc,
            a.OverallEstimatedLevel, a.OverallConfidence, a.IsProvisional, a.Items.Count);

    private static PlacementItemHistoryDto ToItemHistoryDto(PlacementAssessmentItem i)
    {
        var submissionData = i.SubmissionDataJson is null
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(i.SubmissionDataJson, JsonOptions);
        var normalizedAnswer = i.NormalizedAnswerJson is null
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, string?>>(i.NormalizedAnswerJson, JsonOptions);

        return new(i.Id, i.Skill, i.TargetCefrLevel, i.ItemType, i.Prompt,
            submissionData, normalizedAnswer, i.IsCorrect, i.Score, i.EvaluatedAtUtc,
            i.DurationSeconds, i.ItemOrder);
    }

    private static ScoringRulesDocument? TryParseScoringDoc(string? scoringRulesJson)
    {
        if (string.IsNullOrWhiteSpace(scoringRulesJson)) return null;
        try
        {
            return JsonSerializer.Deserialize<ScoringRulesDocument>(scoringRulesJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // ── IPlacementAssessmentService ──────────────────────────────────────────────

    public async Task<PlacementAssessmentSummaryDto> StartAssessmentAsync(
        Guid studentProfileId, string source, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles
            .FirstOrDefaultAsync(p => p.Id == studentProfileId, ct)
            ?? throw new InvalidOperationException($"Student profile {studentProfileId} not found.");

        // The student's lifecycle stage is the trusted signal for "placement is actually done" —
        // it may have been set by a different flow (e.g. the legacy static-section placement)
        // than this adaptive one. Starting a fresh adaptive assessment here would create a
        // second, orphaned InProgress row that nothing ever completes, causing /profile (which
        // reads the latest assessment row) to disagree with /dashboard (which reads LifecycleStage).
        if (profile.LifecycleStage >= StudentLifecycleStage.PlacementCompleted)
        {
            var completed = await _db.PlacementAssessments
                .Include(a => a.Items)
                .Where(a => a.StudentProfileId == studentProfileId && a.Status == PlacementStatus.Completed)
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (completed is not null)
            {
                var completedResults = await _db.PlacementSkillResults
                    .Where(r => r.PlacementAssessmentId == completed.Id)
                    .ToListAsync(ct);
                return ToSummaryDto(completed, completedResults);
            }

            return ToSyntheticCompletedSummaryDto(profile);
        }

        var existing = await _db.PlacementAssessments
            .Include(a => a.Items)
            .Where(a => a.StudentProfileId == studentProfileId && a.Status == PlacementStatus.InProgress)
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
        {
            var existingResults = await _db.PlacementSkillResults
                .Where(r => r.PlacementAssessmentId == existing.Id)
                .ToListAsync(ct);
            return ToSummaryDto(existing, existingResults);
        }

        var startingLevel = !string.IsNullOrWhiteSpace(profile.CefrLevel)
            ? profile.CefrLevel
            : _opts.StartingLevelFallback;

        var assessment = PlacementAssessment.CreateAdaptive(studentProfileId, source);
        assessment.Start();

        _db.PlacementAssessments.Add(assessment);

        // Phase 14A — transition lifecycle to PlacementInProgress
        if (profile.LifecycleStage == StudentLifecycleStage.PlacementRequired)
            profile.SetLifecycleStage(StudentLifecycleStage.PlacementInProgress);

        await _db.SaveChangesAsync(ct);

        var itemBank = await LoadItemBankAsync(ct);
        var items = CreateInitialItems(assessment.Id, startingLevel, itemBank, _opts);
        _db.PlacementAssessmentItems.AddRange(items);
        await _db.SaveChangesAsync(ct);

        var loaded = await _db.PlacementAssessments
            .Include(a => a.Items)
            .FirstAsync(a => a.Id == assessment.Id, ct);

        return ToSummaryDto(loaded, new List<PlacementSkillResult>());
    }

    private static List<PlacementAssessmentItem> CreateInitialItems(
        Guid assessmentId, string startingLevel, IReadOnlyList<PlacementItemTemplate> itemBank, PlacementAssessmentOptions opts)
    {
        var items = new List<PlacementAssessmentItem>();
        var order = 0;

        var startIdx = Array.IndexOf(CefrLevels, startingLevel);
        if (startIdx < 0) startIdx = 1;

        foreach (var skill in opts.SkillsToAssess)
        {
            var atLevel = itemBank
                .Where(t => t.Skill == skill && t.CefrLevel == CefrLevels[startIdx])
                .Take(2);

            foreach (var template in atLevel)
            {
                items.Add(PlacementAssessmentItem.Create(
                    assessmentId, template.Skill, template.CefrLevel,
                    PlacementItemSchemaLabel.ExtractComponentType(template.FormIoSchemaJson),
                    PlacementItemSchemaLabel.ExtractLabel(template.FormIoSchemaJson), order++,
                    template.DefinitionId, template.FormIoSchemaJson,
                    template.ScoringRulesJson, template.ScoringRulesVersion));
            }

            if (startIdx + 1 < CefrLevels.Length)
            {
                var aboveLevel = itemBank
                    .Where(t => t.Skill == skill && t.CefrLevel == CefrLevels[startIdx + 1])
                    .Take(1);

                foreach (var template in aboveLevel)
                {
                    items.Add(PlacementAssessmentItem.Create(
                        assessmentId, template.Skill, template.CefrLevel,
                        PlacementItemSchemaLabel.ExtractComponentType(template.FormIoSchemaJson),
                        PlacementItemSchemaLabel.ExtractLabel(template.FormIoSchemaJson), order++,
                        template.DefinitionId, template.FormIoSchemaJson,
                        template.ScoringRulesJson, template.ScoringRulesVersion));
                }
            }
        }

        return items;
    }

    public async Task<PlacementAssessmentSummaryDto?> GetLatestAssessmentAsync(
        Guid studentProfileId, CancellationToken ct = default)
    {
        // Prefer a Completed row over a later InProgress one — a stray InProgress row can be
        // created after placement is already complete (see StartAssessmentAsync's guard above),
        // and without this a newer stray row would otherwise shadow the real completed result.
        var assessment = await _db.PlacementAssessments
            .Include(a => a.Items)
            .Where(a => a.StudentProfileId == studentProfileId)
            .OrderByDescending(a => a.Status == PlacementStatus.Completed)
            .ThenByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (assessment is null)
        {
            var profile = await _db.StudentProfiles
                .FirstOrDefaultAsync(p => p.Id == studentProfileId, ct);

            return profile is not null && profile.LifecycleStage >= StudentLifecycleStage.PlacementCompleted
                ? ToSyntheticCompletedSummaryDto(profile)
                : null;
        }

        var skillResults = await _db.PlacementSkillResults
            .Where(r => r.PlacementAssessmentId == assessment.Id)
            .ToListAsync(ct);

        return ToSummaryDto(assessment, skillResults);
    }

    public async Task<IReadOnlyList<PlacementHistoryItemDto>> GetHistoryAsync(
        Guid studentProfileId, CancellationToken ct = default)
    {
        var assessments = await _db.PlacementAssessments
            .Include(a => a.Items)
            .Where(a => a.StudentProfileId == studentProfileId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        return assessments.Select(ToHistoryDto).ToList();
    }

    // Phase 13B — Real response submission
    public async Task<SubmitResponseResult> SubmitResponseAsync(
        Guid assessmentId, Guid itemId, IReadOnlyDictionary<string, JsonElement> submissionData,
        int? durationSeconds, string? skillFilter = null, CancellationToken ct = default)
    {
        var assessment = await _db.PlacementAssessments
            .Include(a => a.Items)
            .FirstOrDefaultAsync(a => a.Id == assessmentId, ct)
            ?? throw new InvalidOperationException($"Assessment {assessmentId} not found.");

        if (assessment.Status != PlacementStatus.InProgress)
            throw new InvalidOperationException($"Assessment is {assessment.Status} — cannot accept responses.");

        var item = assessment.Items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException($"Item {itemId} not found in assessment {assessmentId}.");

        if (item.IsCorrect.HasValue)
        {
            // Idempotent: return existing result without re-scoring
            var existingStates = _opts.SkillsToAssess
                .Distinct()
                .ToDictionary(s => s, s => ComputeSkillConfidence(assessment.Items, s, _opts.StartingLevelFallback));
            var existingItemBank = await LoadItemBankAsync(ct);
            var isAlreadyComplete = ShouldComplete(assessment.Items, existingStates, existingItemBank, out _);

            PlacementNextItemDto? existingNext = null;
            if (!isAlreadyComplete)
                existingNext = ToNextItemDto(FindUnansweredItem(assessment.Items, skillFilter), assessment.Items, existingStates);

            return new SubmitResponseResult(
                item.Id, item.IsCorrect!.Value, item.Score!.Value,
                "Response already recorded for this item.",
                assessment.Status == PlacementStatus.Completed,
                null, existingNext, null);
        }

        // Score the response — deterministically, unless this item has a "speaking" component,
        // in which case it's routed to the AI speaking evaluator instead.
        var scoringDoc = TryParseScoringDoc(item.ScoringRulesJsonSnapshot);
        var scoreResult = scoringDoc is not null && _speakingScorer.CanScore(scoringDoc)
            ? await _speakingScorer.ScoreAsync(
                item.Id, assessment.StudentProfileId, item.Prompt, item.TargetCefrLevel,
                scoringDoc, submissionData, ct)
            : _scoring.ScoreSubmission(item.ScoringRulesJsonSnapshot, submissionData);
        var submissionJson = JsonSerializer.Serialize(submissionData.ToDictionary(kv => kv.Key, kv => kv.Value));
        var normalizedJson = JsonSerializer.Serialize(
            scoreResult.Components.ToDictionary(c => c.ComponentKey, c => c.NormalizedValue));
        item.RecordResponse(submissionJson, normalizedJson, scoreResult.IsCorrect, scoreResult.Score, durationSeconds);

        await _db.SaveChangesAsync(ct);

        // Recompute confidence for all skills
        var skillStates = _opts.SkillsToAssess
            .Distinct()
            .ToDictionary(s => s, s => ComputeSkillConfidence(assessment.Items, s, _opts.StartingLevelFallback));

        var submitItemBank = await LoadItemBankAsync(ct);
        if (ShouldComplete(assessment.Items, skillStates, submitItemBank, out var completionReason))
        {
            var summary = await FinalizeCompletionAsync(assessment, skillStates, completionReason, ct);
            return new SubmitResponseResult(
                item.Id, scoreResult.IsCorrect, scoreResult.Score,
                scoreResult.EvaluationNotes, true, completionReason, null, summary);
        }

        // Not complete — add next adaptive item (globally or scoped to skillFilter) and return pointer
        var nextItem = skillFilter is null
            ? await AddNextAdaptiveItemAsync(assessment, skillStates, ct)
            : await AddNextItemForSkillAsync(assessment, skillFilter, skillStates, ct);
        var nextDto = ToNextItemDto(nextItem, assessment.Items, skillStates);

        return new SubmitResponseResult(
            item.Id, scoreResult.IsCorrect, scoreResult.Score,
            scoreResult.EvaluationNotes, false, null, nextDto, null);
    }

    private async Task<PlacementAssessmentItem?> AddNextAdaptiveItemAsync(
        PlacementAssessment assessment,
        Dictionary<string, SkillConfidenceState> skillStates,
        CancellationToken ct)
    {
        var itemBank = await LoadItemBankAsync(ct);

        var nextSkill = SelectNextSkill(assessment.Items, skillStates, itemBank);
        if (nextSkill is null) return null;

        var state = skillStates.TryGetValue(nextSkill, out var s) ? s
            : new SkillConfidenceState(_opts.StartingLevelFallback, 0, 0, 0, 0);

        var template = SelectNextTemplate(assessment.Items, nextSkill, state, itemBank);
        return template is null ? null : await CreateItemAsync(assessment, template, ct);
    }

    /// <summary>Same as <see cref="AddNextAdaptiveItemAsync"/> but restricted to one skill —
    /// used by the placement-cards flow so a card never silently hands back a different
    /// skill's question. Returns null when the skill is already confident or its item bank
    /// is exhausted, which the caller treats as "this card is done".</summary>
    private async Task<PlacementAssessmentItem?> AddNextItemForSkillAsync(
        PlacementAssessment assessment,
        string skill,
        Dictionary<string, SkillConfidenceState> skillStates,
        CancellationToken ct)
    {
        var state = skillStates.TryGetValue(skill, out var s) ? s
            : new SkillConfidenceState(_opts.StartingLevelFallback, 0, 0, 0, 0);
        if (state.Confidence >= _opts.ConfidenceThreshold) return null;

        var itemBank = await LoadItemBankAsync(ct);
        var template = SelectNextTemplate(assessment.Items, skill, state, itemBank);
        return template is null ? null : await CreateItemAsync(assessment, template, ct);
    }

    private async Task<PlacementAssessmentItem> CreateItemAsync(
        PlacementAssessment assessment, PlacementItemTemplate template, CancellationToken ct)
    {
        var newOrder = assessment.Items.Count;
        var newItem = PlacementAssessmentItem.Create(
            assessment.Id, template.Skill, template.CefrLevel,
            PlacementItemSchemaLabel.ExtractComponentType(template.FormIoSchemaJson),
            PlacementItemSchemaLabel.ExtractLabel(template.FormIoSchemaJson), newOrder,
            template.DefinitionId, template.FormIoSchemaJson,
            template.ScoringRulesJson, template.ScoringRulesVersion);

        _db.PlacementAssessmentItems.Add(newItem);
        await _db.SaveChangesAsync(ct);

        return newItem;
    }

    private static PlacementAssessmentItem? FindUnansweredItem(
        IReadOnlyCollection<PlacementAssessmentItem> items, string? skillFilter)
    {
        return items
            .Where(i => !i.IsCorrect.HasValue && (skillFilter is null || i.Skill == skillFilter))
            .OrderBy(i => i.ItemOrder)
            .FirstOrDefault();
    }

    private PlacementNextItemDto? ToNextItemDto(
        PlacementAssessmentItem? item,
        IReadOnlyCollection<PlacementAssessmentItem> items,
        Dictionary<string, SkillConfidenceState> states)
    {
        if (item is null) return null;

        var scoringDoc = TryParseScoringDoc(item.ScoringRulesJsonSnapshot);
        var hasAudio = !string.IsNullOrWhiteSpace(scoringDoc?.ListeningAudioScript);

        return new PlacementNextItemDto(
            item.Id, item.Skill, item.TargetCefrLevel, item.ItemType, item.Prompt, item.ItemOrder,
            items.Count(i => i.IsCorrect.HasValue),
            EstimateRemaining(items, states),
            hasAudio,
            item.FormIoSchemaJson,
            RendererKind: nameof(FormRendererKind.FormIo));
    }

    private int EstimateRemaining(
        IReadOnlyCollection<PlacementAssessmentItem> items,
        Dictionary<string, SkillConfidenceState> states)
    {
        var answeredCount = items.Count(i => i.IsCorrect.HasValue);
        var pendingSkillCount = _opts.SkillsToAssess.Count(skill =>
            !states.TryGetValue(skill, out var s) || s.Confidence < _opts.ConfidenceThreshold);
        var estimatedTotal = Math.Min(answeredCount + pendingSkillCount * 2, _opts.MaxItems);
        return Math.Max(estimatedTotal - answeredCount, 0);
    }

    private async Task<PlacementAssessmentSummaryDto> FinalizeCompletionAsync(
        PlacementAssessment assessment,
        Dictionary<string, SkillConfidenceState> skillStates,
        string completionReason,
        CancellationToken ct)
    {
        var existingResults = await _db.PlacementSkillResults
            .Where(r => r.PlacementAssessmentId == assessment.Id)
            .ToListAsync(ct);

        if (existingResults.Count > 0)
            return ToSummaryDto(assessment, existingResults);

        var skillResults = BuildSkillResults(assessment.Id, assessment.Items);
        foreach (var r in skillResults)
            _db.PlacementSkillResults.Add(r);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (assessment.Status != PlacementStatus.Completed)
        {
            // Lost the race to a concurrent completion — a unique index on
            // (assessment, skill) rejected our duplicate insert. Detach the
            // rows we just tried to add and return the winner's result instead.
            foreach (var r in skillResults)
                _db.Entry(r).State = EntityState.Detached;

            var winnerResults = await _db.PlacementSkillResults
                .Where(r => r.PlacementAssessmentId == assessment.Id)
                .ToListAsync(ct);
            var winnerAssessment = await _db.PlacementAssessments
                .Include(a => a.Items)
                .FirstAsync(a => a.Id == assessment.Id, ct);
            return ToSummaryDto(winnerAssessment, winnerResults);
        }

        var overallCefr = ComputeOverallCefr(skillResults, _opts.StartingLevelFallback);
        var overallConfidence = skillResults.Count > 0
            ? Math.Round(skillResults.Average(r => r.Confidence), 3)
            : 0.0;

        var isProvisional = overallConfidence < _opts.ConfidenceThreshold;
        var resultSummary = $"Estimated level: {overallCefr}. Confidence: {overallConfidence:P0}. " +
                            $"Completion: {completionReason}. " +
                            (isProvisional ? "Provisional — more evidence needed." : "Sufficient evidence.");

        assessment.CompleteAdaptive(overallCefr, overallConfidence, resultSummary, isProvisional);

        var completionProfile = await _db.StudentProfiles
            .FirstOrDefaultAsync(p => p.Id == assessment.StudentProfileId, ct);

        if (completionProfile is not null)
        {
            if (overallConfidence >= 0.6 && !string.IsNullOrWhiteSpace(overallCefr))
                completionProfile.AdminSetCefrLevel(overallCefr);

            // Phase 14A — transition lifecycle to PlacementCompleted
            if (completionProfile.LifecycleStage == StudentLifecycleStage.PlacementInProgress ||
                completionProfile.LifecycleStage == StudentLifecycleStage.PlacementRequired)
            {
                completionProfile.SetLifecycleStage(StudentLifecycleStage.PlacementCompleted);
            }
        }

        await _db.SaveChangesAsync(ct);

        var learningPlanRegenerated = false;
        string? learningPlanWarning = null;
        try
        {
            await _learningPlan.RegeneratePlanAsync(assessment.StudentProfileId, "placement_completed", ct);
            learningPlanRegenerated = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Learning plan regeneration failed after placement completion for student {StudentId}",
                assessment.StudentProfileId);
            learningPlanWarning = ex.Message;
        }

        // Phase 14B — transition to CourseReady only when plan generation succeeded.
        // If plan regen failed, stay at PlacementCompleted (honest "preparing" state).
        // Idempotent: skip if already CourseReady or further along.
        if (learningPlanRegenerated
            && completionProfile is not null
            && completionProfile.LifecycleStage == StudentLifecycleStage.PlacementCompleted)
        {
            completionProfile.SetLifecycleStage(StudentLifecycleStage.CourseReady);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Student {StudentId} transitioned to CourseReady after placement completion",
                assessment.StudentProfileId);
        }

        return ToSummaryDto(assessment, skillResults, learningPlanRegenerated, learningPlanWarning);
    }

    // Phase 13B — Get next unanswered item
    public async Task<PlacementNextItemDto?> GetNextItemAsync(Guid assessmentId, string? skillFilter = null, CancellationToken ct = default)
    {
        var assessment = await _db.PlacementAssessments
            .Include(a => a.Items)
            .FirstOrDefaultAsync(a => a.Id == assessmentId, ct)
            ?? throw new InvalidOperationException($"Assessment {assessmentId} not found.");

        if (assessment.Status != PlacementStatus.InProgress) return null;

        var skillStates = _opts.SkillsToAssess
            .Distinct()
            .ToDictionary(s => s, s => ComputeSkillConfidence(assessment.Items, s, _opts.StartingLevelFallback));

        var existing = FindUnansweredItem(assessment.Items, skillFilter);
        if (existing is not null) return ToNextItemDto(existing, assessment.Items, skillStates);

        // No queued item for this skill yet (e.g. opening a placement card for the first
        // time) — generate one scoped to it instead of falling back to another skill.
        if (skillFilter is not null)
        {
            var generated = await AddNextItemForSkillAsync(assessment, skillFilter, skillStates, ct);
            return ToNextItemDto(generated, assessment.Items, skillStates);
        }

        return null;
    }

    /// <summary>Per-skill status for the placement cards page (one card per configured skill).</summary>
    public async Task<IReadOnlyList<PlacementSkillStatusDto>> GetSkillStatusAsync(
        Guid studentProfileId, CancellationToken ct = default)
    {
        var assessment = await _db.PlacementAssessments
            .Include(a => a.Items)
            .Where(a => a.StudentProfileId == studentProfileId)
            .OrderByDescending(a => a.Status == PlacementStatus.Completed)
            .ThenByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var items = (IReadOnlyCollection<PlacementAssessmentItem>?)assessment?.Items ?? Array.Empty<PlacementAssessmentItem>();
        var wholeAssessmentDone = assessment?.Status == PlacementStatus.Completed;
        var itemBank = await LoadItemBankAsync(ct);

        var results = new List<PlacementSkillStatusDto>();
        foreach (var skill in _opts.SkillsToAssess.Distinct())
        {
            var state = ComputeSkillConfidence(items, skill, _opts.StartingLevelFallback);
            var exhausted = !itemBank.Any(t => t.Skill == skill && !IsUsed(t, items));
            var completed = wholeAssessmentDone
                || state.Confidence >= _opts.ConfidenceThreshold
                || (exhausted && state.EvidenceCount > 0);
            var percent = completed
                ? 100.0
                : Math.Round(Math.Min(state.Confidence / _opts.ConfidenceThreshold, 0.95) * 100, 0);

            results.Add(new PlacementSkillStatusDto(
                skill, SkillLabels.GetValueOrDefault(skill, skill), percent, completed, state.EvidenceCount));
        }

        return results;
    }

    // Phase 13B — Detailed admin progress view
    public async Task<PlacementAssessmentProgressDto> GetProgressAsync(Guid assessmentId, CancellationToken ct = default)
    {
        var assessment = await _db.PlacementAssessments
            .Include(a => a.Items)
            .FirstOrDefaultAsync(a => a.Id == assessmentId, ct)
            ?? throw new InvalidOperationException($"Assessment {assessmentId} not found.");

        var skillStates = _opts.SkillsToAssess
            .Distinct()
            .ToDictionary(s => s, s => ComputeSkillConfidence(assessment.Items, s, _opts.StartingLevelFallback));

        var answeredCount = assessment.Items.Count(i => i.IsCorrect.HasValue);
        var overallConfidence = skillStates.Count > 0
            ? Math.Round(skillStates.Values.Average(s => s.Confidence), 3)
            : 0.0;

        var progressItemBank = await LoadItemBankAsync(ct);
        ShouldComplete(assessment.Items, skillStates, progressItemBank, out var completionReason);

        var nextUnanswered = assessment.Items
            .Where(i => !i.IsCorrect.HasValue)
            .OrderBy(i => i.ItemOrder)
            .FirstOrDefault();

        var skillProgress = skillStates.Select(kv => new PlacementSkillProgressDto(
            kv.Key,
            kv.Value.EstimatedLevel,
            kv.Value.Confidence,
            kv.Value.EvidenceCount,
            kv.Value.ConsecutiveSuccesses,
            kv.Value.ConsecutiveFailures)).ToList();

        var itemHistory = assessment.Items
            .OrderBy(i => i.ItemOrder)
            .Select(ToItemHistoryDto)
            .ToList();

        return new PlacementAssessmentProgressDto(
            assessment.Id,
            assessment.Status.ToString(),
            answeredCount,
            assessment.Items.Count,
            EstimateRemaining(assessment.Items, skillStates),
            nextUnanswered?.Skill,
            nextUnanswered?.TargetCefrLevel,
            overallConfidence,
            skillProgress,
            itemHistory,
            string.IsNullOrEmpty(completionReason) ? null : completionReason);
    }

    // Phase 13B — Complete with real responses only (no simulation)
    public async Task<PlacementAssessmentSummaryDto> CompleteAssessmentAsync(
        Guid assessmentId, CancellationToken ct = default)
    {
        var assessment = await _db.PlacementAssessments
            .Include(a => a.Items)
            .FirstOrDefaultAsync(a => a.Id == assessmentId, ct)
            ?? throw new InvalidOperationException($"Assessment {assessmentId} not found.");

        if (assessment.Status == PlacementStatus.Completed)
        {
            var existingResults = await _db.PlacementSkillResults
                .Where(r => r.PlacementAssessmentId == assessmentId)
                .ToListAsync(ct);
            return ToSummaryDto(assessment, existingResults);
        }

        if (assessment.Status != PlacementStatus.InProgress)
            throw new InvalidOperationException($"Assessment is {assessment.Status} — cannot complete.");

        var skillStates = _opts.SkillsToAssess
            .Distinct()
            .ToDictionary(s => s, s => ComputeSkillConfidence(assessment.Items, s, _opts.StartingLevelFallback));

        return await FinalizeCompletionAsync(assessment, skillStates, "admin_forced", ct);
    }

    public async Task AbandonAssessmentAsync(Guid assessmentId, CancellationToken ct = default)
    {
        var assessment = await _db.PlacementAssessments
            .FirstOrDefaultAsync(a => a.Id == assessmentId, ct)
            ?? throw new InvalidOperationException($"Assessment {assessmentId} not found.");

        assessment.Abandon();
        await _db.SaveChangesAsync(ct);
    }
}

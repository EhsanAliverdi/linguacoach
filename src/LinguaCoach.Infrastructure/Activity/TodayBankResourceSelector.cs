using LinguaCoach.Application.Activity;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Activity;

/// <summary>
/// Phase D1/D2 implementation of <see cref="ITodayBankResourceSelector"/>. Queries the published
/// Resource Bank (<see cref="IResourceBankQueryService"/>) for a small, CEFR-matched, balanced
/// bundle of vocabulary/grammar/reading entries, runs each candidate through a synthetic-fingerprint
/// novelty precheck (mirroring PracticeGymGenerationJob.TryMaterializeFromTemplateAsync's
/// template-precheck idea) and a cheap feedback-signal check (Phase D2), before handing back a
/// short structured prompt block. Never throws — Today lesson generation must never break because
/// of this selector.
/// </summary>
public sealed class TodayBankResourceSelector : ITodayBankResourceSelector
{
    private const int CandidateScanCap = 10;
    private const int BankQueryPageSize = 20;
    private const int MaxVocabulary = 2;
    private const int MaxGrammar = 1;
    private const int MaxReading = 2;
    private const int MaxOpportunisticReadingForVocabPattern = 1;

    private readonly IResourceBankQueryService _bankQuery;
    private readonly IActivityNoveltyPolicy _noveltyPolicy;
    private readonly LinguaCoachDbContext _db;
    private readonly ILogger<TodayBankResourceSelector> _logger;

    public TodayBankResourceSelector(
        IResourceBankQueryService bankQuery,
        IActivityNoveltyPolicy noveltyPolicy,
        LinguaCoachDbContext db,
        ILogger<TodayBankResourceSelector> logger)
    {
        _bankQuery = bankQuery;
        _noveltyPolicy = noveltyPolicy;
        _db = db;
        _logger = logger;
    }

    public async Task<TodayBankSelectionResult> SelectAsync(TodayBankSelectionRequest request, CancellationToken ct = default)
    {
        try
        {
            var primarySkill = request.PatternPrimarySkill;
            var isVocabulary = string.Equals(primarySkill, "Vocabulary", StringComparison.OrdinalIgnoreCase);
            var isReading = string.Equals(primarySkill, "Reading", StringComparison.OrdinalIgnoreCase);

            if (!isVocabulary && !isReading)
                return TodayBankSelectionResult.SkippedUnsupported;

            var candidates = new List<TodayBankSelectedResource>();
            var anyRawHits = false;

            if (isVocabulary)
            {
                var (vocab, vocabRawHits) = await SelectVocabularyAsync(request, MaxVocabulary, ct);
                candidates.AddRange(vocab);
                anyRawHits |= vocabRawHits;

                var includeGrammar = request.PatternSecondarySkills.Any(
                    s => string.Equals(s, "Grammar", StringComparison.OrdinalIgnoreCase));
                if (includeGrammar)
                {
                    // Opportunistic supplementary content only — gap_fill_workplace_phrase is the
                    // only pattern this ever applies to; there is no dedicated grammar-focused
                    // Today pattern yet, so grammar is never a gating skill on its own here.
                    var (grammar, grammarRawHits) = await SelectGrammarAsync(request, MaxGrammar, ct);
                    candidates.AddRange(grammar);
                    anyRawHits |= grammarRawHits;
                }

                // Phase D2 — "balanced bundle": a short reading excerpt is useful supplementary
                // anchor material even for a vocabulary-focused activity, when one exists at the
                // student's level. Never gates the pattern; purely additive, capped small.
                var (reading, readingRawHits) = await SelectReadingAsync(
                    request, MaxOpportunisticReadingForVocabPattern, ct);
                candidates.AddRange(reading);
                anyRawHits |= readingRawHits;
            }
            else // isReading
            {
                var (reading, readingRawHits) = await SelectReadingAsync(request, MaxReading, ct);
                candidates.AddRange(reading);
                anyRawHits |= readingRawHits;
            }

            if (candidates.Count == 0)
            {
                return anyRawHits
                    ? new TodayBankSelectionResult(TodayBankSelectionOutcome.BlockedByNovelty, Array.Empty<TodayBankSelectedResource>(), null)
                    : TodayBankSelectionResult.NoResources;
            }

            var selected = candidates.Take(request.MaxResources).ToList();
            var promptSupplement = BuildStructuredPromptBlock(selected, request.CefrLevel);

            return new TodayBankSelectionResult(TodayBankSelectionOutcome.BankResourcesFound, selected, promptSupplement);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "TodayBankResourceSelector: selection failed for StudentProfileId={StudentProfileId}, PatternPrimarySkill={PatternPrimarySkill} — continuing without bank resources.",
                request.StudentProfileId, request.PatternPrimarySkill);
            return TodayBankSelectionResult.NoResources;
        }
    }

    private async Task<(List<TodayBankSelectedResource> Selected, bool AnyRawHits)> SelectVocabularyAsync(
        TodayBankSelectionRequest request, int max, CancellationToken ct)
    {
        var selected = new List<TodayBankSelectedResource>();
        var (items, levelUsed, reasonSuffix, anyRawHits) = await QueryWithOptionalWideningAsync(
            request,
            level => _bankQuery.ListVocabularyAsync(
                new ResourceBankListFilter(CefrLevel: level, PageSize: BankQueryPageSize), ct)
                .ContinueWith(t => (IReadOnlyList<(Guid Id, string Display, Guid SourceId)>)
                    t.Result.Items.Select(i => (i.Id, i.Word, i.SourceId)).ToList(), ct),
            ct);

        foreach (var (id, display, sourceId) in items.Take(CandidateScanCap))
        {
            if (selected.Count >= max) break;
            var fingerprint = $"bank-vocab-precheck:{id}";
            if (!await IsAllowedAsync(request.StudentProfileId, fingerprint, id, ct)) continue;
            selected.Add(new TodayBankSelectedResource(id, "Vocabulary", display, sourceId, fingerprint, $"vocabulary{reasonSuffix}"));
        }

        return (selected, anyRawHits);
    }

    private async Task<(List<TodayBankSelectedResource> Selected, bool AnyRawHits)> SelectGrammarAsync(
        TodayBankSelectionRequest request, int max, CancellationToken ct)
    {
        var selected = new List<TodayBankSelectedResource>();
        var (items, _, reasonSuffix, anyRawHits) = await QueryWithOptionalWideningAsync(
            request,
            level => _bankQuery.ListGrammarAsync(
                new ResourceBankListFilter(CefrLevel: level, PageSize: BankQueryPageSize), ct)
                .ContinueWith(t => (IReadOnlyList<(Guid Id, string Display, Guid SourceId)>)
                    t.Result.Items.Select(i => (i.Id, i.GrammarPoint, i.SourceId)).ToList(), ct),
            ct);

        foreach (var (id, display, sourceId) in items.Take(CandidateScanCap))
        {
            if (selected.Count >= max) break;
            var fingerprint = $"bank-grammar-precheck:{id}";
            if (!await IsAllowedAsync(request.StudentProfileId, fingerprint, id, ct)) continue;
            selected.Add(new TodayBankSelectedResource(id, "Grammar", display, sourceId, fingerprint, $"grammar{reasonSuffix} (opportunistic)"));
        }

        return (selected, anyRawHits);
    }

    private async Task<(List<TodayBankSelectedResource> Selected, bool AnyRawHits)> SelectReadingAsync(
        TodayBankSelectionRequest request, int max, CancellationToken ct)
    {
        var selected = new List<TodayBankSelectedResource>();
        var (items, _, reasonSuffix, anyRawHits) = await QueryWithOptionalWideningAsync(
            request,
            level => _bankQuery.ListReadingReferencesAsync(
                new ResourceBankListFilter(CefrLevel: level, PageSize: BankQueryPageSize), ct)
                .ContinueWith(t => (IReadOnlyList<(Guid Id, string Display, Guid SourceId)>)
                    t.Result.Items.Select(i => (i.Id,
                        !string.IsNullOrWhiteSpace(i.ReferenceExcerpt) ? i.ReferenceExcerpt! : i.TextType ?? "reading reference",
                        i.SourceId)).ToList(), ct),
            ct);

        foreach (var (id, display, sourceId) in items.Take(CandidateScanCap))
        {
            if (selected.Count >= max) break;
            var fingerprint = $"bank-reading-precheck:{id}";
            if (!await IsAllowedAsync(request.StudentProfileId, fingerprint, id, ct)) continue;
            selected.Add(new TodayBankSelectedResource(id, "Reading", display, sourceId, fingerprint, $"reading{reasonSuffix}"));
        }

        return (selected, anyRawHits);
    }

    /// <summary>
    /// Phase D2 — queries at the exact routed CEFR level first. Only when that returns zero raw
    /// rows AND the request allows review/scaffold widening does it retry at the next level down
    /// (per CefrLevelConstants.All ordering) — never upward, never for ordinary generation.
    /// </summary>
    private async Task<(IReadOnlyList<(Guid Id, string Display, Guid SourceId)> Items, string LevelUsed, string ReasonSuffix, bool AnyRawHits)>
        QueryWithOptionalWideningAsync(
            TodayBankSelectionRequest request,
            Func<string, Task<IReadOnlyList<(Guid Id, string Display, Guid SourceId)>>> query,
            CancellationToken ct)
    {
        var exact = await query(request.CefrLevel);
        if (exact.Count > 0)
            return (exact, request.CefrLevel, " (exact CEFR match)", true);

        if (request.AllowLowerLevelReview)
        {
            var levelIndex = CefrLevelConstants.All.ToList().IndexOf(request.CefrLevel.ToUpperInvariant());
            if (levelIndex > 0)
            {
                var lowerLevel = CefrLevelConstants.All[levelIndex - 1];
                var widened = await query(lowerLevel);
                if (widened.Count > 0)
                    return (widened, lowerLevel,
                        $" (review/lower-level match at {lowerLevel}, routed level {request.CefrLevel} had none)", true);
            }
        }

        return (Array.Empty<(Guid, string, Guid)>(), request.CefrLevel, string.Empty, false);
    }

    /// <summary>
    /// Combined novelty + feedback check for a single bank candidate. Never throws: a failed
    /// check just excludes that one candidate (logged), never aborts selection for the whole
    /// request.
    /// </summary>
    private async Task<bool> IsAllowedAsync(Guid studentProfileId, string fingerprint, Guid entryId, CancellationToken ct)
    {
        try
        {
            var check = await _noveltyPolicy.CheckAsync(new ActivityNoveltyCheckRequest(
                StudentProfileId: studentProfileId,
                ContentFingerprint: fingerprint), ct);
            if (!check.Allowed) return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "TodayBankResourceSelector: novelty precheck failed for bank entry {EntryId} ({Fingerprint}) — skipping this candidate.",
                entryId, fingerprint);
            return false;
        }

        // Phase D2 — cheap, obvious feedback-signal avoidance: a student who marked a past
        // activity that used this exact bank item NotUseful or DoNotShowSimilarSoon should not
        // have it selected again. Matched via LearningActivity.BankResourceProvenanceJson (a
        // string-contains check on the resource id) rather than ActivityFeedbackSignal's own
        // SourceBankItemId column — that column is FK-constrained to PlacementItemDefinition and
        // cannot reference a Phase E Cefr* bank row. Best-effort only; any failure here just
        // skips the check (never blocks selection of an otherwise-fine candidate).
        try
        {
            var marker = entryId.ToString();
            var hasNegativeFeedback = await _db.ActivityFeedbackSignals
                .AsNoTracking()
                .Where(f => f.StudentProfileId == studentProfileId
                         && (f.UsefulnessRating == ActivityFeedbackUsefulnessRating.NotUseful
                             || f.RepeatPreference == ActivityFeedbackRepeatPreference.DoNotShowSimilarSoon))
                .Join(_db.LearningActivities.AsNoTracking(),
                    f => f.LearningActivityId, a => a.Id, (f, a) => a.BankResourceProvenanceJson)
                .AnyAsync(json => json != null && json.Contains(marker), ct);
            if (hasNegativeFeedback) return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "TodayBankResourceSelector: feedback-signal check failed for bank entry {EntryId} — continuing without it.",
                entryId);
        }

        return true;
    }

    /// <summary>
    /// Phase D2 — a clearly-bounded, structured block (rather than a single loose sentence) so
    /// the AI prompt has an unambiguous list of anchor resources plus explicit constraints. Still
    /// appended to the existing free-text TopicHint field (ActivityGenerationContext has no
    /// dedicated "supporting material" field) — no prompt template changes needed.
    /// </summary>
    private static string BuildStructuredPromptBlock(IReadOnlyList<TodayBankSelectedResource> resources, string cefrLevel)
    {
        var lines = resources.Select(r => $"- [{r.ResourceType}] \"{r.DisplayText}\"");
        return "Approved bank resources to use as anchors (do not invent unrelated vocabulary or "
             + $"content, keep the student's CEFR level at {cefrLevel}, keep all content English-only, "
             + "support-language behavior stays runtime-only): "
             + string.Join(" ", lines) + ".";
    }
}

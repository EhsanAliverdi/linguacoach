using System.Text;
using LinguaCoach.Application.Activity;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Activity;

/// <summary>
/// Phase D1/D2/D3/D4 implementation of <see cref="ITodayBankResourceSelector"/>. Queries the
/// published Resource Bank (<see cref="IResourceBankQueryService"/>) for a small, CEFR-matched,
/// pattern-shaped bundle of vocabulary/grammar/reading-reference/reading-passage entries, runs each
/// candidate through a synthetic-fingerprint novelty precheck and a cheap feedback-signal check
/// (Phase D2), and hands back a short structured prompt block plus per-resource provenance. Never
/// throws — Today lesson generation must never break because of this selector; every failure path
/// degrades to a smaller bundle or to no bank resources at all (the caller then still runs the
/// legacy AI generator).
///
/// Phase D4 — richer, pattern-shaped bundles built on the deeper E8 bank:
/// <list type="bullet">
/// <item><description>Vocabulary-primary patterns: 2-3 vocabulary/usage targets (primary) plus an
/// opportunistic grammar hint (when the pattern lists Grammar as a secondary skill) and an
/// opportunistic short reading reference (supporting).</description></item>
/// <item><description>Reading comprehension/reorder patterns: one full <see cref="Domain.Entities.CefrReadingPassage"/>
/// anchor (primary) plus a couple of supporting vocabulary targets and an optional grammar hint;
/// falls back to a short-reference bundle when no suitable passage exists.</description></item>
/// <item><description>Reading cloze/fill-in-blanks patterns: a short <see cref="Domain.Entities.CefrReadingReference"/>
/// (primary) plus supporting vocabulary/grammar — never a full passage.</description></item>
/// </list>
///
/// Context defaulting (Phase D4): the bank stays **general English by default**. When the learner's
/// routed goal context is not workplace-specific (<see cref="TodayBankSelectionRequest.PrefersWorkplaceContext"/>
/// is false), full reading passages whose bank <c>ContextTags</c> mark them workplace-specific are
/// skipped. The short vocabulary/grammar/reading-reference bank tables carry no context tags at all
/// (only <see cref="Domain.Entities.CefrReadingPassage"/> stores them), so this filter necessarily
/// applies to full passages only — a documented limitation, not an oversight.
/// </summary>
public sealed class TodayBankResourceSelector : ITodayBankResourceSelector
{
    private const int CandidateScanCap = 10;
    private const int BankQueryPageSize = 20;
    // Phase D4 — a vocabulary-primary activity anchors on a slightly richer set of targets than
    // D2's 2, but is still kept small so the prompt stays bounded.
    private const int MaxVocabularyPrimary = 3;
    // Phase D4 — supporting vocabulary attached to a reading (passage/cloze/reference) bundle.
    private const int MaxSupportingVocabulary = 2;
    private const int MaxGrammar = 1;
    private const int MaxReadingReferenceOpportunistic = 1;
    private const int MaxReadingReferencePrimary = 1;
    // Phase D3 — a full passage is far heavier prompt material than a short reference excerpt, so
    // exactly one is injected as the reading anchor (comprehension/reorder patterns work off a
    // single text). Keeps the TopicHint bounded.
    private const int MaxReadingPassage = 1;
    // Phase D3 — defensive upper bound on injected passage text length. Bank passages are short by
    // construction, but a future longer passage must never blow up the prompt.
    private const int MaxInjectedPassageChars = 4000;

    private const string RolePrimary = "primary";
    private const string RoleSupporting = "supporting";

    private const string WorkplaceContextTag = "workplace";

    /// <summary>
    /// Phase D3 — Reading-primary patterns for which a full passage is a suitable anchor:
    /// comprehension over a whole text, and paragraph reordering (needs a coherent multi-paragraph
    /// passage).
    /// </summary>
    private static readonly HashSet<string> FullPassageReadingPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        ExercisePatternKey.ReadingMultipleChoiceSingle,
        ExercisePatternKey.ReadingMultipleChoiceMulti,
        ExercisePatternKey.ReorderParagraphs,
    };

    /// <summary>
    /// Phase D4 — Reading-primary cloze/fill-in-blanks patterns: these generate their own gapped
    /// text and must be anchored on a short reference, never a full passage.
    /// </summary>
    private static readonly HashSet<string> ClozeReadingPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        ExercisePatternKey.ReadingFillInBlanks,
        ExercisePatternKey.ReadingWritingFillInBlanks,
    };

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

            var (candidates, anyRawHits) = isVocabulary
                ? await BuildVocabularyPrimaryBundleAsync(request, ct)
                : await BuildReadingBundleAsync(request, ct);

            if (candidates.Count == 0)
            {
                return anyRawHits
                    ? new TodayBankSelectionResult(TodayBankSelectionOutcome.BlockedByNovelty, Array.Empty<TodayBankSelectedResource>(), null)
                    : TodayBankSelectionResult.NoResources;
            }

            var selected = candidates.Take(request.MaxResources).ToList();
            var promptSupplement = BuildStructuredPromptBlock(selected, request);

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

    // ── Bundle builders (Phase D4) ───────────────────────────────────────────────

    private async Task<(List<TodayBankSelectedResource> Candidates, bool AnyRawHits)> BuildVocabularyPrimaryBundleAsync(
        TodayBankSelectionRequest request, CancellationToken ct)
    {
        var candidates = new List<TodayBankSelectedResource>();
        var anyRawHits = false;

        var (vocab, vocabRaw) = await SelectVocabularyAsync(request, MaxVocabularyPrimary, RolePrimary, "vocabulary", ct);
        candidates.AddRange(vocab);
        anyRawHits |= vocabRaw;

        // Grammar is opportunistic supplementary content, only when the pattern lists Grammar as a
        // secondary skill (e.g. gap_fill_workplace_phrase) — never a gating skill on its own here.
        var includeGrammar = request.PatternSecondarySkills.Any(
            s => string.Equals(s, "Grammar", StringComparison.OrdinalIgnoreCase));
        if (includeGrammar)
        {
            var (grammar, grammarRaw) = await SelectGrammarAsync(request, MaxGrammar, RoleSupporting, ct);
            candidates.AddRange(grammar);
            anyRawHits |= grammarRaw;
        }

        // A short reading excerpt is useful supplementary anchor material even for a
        // vocabulary-focused activity, when one exists at the student's level. Purely additive.
        var (reading, readingRaw) = await SelectReadingReferenceAsync(
            request, MaxReadingReferenceOpportunistic, RoleSupporting, ct);
        candidates.AddRange(reading);
        anyRawHits |= readingRaw;

        return (candidates, anyRawHits);
    }

    private async Task<(List<TodayBankSelectedResource> Candidates, bool AnyRawHits)> BuildReadingBundleAsync(
        TodayBankSelectionRequest request, CancellationToken ct)
    {
        var candidates = new List<TodayBankSelectedResource>();
        var anyRawHits = false;

        // Comprehension/reorder patterns → prefer a full passage anchor plus supporting targets.
        if (ReadingPatternPrefersFullPassage(request.PatternKey))
        {
            var (passages, passageRaw) = await SelectReadingPassageAsync(request, MaxReadingPassage, ct);
            anyRawHits |= passageRaw;

            if (passages.Count > 0)
            {
                candidates.AddRange(passages);

                // Supporting vocabulary targets (same routed CEFR) so the AI can weave in level-
                // appropriate words; the passage remains the anchor. Optional grammar hint too.
                var (vocabSupport, vocabRaw) = await SelectVocabularyAsync(
                    request, MaxSupportingVocabulary, RoleSupporting, "vocabulary support", ct);
                candidates.AddRange(vocabSupport);
                anyRawHits |= vocabRaw;

                var (grammarSupport, grammarRaw) = await SelectGrammarAsync(request, MaxGrammar, RoleSupporting, ct);
                candidates.AddRange(grammarSupport);
                anyRawHits |= grammarRaw;

                return (candidates, anyRawHits);
            }

            // No suitable full passage (none at level, or all novelty/context-excluded) → fall back
            // to the short-reference bundle, exactly as D3 fell back to short references.
            var (refBundle, refRaw) = await BuildReadingReferenceBundleAsync(request, ct);
            candidates.AddRange(refBundle);
            anyRawHits |= refRaw;
            return (candidates, anyRawHits);
        }

        // Cloze/fill-in-blanks and every other reading pattern → short reference anchor (never a
        // full passage) plus supporting vocabulary/grammar.
        var (bundle, raw) = await BuildReadingReferenceBundleAsync(request, ct);
        candidates.AddRange(bundle);
        anyRawHits |= raw;
        return (candidates, anyRawHits);
    }

    /// <summary>Phase D4 — a short-reading-reference-anchored bundle: the reference(s) are the
    /// primary anchor, with supporting vocabulary and an optional grammar hint. Used for cloze
    /// patterns, other reading patterns, and as the fallback when a comprehension pattern has no
    /// full passage.</summary>
    private async Task<(List<TodayBankSelectedResource> Candidates, bool AnyRawHits)> BuildReadingReferenceBundleAsync(
        TodayBankSelectionRequest request, CancellationToken ct)
    {
        var candidates = new List<TodayBankSelectedResource>();

        var (refs, refRaw) = await SelectReadingReferenceAsync(request, MaxReadingReferencePrimary, RolePrimary, ct);
        candidates.AddRange(refs);

        var (vocabSupport, vocabRaw) = await SelectVocabularyAsync(
            request, MaxSupportingVocabulary, RoleSupporting, "vocabulary support", ct);
        candidates.AddRange(vocabSupport);

        var (grammarSupport, grammarRaw) = await SelectGrammarAsync(request, MaxGrammar, RoleSupporting, ct);
        candidates.AddRange(grammarSupport);

        return (candidates, refRaw || vocabRaw || grammarRaw);
    }

    // ── Per-type selection ───────────────────────────────────────────────────────

    private async Task<(List<TodayBankSelectedResource> Selected, bool AnyRawHits)> SelectVocabularyAsync(
        TodayBankSelectionRequest request, int max, string role, string reasonLabel, CancellationToken ct)
    {
        var selected = new List<TodayBankSelectedResource>();
        if (max <= 0) return (selected, false);

        var (items, _, reasonSuffix, anyRawHits) = await QueryWithOptionalWideningAsync(
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
            selected.Add(new TodayBankSelectedResource(
                id, "Vocabulary", display, sourceId, fingerprint, $"{reasonLabel}{reasonSuffix}", Role: role));
        }

        return (selected, anyRawHits);
    }

    private async Task<(List<TodayBankSelectedResource> Selected, bool AnyRawHits)> SelectGrammarAsync(
        TodayBankSelectionRequest request, int max, string role, CancellationToken ct)
    {
        var selected = new List<TodayBankSelectedResource>();
        if (max <= 0) return (selected, false);

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
            selected.Add(new TodayBankSelectedResource(
                id, "Grammar", display, sourceId, fingerprint, $"grammar{reasonSuffix} (opportunistic)", Role: role));
        }

        return (selected, anyRawHits);
    }

    private async Task<(List<TodayBankSelectedResource> Selected, bool AnyRawHits)> SelectReadingReferenceAsync(
        TodayBankSelectionRequest request, int max, string role, CancellationToken ct)
    {
        var selected = new List<TodayBankSelectedResource>();
        if (max <= 0) return (selected, false);

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
            selected.Add(new TodayBankSelectedResource(
                id, "Reading", display, sourceId, fingerprint, $"reading{reasonSuffix}", Role: role));
        }

        return (selected, anyRawHits);
    }

    /// <summary>
    /// Phase D3/D4 — selects a full <see cref="Domain.Entities.CefrReadingPassage"/> anchor for a
    /// full-passage-suitable Reading pattern. Lists passages at the routed CEFR (with the same
    /// exact-first / review-only-widen-down policy as every other bank type), runs each through the
    /// shared novelty + feedback check, and (Phase D4) skips passages whose bank context tags mark
    /// them workplace-specific when the learner is not routed for workplace content. Fetches full
    /// detail (title + passage text) only for the finally-selected passages.
    /// </summary>
    private async Task<(List<TodayBankSelectedResource> Selected, bool AnyRawHits)> SelectReadingPassageAsync(
        TodayBankSelectionRequest request, int max, CancellationToken ct)
    {
        var selected = new List<TodayBankSelectedResource>();
        var (items, _, reasonSuffix, anyRawHits) = await QueryWithOptionalWideningAsync(
            request,
            level => _bankQuery.ListReadingPassagesAsync(
                new ResourceBankListFilter(CefrLevel: level, PageSize: BankQueryPageSize), ct)
                .ContinueWith(t => (IReadOnlyList<ResourceBankReadingPassageListItemDto>)t.Result.Items, ct),
            ct);

        foreach (var item in items.Take(CandidateScanCap))
        {
            if (selected.Count >= max) break;
            var fingerprint = $"bank-reading-passage-precheck:{item.Id}";
            if (!await IsAllowedAsync(request.StudentProfileId, fingerprint, item.Id, ct)) continue;

            // Full passage text + context tags live only on the detail DTO — fetch it just for the
            // candidate passage.
            var detail = await _bankQuery.GetReadingPassageDetailAsync(item.Id, ct);
            if (detail is null || string.IsNullOrWhiteSpace(detail.PassageText)) continue;

            // Phase D4 — general English by default: skip workplace-tagged passages unless the
            // learner's routed context is workplace-specific.
            if (!request.PrefersWorkplaceContext && IsWorkplaceTagged(detail.ContextTags))
                continue;

            selected.Add(new TodayBankSelectedResource(
                Id: item.Id,
                ResourceType: "ReadingPassage",
                DisplayText: detail.Title,
                SourceId: item.SourceId,
                ContentFingerprint: fingerprint,
                SelectionReason: $"full reading passage{reasonSuffix}",
                Role: RolePrimary,
                CefrLevel: detail.CefrLevel,
                Title: detail.Title,
                PassageText: detail.PassageText,
                WordCount: detail.WordCount,
                EstimatedReadingMinutes: detail.EstimatedReadingMinutes));
        }

        return (selected, anyRawHits);
    }

    private static bool IsWorkplaceTagged(IReadOnlyList<string> contextTags) =>
        contextTags.Any(t => string.Equals(t, WorkplaceContextTag, StringComparison.OrdinalIgnoreCase));

    private static bool ReadingPatternPrefersFullPassage(string? patternKey) =>
        !string.IsNullOrWhiteSpace(patternKey) && FullPassageReadingPatterns.Contains(patternKey);

    private static bool IsClozeReadingPattern(string? patternKey) =>
        !string.IsNullOrWhiteSpace(patternKey) && ClozeReadingPatterns.Contains(patternKey);

    /// <summary>
    /// Phase D2/D3 — queries at the exact routed CEFR level first. Only when that returns zero raw
    /// rows AND the request allows review/scaffold widening does it retry at the next level down
    /// (per CefrLevelConstants.All ordering) — never upward, never for ordinary generation. Generic
    /// over the row shape so vocabulary/grammar/reading tuples and full-passage list DTOs share one
    /// widening implementation.
    /// </summary>
    private async Task<(IReadOnlyList<T> Items, string LevelUsed, string ReasonSuffix, bool AnyRawHits)>
        QueryWithOptionalWideningAsync<T>(
            TodayBankSelectionRequest request,
            Func<string, Task<IReadOnlyList<T>>> query,
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

        return (Array.Empty<T>(), request.CefrLevel, string.Empty, false);
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

    // ── Prompt assembly ──────────────────────────────────────────────────────────

    /// <summary>
    /// Phase D2/D3/D4 — a clearly-bounded, structured block so the AI prompt has an unambiguous
    /// list of anchor resources, a pattern-specific instruction, and explicit constraints. Still
    /// appended to the existing free-text TopicHint field (no prompt template changes needed). Full
    /// passages get their own delimited, length-capped anchor sub-block.
    /// </summary>
    private static string BuildStructuredPromptBlock(IReadOnlyList<TodayBankSelectedResource> resources, TodayBankSelectionRequest request)
    {
        var cefrLevel = request.CefrLevel;
        var sb = new StringBuilder();
        sb.Append("Approved bank resources to use as anchors (do not invent unrelated vocabulary or ")
          .Append($"content, keep the student's CEFR level at {cefrLevel}, keep all content English-only, ")
          .Append("support-language behavior stays runtime-only). ")
          .Append(PatternInstruction(request)).Append(' ');

        var shortResources = resources.Where(r => !IsFullPassage(r)).ToList();
        var passages = resources.Where(IsFullPassage).ToList();

        if (shortResources.Count > 0)
        {
            sb.Append("Targets: ");
            sb.Append(string.Join(" ", shortResources.Select(FormatShortResource)));
            if (passages.Count > 0) sb.Append(' ');
        }

        // Phase D3 — each full passage gets its own bounded, unambiguous anchor block.
        foreach (var p in passages)
        {
            var passageText = p.PassageText ?? string.Empty;
            if (passageText.Length > MaxInjectedPassageChars)
                passageText = passageText[..MaxInjectedPassageChars] + " [passage truncated]";

            sb.Append("- [ReadingPassage] Use ONLY the following full reading passage as the reading anchor. ")
              .Append($"Title: \"{p.Title ?? p.DisplayText}\". CEFR: {p.CefrLevel ?? cefrLevel}. ")
              .Append($"Word count: {p.WordCount?.ToString() ?? "n/a"}. ")
              .Append($"Estimated reading time: {p.EstimatedReadingMinutes?.ToString() ?? "n/a"} min. ")
              .Append("Base every comprehension question or task strictly on this passage, do not invent ")
              .Append("unrelated passage content, and keep the difficulty aligned with the stated CEFR. ")
              .Append("Passage text: <<<").Append(passageText).Append(">>> ");
        }

        return sb.ToString().TrimEnd() + ".";
    }

    private static string FormatShortResource(TodayBankSelectedResource r) =>
        $"- [{r.ResourceType}/{r.Role}] \"{r.DisplayText}\"";

    /// <summary>
    /// Phase D4 — a concise, deterministic, pattern-specific instruction. Bounded to one sentence;
    /// centralizes "use these targets naturally / do not default to workplace / use the passage
    /// only" rules per pattern family so the prompt-shaping logic lives in one place.
    /// </summary>
    private static string PatternInstruction(TodayBankSelectionRequest request)
    {
        var patternKey = request.PatternKey;

        if (ReadingPatternPrefersFullPassage(patternKey))
        {
            return string.Equals(patternKey, ExercisePatternKey.ReorderParagraphs, StringComparison.OrdinalIgnoreCase)
                ? "Use the selected full reading passage as the source text for the reordering task; do not invent unrelated content, and use any supporting vocabulary only as level-appropriate targets."
                : "Use the selected full reading passage as the ONLY reading passage; every question must be answerable from that passage, and weave in any supporting vocabulary naturally without adding unrelated advanced words.";
        }

        if (IsClozeReadingPattern(patternKey))
            return "Create a CEFR-aligned short gapped text inspired by the selected short reading reference and the vocabulary/grammar targets; do NOT copy a full reading passage into the activity.";

        if (string.Equals(request.PatternPrimarySkill, "Reading", StringComparison.OrdinalIgnoreCase))
            return "Use the selected short reading reference and vocabulary/grammar targets to shape a CEFR-aligned reading task; keep it answerable and self-contained.";

        // Vocabulary-primary family (phrase_match, gap-fill patterns, etc.).
        return "Use the selected vocabulary/usage targets naturally and, where present, the grammar target; keep distractors CEFR-aligned, keep the context aligned with the learner's context, and do not default to workplace unless workplace context is indicated.";
    }

    private static bool IsFullPassage(TodayBankSelectedResource r) =>
        string.Equals(r.ResourceType, "ReadingPassage", StringComparison.OrdinalIgnoreCase);
}

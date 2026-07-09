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
/// Phase D5 — context-aware selection across all bank types using the E9 published metadata. The
/// lean bank tables now carry context/focus/subskill/difficulty (Phase E9), so the selector applies
/// those as E9 query filters through a deterministic strict→loose relaxation ladder (context kept
/// longest; drop difficulty → focus → subskill → context → general), each combined with the
/// exact-CEFR-first / review-only-widen-down policy. **General English stays the default**: when the
/// learner is not workplace-routed (<see cref="TodayBankSelectionRequest.PrefersWorkplaceContext"/>
/// is false), workplace-tagged rows are skipped on **every** bank type — vocabulary/grammar/
/// reading-reference (via the E9 context metadata) as well as full passages (via detail context
/// tags) — closing the D4-era limitation where only passages could be context-filtered. When the
/// learner is workplace-routed, workplace content is preferred/permitted. Topic matching is purely
/// deterministic metadata matching — no embeddings, no vector search. All filters relax safely to a
/// smaller/general bundle rather than producing an empty result, and the caller still falls back to
/// legacy AI generation when no bank resource remains.
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

                // Phase D6 — anchor supporting resources on the selected passage's topical context so
                // the vocabulary/grammar the AI weaves in match the passage topic (e.g. a travel
                // passage pulls travel vocabulary). Relaxes to the general ladder when no anchor match.
                var anchor = PickAnchorContextTag(passages[0].MatchedContextTags);

                // Supporting vocabulary targets (same routed CEFR) so the AI can weave in level-
                // appropriate words; the passage remains the anchor. Optional grammar hint too.
                var (vocabSupport, vocabRaw) = await SelectVocabularyAsync(
                    request, MaxSupportingVocabulary, RoleSupporting, "vocabulary support", ct, anchor);
                candidates.AddRange(vocabSupport);
                anyRawHits |= vocabRaw;

                var (grammarSupport, grammarRaw) = await SelectGrammarAsync(request, MaxGrammar, RoleSupporting, ct, anchor);
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

        // Phase D6 — when a primary reference was chosen, anchor supporting resources on its topical
        // context (same deterministic context matching as the full-passage bundle).
        var anchor = refs.Count > 0 ? PickAnchorContextTag(refs[0].MatchedContextTags) : null;

        var (vocabSupport, vocabRaw) = await SelectVocabularyAsync(
            request, MaxSupportingVocabulary, RoleSupporting, "vocabulary support", ct, anchor);
        candidates.AddRange(vocabSupport);

        var (grammarSupport, grammarRaw) = await SelectGrammarAsync(request, MaxGrammar, RoleSupporting, ct, anchor);
        candidates.AddRange(grammarSupport);

        return (candidates, refRaw || vocabRaw || grammarRaw);
    }

    // ── Per-type selection ───────────────────────────────────────────────────────

    private Task<(List<TodayBankSelectedResource> Selected, bool AnyRawHits)> SelectVocabularyAsync(
        TodayBankSelectionRequest request, int max, string role, string reasonLabel, CancellationToken ct,
        string? anchorContextTag = null) =>
        SelectLeanAsync(request, max, role, "Vocabulary", reasonLabel, "bank-vocab-precheck",
            filter => _bankQuery.ListVocabularyAsync(filter, ct)
                .ContinueWith(t => (IReadOnlyList<LeanBankRow>)t.Result.Items.Select(i =>
                    new LeanBankRow(i.Id, i.Word, i.SourceId, i.ContextTags ?? Array.Empty<string>())).ToList(), ct),
            ct, anchorContextTag);

    private Task<(List<TodayBankSelectedResource> Selected, bool AnyRawHits)> SelectGrammarAsync(
        TodayBankSelectionRequest request, int max, string role, CancellationToken ct,
        string? anchorContextTag = null) =>
        SelectLeanAsync(request, max, role, "Grammar", "grammar", "bank-grammar-precheck",
            filter => _bankQuery.ListGrammarAsync(filter, ct)
                .ContinueWith(t => (IReadOnlyList<LeanBankRow>)t.Result.Items.Select(i =>
                    new LeanBankRow(i.Id, i.GrammarPoint, i.SourceId, i.ContextTags ?? Array.Empty<string>())).ToList(), ct),
            ct, anchorContextTag);

    private Task<(List<TodayBankSelectedResource> Selected, bool AnyRawHits)> SelectReadingReferenceAsync(
        TodayBankSelectionRequest request, int max, string role, CancellationToken ct,
        string? anchorContextTag = null) =>
        SelectLeanAsync(request, max, role, "Reading", "reading", "bank-reading-precheck",
            filter => _bankQuery.ListReadingReferencesAsync(filter, ct)
                .ContinueWith(t => (IReadOnlyList<LeanBankRow>)t.Result.Items.Select(i =>
                    new LeanBankRow(i.Id,
                        !string.IsNullOrWhiteSpace(i.ReferenceExcerpt) ? i.ReferenceExcerpt! : i.TextType ?? "reading reference",
                        i.SourceId, i.ContextTags ?? Array.Empty<string>())).ToList(), ct),
            ct, anchorContextTag);

    /// <summary>A projected lean-bank row carrying just what the selector needs: identity, a display
    /// string, the source id, and (Phase E9) the row's published context tags for D5's context
    /// defaulting.</summary>
    private readonly record struct LeanBankRow(Guid Id, string Display, Guid SourceId, IReadOnlyList<string> ContextTags);

    /// <summary>
    /// Phase D5 — shared selection for the lean bank tables (vocabulary/grammar/reading-reference)
    /// using the E9 published metadata filters. Tries a strict→loose relaxation ladder of
    /// context/focus/subskill/difficulty filters (each combined with the existing exact-CEFR-first /
    /// review-only-widen-down policy); the first ladder step that yields any allowed candidate wins.
    /// General-English default: when the learner is not workplace-routed, workplace-tagged rows are
    /// skipped on every bank type (not just full passages). Never throws.
    /// </summary>
    private async Task<(List<TodayBankSelectedResource> Selected, bool AnyRawHits)> SelectLeanAsync(
        TodayBankSelectionRequest request, int max, string role, string resourceType, string reasonLabel,
        string fingerprintPrefix, Func<ResourceBankListFilter, Task<IReadOnlyList<LeanBankRow>>> listFunc,
        CancellationToken ct, string? anchorContextTag = null)
    {
        var selected = new List<TodayBankSelectedResource>();
        if (max <= 0) return (selected, false);

        var anyRawHits = false;
        foreach (var attempt in BuildFilterLadder(request, anchorContextTag))
        {
            var (items, _, reasonSuffix, rawHits) = await QueryWithOptionalWideningAsync(
                request,
                level => listFunc(attempt.Filter with { CefrLevel = level, PageSize = BankQueryPageSize }),
                ct);
            anyRawHits |= rawHits;
            if (items.Count == 0) continue;

            var stepSelected = new List<TodayBankSelectedResource>();
            foreach (var row in items.Take(CandidateScanCap))
            {
                if (stepSelected.Count >= max) break;
                // General-English default (Phase D5): skip workplace-tagged rows unless routed workplace.
                if (!request.PrefersWorkplaceContext && IsWorkplaceTagged(row.ContextTags)) continue;

                var fingerprint = $"{fingerprintPrefix}:{row.Id}";
                if (!await IsAllowedAsync(request.StudentProfileId, fingerprint, row.Id, ct)) continue;

                stepSelected.Add(new TodayBankSelectedResource(
                    row.Id, resourceType, row.Display, row.SourceId, fingerprint,
                    $"{reasonLabel}{reasonSuffix}{attempt.ReasonSuffix}", Role: role,
                    AppliedFilters: attempt.ProvenanceLabel, MatchedContextTags: row.ContextTags));
            }

            if (stepSelected.Count > 0)
            {
                selected.AddRange(stepSelected);
                break; // first ladder step that yields allowed candidates wins
            }
        }

        return (selected, anyRawHits);
    }

    /// <summary>
    /// Phase D5/D6 — builds the deterministic strict→loose filter relaxation ladder from the request's
    /// E9 metadata preferences. Order (per the D5 plan): keep context longest, drop difficulty →
    /// focus → subskill → context. Consecutive identical filter sets (when a preference is absent)
    /// are de-duplicated so absent preferences add no extra queries. The final step carries no
    /// positive metadata filter (the general/no-filter attempt).
    ///
    /// Phase D6 — when an <paramref name="anchorContextTag"/> is supplied (the topical context of a
    /// selected reading passage/reference), a small set of **topic-anchor** rungs is prepended so
    /// supporting resources prefer the anchor's context first (e.g. a travel passage pulls travel
    /// vocabulary), before falling through to the D5 general ladder. This is pure deterministic
    /// context-tag matching — no embeddings, no vector/semantic search — and it still relaxes all the
    /// way down to the general attempt, so it can only narrow, never empty, the result.
    /// </summary>
    private static IReadOnlyList<FilterAttempt> BuildFilterLadder(TodayBankSelectionRequest request, string? anchorContextTag = null)
    {
        // Positive context filter only when workplace-routed; otherwise context is handled by the
        // post-query workplace exclusion, so no positive context filter is added.
        var contextTag = request.PrefersWorkplaceContext ? WorkplaceContextTag : null;
        var focusTag = request.PreferredFocusTags?.FirstOrDefault(f => !string.IsNullOrWhiteSpace(f));
        var subskill = string.IsNullOrWhiteSpace(request.PreferredSubskill) ? null : request.PreferredSubskill;
        var difficulty = request.PreferredDifficultyBand;

        var raw = new List<(string? Context, string? Focus, string? Subskill, int? Difficulty, bool TopicAnchor)>();

        // Phase D6 topic-anchor rungs (strictest first). Only added when a safe anchor context tag is
        // known; workplace is never used as a topic anchor (it is a routing context, not a topic, and
        // is governed by the workplace-exclusion policy instead).
        var safeAnchor = ResolveSafeAnchorContextTag(anchorContextTag, request.PrefersWorkplaceContext);
        if (safeAnchor is not null)
        {
            raw.Add((safeAnchor, focusTag, subskill, difficulty, true));
            raw.Add((safeAnchor, focusTag, subskill, null, true));  // drop difficulty, keep topic
            raw.Add((safeAnchor, null, subskill, null, true));       // drop focus, keep topic
            raw.Add((safeAnchor, null, null, null, true));           // topic context only
        }

        // D5 general ladder (unchanged): keep routing context longest, then relax to general.
        raw.Add((contextTag, focusTag, subskill, difficulty, false));
        raw.Add((contextTag, focusTag, subskill, null, false));   // drop difficulty
        raw.Add((contextTag, null, subskill, null, false));        // drop focus
        raw.Add((contextTag, null, null, null, false));            // drop subskill
        raw.Add((null, null, null, null, false));                  // general (drop context)

        var ladder = new List<FilterAttempt>();
        (string? Context, string? Focus, string? Subskill, int? Difficulty, bool TopicAnchor)? previous = null;
        var strictest = raw[0];
        foreach (var f in raw)
        {
            if (previous is { } p && p.Equals(f)) continue; // de-dupe consecutive identical sets
            previous = f;
            ladder.Add(new FilterAttempt(
                new ResourceBankListFilter(ContextTag: f.Context, FocusTag: f.Focus, Subskill: f.Subskill, DifficultyBand: f.Difficulty),
                ProvenanceLabel: DescribeFilters(f),
                ReasonSuffix: DescribeRelaxation(strictest, f)));
        }
        return ladder;
    }

    /// <summary>Phase D6 — normalizes a candidate anchor context tag for topic matching. Returns null
    /// for a blank tag, or for the workplace tag when the learner is not workplace-routed (workplace
    /// is a routing context handled by the exclusion policy, never a topic anchor).</summary>
    private static string? ResolveSafeAnchorContextTag(string? anchorContextTag, bool prefersWorkplace)
    {
        if (string.IsNullOrWhiteSpace(anchorContextTag)) return null;
        var tag = anchorContextTag.Trim();
        if (!prefersWorkplace && string.Equals(tag, WorkplaceContextTag, StringComparison.OrdinalIgnoreCase))
            return null;
        return tag;
    }

    /// <summary>Phase D6 — picks the topical anchor context tag from a selected passage/reference's
    /// context tags: the first non-workplace tag (workplace is a routing context, not a topic). Falls
    /// back to null when the anchor carries no usable topical tag, leaving the D5 general ladder.</summary>
    private static string? PickAnchorContextTag(IReadOnlyList<string> anchorContextTags) =>
        anchorContextTags?.FirstOrDefault(t =>
            !string.IsNullOrWhiteSpace(t) && !string.Equals(t, WorkplaceContextTag, StringComparison.OrdinalIgnoreCase));

    private readonly record struct FilterAttempt(ResourceBankListFilter Filter, string ProvenanceLabel, string ReasonSuffix);

    private static string DescribeFilters((string? Context, string? Focus, string? Subskill, int? Difficulty, bool TopicAnchor) f)
    {
        var parts = new List<string>();
        if (f.Context is not null) parts.Add(f.TopicAnchor ? $"context={f.Context}(topic-anchor)" : $"context={f.Context}");
        if (f.Focus is not null) parts.Add($"focus={f.Focus}");
        if (f.Subskill is not null) parts.Add($"subskill={f.Subskill}");
        if (f.Difficulty is not null) parts.Add($"difficulty={f.Difficulty}");
        return parts.Count == 0 ? "none" : string.Join(",", parts);
    }

    private static string DescribeRelaxation(
        (string? Context, string? Focus, string? Subskill, int? Difficulty, bool TopicAnchor) strictest,
        (string? Context, string? Focus, string? Subskill, int? Difficulty, bool TopicAnchor) applied)
    {
        var relaxed = new List<string>();
        // Losing the topic anchor is itself a relaxation worth recording.
        if (strictest.TopicAnchor && !applied.TopicAnchor) relaxed.Add("topic");
        if (strictest.Context is not null && applied.Context is null) relaxed.Add("context");
        if (strictest.Focus is not null && applied.Focus is null) relaxed.Add("focus");
        if (strictest.Subskill is not null && applied.Subskill is null) relaxed.Add("subskill");
        if (strictest.Difficulty is not null && applied.Difficulty is null) relaxed.Add("difficulty");
        return relaxed.Count == 0 ? string.Empty : $" [relaxed: {string.Join(",", relaxed)}]";
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
                EstimatedReadingMinutes: detail.EstimatedReadingMinutes,
                // Phase D5 — record the passage's context match for provenance parity with the lean
                // bank types (the passage's workplace-context handling is the D4 exclusion above).
                AppliedFilters: request.PrefersWorkplaceContext ? "context=workplace" : "context=general",
                MatchedContextTags: detail.ContextTags));
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

        // Phase D5 — a concise, bounded note on which selection filters shaped this bundle, so the
        // AI keeps the same context/focus emphasis the selector matched on.
        var appliedNote = DescribeAppliedFilters(resources, request);
        if (appliedNote is not null)
            sb.Append(appliedNote).Append(' ');

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

    /// <summary>Phase D5 — a one-line, bounded note describing the selection emphasis: the learner
    /// context (general vs workplace) and any distinct focus/subskill filters that survived onto the
    /// selected bundle. Null when there is nothing meaningful to say.</summary>
    private static string? DescribeAppliedFilters(IReadOnlyList<TodayBankSelectedResource> resources, TodayBankSelectionRequest request)
    {
        var contextWord = request.PrefersWorkplaceContext ? "workplace" : "general English";
        var extras = resources
            .Select(r => r.AppliedFilters)
            .Where(a => a is not null && a != "none" && a != "context=general" && a != "context=workplace")
            .Distinct()
            .ToList();
        var extraNote = extras.Count > 0 ? $"; matched filters: {string.Join("; ", extras)}" : string.Empty;
        return $"Selection emphasis: keep the {contextWord} context of these resources{extraNote}.";
    }

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

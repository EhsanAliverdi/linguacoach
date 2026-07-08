using LinguaCoach.Application.Activity;
using LinguaCoach.Application.ResourceImport;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Activity;

/// <summary>
/// Phase D1 implementation of <see cref="ITodayBankResourceSelector"/>. Queries the published
/// Resource Bank (<see cref="IResourceBankQueryService"/>) for a small, CEFR-matched set of
/// vocabulary/grammar/reading entries and runs each candidate through a synthetic-fingerprint
/// novelty precheck (mirroring PracticeGymGenerationJob.TryMaterializeFromTemplateAsync's
/// template-precheck idea) before handing back a short prompt-supplement sentence. Never throws —
/// Today lesson generation must never break because of this selector.
/// </summary>
public sealed class TodayBankResourceSelector : ITodayBankResourceSelector
{
    private const int CandidateScanCap = 10;
    private const int BankQueryPageSize = 20;

    private readonly IResourceBankQueryService _bankQuery;
    private readonly IActivityNoveltyPolicy _noveltyPolicy;
    private readonly ILogger<TodayBankResourceSelector> _logger;

    public TodayBankResourceSelector(
        IResourceBankQueryService bankQuery,
        IActivityNoveltyPolicy noveltyPolicy,
        ILogger<TodayBankResourceSelector> logger)
    {
        _bankQuery = bankQuery;
        _noveltyPolicy = noveltyPolicy;
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
                var vocab = await _bankQuery.ListVocabularyAsync(
                    new ResourceBankListFilter(CefrLevel: request.CefrLevel, PageSize: BankQueryPageSize), ct);
                anyRawHits |= vocab.Items.Count > 0;

                foreach (var item in vocab.Items.Take(CandidateScanCap))
                {
                    if (await IsAllowedAsync(request.StudentProfileId, "bank-vocab-precheck", item.Id, ct))
                        candidates.Add(new TodayBankSelectedResource(item.Id, "Vocabulary", item.Word));
                }

                var includeGrammar = request.PatternSecondarySkills.Any(
                    s => string.Equals(s, "Grammar", StringComparison.OrdinalIgnoreCase));
                if (includeGrammar)
                {
                    // Opportunistic supplementary content only — gap_fill_workplace_phrase is the
                    // only pattern this ever applies to; there is no dedicated grammar-focused
                    // Today pattern yet, so grammar is never a gating skill on its own here.
                    var grammar = await _bankQuery.ListGrammarAsync(
                        new ResourceBankListFilter(CefrLevel: request.CefrLevel, PageSize: BankQueryPageSize), ct);
                    anyRawHits |= grammar.Items.Count > 0;

                    foreach (var item in grammar.Items.Take(CandidateScanCap))
                    {
                        if (await IsAllowedAsync(request.StudentProfileId, "bank-grammar-precheck", item.Id, ct))
                            candidates.Add(new TodayBankSelectedResource(item.Id, "Grammar", item.GrammarPoint));
                    }
                }
            }
            else // isReading
            {
                var reading = await _bankQuery.ListReadingReferencesAsync(
                    new ResourceBankListFilter(CefrLevel: request.CefrLevel, PageSize: BankQueryPageSize), ct);
                anyRawHits |= reading.Items.Count > 0;

                foreach (var item in reading.Items.Take(CandidateScanCap))
                {
                    if (await IsAllowedAsync(request.StudentProfileId, "bank-reading-precheck", item.Id, ct))
                    {
                        var display = !string.IsNullOrWhiteSpace(item.ReferenceExcerpt)
                            ? item.ReferenceExcerpt!
                            : item.TextType ?? "reading reference";
                        candidates.Add(new TodayBankSelectedResource(item.Id, "Reading", display));
                    }
                }
            }

            if (candidates.Count == 0)
            {
                return anyRawHits
                    ? new TodayBankSelectionResult(TodayBankSelectionOutcome.BlockedByNovelty, Array.Empty<TodayBankSelectedResource>(), null)
                    : TodayBankSelectionResult.NoResources;
            }

            var selected = candidates.Take(request.MaxResources).ToList();
            var promptSupplement =
                $"Where natural, incorporate these approved vocabulary/grammar/reading resources: " +
                $"{string.Join(", ", selected.Select(r => r.DisplayText))}. " +
                "Do not invent unrelated vocabulary or change the CEFR level.";

            return new TodayBankSelectionResult(
                TodayBankSelectionOutcome.BankResourcesFound, selected, promptSupplement);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "TodayBankResourceSelector: selection failed for StudentProfileId={StudentProfileId}, PatternPrimarySkill={PatternPrimarySkill} — continuing without bank resources.",
                request.StudentProfileId, request.PatternPrimarySkill);
            return TodayBankSelectionResult.NoResources;
        }
    }

    /// <summary>
    /// Synthetic-fingerprint novelty precheck for a single bank candidate — mirrors
    /// PracticeGymGenerationJob's per-template precheck idea, one check per candidate resource.
    /// Never throws: a failed check just excludes that one candidate (logged), never aborts
    /// selection for the whole request.
    /// </summary>
    private async Task<bool> IsAllowedAsync(Guid studentProfileId, string prefix, Guid entryId, CancellationToken ct)
    {
        try
        {
            var check = await _noveltyPolicy.CheckAsync(new ActivityNoveltyCheckRequest(
                StudentProfileId: studentProfileId,
                ContentFingerprint: $"{prefix}:{entryId}"), ct);
            return check.Allowed;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "TodayBankResourceSelector: novelty precheck failed for bank entry {EntryId} ({Prefix}) — skipping this candidate.",
                entryId, prefix);
            return false;
        }
    }
}

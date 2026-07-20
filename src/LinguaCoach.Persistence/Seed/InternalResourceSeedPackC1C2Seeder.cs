using System.Text;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Persistence.Seed;

/// <summary>
/// Sprint 9 — "Internal Resource Bank C1/C2 Depth". A third original, English-only content pack
/// flowing through the exact same Phase E1-E4 staging pipeline as
/// <see cref="InternalResourceSeedPackSeeder"/> (E6/E7) and <see cref="InternalResourceSeedPackE8Seeder"/>
/// (E8): import → deterministic metadata mapping → deterministic validation → admin approval →
/// publish → the published Resource Bank browse/search surface. Never writes a
/// <see cref="ResourceBankItem"/> row directly.
///
/// Fills a real, live-confirmed gap: prior to this pack, zero Vocabulary/Grammar resources existed
/// above B2 anywhere in the bank — C1/C2 placement (Sprint 8.2/8.3) could estimate a student's true
/// level, but nothing above B2 existed for that student to actually learn from afterward. Distinct
/// <see cref="CefrResourceSource"/> from E6/E7/E8, so this seeder is fully independent and
/// idempotent by its own source name.
///
/// Content is 100% original, written for this codebase — not copied from any textbook, dataset,
/// test-prep material, or external site. English-only; no Persian/bilingual/support-language
/// content. Vocabulary/grammar targets genuine C1 (Advanced) / C2 (Proficiency) constructs —
/// nuanced lexis, register-sensitive collocations, inversion, mixed conditionals, subjunctive
/// mood, cleft sentences, ellipsis, and other advanced structures — matching the same quality bar
/// as the Sprint 8.3 C1/C2 placement items.
///
/// Every row carries its own accurate cefrLevel/skill/subskill/tags columns, so
/// <see cref="LinguaCoach.Infrastructure.ResourceImport.ResourceImportService"/>'s deterministic
/// row-metadata mapping populates those fields at staging time — no AI analysis call is made or
/// needed. Idempotent by source name: if a source named <see cref="SourceName"/> already exists,
/// this seeder is a complete no-op.
/// </summary>
public static class InternalResourceSeedPackC1C2Seeder
{
    public const string SourceName = "SpeakPath Internal English Seed Pack C1-C2 (Advanced/Proficiency Depth)";

    public static async Task SeedAsync(
        LinguaCoachDbContext db,
        IResourceImportService importService,
        IResourceCandidateValidationService validationService,
        IResourceCandidatePublishService publishService,
        ILogger logger,
        CancellationToken ct = default)
    {
        var existing = await db.CefrResourceSources.FirstOrDefaultAsync(s => s.Name == SourceName, ct);
        if (existing is not null)
        {
            logger.LogInformation(
                "InternalResourceSeedPackC1C2Seeder: source '{Name}' already exists — skipping entirely.", SourceName);
            return;
        }

        // Honest provenance: original, internally-authored content, not an external dataset.
        var source = new CefrResourceSource(
            name: SourceName,
            licenseType: "Internal/Original",
            allowsStudentDisplay: true,
            allowsCommercialUse: true,
            attributionText: "SpeakPath internal content team",
            sourceVersion: "c1c2",
            usageRestrictionNotes:
                "Original, internally-authored, English-only C1/C2 depth content written for " +
                "Sprint 9. Not sourced from any external dataset, textbook, test-prep material, or " +
                "copyrighted work. General English by default.");

        source.ApproveForImport("Internal original content authored for this codebase — no external licensing review required (Sprint 9).");
        db.CefrResourceSources.Add(source);
        await db.SaveChangesAsync(ct);

        // Phase 4.2 — every publishable candidate must trace back to an ImportPackage with an
        // approved Import Execution Plan. Same self-approved-package precedent as E6/E8 — never a
        // backdoor around the gate, and (unlike the Sprint 8.1 orphaned candidates) this seeder
        // creates its package from the start, so no retroactive backfill will ever be needed for
        // this source.
        var importPackageId = await SeedApprovedPackageAsync(db, source.Id, ct);

        var vocabularyRun = await ImportAsync(importService, source.Id, VocabularyJson, "c1c2-vocabulary.json", importPackageId, ct);
        var grammarRun = await ImportAsync(importService, source.Id, GrammarJson, "c1c2-grammar.json", importPackageId, ct);

        logger.LogInformation(
            "InternalResourceSeedPackC1C2Seeder: staged vocabulary {VocabSucceeded}/{VocabTotal}, " +
            "grammar {GrammarSucceeded}/{GrammarTotal}.",
            vocabularyRun.SucceededCount, vocabularyRun.TotalRecordCount,
            grammarRun.SucceededCount, grammarRun.TotalRecordCount);

        var candidateIds = await (
            from c in db.ResourceCandidates
            join r in db.ResourceRawRecords on c.ResourceRawRecordId equals r.Id
            join run in db.ResourceImportRuns on r.ResourceImportRunId equals run.Id
            where run.CefrResourceSourceId == source.Id
            select c.Id)
            .ToListAsync(ct);

        var publishedCount = 0;
        foreach (var candidateId in candidateIds)
        {
            var validationResult = await validationService.ValidateAsync(candidateId, ct);
            if (validationResult.Status != ResourceCandidateValidationStatus.Passed.ToString())
            {
                logger.LogWarning(
                    "InternalResourceSeedPackC1C2Seeder: candidate {CandidateId} did not pass validation ({Status}) — " +
                    "left unapproved/unpublished. Errors: {Errors}",
                    candidateId, validationResult.Status, string.Join("; ", validationResult.Errors));
                continue;
            }

            var candidate = await db.ResourceCandidates.FirstAsync(c => c.Id == candidateId, ct);
            candidate.Approve("Approved by InternalResourceSeedPackC1C2Seeder — pre-reviewed internal content (Sprint 9).");
            await db.SaveChangesAsync(ct);

            var publishResult = await publishService.PublishAsync(candidateId, publishedByUserId: null, ct);
            if (!publishResult.Success)
            {
                logger.LogWarning(
                    "InternalResourceSeedPackC1C2Seeder: candidate {CandidateId} failed to publish: {Errors}",
                    candidateId, string.Join("; ", publishResult.Errors));
                continue;
            }

            publishedCount++;
        }

        logger.LogInformation(
            "InternalResourceSeedPackC1C2Seeder: published {PublishedCount}/{TotalCandidates} candidates from source '{Name}'.",
            publishedCount, candidateIds.Count, SourceName);
    }

    private static async Task<ResourceImportResult> ImportAsync(
        IResourceImportService importService, Guid sourceId, string json, string fileName,
        Guid importPackageId, CancellationToken ct)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return await importService.ImportAsync(
            new ResourceImportRequest(sourceId, stream, fileName, ResourceImportMode.Json, ImportPackageId: importPackageId), ct);
    }

    private static async Task<Guid> SeedApprovedPackageAsync(LinguaCoachDbContext db, Guid sourceId, CancellationToken ct)
    {
        var package = new ImportPackage(sourceId, SourceName, DateTimeOffset.UtcNow);
        db.ImportPackages.Add(package);
        await db.SaveChangesAsync(ct);

        var plan = new ImportProfile(
            package.Id, 1, profileJson: "{}", sampleAssetIds: Array.Empty<Guid>(),
            estimatedCandidateCount: 1, createdAtUtc: DateTimeOffset.UtcNow);
        plan.SubmitForApproval();
        plan.Approve(approvedByUserId: null, DateTimeOffset.UtcNow, approvedCostCeiling: 0m);
        db.ImportProfiles.Add(plan);
        package.ApproveProfile(plan.Id);
        await db.SaveChangesAsync(ct);

        return package.Id;
    }

    // ── Seed content ────────────────────────────────────────────────────────────────
    // 24 vocabulary (12 C1 / 12 C2) / 12 grammar (6 C1 / 6 C2), all original English-only content.
    // Subskills restricted to the taxonomy the validation gate enforces (same constraint as E6/E8).

    private const string VocabularyJson = """
    [
      {"word":"ambiguous","partOfSpeech":"adjective","definition":"Having more than one possible meaning; open to more than one interpretation.","cefrLevel":"C1","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["general","study"]},
      {"word":"articulate","partOfSpeech":"adjective","definition":"Able to express thoughts and ideas clearly and effectively.","cefrLevel":"C1","skill":"vocabulary","subskill":"vocabulary.word_form","tags":["workplace","general"]},
      {"word":"contentious","partOfSpeech":"adjective","definition":"Likely to cause disagreement or controversy.","cefrLevel":"C1","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["general","workplace"]},
      {"word":"discrepancy","partOfSpeech":"noun","definition":"A difference between two things that are expected to be the same.","cefrLevel":"C1","skill":"vocabulary","subskill":"vocabulary.collocation","tags":["workplace","study"]},
      {"word":"exacerbate","partOfSpeech":"verb","definition":"To make a problem, situation, or feeling worse.","cefrLevel":"C1","skill":"vocabulary","subskill":"vocabulary.productive","tags":["general","study"]},
      {"word":"inherent","partOfSpeech":"adjective","definition":"Existing as a natural or basic part of something; inseparable from it.","cefrLevel":"C1","skill":"vocabulary","subskill":"vocabulary.collocation","tags":["study","general"]},
      {"word":"plausible","partOfSpeech":"adjective","definition":"Seeming reasonable, likely, or probable.","cefrLevel":"C1","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["general"]},
      {"word":"resilience","partOfSpeech":"noun","definition":"The ability to recover quickly from difficulties or setbacks.","cefrLevel":"C1","skill":"vocabulary","subskill":"vocabulary.word_form","tags":["general","workplace"]},
      {"word":"substantiate","partOfSpeech":"verb","definition":"To provide evidence or proof to support a claim or statement.","cefrLevel":"C1","skill":"vocabulary","subskill":"vocabulary.productive","tags":["study","workplace"]},
      {"word":"tentative","partOfSpeech":"adjective","definition":"Not certain or fixed; provisional, done with caution.","cefrLevel":"C1","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["workplace","general"]},
      {"word":"unprecedented","partOfSpeech":"adjective","definition":"Never having happened, existed, or been done before.","cefrLevel":"C1","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["general","study"]},
      {"word":"viable","partOfSpeech":"adjective","definition":"Capable of working successfully; feasible in practice.","cefrLevel":"C1","skill":"vocabulary","subskill":"vocabulary.collocation","tags":["workplace","general"]},
      {"word":"equivocal","partOfSpeech":"adjective","definition":"Open to more than one interpretation, often deliberately unclear.","cefrLevel":"C2","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["general","study"]},
      {"word":"fastidious","partOfSpeech":"adjective","definition":"Very attentive to detail and accuracy; hard to please.","cefrLevel":"C2","skill":"vocabulary","subskill":"vocabulary.word_form","tags":["workplace","general"]},
      {"word":"incongruous","partOfSpeech":"adjective","definition":"Not in harmony or agreement with its surroundings; out of place.","cefrLevel":"C2","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["general"]},
      {"word":"juxtapose","partOfSpeech":"verb","definition":"To place two things side by side, especially to compare or contrast them.","cefrLevel":"C2","skill":"vocabulary","subskill":"vocabulary.productive","tags":["study","general"]},
      {"word":"nuanced","partOfSpeech":"adjective","definition":"Characterized by subtle shades of meaning, expression, or feeling.","cefrLevel":"C2","skill":"vocabulary","subskill":"vocabulary.word_form","tags":["study","general"]},
      {"word":"obfuscate","partOfSpeech":"verb","definition":"To deliberately make something unclear or difficult to understand.","cefrLevel":"C2","skill":"vocabulary","subskill":"vocabulary.productive","tags":["workplace","general"]},
      {"word":"paradigm","partOfSpeech":"noun","definition":"A typical example or pattern of something; a model.","cefrLevel":"C2","skill":"vocabulary","subskill":"vocabulary.collocation","tags":["study","workplace"]},
      {"word":"pragmatic","partOfSpeech":"adjective","definition":"Dealing with things sensibly and realistically, based on practical considerations.","cefrLevel":"C2","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["workplace","general"]},
      {"word":"quintessential","partOfSpeech":"adjective","definition":"Representing the most perfect or typical example of a quality or type.","cefrLevel":"C2","skill":"vocabulary","subskill":"vocabulary.collocation","tags":["general"]},
      {"word":"surreptitious","partOfSpeech":"adjective","definition":"Done secretly or quietly to avoid being noticed.","cefrLevel":"C2","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["general"]},
      {"word":"tenuous","partOfSpeech":"adjective","definition":"Very weak or slight; barely holding together.","cefrLevel":"C2","skill":"vocabulary","subskill":"vocabulary.word_form","tags":["study","general"]},
      {"word":"vindicate","partOfSpeech":"verb","definition":"To clear someone of blame or suspicion, or to show that a belief was justified.","cefrLevel":"C2","skill":"vocabulary","subskill":"vocabulary.productive","tags":["general","workplace"]}
    ]
    """;

    private const string GrammarJson = """
    [
      {"grammarKey":"Inversion after negative adverbials","explanation":"Front a negative or restrictive adverbial (Not only, Rarely, Under no circumstances) and invert the subject and auxiliary verb for emphasis. Example: Not only did she arrive late, but she also forgot the documents. Rarely have I seen such dedication.","cefrLevel":"C1","skill":"grammar","subskill":"grammar.word_order","tags":["study","general"]},
      {"grammarKey":"Mixed conditionals","explanation":"Combine a past condition with a present result, or a present condition with a past result, when the timeframes of the condition and consequence differ. Example: If she had studied medicine, she would be a doctor now. If he were more organised, he would have finished on time.","cefrLevel":"C1","skill":"grammar","subskill":"grammar.tense_aspect","tags":["general","study"]},
      {"grammarKey":"Cleft sentences for emphasis","explanation":"Use 'It is/was ... that' or 'What ... is/was' to give special emphasis to one part of a sentence. Example: It was the manager who approved the budget. What surprised everyone was how quickly the project finished.","cefrLevel":"C1","skill":"grammar","subskill":"grammar.word_order","tags":["workplace","general"]},
      {"grammarKey":"Subjunctive mood in formal register","explanation":"Use the base form of the verb (not the third-person -s form) after verbs like 'suggest', 'insist', 'recommend' and after 'It is essential/important that' in formal English. Example: It is essential that he arrive on time. The committee recommended that the policy be reviewed.","cefrLevel":"C1","skill":"grammar","subskill":"grammar.tense_aspect","tags":["workplace","study"]},
      {"grammarKey":"Participle clauses for concise subordination","explanation":"Use a present or past participle clause to replace a longer relative or adverbial clause, giving writing a more concise, formal style. Example: Having finished the report, she left the office early. Written in haste, the email contained several errors.","cefrLevel":"C1","skill":"grammar","subskill":"grammar.word_order","tags":["study","workplace"]},
      {"grammarKey":"Nominalization in formal writing","explanation":"Convert a verb or adjective into a noun to create a more formal, impersonal, academic style, often used with articles and determiners. Example: 'The company decided to expand' becomes 'The company's decision to expand...'. 'They are reluctant to change' becomes 'their reluctance to change'.","cefrLevel":"C1","skill":"grammar","subskill":"grammar.articles_determiners","tags":["study","workplace"]},
      {"grammarKey":"Fronting for rhetorical emphasis","explanation":"Move an object, complement, or adverbial to the front of the sentence, ahead of the subject, for dramatic or rhetorical effect, typically in more literary or persuasive writing. Example: Never before had the team faced such a challenge. Such was her determination that nothing could stop her.","cefrLevel":"C2","skill":"grammar","subskill":"grammar.word_order","tags":["study","general"]},
      {"grammarKey":"Ellipsis in formal and literary register","explanation":"Omit words that are recoverable from context to create more concise, sophisticated sentences, common in formal writing and literary style. Example: Some praised the decision; others condemned it [omitting 'the decision']. He wanted to leave early, and she [wanted to leave early] too.","cefrLevel":"C2","skill":"grammar","subskill":"grammar.word_order","tags":["study","general"]},
      {"grammarKey":"Modal perfect for speculation about the past","explanation":"Use a modal verb + have + past participle to speculate, criticise, or express regret about a past event, distinguishing degrees of certainty. Example: She might have missed the train. He should have told us sooner. They can't have known about the change.","cefrLevel":"C2","skill":"grammar","subskill":"grammar.tense_aspect","tags":["general","workplace"]},
      {"grammarKey":"Inversion in conditionals without if","explanation":"Omit 'if' and invert the subject and auxiliary in formal conditional sentences, a hallmark of very formal or literary register. Example: Had I known about the delay, I would have left earlier. Were she to accept the offer, the whole plan would change.","cefrLevel":"C2","skill":"grammar","subskill":"grammar.tense_aspect","tags":["study","workplace"]},
      {"grammarKey":"Emphatic do-support in formal writing","explanation":"Use 'do/does/did' before the base verb to add emphasis or contradict an expectation, common in persuasive or formal argumentative writing. Example: The report does raise some valid concerns. She did warn us, even if we chose not to listen.","cefrLevel":"C2","skill":"grammar","subskill":"grammar.tense_aspect","tags":["study","general"]},
      {"grammarKey":"Nominal relative clauses","explanation":"Use 'what', 'whoever', 'whatever', or 'whichever' to introduce a clause that functions as the subject or object of the sentence, without a separate antecedent noun. Example: What concerns me most is the lack of evidence. Whoever wrote this report clearly understood the issue.","cefrLevel":"C2","skill":"grammar","subskill":"grammar.word_order","tags":["study","general"]}
    ]
    """;
}

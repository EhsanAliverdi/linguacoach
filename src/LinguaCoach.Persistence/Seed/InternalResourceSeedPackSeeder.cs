using System.Text;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Persistence.Seed;

/// <summary>
/// Phase E6 — "First Real English Resource Depth". Seeds a small, original, English-only content
/// slice (vocabulary/grammar/reading) through the FULL Phase E1-E4 staging pipeline
/// (<see cref="IResourceImportService"/> → <see cref="IResourceCandidateValidationService"/> →
/// admin approval → <see cref="IResourceCandidatePublishService"/>) rather than writing
/// Cefr*Entry rows directly — the whole point of this phase is proving the pipeline handles real
/// content, never bypassing it. See docs/architecture/english-resource-bank-import-platform.md.
///
/// Content is 100% original, written for this codebase — not copied from any textbook, dataset,
/// or external site (no CEFR-J, no CMUdict, no British Council/Cambridge/Oxford/BBC content).
/// Every row already carries its own accurate cefrLevel/skill/subskill/tags columns (the author —
/// this seeder's content — already knows the correct classification), so
/// <see cref="LinguaCoach.Infrastructure.ResourceImport.ResourceImportService"/>'s Phase E6
/// deterministic row-metadata mapping populates those fields at staging time — no AI analysis
/// call is made or needed here (<see cref="LinguaCoach.Infrastructure.ResourceImport.ResourceCandidateAnalysisService"/>
/// is never invoked by this seeder).
///
/// Idempotent by <see cref="CefrResourceSource"/> name: if a source named <see cref="SourceName"/>
/// already exists, this seeder is a complete no-op (does not re-import, re-validate, or
/// re-publish). Safe to run on every startup, matching every other seeder's convention in this
/// codebase (see e.g. ActivityTemplateSeeder).
/// </summary>
public static class InternalResourceSeedPackSeeder
{
    public const string SourceName = "SpeakPath Internal English Seed Pack v1";

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
                "InternalResourceSeedPackSeeder: source '{Name}' already exists — skipping entirely.", SourceName);
            return;
        }

        // Honest provenance: this is original, internally-authored content, not an external
        // dataset. LicenseType/AttributionText must never claim otherwise (no fake CC-BY /
        // external-source attribution).
        var source = new CefrResourceSource(
            name: SourceName,
            licenseType: "Internal/Original",
            allowsStudentDisplay: true,
            allowsCommercialUse: true,
            attributionText: "SpeakPath internal content team",
            sourceVersion: "v1",
            usageRestrictionNotes:
                "Original, internally-authored, English-only seed content written for Phase E6 " +
                "content-depth. Not sourced from any external dataset, textbook, or copyrighted work.");

        // Internal, pre-reviewed content doesn't need an external licensing review — approve
        // immediately via the same real ApproveForImport() method an admin would use, rather than
        // a backdoor field set.
        source.ApproveForImport("Internal original content authored for this codebase — no external licensing review required.");
        db.CefrResourceSources.Add(source);
        await db.SaveChangesAsync(ct);

        var vocabularyRun = await ImportAsync(importService, source.Id, VocabularyJson, "internal-seed-vocabulary.json", ct);
        var grammarRun = await ImportAsync(importService, source.Id, GrammarJson, "internal-seed-grammar.json", ct);
        var readingRun = await ImportAsync(importService, source.Id, ReadingJson, "internal-seed-reading.json", ct);

        logger.LogInformation(
            "InternalResourceSeedPackSeeder: staged vocabulary {VocabSucceeded}/{VocabTotal}, " +
            "grammar {GrammarSucceeded}/{GrammarTotal}, reading {ReadingSucceeded}/{ReadingTotal}.",
            vocabularyRun.SucceededCount, vocabularyRun.TotalRecordCount,
            grammarRun.SucceededCount, grammarRun.TotalRecordCount,
            readingRun.SucceededCount, readingRun.TotalRecordCount);

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
            // Phase E2's deterministic rule-validation gate IS needed and must actually run — it's
            // the sole authority on ValidationStatus, even for pre-classified internal content.
            var validationResult = await validationService.ValidateAsync(candidateId, ct);
            if (validationResult.Status != ResourceCandidateValidationStatus.Passed.ToString())
            {
                logger.LogWarning(
                    "InternalResourceSeedPackSeeder: candidate {CandidateId} did not pass validation ({Status}) — " +
                    "left unapproved/unpublished. Errors: {Errors}",
                    candidateId, validationResult.Status, string.Join("; ", validationResult.Errors));
                continue;
            }

            // Judgment call: normally a human admin approves a candidate via the admin review UI.
            // For this controlled, pre-reviewed internal seed pack — content written directly into
            // this codebase's own source and reviewed as part of landing this change, not arbitrary
            // imported data — it is reasonable for the seeder itself to perform the same "admin
            // approval" action on behalf of that review, via the real Approve() method (never a
            // backdoor). This mirrors ActivityTemplateSeeder's own precedent of calling
            // template.Approve()/template.Publish() directly for its own hand-authored content.
            var candidate = await db.ResourceCandidates.FirstAsync(c => c.Id == candidateId, ct);
            candidate.Approve("Approved by InternalResourceSeedPackSeeder — pre-reviewed internal content (Phase E6).");
            await db.SaveChangesAsync(ct);

            var publishResult = await publishService.PublishAsync(candidateId, publishedByUserId: null, ct);
            if (!publishResult.Success)
            {
                logger.LogWarning(
                    "InternalResourceSeedPackSeeder: candidate {CandidateId} failed to publish: {Errors}",
                    candidateId, string.Join("; ", publishResult.Errors));
                continue;
            }

            publishedCount++;
        }

        logger.LogInformation(
            "InternalResourceSeedPackSeeder: published {PublishedCount}/{TotalCandidates} candidates from source '{Name}'.",
            publishedCount, candidateIds.Count, SourceName);
    }

    private static async Task<ResourceImportResult> ImportAsync(
        IResourceImportService importService, Guid sourceId, string json, string fileName, CancellationToken ct)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return await importService.ImportAsync(
            new ResourceImportRequest(sourceId, stream, fileName, ResourceImportMode.Json), ct);
    }

    // ── Seed content ────────────────────────────────────────────────────────────────
    // Embedded directly as C# string constants (matches ActivityTemplateSeeder's existing
    // convention of embedding original hand-authored content in code rather than loading external
    // seed files from disk at startup — this codebase has no precedent for the latter). Every
    // entry below is original English-only content written for this phase; word/definition
    // choices are a deliberate mix of everyday and workplace-relevant vocabulary, not
    // workplace-exclusive. 32 vocabulary / 12 grammar / 10 reading entries — comfortably within
    // the suggested 30-40 / 10-15 / 8-12 ranges.

    private const string VocabularyJson = """
    [
      {"word":"apple","partOfSpeech":"noun","definition":"A round fruit with red, green, or yellow skin.","cefrLevel":"A1","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["everyday"]},
      {"word":"house","partOfSpeech":"noun","definition":"A building where people live.","cefrLevel":"A1","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["everyday"]},
      {"word":"water","partOfSpeech":"noun","definition":"A clear liquid that people drink and use for washing.","cefrLevel":"A1","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["everyday"]},
      {"word":"friend","partOfSpeech":"noun","definition":"A person you know well and like.","cefrLevel":"A1","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["everyday"]},
      {"word":"morning","partOfSpeech":"noun","definition":"The early part of the day, before noon.","cefrLevel":"A1","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["everyday"]},
      {"word":"walk","partOfSpeech":"verb","definition":"To move on foot at a normal speed.","cefrLevel":"A1","skill":"vocabulary","subskill":"vocabulary.productive","tags":["everyday"]},
      {"word":"happy","partOfSpeech":"adjective","definition":"Feeling or showing pleasure or contentment.","cefrLevel":"A1","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["everyday"]},
      {"word":"book","partOfSpeech":"noun","definition":"A set of printed pages fastened together, meant for reading.","cefrLevel":"A1","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["everyday"]},
      {"word":"schedule","partOfSpeech":"noun","definition":"A plan that lists times when things will happen.","cefrLevel":"A2","skill":"vocabulary","subskill":"vocabulary.productive","tags":["workplace"]},
      {"word":"colleague","partOfSpeech":"noun","definition":"A person you work with.","cefrLevel":"A2","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["workplace"]},
      {"word":"weather","partOfSpeech":"noun","definition":"The condition of the atmosphere at a particular place and time.","cefrLevel":"A2","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["everyday"]},
      {"word":"invite","partOfSpeech":"verb","definition":"To ask someone to come to an event.","cefrLevel":"A2","skill":"vocabulary","subskill":"vocabulary.productive","tags":["everyday"]},
      {"word":"journey","partOfSpeech":"noun","definition":"An act of travelling from one place to another.","cefrLevel":"A2","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["everyday"]},
      {"word":"receipt","partOfSpeech":"noun","definition":"A printed note showing that money or goods were received.","cefrLevel":"A2","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["everyday","workplace"]},
      {"word":"catch up","partOfSpeech":"phrasal verb","definition":"To reach the same level or progress as someone else, or to talk with someone after not seeing them for a while.","cefrLevel":"A2","skill":"vocabulary","subskill":"vocabulary.phrasal_verbs","tags":["everyday","workplace"]},
      {"word":"borrow","partOfSpeech":"verb","definition":"To take and use something that belongs to someone else, planning to return it.","cefrLevel":"A2","skill":"vocabulary","subskill":"vocabulary.productive","tags":["everyday"]},
      {"word":"negotiate","partOfSpeech":"verb","definition":"To discuss something with someone to reach an agreement.","cefrLevel":"B1","skill":"vocabulary","subskill":"vocabulary.productive","tags":["workplace"]},
      {"word":"feedback","partOfSpeech":"noun","definition":"Comments about how well something was done, used to help improve it.","cefrLevel":"B1","skill":"vocabulary","subskill":"vocabulary.collocation","tags":["workplace"]},
      {"word":"reliable","partOfSpeech":"adjective","definition":"Able to be trusted to do what is expected.","cefrLevel":"B1","skill":"vocabulary","subskill":"vocabulary.word_form","tags":["everyday","workplace"]},
      {"word":"achievement","partOfSpeech":"noun","definition":"Something successfully completed through effort or skill.","cefrLevel":"B1","skill":"vocabulary","subskill":"vocabulary.word_form","tags":["workplace"]},
      {"word":"environment","partOfSpeech":"noun","definition":"The natural world, or the conditions someone works or lives in.","cefrLevel":"B1","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["everyday","workplace"]},
      {"word":"opportunity","partOfSpeech":"noun","definition":"A chance to do something or to achieve a goal.","cefrLevel":"B1","skill":"vocabulary","subskill":"vocabulary.collocation","tags":["workplace"]},
      {"word":"punctual","partOfSpeech":"adjective","definition":"Arriving or doing something at the agreed time; not late.","cefrLevel":"B1","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["workplace"]},
      {"word":"carry out","partOfSpeech":"phrasal verb","definition":"To do a task, plan, or instruction.","cefrLevel":"B1","skill":"vocabulary","subskill":"vocabulary.phrasal_verbs","tags":["workplace"]},
      {"word":"collaborate","partOfSpeech":"verb","definition":"To work together with someone to produce or achieve something.","cefrLevel":"B2","skill":"vocabulary","subskill":"vocabulary.productive","tags":["workplace"]},
      {"word":"prioritize","partOfSpeech":"verb","definition":"To decide which of several tasks is the most important and deal with that first.","cefrLevel":"B2","skill":"vocabulary","subskill":"vocabulary.productive","tags":["workplace"]},
      {"word":"comprehensive","partOfSpeech":"adjective","definition":"Including everything that is necessary; complete.","cefrLevel":"B2","skill":"vocabulary","subskill":"vocabulary.word_form","tags":["workplace"]},
      {"word":"initiative","partOfSpeech":"noun","definition":"The ability to decide and act on your own without being told what to do.","cefrLevel":"B2","skill":"vocabulary","subskill":"vocabulary.collocation","tags":["workplace"]},
      {"word":"sustainable","partOfSpeech":"adjective","definition":"Able to continue over time without damaging resources or the environment.","cefrLevel":"B2","skill":"vocabulary","subskill":"vocabulary.word_form","tags":["everyday","workplace"]},
      {"word":"ambiguous","partOfSpeech":"adjective","definition":"Having more than one possible meaning; unclear.","cefrLevel":"B2","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["workplace"]},
      {"word":"facilitate","partOfSpeech":"verb","definition":"To make a process or action easier.","cefrLevel":"B2","skill":"vocabulary","subskill":"vocabulary.productive","tags":["workplace"]},
      {"word":"follow up","partOfSpeech":"phrasal verb","definition":"To do something additional after an earlier action, to check progress or continue it.","cefrLevel":"B2","skill":"vocabulary","subskill":"vocabulary.phrasal_verbs","tags":["workplace"]}
    ]
    """;

    private const string GrammarJson = """
    [
      {"grammarKey":"Present Simple for habits","explanation":"Use the present simple to talk about habits, routines, and facts that are generally true. Add -s/-es for he/she/it. Example: She works in an office. Water boils at 100 degrees.","cefrLevel":"A1","skill":"grammar","subskill":"grammar.tense_aspect","tags":["everyday"]},
      {"grammarKey":"Present Continuous for now","explanation":"Use the present continuous (am/is/are + verb-ing) for actions happening right now or temporary situations. Example: I am reading a book at the moment.","cefrLevel":"A1","skill":"grammar","subskill":"grammar.tense_aspect","tags":["everyday"]},
      {"grammarKey":"Articles a/an/the","explanation":"Use 'a' or 'an' before a singular countable noun mentioned for the first time. Use 'the' when both speakers know which specific thing is meant. Example: I saw a dog. The dog was brown.","cefrLevel":"A1","skill":"grammar","subskill":"grammar.articles_determiners","tags":["everyday"]},
      {"grammarKey":"Yes/No questions with do/does","explanation":"Form yes/no questions in the present simple with Do/Does + subject + base verb. Example: Do you like coffee? Does she work here?","cefrLevel":"A1","skill":"grammar","subskill":"grammar.question_forms","tags":["everyday"]},
      {"grammarKey":"Prepositions of place","explanation":"Use in, on, and at to describe location. Example: The keys are in the drawer. The book is on the table. She is at the station.","cefrLevel":"A1","skill":"grammar","subskill":"grammar.prepositions","tags":["everyday"]},
      {"grammarKey":"Past Simple for finished actions","explanation":"Use the past simple to describe actions that started and finished at a specific time in the past. Regular verbs add -ed; many common verbs are irregular. Example: We visited the museum last week.","cefrLevel":"A2","skill":"grammar","subskill":"grammar.tense_aspect","tags":["everyday"]},
      {"grammarKey":"Word order in statements","explanation":"English statements normally follow Subject + Verb + Object order. Example: She sent the report yesterday, not 'Yesterday sent she the report.'","cefrLevel":"A2","skill":"grammar","subskill":"grammar.word_order","tags":["workplace"]},
      {"grammarKey":"Wh- questions","explanation":"Form information questions with a question word (who, what, where, when, why, how) followed by an auxiliary verb and the subject. Example: Where did you put the file?","cefrLevel":"A2","skill":"grammar","subskill":"grammar.question_forms","tags":["workplace"]},
      {"grammarKey":"Prepositions of time","explanation":"Use in for months/years, on for days/dates, and at for clock times. Example: The meeting is at 3pm on Monday in March.","cefrLevel":"A2","skill":"grammar","subskill":"grammar.prepositions","tags":["workplace"]},
      {"grammarKey":"Present Perfect for experience","explanation":"Use the present perfect (have/has + past participle) to talk about experiences or changes without a specific finished time. Example: I have visited three countries this year.","cefrLevel":"B1","skill":"grammar","subskill":"grammar.tense_aspect","tags":["everyday"]},
      {"grammarKey":"Determiners: some, any, much, many","explanation":"Use 'some' in positive statements, 'any' in negatives and questions, 'many' with countable nouns, and 'much' with uncountable nouns. Example: There isn't much time, so we don't have many options.","cefrLevel":"B1","skill":"grammar","subskill":"grammar.articles_determiners","tags":["workplace"]},
      {"grammarKey":"Indirect questions","explanation":"Embed a question inside a polite statement using normal word order, not question order. Example: Could you tell me where the meeting room is? (not 'where is the meeting room').","cefrLevel":"B2","skill":"grammar","subskill":"grammar.word_order","tags":["workplace"]}
    ]
    """;

    private const string ReadingJson = """
    [
      {"title":"A Morning Routine","passage":"Every morning, Mia wakes up at seven o'clock. She drinks a glass of water, gets dressed, and walks to the small cafe near her house for breakfast before work.","cefrLevel":"A1","skill":"reading","subskill":"reading.gist","textType":"personal narrative","tags":["everyday"]},
      {"title":"The Office Notice","passage":"Notice: The third-floor printer will be out of service on Friday for repairs. Please use the printer on the second floor instead. We apologize for any inconvenience this may cause.","cefrLevel":"A2","skill":"reading","subskill":"reading.detail","textType":"workplace notice","tags":["workplace"]},
      {"title":"Weekend Plans","passage":"Hi Sam, are you free this Saturday? I was thinking we could go hiking in the morning and then get lunch afterwards. Let me know if that works for you. Talk soon, Alex","cefrLevel":"A2","skill":"reading","subskill":"reading.gist","textType":"email","tags":["everyday"]},
      {"title":"Meeting Minutes Excerpt","passage":"Attendees agreed to postpone the budget review until next month. Action items: Tom will update the spreadsheet, and Lena will schedule the follow-up call for the first week of next month.","cefrLevel":"A2","skill":"reading","subskill":"reading.scanning","textType":"meeting minutes","tags":["workplace"]},
      {"title":"New Team Member","passage":"We are pleased to welcome Priya to the marketing team. She previously worked at a design agency and brings five years of experience in brand strategy. Please introduce yourselves when you see her around the office.","cefrLevel":"B1","skill":"reading","subskill":"reading.inference","textType":"announcement","tags":["workplace"]},
      {"title":"Coffee Shop Review","passage":"The new cafe downtown has a cozy atmosphere and friendly staff. Although the prices are slightly higher than average, the quality of the coffee more than makes up for it. I would recommend trying their pastries too.","cefrLevel":"B1","skill":"reading","subskill":"reading.vocabulary_in_context","textType":"review","tags":["everyday"]},
      {"title":"Remote Work Guidelines","passage":"Employees working remotely should be reachable during core hours, from ten in the morning until four in the afternoon. Any exceptions must be approved by a manager in advance and communicated to the team.","cefrLevel":"B1","skill":"reading","subskill":"reading.scanning","textType":"policy excerpt","tags":["workplace"]},
      {"title":"Project Deadline Update","passage":"Due to unforeseen delays in the supplier's delivery schedule, the project timeline has shifted by two weeks. The team should adjust internal milestones accordingly, though the final client presentation date remains unchanged.","cefrLevel":"B2","skill":"reading","subskill":"reading.inference","textType":"workplace memo","tags":["workplace"]},
      {"title":"A Difficult Decision","passage":"After weeks of deliberation, Daniel finally decided to accept the job offer abroad, even though it meant leaving his close-knit group of friends behind. He reasoned the opportunity was simply too valuable to pass up.","cefrLevel":"B2","skill":"reading","subskill":"reading.gist","textType":"short narrative","tags":["everyday"]},
      {"title":"Customer Complaint Response","passage":"Thank you for bringing this issue to our attention. We sincerely apologize for the delay in your order and have arranged a full refund, along with a discount code for your next purchase as a gesture of goodwill.","cefrLevel":"B2","skill":"reading","subskill":"reading.detail","textType":"business correspondence","tags":["workplace"]}
    ]
    """;
}

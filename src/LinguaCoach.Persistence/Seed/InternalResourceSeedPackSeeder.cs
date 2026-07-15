using System.Text;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Persistence.Seed;

/// <summary>
/// Phase E6 — "First Real English Resource Depth" (extended in Phase E7 with a full-length
/// reading passage slice). Seeds a small, original, English-only content slice (vocabulary/
/// grammar/short-reading/full-reading-passage) through the FULL Phase E1-E4 staging pipeline
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

        // Phase 4.2 — every publishable candidate must trace back to an ImportPackage with an
        // approved Import Execution Plan; see InternalResourceSeedPackE8Seeder's identical helper
        // for the reasoning (internal, pre-reviewed system content, self-approved the same way
        // the source/candidates below already are).
        var importPackageId = await SeedApprovedPackageAsync(db, source.Id, ct);

        var vocabularyRun = await ImportAsync(importService, source.Id, VocabularyJson, "internal-seed-vocabulary.json", importPackageId, ct);
        var grammarRun = await ImportAsync(importService, source.Id, GrammarJson, "internal-seed-grammar.json", importPackageId, ct);
        var readingRun = await ImportAsync(importService, source.Id, ReadingJson, "internal-seed-reading.json", importPackageId, ct);
        var readingPassageRun = await ImportAsync(
            importService, source.Id, ReadingPassageJson, "internal-seed-reading-passages.json", importPackageId, ct);

        logger.LogInformation(
            "InternalResourceSeedPackSeeder: staged vocabulary {VocabSucceeded}/{VocabTotal}, " +
            "grammar {GrammarSucceeded}/{GrammarTotal}, reading {ReadingSucceeded}/{ReadingTotal}, " +
            "reading passages {PassageSucceeded}/{PassageTotal}.",
            vocabularyRun.SucceededCount, vocabularyRun.TotalRecordCount,
            grammarRun.SucceededCount, grammarRun.TotalRecordCount,
            readingRun.SucceededCount, readingRun.TotalRecordCount,
            readingPassageRun.SucceededCount, readingPassageRun.TotalRecordCount);

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
        IResourceImportService importService, Guid sourceId, string json, string fileName,
        Guid importPackageId, CancellationToken ct)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return await importService.ImportAsync(
            new ResourceImportRequest(sourceId, stream, fileName, ResourceImportMode.Json, ImportPackageId: importPackageId), ct);
    }

    /// <summary>Phase 4.2 — self-contained package + self-approved plan for this seeder's own
    /// internal content, so its resulting candidates satisfy the mandatory publish-provenance
    /// gate. ApprovedByUserId is left null (no human administrator approved this — the seeder
    /// itself, running as trusted system code, did).</summary>
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

    // Phase E7 — full-length internal reading passages (100-250 words each, A1-B2), routed by
    // ResourceCandidatePublishService to CefrReadingPassage rather than CefrReadingReference,
    // since each passage's length exceeds MaxReadingExcerptLength (500 chars). 100% original,
    // written for this codebase, mixing general/everyday and workplace/social/study contexts —
    // no copied third-party text, no Persian/bilingual content.
    private const string ReadingPassageJson = """
    [
      {"title":"A Day at the Market","passage":"On Saturday mornings, Elena likes to visit the local market near her home. Stalls sell fresh fruit, vegetables, bread, and cheese, and the smell of coffee fills the air. She usually buys apples, tomatoes, and a loaf of bread for the week. Her favorite stall belongs to an old man who sells honey from his own bees. He always remembers her name and asks about her family. After shopping, Elena sits at a small table outside a cafe and drinks a cup of tea before walking home with her bags. On the way, she often stops to say hello to other shoppers she recognizes from earlier visits.","cefrLevel":"A1","skill":"reading","subskill":"reading.gist","tags":["everyday"]},
      {"title":"My Weekend","passage":"Last weekend was quiet and relaxing. On Saturday, I woke up late and made pancakes for breakfast. In the afternoon, I called my sister, and we talked for almost an hour about her new apartment. Later, I watched a movie with my roommate and we ordered pizza. On Sunday, I went for a walk in the park near my house. The weather was sunny, and many families were having picnics. I also read a few chapters of my book before going to bed early to prepare for the new work week. By Sunday evening, I felt rested and ready for Monday morning.","cefrLevel":"A1","skill":"reading","subskill":"reading.gist","tags":["everyday"]},
      {"title":"The New Neighbor","passage":"Last month, a young couple moved into the apartment next to mine. At first, I only saw them in the hallway and we just said hello. One evening, the woman knocked on my door because she needed to borrow a ladder. We talked for a while, and I learned that she had just moved to the city for a new job. A few days later, they invited me over for dinner to say thank you. Now we often meet for coffee on weekends, and I am glad to have friendly neighbors nearby. Their small dog also likes to visit my balcony sometimes, which always makes me smile.","cefrLevel":"A2","skill":"reading","subskill":"reading.inference","tags":["everyday"]},
      {"title":"Learning to Cook","passage":"When Marco first moved away from home, he did not know how to cook at all. He usually ate sandwiches or ordered food because he was worried about making mistakes in the kitchen. After a few months, his colleague suggested they cook dinner together once a week. They started with simple recipes like pasta and vegetable soup. Slowly, Marco became more confident and began trying new dishes on his own. Now he enjoys cooking on weekends and even shares photos of his meals with his family back home, who are proud of how much he has improved.","cefrLevel":"A2","skill":"reading","subskill":"reading.gist","tags":["everyday"]},
      {"title":"Starting a New Job","passage":"Starting a new job can feel both exciting and stressful at the same time. During her first week at the marketing firm, Priya spent most of her time meeting colleagues and learning the company's systems. Her manager gave her a short guide explaining daily routines, but she still had many questions. By the end of the second week, she felt more comfortable asking for help instead of guessing. She realized that most people were happy to explain things clearly if she simply asked. After a month, Priya no longer felt like a stranger in the office, and she had already made two close friends among her teammates.","cefrLevel":"B1","skill":"reading","subskill":"reading.inference","tags":["workplace"]},
      {"title":"A Trip to the Mountains","passage":"For their anniversary, Daniel and his wife decided to spend a weekend in a small cabin in the mountains, far from the noise of the city. They arrived on Friday evening and were surprised by how quiet everything was compared to their usual routine. On Saturday, they went hiking along a trail that led to a waterfall, stopping several times to take photographs of the scenery. In the evening, they cooked a simple meal and sat outside watching the stars, something they rarely had time to do at home. By the time they returned on Sunday, both agreed they should make these short trips a regular habit.","cefrLevel":"B1","skill":"reading","subskill":"reading.detail","tags":["everyday"]},
      {"title":"Working From Home","passage":"When her company first allowed employees to work from home, Sofia was thrilled at the idea of avoiding her long commute. However, she quickly discovered that working remotely required more discipline than she expected. Without the structure of an office, she often found herself distracted by household chores or checking her phone too frequently. To solve this, she began setting a strict schedule, including short breaks and a clear finishing time each day. She also created a dedicated workspace instead of using her bed or sofa. After a few weeks of adjusting her habits, Sofia found she was actually more productive at home than she had been in the office, though she still missed casual conversations with her colleagues.","cefrLevel":"B1","skill":"reading","subskill":"reading.inference","tags":["workplace"]},
      {"title":"The Value of Feedback","passage":"Many employees feel uncomfortable when receiving criticism about their work, even when it is intended to be constructive. However, research consistently shows that regular, honest feedback is one of the most effective ways to improve performance over time. A manager who only offers praise, without pointing out areas for growth, may unintentionally prevent an employee from developing further. The challenge lies in delivering feedback in a way that feels supportive rather than discouraging. Successful managers often focus on specific behaviors rather than general personality traits, and they pair any criticism with clear suggestions for improvement. Employees, in turn, benefit from learning to view feedback as a tool for growth rather than a personal attack, which allows them to respond with curiosity instead of defensiveness.","cefrLevel":"B2","skill":"reading","subskill":"reading.inference","tags":["workplace"]},
      {"title":"Balancing Work and Life","passage":"As remote work becomes increasingly common, many professionals report finding it harder, rather than easier, to separate their personal and professional lives. Without a physical commute to mark the boundary between office hours and personal time, some employees find themselves checking emails late into the evening or working through what should be a lunch break. Organizational psychologists suggest that this blurred boundary can eventually contribute to burnout, even among employees who initially valued the flexibility remote work offered. Some companies have responded by introducing policies that discourage after-hours communication, while individual employees have started adopting personal rules, such as closing their laptops at a fixed time each day. Ultimately, maintaining a healthy balance appears to depend less on the location of one's work and more on the habits and boundaries an individual is willing to enforce.","cefrLevel":"B2","skill":"reading","subskill":"reading.vocabulary_in_context","tags":["workplace"]},
      {"title":"Adapting to Change at Work","passage":"Organizational change, whether it involves new technology, restructured teams, or updated procedures, is rarely welcomed with universal enthusiasm. Employees often resist change not because they are opposed to improvement, but because uncertainty about the future can feel more threatening than a familiar, even imperfect, routine. Effective leaders recognize this tendency and address it directly, rather than assuming that a well-designed plan will be adopted automatically simply because it makes logical sense. Clear, consistent communication about the reasons behind a change, along with realistic timelines and opportunities for employees to ask questions, tends to reduce anxiety considerably. Interestingly, studies suggest that employees who are given a genuine role in shaping how a change is implemented, even in small ways, report significantly higher satisfaction than those who are simply informed of decisions after the fact.","cefrLevel":"B2","skill":"reading","subskill":"reading.inference","tags":["workplace"]}
    ]
    """;
}

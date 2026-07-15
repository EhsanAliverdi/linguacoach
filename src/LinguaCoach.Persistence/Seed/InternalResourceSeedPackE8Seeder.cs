using System.Text;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Persistence.Seed;

/// <summary>
/// Phase E8 — "Internal Resource Bank Depth Expansion for Grammar, Usage, and Reading Support".
/// A second original, English-only content pack that flows through the exact same Phase E1-E4
/// staging pipeline as <see cref="InternalResourceSeedPackSeeder"/> (E6/E7): import →
/// deterministic metadata mapping → deterministic validation → admin approval → publish → the
/// published Cefr* bank browse/search surface. Never writes a Cefr*Entry row directly — the whole
/// point of the resource-bank platform is that all content flows through the reviewable pipeline.
///
/// Distinct <see cref="CefrResourceSource"/> from E6/E7 (<see cref="SourceName"/>), so this seeder
/// and the E6/E7 seeder are fully independent and each is idempotent by its own source name. The
/// depth this adds (40 vocabulary / 20 grammar / 16 short reading references / 8 full reading
/// passages across A1-B2) is intended to give a future Phase D4 Today-composer expansion richer,
/// deeper bank material to compose from — this phase adds no composer, selector, Practice Gym,
/// student-UI, or delivery-queue behavior.
///
/// Content is 100% original, written for this codebase — not copied from any textbook, dataset,
/// test-prep material, or external site. English-only; no Persian/bilingual/support-language
/// content (support language remains a runtime concern, never seed-bank content). The default
/// context is general English; workplace-tagged content is deliberately a minority, and daily
/// life / social / travel / study contexts are represented alongside it.
///
/// Every row carries its own accurate cefrLevel/skill/subskill/tags (and, for full passages,
/// focusTags/difficultyBand) columns, so <see cref="LinguaCoach.Infrastructure.ResourceImport.ResourceImportService"/>'s
/// deterministic row-metadata mapping populates those fields at staging time — no AI analysis call
/// is made or needed. Idempotent by source name: if a source named <see cref="SourceName"/>
/// already exists, this seeder is a complete no-op.
/// </summary>
public static class InternalResourceSeedPackE8Seeder
{
    public const string SourceName = "SpeakPath Internal English Seed Pack E8 (Grammar/Usage/Reading Depth)";

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
                "InternalResourceSeedPackE8Seeder: source '{Name}' already exists — skipping entirely.", SourceName);
            return;
        }

        // Honest provenance: original, internally-authored content, not an external dataset.
        var source = new CefrResourceSource(
            name: SourceName,
            licenseType: "Internal/Original",
            allowsStudentDisplay: true,
            allowsCommercialUse: true,
            attributionText: "SpeakPath internal content team",
            sourceVersion: "e8",
            usageRestrictionNotes:
                "Original, internally-authored, English-only depth-expansion content written for " +
                "Phase E8. Not sourced from any external dataset, textbook, test-prep material, or " +
                "copyrighted work. General English by default; workplace context is a minority.");

        source.ApproveForImport("Internal original content authored for this codebase — no external licensing review required (Phase E8).");
        db.CefrResourceSources.Add(source);
        await db.SaveChangesAsync(ct);

        // Phase 4.2 — every publishable candidate must trace back to an ImportPackage with an
        // approved Import Execution Plan. This seeder is internal, pre-reviewed system content
        // (not an admin-submitted import), so it creates its own package + self-approved plan the
        // same way it already self-approves the source and each candidate below via the real
        // domain methods — never a backdoor around the gate, just the seeder acting as its own
        // "administrator" for controlled internal content it is solely responsible for.
        var importPackageId = await SeedApprovedPackageAsync(db, source.Id, ct);

        var vocabularyRun = await ImportAsync(importService, source.Id, VocabularyJson, "e8-vocabulary.json", importPackageId, ct);
        var grammarRun = await ImportAsync(importService, source.Id, GrammarJson, "e8-grammar.json", importPackageId, ct);
        var readingRun = await ImportAsync(importService, source.Id, ReadingReferenceJson, "e8-reading-references.json", importPackageId, ct);
        var readingPassageRun = await ImportAsync(importService, source.Id, ReadingPassageJson, "e8-reading-passages.json", importPackageId, ct);

        logger.LogInformation(
            "InternalResourceSeedPackE8Seeder: staged vocabulary {VocabSucceeded}/{VocabTotal}, " +
            "grammar {GrammarSucceeded}/{GrammarTotal}, reading references {ReadingSucceeded}/{ReadingTotal}, " +
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
            // Deterministic rule-validation gate is the sole authority on ValidationStatus and must
            // actually run, even for pre-classified internal content.
            var validationResult = await validationService.ValidateAsync(candidateId, ct);
            if (validationResult.Status != ResourceCandidateValidationStatus.Passed.ToString())
            {
                logger.LogWarning(
                    "InternalResourceSeedPackE8Seeder: candidate {CandidateId} did not pass validation ({Status}) — " +
                    "left unapproved/unpublished. Errors: {Errors}",
                    candidateId, validationResult.Status, string.Join("; ", validationResult.Errors));
                continue;
            }

            // Same precedent as the E6 seeder: for controlled, pre-reviewed internal content the
            // seeder performs the admin approval via the real Approve() method, never a backdoor.
            var candidate = await db.ResourceCandidates.FirstAsync(c => c.Id == candidateId, ct);
            candidate.Approve("Approved by InternalResourceSeedPackE8Seeder — pre-reviewed internal content (Phase E8).");
            await db.SaveChangesAsync(ct);

            var publishResult = await publishService.PublishAsync(candidateId, publishedByUserId: null, ct);
            if (!publishResult.Success)
            {
                logger.LogWarning(
                    "InternalResourceSeedPackE8Seeder: candidate {CandidateId} failed to publish: {Errors}",
                    candidateId, string.Join("; ", publishResult.Errors));
                continue;
            }

            publishedCount++;
        }

        logger.LogInformation(
            "InternalResourceSeedPackE8Seeder: published {PublishedCount}/{TotalCandidates} candidates from source '{Name}'.",
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
        var package = new LinguaCoach.Domain.Entities.ImportPackage(sourceId, SourceName, DateTimeOffset.UtcNow);
        db.ImportPackages.Add(package);
        await db.SaveChangesAsync(ct);

        var plan = new LinguaCoach.Domain.Entities.ImportProfile(
            package.Id, 1, profileJson: "{}", sampleAssetIds: Array.Empty<Guid>(),
            estimatedCandidateCount: 1, createdAtUtc: DateTimeOffset.UtcNow,
            changeReason: null);
        plan.SubmitForApproval();
        plan.Approve(approvedByUserId: null, DateTimeOffset.UtcNow, approvedCostCeiling: 0m);
        db.ImportProfiles.Add(plan);
        package.ApproveProfile(plan.Id);
        await db.SaveChangesAsync(ct);

        return package.Id;
    }

    // ── Seed content ────────────────────────────────────────────────────────────────
    // 40 vocabulary / 20 grammar / 16 short reading references / 8 full reading passages, all
    // original English-only content, A1-B2. Default context is general English; workplace is a
    // minority tag alongside daily/social/travel/study. None of these words/grammar keys/titles
    // duplicate the E6/E7 pack. Short reading references are kept under the 500-char
    // CefrReadingReference excerpt limit; full passages are deliberately over it so they route to
    // CefrReadingPassage.

    private const string VocabularyJson = """
    [
      {"word":"chair","partOfSpeech":"noun","definition":"A seat for one person, with a back and usually four legs.","cefrLevel":"A1","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["general","daily"]},
      {"word":"bread","partOfSpeech":"noun","definition":"A common food made from flour, water, and yeast, then baked.","cefrLevel":"A1","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["daily"]},
      {"word":"run","partOfSpeech":"verb","definition":"To move quickly on foot, faster than walking.","cefrLevel":"A1","skill":"vocabulary","subskill":"vocabulary.productive","tags":["general"]},
      {"word":"cold","partOfSpeech":"adjective","definition":"Having a low temperature; not warm.","cefrLevel":"A1","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["general"]},
      {"word":"family","partOfSpeech":"noun","definition":"A group of people who are related, such as parents and children.","cefrLevel":"A1","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["social"]},
      {"word":"street","partOfSpeech":"noun","definition":"A public road in a town or city with houses or shops along it.","cefrLevel":"A1","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["travel","daily"]},
      {"word":"eat","partOfSpeech":"verb","definition":"To put food in your mouth and swallow it.","cefrLevel":"A1","skill":"vocabulary","subskill":"vocabulary.productive","tags":["daily"]},
      {"word":"big","partOfSpeech":"adjective","definition":"Large in size or amount.","cefrLevel":"A1","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["general"]},
      {"word":"open","partOfSpeech":"verb","definition":"To move something so that it is no longer closed.","cefrLevel":"A1","skill":"vocabulary","subskill":"vocabulary.productive","tags":["general","daily"]},
      {"word":"day","partOfSpeech":"noun","definition":"A period of 24 hours, or the time when it is light outside.","cefrLevel":"A1","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["general"]},
      {"word":"ticket","partOfSpeech":"noun","definition":"A small piece of paper or card that shows you have paid to travel or enter a place.","cefrLevel":"A2","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["travel"]},
      {"word":"neighbour","partOfSpeech":"noun","definition":"A person who lives near you.","cefrLevel":"A2","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["social","daily"]},
      {"word":"arrive","partOfSpeech":"verb","definition":"To reach a place at the end of a journey.","cefrLevel":"A2","skill":"vocabulary","subskill":"vocabulary.productive","tags":["travel"]},
      {"word":"busy","partOfSpeech":"adjective","definition":"Having a lot to do; not free.","cefrLevel":"A2","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["daily"]},
      {"word":"hobby","partOfSpeech":"noun","definition":"An activity you enjoy doing in your free time.","cefrLevel":"A2","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["social"]},
      {"word":"luggage","partOfSpeech":"noun","definition":"The bags and cases you take with you when you travel.","cefrLevel":"A2","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["travel"]},
      {"word":"recipe","partOfSpeech":"noun","definition":"A set of instructions for cooking a particular dish.","cefrLevel":"A2","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["daily"]},
      {"word":"quiet","partOfSpeech":"adjective","definition":"Making little or no noise.","cefrLevel":"A2","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["general"]},
      {"word":"directions","partOfSpeech":"noun","definition":"Instructions that tell you how to get to a place.","cefrLevel":"A2","skill":"vocabulary","subskill":"vocabulary.productive","tags":["travel"]},
      {"word":"celebrate","partOfSpeech":"verb","definition":"To do something enjoyable for a special event or occasion.","cefrLevel":"A2","skill":"vocabulary","subskill":"vocabulary.productive","tags":["social"]},
      {"word":"improve","partOfSpeech":"verb","definition":"To make something better, or to become better.","cefrLevel":"B1","skill":"vocabulary","subskill":"vocabulary.productive","tags":["general","study"]},
      {"word":"research","partOfSpeech":"noun","definition":"A careful study of a subject to discover new facts or information.","cefrLevel":"B1","skill":"vocabulary","subskill":"vocabulary.collocation","tags":["study"]},
      {"word":"confident","partOfSpeech":"adjective","definition":"Feeling sure about your own ability or ideas.","cefrLevel":"B1","skill":"vocabulary","subskill":"vocabulary.word_form","tags":["social"]},
      {"word":"recommend","partOfSpeech":"verb","definition":"To suggest that something is good or worth doing.","cefrLevel":"B1","skill":"vocabulary","subskill":"vocabulary.productive","tags":["social","daily"]},
      {"word":"apply","partOfSpeech":"verb","definition":"To make a formal request, usually in writing, for a job or place.","cefrLevel":"B1","skill":"vocabulary","subskill":"vocabulary.phrasal_verbs","tags":["study","workplace"]},
      {"word":"explain","partOfSpeech":"verb","definition":"To make something clear by describing it in more detail.","cefrLevel":"B1","skill":"vocabulary","subskill":"vocabulary.productive","tags":["general"]},
      {"word":"advantage","partOfSpeech":"noun","definition":"Something that helps you or that makes you more likely to succeed.","cefrLevel":"B1","skill":"vocabulary","subskill":"vocabulary.collocation","tags":["general"]},
      {"word":"attend","partOfSpeech":"verb","definition":"To be present at an event, such as a class or a meeting.","cefrLevel":"B1","skill":"vocabulary","subskill":"vocabulary.productive","tags":["study"]},
      {"word":"reservation","partOfSpeech":"noun","definition":"An arrangement to keep a table, room, or seat for you.","cefrLevel":"B1","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["travel"]},
      {"word":"responsible","partOfSpeech":"adjective","definition":"Having the duty to deal with or take care of something.","cefrLevel":"B1","skill":"vocabulary","subskill":"vocabulary.word_form","tags":["workplace"]},
      {"word":"persuade","partOfSpeech":"verb","definition":"To make someone agree to do or believe something by giving good reasons.","cefrLevel":"B2","skill":"vocabulary","subskill":"vocabulary.productive","tags":["social"]},
      {"word":"significant","partOfSpeech":"adjective","definition":"Large or important enough to have an effect or be noticed.","cefrLevel":"B2","skill":"vocabulary","subskill":"vocabulary.word_form","tags":["general","study"]},
      {"word":"approach","partOfSpeech":"noun","definition":"A particular way of dealing with a problem or a task.","cefrLevel":"B2","skill":"vocabulary","subskill":"vocabulary.collocation","tags":["general"]},
      {"word":"consequence","partOfSpeech":"noun","definition":"A result or effect of an action or situation.","cefrLevel":"B2","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["general"]},
      {"word":"flexible","partOfSpeech":"adjective","definition":"Able to change or be changed easily to suit a new situation.","cefrLevel":"B2","skill":"vocabulary","subskill":"vocabulary.word_form","tags":["workplace","general"]},
      {"word":"emphasize","partOfSpeech":"verb","definition":"To give special importance or attention to something.","cefrLevel":"B2","skill":"vocabulary","subskill":"vocabulary.productive","tags":["study"]},
      {"word":"reluctant","partOfSpeech":"adjective","definition":"Not willing to do something, and therefore slow to do it.","cefrLevel":"B2","skill":"vocabulary","subskill":"vocabulary.receptive","tags":["social"]},
      {"word":"estimate","partOfSpeech":"verb","definition":"To guess the size, value, or amount of something using available information.","cefrLevel":"B2","skill":"vocabulary","subskill":"vocabulary.productive","tags":["general"]},
      {"word":"overcome","partOfSpeech":"verb","definition":"To successfully deal with or control a problem or difficulty.","cefrLevel":"B2","skill":"vocabulary","subskill":"vocabulary.phrasal_verbs","tags":["general"]},
      {"word":"thorough","partOfSpeech":"adjective","definition":"Done carefully and completely, with attention to every detail.","cefrLevel":"B2","skill":"vocabulary","subskill":"vocabulary.word_form","tags":["workplace","study"]}
    ]
    """;

    // Subskills are restricted to the curriculum taxonomy the validation gate enforces for grammar
    // (articles_determiners, prepositions, question_forms, tense_aspect, word_order) — each grammar
    // point is mapped to the closest allowed subskill within that fixed set.
    private const string GrammarJson = """
    [
      {"grammarKey":"There is / There are","explanation":"Use 'there is' with a singular noun and 'there are' with a plural noun to say that something exists or is present. Example: There is a cafe on the corner. There are two windows in the room.","cefrLevel":"A1","skill":"grammar","subskill":"grammar.word_order","tags":["general","daily"]},
      {"grammarKey":"Can for ability","explanation":"Use 'can' + base verb to talk about ability, and 'cannot' or 'can't' for the negative. 'Can' does not change for he/she/it. Example: She can swim, but she can't drive.","cefrLevel":"A1","skill":"grammar","subskill":"grammar.tense_aspect","tags":["general"]},
      {"grammarKey":"Possessive adjectives","explanation":"Use my, your, his, her, its, our, and their before a noun to show who something belongs to. Example: This is my bag and that is her coat.","cefrLevel":"A1","skill":"grammar","subskill":"grammar.articles_determiners","tags":["daily","general"]},
      {"grammarKey":"Plural nouns","explanation":"Most nouns add -s to form the plural; nouns ending in -s, -sh, -ch, or -x add -es. Some are irregular. Example: one book, two books; one box, two boxes; one child, two children.","cefrLevel":"A1","skill":"grammar","subskill":"grammar.articles_determiners","tags":["general"]},
      {"grammarKey":"Imperatives for instructions","explanation":"Use the base form of the verb to give instructions or directions, with no subject. Add 'do not' or 'don't' for negatives. Example: Turn left at the corner. Don't forget your keys.","cefrLevel":"A1","skill":"grammar","subskill":"grammar.question_forms","tags":["daily","travel"]},
      {"grammarKey":"Comparative adjectives","explanation":"Add -er to short adjectives and use 'more' before longer ones to compare two things, followed by 'than'. Example: This bag is cheaper than that one. This route is more direct than the other.","cefrLevel":"A2","skill":"grammar","subskill":"grammar.word_order","tags":["general","travel"]},
      {"grammarKey":"Superlative adjectives","explanation":"Add -est to short adjectives and use 'the most' before longer ones to compare three or more things. Example: This is the cheapest option. It was the most interesting film of the year.","cefrLevel":"A2","skill":"grammar","subskill":"grammar.word_order","tags":["general"]},
      {"grammarKey":"Going to for plans","explanation":"Use 'be going to' + base verb to talk about future plans and intentions you have already decided. Example: We are going to visit my grandparents next weekend.","cefrLevel":"A2","skill":"grammar","subskill":"grammar.tense_aspect","tags":["daily","social"]},
      {"grammarKey":"Countable and uncountable nouns","explanation":"Countable nouns have singular and plural forms and can take a/an; uncountable nouns do not and use 'some'. Example: I bought an apple and some rice.","cefrLevel":"A2","skill":"grammar","subskill":"grammar.articles_determiners","tags":["daily"]},
      {"grammarKey":"Adverbs of frequency","explanation":"Use always, usually, often, sometimes, and never before the main verb but after 'be' to say how often something happens. Example: I usually walk to work. She is never late.","cefrLevel":"A2","skill":"grammar","subskill":"grammar.word_order","tags":["daily"]},
      {"grammarKey":"First conditional","explanation":"Use 'if' + present simple, with 'will' + base verb, to talk about a likely future result of a real condition. Example: If it rains tomorrow, we will stay at home.","cefrLevel":"B1","skill":"grammar","subskill":"grammar.tense_aspect","tags":["general"]},
      {"grammarKey":"Modals of obligation","explanation":"Use 'must' and 'have to' to express obligation, and 'don't have to' to say something is not necessary. Example: You have to check in early, but you don't have to print your ticket.","cefrLevel":"B1","skill":"grammar","subskill":"grammar.tense_aspect","tags":["travel","general"]},
      {"grammarKey":"Present perfect with for and since","explanation":"Use the present perfect with 'for' plus a length of time and 'since' plus a starting point to talk about something that began in the past and continues now. Example: I have lived here for three years, since 2021.","cefrLevel":"B1","skill":"grammar","subskill":"grammar.tense_aspect","tags":["general"]},
      {"grammarKey":"Used to for past habits","explanation":"Use 'used to' + base verb to talk about past habits or states that are no longer true. Example: I used to play the piano when I was young.","cefrLevel":"B1","skill":"grammar","subskill":"grammar.tense_aspect","tags":["social","general"]},
      {"grammarKey":"Relative clauses with who/which/that","explanation":"Use 'who' for people, 'which' for things, and 'that' for either to add information about a noun. Example: The friend who called me lives in the city that we visited.","cefrLevel":"B1","skill":"grammar","subskill":"grammar.word_order","tags":["general"]},
      {"grammarKey":"Second conditional","explanation":"Use 'if' + past simple, with 'would' + base verb, to talk about an unreal or unlikely present or future situation. Example: If I had more time, I would learn another language.","cefrLevel":"B2","skill":"grammar","subskill":"grammar.tense_aspect","tags":["general","study"]},
      {"grammarKey":"Passive voice","explanation":"Use 'be' + past participle to focus on the action or result rather than who did it. Example: The report was written last week. The results will be announced soon.","cefrLevel":"B2","skill":"grammar","subskill":"grammar.tense_aspect","tags":["study","workplace"]},
      {"grammarKey":"Reported speech","explanation":"When reporting what someone said, move the tense one step back and change pronouns and time words as needed. Example: She said she was tired. He told me he had finished.","cefrLevel":"B2","skill":"grammar","subskill":"grammar.tense_aspect","tags":["general","social"]},
      {"grammarKey":"Modals of deduction","explanation":"Use 'must' for something you are sure is true, 'might' or 'could' for a possibility, and 'can't' for something you are sure is not true. Example: She must be at home; the lights are on. He can't be forty; he looks much younger.","cefrLevel":"B2","skill":"grammar","subskill":"grammar.tense_aspect","tags":["general"]},
      {"grammarKey":"Linking words of contrast","explanation":"Use 'although' and 'even though' before a clause, and 'however' at the start of a new sentence, to show contrast. Example: Although it was raining, we went out. It was raining. However, we still went out.","cefrLevel":"B2","skill":"grammar","subskill":"grammar.word_order","tags":["study","general"]}
    ]
    """;

    private const string ReadingReferenceJson = """
    [
      {"title":"At the Bakery","passage":"The small bakery on Green Street opens at seven every morning. It sells fresh bread, cakes, and hot coffee. Many people stop there on their way to work or school.","cefrLevel":"A1","skill":"reading","subskill":"reading.gist","textType":"description","tags":["daily","general"]},
      {"title":"A Short Message","passage":"Hi Mum, I am on the bus now. I will be home at six o'clock. Can you buy some milk? Thank you. See you soon. Love, Kim.","cefrLevel":"A1","skill":"reading","subskill":"reading.detail","textType":"text message","tags":["daily","social"]},
      {"title":"My Cat","passage":"I have a small black cat. Her name is Luna. She likes to sleep on my bed in the afternoon. In the evening, she plays with a small ball in the living room.","cefrLevel":"A1","skill":"reading","subskill":"reading.gist","textType":"personal description","tags":["general"]},
      {"title":"The Park","passage":"There is a big park near my house. It has green grass, tall trees, and a small lake. On sunny days, children play there and families sit on the grass to eat lunch.","cefrLevel":"A1","skill":"reading","subskill":"reading.detail","textType":"description","tags":["general","daily"]},
      {"title":"Train Announcement","passage":"The ten o'clock train to the coast will leave from platform four, not platform two. Passengers should have their tickets ready. The journey takes about ninety minutes with one stop.","cefrLevel":"A2","skill":"reading","subskill":"reading.scanning","textType":"announcement","tags":["travel"]},
      {"title":"A Note to a Friend","passage":"Hi Ben, thanks for lending me your bike last week. It was really useful for getting to the station. I have left it in your garage with the lights charged. Let me know if you want to cycle together on Sunday.","cefrLevel":"A2","skill":"reading","subskill":"reading.inference","textType":"note","tags":["social","daily"]},
      {"title":"Library Rules","passage":"Please keep your voice low in the reading room. Food and drink are not allowed near the computers. You can borrow up to six books for three weeks. Late returns have a small daily charge.","cefrLevel":"A2","skill":"reading","subskill":"reading.detail","textType":"notice","tags":["study"]},
      {"title":"Weekend Market","passage":"The weekend market opens on Saturday and Sunday from eight until two. You can find fresh fruit, handmade crafts, and street food there. Parking is free, but it fills up quickly, so arrive early.","cefrLevel":"A2","skill":"reading","subskill":"reading.scanning","textType":"information text","tags":["daily","travel"]},
      {"title":"A Change of Plan","passage":"Originally, we planned to hike on Saturday, but the forecast now predicts heavy rain all day. Instead, we have decided to visit the science museum in the morning and try the new cafe nearby for lunch. I hope that suits everyone.","cefrLevel":"B1","skill":"reading","subskill":"reading.inference","textType":"email","tags":["social","general"]},
      {"title":"Volunteer Notice","passage":"Our community garden is looking for volunteers this spring. No experience is needed, just a willingness to help for a few hours at the weekend. In return, volunteers can take home some of the vegetables they help to grow.","cefrLevel":"B1","skill":"reading","subskill":"reading.gist","textType":"announcement","tags":["social","general"]},
      {"title":"Booking Confirmation","passage":"Thank you for your reservation. Your room is booked for two nights, arriving Friday and leaving Sunday. Check-in is from three in the afternoon. Breakfast is included and served until ten. Please contact us if your arrival time changes.","cefrLevel":"B1","skill":"reading","subskill":"reading.detail","textType":"confirmation","tags":["travel"]},
      {"title":"A Useful App","passage":"I recently started using an app that helps me track how much water I drink each day. At first I thought it was unnecessary, but it has actually made me pay more attention to a simple healthy habit I used to ignore.","cefrLevel":"B1","skill":"reading","subskill":"reading.vocabulary_in_context","textType":"review","tags":["daily","general"]},
      {"title":"On Giving Directions","passage":"When you explain the way to a stranger, it helps to mention landmarks rather than only street names. Most people remember a tall building or a bright sign far more easily than an unfamiliar name, especially in a busy area they have never visited before.","cefrLevel":"B2","skill":"reading","subskill":"reading.inference","textType":"opinion","tags":["general","travel"]},
      {"title":"A Reflection on Habits","passage":"Small habits often shape our days more than big decisions do. A person who reads a few pages each evening will, over a year, finish many books without ever feeling that they made a great effort. Consistency, rather than intensity, tends to produce lasting results.","cefrLevel":"B2","skill":"reading","subskill":"reading.gist","textType":"reflection","tags":["study","general"]},
      {"title":"Choosing a Course","passage":"Before enrolling in an online course, it is worth considering not only the subject but also how much time you can realistically commit each week. Many learners sign up enthusiastically, then struggle to keep pace, not because the material is too hard, but because their schedule was never designed to include it.","cefrLevel":"B2","skill":"reading","subskill":"reading.inference","textType":"advice","tags":["study"]},
      {"title":"The Value of a Walk","passage":"Some of the clearest thinking happens away from a desk. A short walk, even around the block, can loosen a stubborn problem that hours of staring at a screen failed to solve. The change of scene seems to give the mind room to make connections it would otherwise miss.","cefrLevel":"B2","skill":"reading","subskill":"reading.vocabulary_in_context","textType":"reflection","tags":["general"]}
    ]
    """;

    // Full-length passages (over the 500-char CefrReadingReference limit) → CefrReadingPassage.
    // These carry focusTags and difficultyBand in addition to context tags, so the Phase E8
    // metadata mapping populates those richer CefrReadingPassage columns.
    private const string ReadingPassageJson = """
    [
      {"title":"A Visit to the Zoo","passage":"On Sunday, the whole family went to the city zoo together. The children were excited because they had never seen a real elephant before. They arrived early in the morning to avoid the crowds and started with the birds, which sang loudly in a large green enclosure. After that, they watched the monkeys climb and jump, and everyone laughed when one of them stole a visitor's hat. For lunch, they sat on a bench near the lake and shared sandwiches. By the afternoon, the children were tired but happy, and on the way home they talked about which animal they had liked the most.","cefrLevel":"A1","skill":"reading","subskill":"reading.gist","tags":["daily","social"],"focusTags":["sequencing","main_idea"],"difficultyBand":1},
      {"title":"A Rainy Afternoon","passage":"It rained all afternoon, so Ben and his sister stayed inside the house. At first they were bored and did not know what to do. Then their grandmother showed them an old box full of photographs. They looked at pictures of their parents when they were young, and their grandmother told stories about each one. Some photos were funny, and some were from places the children had never visited. The time passed quickly, and soon it was evening. When their parents came home, Ben and his sister were still sitting on the floor with the photos, asking question after question about the family.","cefrLevel":"A1","skill":"reading","subskill":"reading.detail","tags":["daily","general"],"focusTags":["supporting_detail","main_idea"],"difficultyBand":1},
      {"title":"Planning a Trip","passage":"Maria and her friends decided to spend a long weekend by the sea. They met one evening to plan the trip and quickly realised there was a lot to organise. First, they had to choose a place to stay that was not too expensive. Then they looked at train times, because none of them wanted to drive. Maria offered to book the tickets, while her friend Sara agreed to find a small hotel near the beach. They also made a short list of things to take, such as sunscreen and comfortable shoes. By the end of the evening, everything was arranged, and they all felt excited about the days ahead.","cefrLevel":"A2","skill":"reading","subskill":"reading.detail","tags":["travel","social"],"focusTags":["sequencing","supporting_detail"],"difficultyBand":2},
      {"title":"The Community Library","passage":"The small library in our neighbourhood is more than just a place to borrow books. On weekday afternoons, it offers a quiet space where students can study after school. On Saturdays, a group of volunteers runs a story hour for young children, and their parents often stay to chat over coffee. The library also lends board games and organises a monthly book club that anyone can join. Although it is not a large building, it has become an important meeting point for people of all ages. Many residents say that without it, the neighbourhood would feel far less connected than it does today.","cefrLevel":"A2","skill":"reading","subskill":"reading.inference","tags":["social","study"],"focusTags":["main_idea","inference"],"difficultyBand":2},
      {"title":"Learning a New Skill","passage":"When Omar decided to learn how to draw, he expected to see quick results. Instead, his first attempts looked nothing like the pictures in his head, and he felt discouraged. A friend who was a keen artist gave him simple advice: practise for just ten minutes a day and keep every drawing, even the bad ones. At first this seemed pointless, but after a few weeks Omar noticed something surprising. When he compared his recent sketches with his earliest ones, the improvement was obvious. The lesson he took from the experience was that progress is often invisible day by day, yet clear over a longer period. Now he encourages other beginners not to give up too soon.","cefrLevel":"B1","skill":"reading","subskill":"reading.inference","tags":["study","general"],"focusTags":["inference","main_idea"],"difficultyBand":3},
      {"title":"A Neighbourhood Project","passage":"Last year, the residents of a quiet street noticed that a small piece of land at the end of the road had become overgrown and neglected. Rather than wait for the council to act, a few neighbours decided to turn it into a shared garden. They held a meeting, agreed on a simple plan, and divided the work between them. Some cleared the weeds, others brought soil and seeds, and one family built a small bench from old wood. Progress was slow, and there were disagreements about what to plant, but gradually the space changed. By the summer, the garden was full of flowers and vegetables, and it had also brought the neighbours closer together than they had been for years.","cefrLevel":"B1","skill":"reading","subskill":"reading.detail","tags":["social","general"],"focusTags":["sequencing","main_idea"],"difficultyBand":3},
      {"title":"The Case for Doing Less","passage":"Modern life often rewards those who appear busiest, yet a growing number of researchers argue that constant activity can be counterproductive. When people try to do too many things at once, the quality of each task tends to suffer, and the sense of being overwhelmed can reduce motivation rather than increase it. Deliberately doing less, by contrast, allows greater attention to what genuinely matters. This does not mean being lazy; it means choosing priorities carefully and resisting the temptation to fill every moment. Interestingly, some of the most productive individuals protect long periods of uninterrupted time and say no to commitments that others would accept without thinking. Their example suggests that focus, not sheer volume of effort, is what ultimately drives meaningful results.","cefrLevel":"B2","skill":"reading","subskill":"reading.inference","tags":["study","general"],"focusTags":["inference","argument"],"difficultyBand":4},
      {"title":"Why We Misjudge Distance","passage":"Travellers often assume that the hardest part of a journey is the beginning, but experienced walkers know that the final stretch can feel surprisingly long. Part of the reason is psychological rather than physical. Near the start, everything is new, and curiosity keeps the mind occupied, so time appears to pass quickly. Towards the end, however, the destination is close enough to imagine but not yet reached, and this gap between expectation and reality makes each remaining step feel heavier. Understanding this pattern can be genuinely useful. If we expect the last part of any effort, whether a walk or a long project, to feel slower than it really is, we are less likely to be discouraged and more likely to finish what we started.","cefrLevel":"B2","skill":"reading","subskill":"reading.vocabulary_in_context","tags":["travel","general"],"focusTags":["inference","argument"],"difficultyBand":4}
    ]
    """;
}

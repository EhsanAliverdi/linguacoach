using System.Text;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Application.Speaking;
using LinguaCoach.Application.Storage;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Speaking;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Sprint 9 — "Internal Resource Bank Listening Depth". Fills a live-confirmed gap: zero Listening
/// resources existed in the bank at ANY CEFR level, which is why the already-working
/// highlight_correct_summary/select_missing_word composers (Phase K17) had never produced a single
/// exercise — not because they were broken, but because they had no Listening content to compose
/// from.
///
/// Unlike E6/E7/E8/C1C2 (text-only), a <see cref="ResourceCandidateType.ListeningPassage"/> cannot
/// publish without a real audio file attached (<see cref="ResourceCandidatePublishService"/>'s own
/// hard gate) — staging JSON alone is not enough. This seeder therefore does one thing none of the
/// other internal packs do: after staging each transcript through the normal Phase E1 pipeline, it
/// calls the real Gemini TTS provider directly (<see cref="GeminiTextToSpeechService"/>, the same
/// production code every other TTS caller in this codebase uses) to synthesize real audio, uploads
/// it to the same <see cref="IFileStorageService"/> abstraction the admin manual-upload flow uses,
/// and attaches it via the candidate's own <c>AttachAudio</c> method — never a synthetic/placeholder
/// audio reference. The API key is read directly from the admin-configured
/// <see cref="AiProviderCredential"/> row (provider "gemini") rather than environment variables,
/// since this dev environment has no GEMINI_API_KEY env var set but does have a real key configured
/// via the admin AI settings UI.
///
/// Content is 100% original, written for this codebase. English-only. Idempotent by source name.
/// </summary>
public static class InternalResourceSeedPackListeningSeeder
{
    public const string SourceName = "SpeakPath Internal English Seed Pack Listening (A1-C2 Depth)";

    public static async Task SeedAsync(
        LinguaCoachDbContext db,
        IResourceImportService importService,
        IResourceCandidateValidationService validationService,
        IResourceCandidatePublishService publishService,
        IResourceCandidateContentSerializer contentSerializer,
        IServiceProvider services,
        IFileStorageService storage,
        ILogger logger,
        CancellationToken ct = default)
    {
        // Called directly (bypassing TtsProviderResolver's feature-key routing, which currently
        // points tts.listening at the "fake" provider for dev/test safety) — this is a one-time,
        // explicitly-authorized real synthesis pass for internally-authored seed content, not a
        // runtime student-facing feature, so it always uses the real Gemini implementation.
        var geminiTts = services.GetRequiredService<GeminiTextToSpeechService>();
        var existing = await db.CefrResourceSources.FirstOrDefaultAsync(s => s.Name == SourceName, ct);
        if (existing is not null)
        {
            logger.LogInformation(
                "InternalResourceSeedPackListeningSeeder: source '{Name}' already exists — skipping entirely.", SourceName);
            return;
        }

        var credential = await db.AiProviderCredentials.AsNoTracking()
            .FirstOrDefaultAsync(c => c.ProviderName == "gemini", ct);
        if (string.IsNullOrWhiteSpace(credential?.ApiKey))
        {
            logger.LogWarning(
                "InternalResourceSeedPackListeningSeeder: no Gemini API key configured in ai_provider_credentials — " +
                "skipping entirely. Configure one via the admin AI settings page and restart to seed Listening content.");
            return;
        }

        var source = new CefrResourceSource(
            name: SourceName,
            licenseType: "Internal/Original",
            allowsStudentDisplay: true,
            allowsCommercialUse: true,
            attributionText: "SpeakPath internal content team",
            sourceVersion: "listening",
            usageRestrictionNotes:
                "Original, internally-authored, English-only Listening depth content written for " +
                "Sprint 9. Audio synthesized via Gemini TTS from this pack's own original transcripts — " +
                "not sourced from any external dataset, textbook, or copyrighted recording.");

        source.ApproveForImport("Internal original content authored for this codebase — no external licensing review required (Sprint 9).");
        db.CefrResourceSources.Add(source);
        await db.SaveChangesAsync(ct);

        var importPackageId = await SeedApprovedPackageAsync(db, source.Id, ct);

        var listeningRun = await ImportAsync(importService, source.Id, TranscriptJson, "listening-transcripts.json", importPackageId, ct);
        logger.LogInformation(
            "InternalResourceSeedPackListeningSeeder: staged listening {Succeeded}/{Total}.",
            listeningRun.SucceededCount, listeningRun.TotalRecordCount);

        var candidateIds = await (
            from c in db.ResourceCandidates
            join r in db.ResourceRawRecords on c.ResourceRawRecordId equals r.Id
            join run in db.ResourceImportRuns on r.ResourceImportRunId equals run.Id
            where run.CefrResourceSourceId == source.Id
            select c.Id)
            .ToListAsync(ct);

        var synthesizedCount = 0;
        var publishedCount = 0;
        foreach (var candidateId in candidateIds)
        {
            var candidate = await db.ResourceCandidates.FirstAsync(c => c.Id == candidateId, ct);

            var parseResult = contentSerializer.Parse(candidate.CandidateType, candidate.NormalizedJson, candidate.CanonicalText);
            var transcript = (parseResult.Content as ListeningCandidateContent)?.Transcript;
            if (string.IsNullOrWhiteSpace(transcript))
            {
                logger.LogWarning(
                    "InternalResourceSeedPackListeningSeeder: candidate {CandidateId} has no transcript to synthesize — skipped.",
                    candidateId);
                continue;
            }

            var ttsResult = await geminiTts.GenerateSpeechAsync(
                transcript,
                new TextToSpeechOptions(TargetLanguageCode: "en", ApiKeyOverride: credential.ApiKey),
                ct);
            if (!ttsResult.Success || ttsResult.AudioBytes is null)
            {
                logger.LogWarning(
                    "InternalResourceSeedPackListeningSeeder: TTS synthesis failed for candidate {CandidateId}: {Reason}",
                    candidateId, ttsResult.FailureReason);
                continue;
            }

            var storageKey = $"resource-import-audio/{candidateId:N}.wav";
            using (var audioStream = new MemoryStream(ttsResult.AudioBytes))
            {
                await storage.SaveAsync(storageKey, audioStream, ttsResult.AudioContentType, ct, ttsResult.AudioBytes.LongLength);
            }

            candidate.AttachAudio(storageKey, ttsResult.AudioContentType);
            if (ttsResult.DurationMs > 0)
                candidate.SetAudioDuration(Math.Round(ttsResult.DurationMs / 1000m, 1));
            await db.SaveChangesAsync(ct);
            synthesizedCount++;

            var validationResult = await validationService.ValidateAsync(candidateId, ct);
            if (validationResult.Status != ResourceCandidateValidationStatus.Passed.ToString())
            {
                logger.LogWarning(
                    "InternalResourceSeedPackListeningSeeder: candidate {CandidateId} did not pass validation ({Status}) — " +
                    "left unapproved/unpublished. Errors: {Errors}",
                    candidateId, validationResult.Status, string.Join("; ", validationResult.Errors));
                continue;
            }

            candidate.Approve("Approved by InternalResourceSeedPackListeningSeeder — pre-reviewed internal content (Sprint 9).");
            await db.SaveChangesAsync(ct);

            var publishResult = await publishService.PublishAsync(candidateId, publishedByUserId: null, ct);
            if (!publishResult.Success)
            {
                logger.LogWarning(
                    "InternalResourceSeedPackListeningSeeder: candidate {CandidateId} failed to publish: {Errors}",
                    candidateId, string.Join("; ", publishResult.Errors));
                continue;
            }

            publishedCount++;
        }

        logger.LogInformation(
            "InternalResourceSeedPackListeningSeeder: synthesized audio for {SynthesizedCount}/{TotalCandidates}, " +
            "published {PublishedCount} from source '{Name}'.",
            synthesizedCount, candidateIds.Count, publishedCount, SourceName);
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
    // 16 original listening transcripts (3 each A1/A2/B1/B2, 2 each C1/C2), spoken-register text
    // (announcements, calls, guides, lectures) suitable for real TTS synthesis.

    private const string TranscriptJson = """
    [
      {"title":"Morning Announcement","transcript":"Good morning everyone. The office will open at nine o'clock today. Please remember to bring your identification card. Thank you for your attention.","cefrLevel":"A1","skill":"listening","subskill":"listening.detail","tags":["workplace","daily"]},
      {"title":"Weather Report","transcript":"Today the weather will be sunny with a light wind. The temperature will reach twenty degrees in the afternoon. Tomorrow it will rain, so please bring an umbrella.","cefrLevel":"A1","skill":"listening","subskill":"listening.detail","tags":["daily","general"]},
      {"title":"Shop Opening Hours","transcript":"Our shop is open from nine in the morning until six in the evening. We are closed on Sundays. Thank you for visiting us today.","cefrLevel":"A1","skill":"listening","subskill":"listening.keyword_recognition","tags":["daily"]},
      {"title":"Meeting Reminder","transcript":"This is a reminder that the team meeting will start at two o'clock this afternoon in the main conference room. Please bring your notes from last week. If you cannot attend, please send a message to the organizer.","cefrLevel":"A2","skill":"listening","subskill":"listening.detail","tags":["workplace"]},
      {"title":"Travel Update","transcript":"The train to the city centre has been delayed by fifteen minutes because of engineering work. We apologize for the inconvenience. The next train will arrive on platform three.","cefrLevel":"A2","skill":"listening","subskill":"listening.detail","tags":["travel"]},
      {"title":"Cooking Instructions","transcript":"First, chop the onions and garlic finely. Then heat some oil in a large pan and cook them until soft. Add the tomatoes and let everything simmer for twenty minutes before serving.","cefrLevel":"A2","skill":"listening","subskill":"listening.dictation","tags":["daily"]},
      {"title":"Job Interview Tips","transcript":"When you attend a job interview, it helps to prepare answers about your previous experience and your strengths. Try to arrive a few minutes early and dress appropriately for the company. Remember to ask a thoughtful question at the end.","cefrLevel":"B1","skill":"listening","subskill":"listening.gist","tags":["workplace","study"]},
      {"title":"Customer Service Call","transcript":"Thank you for calling our support line. I understand you are having trouble with your recent order. Let me check the details and see what we can do to resolve this for you as quickly as possible.","cefrLevel":"B1","skill":"listening","subskill":"listening.inference","tags":["workplace","daily"]},
      {"title":"City Guide","transcript":"The old town is famous for its narrow streets and historic buildings. Visitors often spend an afternoon exploring the market and trying the local food. In the evening, many restaurants offer live music and a relaxed atmosphere.","cefrLevel":"B1","skill":"listening","subskill":"listening.gist","tags":["travel","general"]},
      {"title":"Podcast Introduction","transcript":"Welcome back to the show. Today we are discussing how remote work has changed the way companies communicate with their employees. Our guest has spent the last decade researching workplace culture, and she has some surprising insights to share.","cefrLevel":"B2","skill":"listening","subskill":"listening.gist","tags":["workplace","study"]},
      {"title":"Environmental Report","transcript":"Recent studies suggest that reducing food waste could have a significant impact on carbon emissions. Many households throw away food that could easily be used in another meal. Simple changes in planning and storage can make a real difference over time.","cefrLevel":"B2","skill":"listening","subskill":"listening.inference","tags":["study","general"]},
      {"title":"Business News Update","transcript":"The company announced yesterday that it plans to expand into three new markets over the next two years. Analysts have responded positively, though some remain cautious about the costs involved in such rapid growth.","cefrLevel":"B2","skill":"listening","subskill":"listening.inference","tags":["workplace","general"]},
      {"title":"Panel Discussion Excerpt","transcript":"What strikes me most about this debate is how rarely participants acknowledge the underlying assumptions behind their arguments. We tend to focus on conclusions without examining the premises that led us there, and that, I would argue, is where genuine disagreement actually originates.","cefrLevel":"C1","skill":"listening","subskill":"listening.inference","tags":["study","general"]},
      {"title":"Documentary Narration","transcript":"Beneath the surface of the ocean lies an ecosystem so intricate that scientists are still uncovering its secrets. Every organism, however small, plays a role in a delicate balance that has evolved over millions of years, and disrupting even one link can have consequences far beyond what we might expect.","cefrLevel":"C1","skill":"listening","subskill":"listening.gist","tags":["general","study"]},
      {"title":"Academic Lecture Excerpt","transcript":"It would be a mistake to assume that innovation follows a linear trajectory. History suggests, rather, that breakthroughs often emerge from the unexpected convergence of unrelated disciplines, and that the most rigid adherence to established methodology can, paradoxically, become the very obstacle to genuine progress.","cefrLevel":"C2","skill":"listening","subskill":"listening.inference","tags":["study","general"]},
      {"title":"Literary Radio Drama Excerpt","transcript":"She stood at the threshold, unable to decide whether the silence behind her was an invitation or a warning. Every choice, she reflected, carries within it the ghost of the path not taken, and perhaps that is the truest burden of freedom: not the decision itself, but the knowledge of what it forecloses.","cefrLevel":"C2","skill":"listening","subskill":"listening.inference","tags":["general"]}
    ]
    """;
}

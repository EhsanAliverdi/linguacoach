using System.Text.Json;
using System.Text.Json.Serialization;
using LinguaCoach.Application.Exercises;
using LinguaCoach.Application.Lessons;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Application.Speaking;
using LinguaCoach.Application.Storage;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure;
using LinguaCoach.Infrastructure.Speaking;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.ContentSeeder;

/// <summary>
/// Full content reseed (2026-07-29) — standalone console tool, no controllers/HTTP endpoints, no
/// live AI-provider calls. Reads hand-authored seed JSON (see Plan Phase C/E/F) and loads the DB by
/// calling the existing, tested, deterministic <see cref="IGenerateLessonFromResourcesHandler"/> /
/// <see cref="IGenerateActivitiesFromLessonHandler"/> pipeline in-process (same DI wiring
/// `LinguaCoach.Api/Program.cs` uses), then links the resulting Module directly to the SkillGraphNode
/// it teaches — no AI auto-tagging. Resumable: writes a `.checkpoint.json` file of processed keys
/// next to each input file.
///
/// Usage: dotnet run --project tools/LinguaCoach.ContentSeeder -- grammar path/to/grammar-seed.json
///        dotnet run --project tools/LinguaCoach.ContentSeeder -- vocabulary path/to/vocabulary-seed.json
///        dotnet run --project tools/LinguaCoach.ContentSeeder -- cefr-scales path/to/cefr-scales-seed.json
///        dotnet run --project tools/LinguaCoach.ContentSeeder -- speaking path/to/speaking-seed.json
///        dotnet run --project tools/LinguaCoach.ContentSeeder -- listening path/to/listening-seed.json
///        dotnet run --project tools/LinguaCoach.ContentSeeder -- reading data/cerfj-reading.json
///        dotnet run --project tools/LinguaCoach.ContentSeeder -- prerequisites path/to/prerequisites-seed.json
/// </summary>
public static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static async Task<int> Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: ContentSeeder <grammar|vocabulary> <seed-file.json>");
            return 1;
        }

        var domain = args[0];
        var seedFilePath = args[1];
        if (!File.Exists(seedFilePath))
        {
            Console.Error.WriteLine($"Seed file not found: {seedFilePath}");
            return 1;
        }

        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5433;Database=linguacoach_dev;Username=postgres;Password=postgres";

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddDbContext<LinguaCoachDbContext>(options => options.UseNpgsql(connectionString));
        services.AddInfrastructure(configuration);
        await using var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<LinguaCoachDbContext>();
        var lessonHandler = sp.GetRequiredService<IGenerateLessonFromResourcesHandler>();
        var activitiesHandler = sp.GetRequiredService<IGenerateActivitiesFromLessonHandler>();

        var seeder = new LeafContentSeeder(db, lessonHandler, activitiesHandler);

        return domain switch
        {
            "grammar" => await SeedGrammarAsync(db, seedFilePath),
            "vocabulary" => await SeedVocabularyAsync(db, seedFilePath),
            "pronunciation" => await SeedPronunciationAsync(db, seedFilePath),
            "functional-language" => await SeedFunctionalLanguageAsync(db, seedFilePath),
            "reading-comprehension" => await SeedReadingComprehensionAsync(db, seedFilePath),
            "writing" => await SeedWritingAsync(db, seedFilePath),
            "listening-comprehension" => await SeedListeningComprehensionAsync(db, seedFilePath),
            "cefr-scales" => await SeedCefrScalesAsync(db, seedFilePath),
            "speaking" => await SeedSpeakingAsync(seeder, db, seedFilePath),
            "listening" => await SeedListeningAsync(seeder, sp, db, seedFilePath),
            "reading" => await SeedReadingAsync(seeder, db, seedFilePath),
            "prerequisites" => await SeedPrerequisitesAsync(db, seedFilePath),
            _ => Fail($"Unknown domain '{domain}' — expected 'grammar', 'vocabulary', 'pronunciation', 'functional-language', 'reading-comprehension', 'writing', 'listening-comprehension', 'cefr-scales', 'speaking', 'listening', 'reading', or 'prerequisites'."),
        };
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }

    private static async Task<int> SeedGrammarAsync(LinguaCoachDbContext db, string path)
    {
        var file = JsonSerializer.Deserialize<GrammarSeedFile>(await File.ReadAllTextAsync(path), JsonOptions)
            ?? throw new InvalidOperationException("Empty/invalid grammar seed file.");

        Console.WriteLine($"Grammar seed v{file.Version}" + (file.VersionNotes is { Count: > 0 } notes
            ? $" — {string.Join(" | ", notes)}" : "."));

        // Containers are skill-less (2026-07-31 container/leaf redesign) — Skill is a leaf-only
        // measurable-skill attribute; a topical container like "Verb be" is purely structural.
        var containerIds = await UpsertContainersAsync(db, file.Containers.Select(c =>
            (c.Key, c.Title, c.CefrLevel, c.DifficultyBand, Skill: (string?)null, c.ParentKey, c.Description,
             c.CefrConfidence, c.CefrSource, c.NodeType, c.RoutingEligible)));

        foreach (var leaf in file.Leaves)
        {
            var parentId = leaf.ParentKey is not null && containerIds.TryGetValue(leaf.ParentKey, out var pid) ? pid : (Guid?)null;
            var leafId = await UpsertLeafAsync(db, leaf.Key, leaf.Title, leaf.CefrLevel, leaf.DifficultyBand,
                CurriculumSkillConstants.Grammar, parentId, description: leaf.Description ?? leaf.LessonBody,
                cefrConfidence: leaf.CefrConfidence, cefrSource: leaf.CefrSource, nodeType: leaf.NodeType, routingEligible: leaf.RoutingEligible,
                subskill: leaf.Subskill);

            await RichContentSeeder.SeedLeafAsync(db, leafId, leaf.Title, leaf.CefrLevel, CurriculumSkillConstants.Grammar,
                leaf.Subskill, leaf.DifficultyBand, leaf.LessonBody, leaf.Examples, leaf.CommonMistakes, leaf.Exercises, leaf.ImageUrl);
            Console.WriteLine($"  Grammar leaf '{leaf.Key}': {leaf.Exercises.Count} exercise(s) seeded.");
        }

        Console.WriteLine($"Grammar seeding complete. {file.Leaves.Count} leaves processed.");
        return 0;
    }

    private static async Task<int> SeedVocabularyAsync(LinguaCoachDbContext db, string path)
    {
        var file = JsonSerializer.Deserialize<VocabularySeedFile>(await File.ReadAllTextAsync(path), JsonOptions)
            ?? throw new InvalidOperationException("Empty/invalid vocabulary seed file.");

        // Containers are skill-less — see the grammar seeder's comment above.
        var containerIds = await UpsertContainersAsync(db, file.Containers.Select(c =>
            (c.Key, c.Title, c.CefrLevel, DifficultyBand: 1, Skill: (string?)null, c.ParentKey, Description: (string?)null)));

        foreach (var leaf in file.Leaves)
        {
            var parentId = leaf.ParentKey is not null && containerIds.TryGetValue(leaf.ParentKey, out var pid) ? pid : (Guid?)null;
            var leafId = await UpsertLeafAsync(db, leaf.Key, leaf.Title, leaf.CefrLevel, leaf.DifficultyBand,
                CurriculumSkillConstants.Vocabulary, parentId, description: leaf.LessonBody);

            await RichContentSeeder.SeedLeafAsync(db, leafId, leaf.Title, leaf.CefrLevel, CurriculumSkillConstants.Vocabulary,
                leaf.Subskill, leaf.DifficultyBand, leaf.LessonBody, leaf.Examples, leaf.CommonMistakes, leaf.Exercises, leaf.ImageUrl);
            Console.WriteLine($"  Vocabulary leaf '{leaf.Key}': {leaf.Exercises.Count} exercise(s) seeded.");
        }

        Console.WriteLine($"Vocabulary seeding complete. {file.Leaves.Count} leaves processed.");
        return 0;
    }

    private static async Task<int> SeedPronunciationAsync(LinguaCoachDbContext db, string path)
    {
        var file = JsonSerializer.Deserialize<PronunciationSeedFile>(await File.ReadAllTextAsync(path), JsonOptions)
            ?? throw new InvalidOperationException("Empty/invalid pronunciation seed file.");

        // Containers are skill-less — see the grammar seeder's comment above.
        var containerIds = await UpsertContainersAsync(db, file.Containers.Select(c =>
            (c.Key, c.Title, c.CefrLevel, DifficultyBand: 1, Skill: (string?)null, c.ParentKey, Description: (string?)null)));

        foreach (var leaf in file.Leaves)
        {
            var parentId = leaf.ParentKey is not null && containerIds.TryGetValue(leaf.ParentKey, out var pid) ? pid : (Guid?)null;
            var leafId = await UpsertLeafAsync(db, leaf.Key, leaf.Title, leaf.CefrLevel, leaf.DifficultyBand,
                CurriculumSkillConstants.Pronunciation, parentId, description: leaf.LessonBody);

            await RichContentSeeder.SeedLeafAsync(db, leafId, leaf.Title, leaf.CefrLevel, CurriculumSkillConstants.Pronunciation,
                subskill: null, leaf.DifficultyBand, leaf.LessonBody, leaf.Examples, leaf.CommonMistakes, leaf.Exercises, leaf.ImageUrl);
            Console.WriteLine($"  Pronunciation leaf '{leaf.Key}': {leaf.Exercises.Count} exercise(s) seeded.");
        }

        Console.WriteLine($"Pronunciation seeding complete. {file.Leaves.Count} leaves processed.");
        return 0;
    }

    private static async Task<int> SeedFunctionalLanguageAsync(LinguaCoachDbContext db, string path)
    {
        var file = JsonSerializer.Deserialize<FunctionalLanguageSeedFile>(await File.ReadAllTextAsync(path), JsonOptions)
            ?? throw new InvalidOperationException("Empty/invalid functional-language seed file.");

        // Containers are skill-less — see the grammar seeder's comment above.
        var containerIds = await UpsertContainersAsync(db, file.Containers.Select(c =>
            (c.Key, c.Title, c.CefrLevel, DifficultyBand: 1, Skill: (string?)null, c.ParentKey, Description: (string?)null)));

        foreach (var leaf in file.Leaves)
        {
            var parentId = leaf.ParentKey is not null && containerIds.TryGetValue(leaf.ParentKey, out var pid) ? pid : (Guid?)null;
            var resolvedSubskill = leaf.Subskill ?? CurriculumSubskillConstants.SpeakingFunctionalPhrases;
            var leafId = await UpsertLeafAsync(db, leaf.Key, leaf.Title, leaf.CefrLevel, leaf.DifficultyBand,
                CurriculumSkillConstants.Speaking, parentId, description: leaf.LessonBody, subskill: resolvedSubskill);

            await RichContentSeeder.SeedLeafAsync(db, leafId, leaf.Title, leaf.CefrLevel, CurriculumSkillConstants.Speaking,
                resolvedSubskill, leaf.DifficultyBand, leaf.LessonBody, leaf.Examples,
                leaf.CommonMistakes, leaf.Exercises, leaf.ImageUrl);
            Console.WriteLine($"  Functional-language leaf '{leaf.Key}': {leaf.Exercises.Count} exercise(s) seeded.");
        }

        Console.WriteLine($"Functional-language seeding complete. {file.Leaves.Count} leaves processed.");
        return 0;
    }

    private static async Task<int> SeedReadingComprehensionAsync(LinguaCoachDbContext db, string path)
    {
        var file = JsonSerializer.Deserialize<ReadingSeedFile>(await File.ReadAllTextAsync(path), JsonOptions)
            ?? throw new InvalidOperationException("Empty/invalid reading seed file.");

        // Containers are skill-less — see the grammar seeder's comment above.
        var containerIds = await UpsertContainersAsync(db, file.Containers.Select(c =>
            (c.Key, c.Title, c.CefrLevel, DifficultyBand: 1, Skill: (string?)null, c.ParentKey, Description: (string?)null)));

        foreach (var leaf in file.Leaves)
        {
            var parentId = leaf.ParentKey is not null && containerIds.TryGetValue(leaf.ParentKey, out var pid) ? pid : (Guid?)null;
            var leafId = await UpsertLeafAsync(db, leaf.Key, leaf.Title, leaf.CefrLevel, leaf.DifficultyBand,
                CurriculumSkillConstants.Reading, parentId, description: leaf.LessonBody, subskill: leaf.Subskill);

            await RichContentSeeder.SeedLeafAsync(db, leafId, leaf.Title, leaf.CefrLevel, CurriculumSkillConstants.Reading,
                leaf.Subskill, leaf.DifficultyBand, leaf.LessonBody, leaf.Examples, leaf.CommonMistakes, leaf.Exercises, leaf.ImageUrl);
            Console.WriteLine($"  Reading leaf '{leaf.Key}': {leaf.Exercises.Count} exercise(s) seeded.");
        }

        Console.WriteLine($"Reading seeding complete. {file.Leaves.Count} leaves processed.");
        return 0;
    }

    private static async Task<int> SeedWritingAsync(LinguaCoachDbContext db, string path)
    {
        var file = JsonSerializer.Deserialize<WritingSeedFile>(await File.ReadAllTextAsync(path), JsonOptions)
            ?? throw new InvalidOperationException("Empty/invalid writing seed file.");

        // Containers are skill-less — see the grammar seeder's comment above.
        var containerIds = await UpsertContainersAsync(db, file.Containers.Select(c =>
            (c.Key, c.Title, c.CefrLevel, DifficultyBand: 1, Skill: (string?)null, c.ParentKey, Description: (string?)null)));

        foreach (var leaf in file.Leaves)
        {
            var parentId = leaf.ParentKey is not null && containerIds.TryGetValue(leaf.ParentKey, out var pid) ? pid : (Guid?)null;
            var leafId = await UpsertLeafAsync(db, leaf.Key, leaf.Title, leaf.CefrLevel, leaf.DifficultyBand,
                CurriculumSkillConstants.Writing, parentId, description: leaf.LessonBody, subskill: leaf.Subskill);

            await RichContentSeeder.SeedLeafAsync(db, leafId, leaf.Title, leaf.CefrLevel, CurriculumSkillConstants.Writing,
                leaf.Subskill, leaf.DifficultyBand, leaf.LessonBody, leaf.Examples, leaf.CommonMistakes, leaf.Exercises, leaf.ImageUrl);
            Console.WriteLine($"  Writing leaf '{leaf.Key}': {leaf.Exercises.Count} exercise(s) seeded.");
        }

        Console.WriteLine($"Writing seeding complete. {file.Leaves.Count} leaves processed.");
        return 0;
    }

    private static async Task<int> SeedListeningComprehensionAsync(LinguaCoachDbContext db, string path)
    {
        var file = JsonSerializer.Deserialize<ListeningComprehensionSeedFile>(await File.ReadAllTextAsync(path), JsonOptions)
            ?? throw new InvalidOperationException("Empty/invalid listening-comprehension seed file.");

        // Containers are skill-less — see the grammar seeder's comment above.
        var containerIds = await UpsertContainersAsync(db, file.Containers.Select(c =>
            (c.Key, c.Title, c.CefrLevel, DifficultyBand: 1, Skill: (string?)null, c.ParentKey, Description: (string?)null)));

        foreach (var leaf in file.Leaves)
        {
            var parentId = leaf.ParentKey is not null && containerIds.TryGetValue(leaf.ParentKey, out var pid) ? pid : (Guid?)null;
            var leafId = await UpsertLeafAsync(db, leaf.Key, leaf.Title, leaf.CefrLevel, leaf.DifficultyBand,
                CurriculumSkillConstants.Listening, parentId, description: leaf.LessonBody, subskill: leaf.Subskill);

            await RichContentSeeder.SeedLeafAsync(db, leafId, leaf.Title, leaf.CefrLevel, CurriculumSkillConstants.Listening,
                leaf.Subskill, leaf.DifficultyBand, leaf.LessonBody, leaf.Examples, leaf.CommonMistakes, leaf.Exercises, leaf.ImageUrl);
            Console.WriteLine($"  Listening-comprehension leaf '{leaf.Key}': {leaf.Exercises.Count} exercise(s) seeded.");
        }

        Console.WriteLine($"Listening-comprehension seeding complete. {file.Leaves.Count} leaves processed.");
        return 0;
    }

    /// <summary>Full content reseed Phase E (2026-07-29) — Reading/Listening/Speaking/Writing/
    /// Vocabulary/Grammar/Pronunciation/Fluency taxonomy from the CEFR Companion Volume's real
    /// descriptor scales. Pure taxonomy in this pass — no ResourceBankItem/Lesson/Exercise/Module
    /// chain (see plan Decision 5): these nodes exist so content (starting with reading passages)
    /// has something real to link to via ModuleSkillGraphNodeLink.</summary>
    private static async Task<int> SeedCefrScalesAsync(LinguaCoachDbContext db, string path)
    {
        var file = JsonSerializer.Deserialize<CefrScalesSeedFile>(await File.ReadAllTextAsync(path), JsonOptions)
            ?? throw new InvalidOperationException("Empty/invalid CEFR-scales seed file.");

        var containerIds = await UpsertContainersAsync(db, file.Containers.Select(c =>
            (c.Key, c.Title, c.CefrLevel, DifficultyBand: 1, c.Skill)));

        var processed = 0;
        foreach (var leaf in file.Leaves)
        {
            var parentId = containerIds.TryGetValue(leaf.ParentKey, out var pid) ? pid : (Guid?)null;
            await UpsertLeafAsync(db, leaf.Key, leaf.Title, leaf.CefrLevel, difficultyBand: 1, leaf.Skill,
                parentId, descriptionForAi: null, description: leaf.Descriptor);
            processed++;
        }

        Console.WriteLine($"CEFR-scale taxonomy seeding complete. {containerIds.Count} containers, {processed} leaves.");
        return 0;
    }

    /// <summary>Full content reseed Phase F (2026-07-29) — hand-authored speaking prompts. Each prompt
    /// gets its own leaf SkillGraphNode (so the idempotency check in SeedOneAsync is per-prompt, not
    /// shared) plus a secondary link to the matching Phase E CEFR-scale leaf (ScaleLeafKey) so it's
    /// also visible under the real Council-of-Europe speaking taxonomy.</summary>
    private static async Task<int> SeedSpeakingAsync(LeafContentSeeder seeder, LinguaCoachDbContext db, string path)
    {
        var file = JsonSerializer.Deserialize<SpeakingSeedFile>(await File.ReadAllTextAsync(path), JsonOptions)
            ?? throw new InvalidOperationException("Empty/invalid speaking seed file.");

        var source = await GetOrCreateSourceAsync(db, "LinguaCoach Speaking Prompts");
        var checkpoint = Checkpoint.Load(path);
        var containerIds = await UpsertContainersAsync(db, file.Containers.Select(c =>
            (c.Key, c.Title, c.CefrLevel, DifficultyBand: 1, Skill: CurriculumSkillConstants.Speaking)));

        var processed = 0;
        foreach (var leaf in file.Leaves)
        {
            if (checkpoint.Contains(leaf.Key)) continue;

            var parentId = containerIds.TryGetValue(leaf.ParentKey, out var pid) ? pid : (Guid?)null;
            var leafId = await UpsertLeafAsync(db, leaf.Key, leaf.Title, leaf.CefrLevel, difficultyBand: 1,
                CurriculumSkillConstants.Speaking, parentId);

            var scaleLeafId = await db.SkillGraphNodes
                .Where(n => n.Key == leaf.ScaleLeafKey)
                .Select(n => (Guid?)n.Id)
                .FirstOrDefaultAsync();

            var content = ResourceBankItemContent.Serialize(new SpeakingPromptContent(
                leaf.Title, leaf.PromptText, leaf.SuggestedDurationSeconds, ImageUrl: null));
            await seeder.SeedOneAsync(source.Id, leaf.CefrLevel, content, leaf.Title, leafId, leaf.ActivityType,
                PublishedResourceType.Speaking, scaleLeafId.HasValue ? [scaleLeafId.Value] : null);

            checkpoint.MarkProcessed(leaf.Key);
            if (++processed % 50 == 0)
            {
                checkpoint.Save(path);
                Console.WriteLine($"Speaking: {processed}/{file.Leaves.Count} processed.");
            }
        }

        checkpoint.Save(path);
        Console.WriteLine($"Speaking seeding complete. {processed} leaves processed this run.");
        return 0;
    }

    /// <summary>Full content reseed Phase G (2026-07-29) — listening passages. Unlike every other
    /// domain seeded so far, this makes a REAL, PRE-AUTHORIZED live Gemini TTS call per transcript
    /// (ListeningPassageContent.AudioStorageKey/AudioContentType are non-nullable — a transcript
    /// alone is not valid). Synthesized audio is cached to <paramref name="path"/>'s sibling
    /// `data/seed-audio/listening/` directory first (by leaf key) so re-running this tool never
    /// re-spends TTS quota on an already-synthesized passage, then uploaded via IFileStorageService
    /// (MinIO in dev — see FILE_STORAGE_PROVIDER) so the running app can actually stream it back.
    /// Bypasses TtsProviderResolver deliberately: the `tts.listening` DB category defaults to the
    /// fake provider for CI safety, so this calls GeminiTextToSpeechService directly with the real
    /// key from AiProviderCredentials, mirroring the existing
    /// InternalResourceSeedPackListeningSeeder precedent.</summary>
    private static async Task<int> SeedListeningAsync(
        LeafContentSeeder seeder, IServiceProvider sp, LinguaCoachDbContext db, string path)
    {
        var file = JsonSerializer.Deserialize<ListeningSeedFile>(await File.ReadAllTextAsync(path), JsonOptions)
            ?? throw new InvalidOperationException("Empty/invalid listening seed file.");

        var credential = await db.AiProviderCredentials.FirstOrDefaultAsync(c => c.ProviderName == "gemini");
        if (string.IsNullOrWhiteSpace(credential?.ApiKey))
            return Fail("No Gemini API key configured in AiProviderCredentials — cannot synthesize listening audio.");

        var geminiTts = sp.GetRequiredService<GeminiTextToSpeechService>();
        var storage = sp.GetRequiredService<IFileStorageService>();

        // Confirmed by hand during Phase G validation: MinioFileStorageService.SaveAsync does NOT
        // throw when the target bucket doesn't exist — PutObjectAsync returns successfully with no
        // object actually persisted server-side, silently orphaning the DB's AudioStorageKey. Fail
        // fast here instead of discovering that after seeding hundreds of leaves.
        var storageHealth = await storage.HealthCheckAsync();
        if (storageHealth is not null)
            return Fail($"File storage health check failed — {storageHealth}");

        var audioCacheDir = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, "..", "seed-audio", "listening");
        Directory.CreateDirectory(audioCacheDir);

        var source = await GetOrCreateSourceAsync(db, "LinguaCoach Listening Passages");
        var checkpoint = Checkpoint.Load(path);
        var containerIds = await UpsertContainersAsync(db, file.Containers.Select(c =>
            (c.Key, c.Title, c.CefrLevel, DifficultyBand: 1, Skill: CurriculumSkillConstants.Listening)));

        var processed = 0;
        foreach (var leaf in file.Leaves)
        {
            if (checkpoint.Contains(leaf.Key)) continue;

            var parentId = containerIds.TryGetValue(leaf.ParentKey, out var pid) ? pid : (Guid?)null;
            var leafId = await UpsertLeafAsync(db, leaf.Key, leaf.Title, leaf.CefrLevel, difficultyBand: 1,
                CurriculumSkillConstants.Listening, parentId);

            var alreadySeeded = await db.ModuleSkillGraphNodeLinks.AnyAsync(l => l.SkillGraphNodeId == leafId);
            if (alreadySeeded)
            {
                checkpoint.MarkProcessed(leaf.Key);
                continue;
            }

            var cacheFile = Path.Combine(audioCacheDir, $"{Slug(leaf.Key)}.wav");
            byte[] audioBytes;
            if (File.Exists(cacheFile))
            {
                audioBytes = await File.ReadAllBytesAsync(cacheFile);
            }
            else
            {
                var ttsResult = await geminiTts.GenerateSpeechAsync(
                    leaf.Transcript, new TextToSpeechOptions(TargetLanguageCode: "en", ApiKeyOverride: credential.ApiKey));
                if (!ttsResult.Success || ttsResult.AudioBytes is null || ttsResult.AudioBytes.Length < 1000)
                {
                    Console.Error.WriteLine($"Listening: TTS failed for '{leaf.Key}' — {ttsResult.FailureReason ?? "empty/too-small audio"}. Skipping.");
                    continue;
                }
                audioBytes = ttsResult.AudioBytes;
                await File.WriteAllBytesAsync(cacheFile, audioBytes);
            }

            var storageKey = $"listening-seed-audio/{Slug(leaf.Key)}.wav";
            using (var audioStream = new MemoryStream(audioBytes))
                await storage.SaveAsync(storageKey, audioStream, "audio/wav", knownSizeBytes: audioBytes.LongLength);

            // 16-bit mono PCM WAV at 24kHz (fixed format produced by GeminiTextToSpeechService), less the 44-byte header.
            var audioDurationSeconds = Math.Round((audioBytes.Length - 44) / 48000m, 1);

            var scaleLeafId = await db.SkillGraphNodes
                .Where(n => n.Key == leaf.ScaleLeafKey)
                .Select(n => (Guid?)n.Id)
                .FirstOrDefaultAsync();

            var content = ResourceBankItemContent.Serialize(new ListeningPassageContent(
                leaf.Title, leaf.Transcript, storageKey, "audio/wav",
                AttributionText: "Synthesized audio (Gemini TTS) — LinguaCoach Listening Passages seed.",
                AudioDurationSeconds: audioDurationSeconds));
            try
            {
                await seeder.SeedOneAsync(source.Id, leaf.CefrLevel, content, leaf.Title, leafId, leaf.ActivityType,
                    PublishedResourceType.Listening, scaleLeafId.HasValue ? [scaleLeafId.Value] : null);
            }
            catch (ExerciseValidationException ex)
            {
                // Audio was already synthesized and uploaded (cached locally either way) — only the
                // deterministic exercise composer rejected this transcript (e.g. too few distinct
                // long content words for a cloze). Skip without marking processed so a fixed
                // transcript gets retried on the next run, same discipline as elsewhere this session.
                Console.Error.WriteLine($"Listening: exercise generation failed for '{leaf.Key}' — {ex.Message}. Skipping.");
                continue;
            }

            checkpoint.MarkProcessed(leaf.Key);
            if (++processed % 20 == 0)
            {
                checkpoint.Save(path);
                Console.WriteLine($"Listening: {processed}/{file.Leaves.Count} processed.");
            }
        }

        checkpoint.Save(path);
        Console.WriteLine($"Listening seeding complete. {processed} leaves processed this run.");
        return 0;
    }

    /// <summary>Full content reseed Phase (reading) — parses the already-complete, pre-written
    /// `data/cerfj-reading.json` JSONL dataset directly (no separate authored seed-json file needed,
    /// per plan Decision 7 — all 3,053 passages have essentially unique topics, so there is no
    /// container-grouping value to add). Each passage gets its own leaf SkillGraphNode (so
    /// SeedOneAsync's idempotency check is per-passage) plus secondary links to (a) every
    /// already-seeded vocabulary leaf whose headword appears in the passage's own
    /// scientific_metadata.constraints.target_vocabulary list, and (b) the matching Phase E
    /// "Overall Reading Comprehension" CEFR-scale leaf.</summary>
    private static async Task<int> SeedReadingAsync(LeafContentSeeder seeder, LinguaCoachDbContext db, string path)
    {
        var lines = (await File.ReadAllLinesAsync(path)).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

        var source = await GetOrCreateSourceAsync(db, "CEFR-J Synthetic Reading Passage Dataset");
        var checkpoint = Checkpoint.Load(path);
        var containerIds = await UpsertContainersAsync(db, new[]
        {
            ("reading.topic_passages", "Reading Passages", "A1", 1, CurriculumSkillConstants.Reading),
        });
        var containerId = containerIds["reading.topic_passages"];

        var vocabByHeadword = await BuildVocabularyHeadwordLookupAsync(db);

        var processed = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            var passageNumber = i + 1;
            var key = $"reading.passage_{passageNumber:0000}";
            if (checkpoint.Contains(key)) continue;

            using var doc = JsonDocument.Parse(lines[i]);
            var root = doc.RootElement;
            var metadata = root.GetProperty("scientific_metadata");
            var topic = metadata.GetProperty("topic").GetString()!;
            var cefrLevel = metadata.GetProperty("target_level").GetString()!;
            var targetVocabulary = metadata.TryGetProperty("constraints", out var constraints)
                && constraints.TryGetProperty("target_vocabulary", out var vocabArray)
                    ? vocabArray.EnumerateArray().Select(v => v.GetString()!).ToList()
                    : [];

            var assistantContent = root.GetProperty("messages")
                .EnumerateArray()
                .First(m => m.GetProperty("role").GetString() == "assistant")
                .GetProperty("content").GetString()!;
            var passageText = CleanReadingPassageText(assistantContent);

            var leafId = await UpsertLeafAsync(db, key, topic, cefrLevel, difficultyBand: 1,
                CurriculumSkillConstants.Reading, containerId);

            var alreadySeeded = await db.ModuleSkillGraphNodeLinks.AnyAsync(l => l.SkillGraphNodeId == leafId);
            if (alreadySeeded)
            {
                checkpoint.MarkProcessed(key);
                continue;
            }

            var wordCount = passageText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
            var estimatedReadingMinutes = Math.Max(1, (int)Math.Round(wordCount / 200.0, MidpointRounding.AwayFromZero));

            var matchedVocabIds = targetVocabulary
                .SelectMany(w => vocabByHeadword.TryGetValue(SlugHeadword(w), out var ids) ? ids : [])
                .Distinct()
                .ToList();
            var scaleLeafId = await db.SkillGraphNodes
                .Where(n => n.Key == $"reading.scale_overall_reading_comprehension.{cefrLevel.ToLowerInvariant()}")
                .Select(n => (Guid?)n.Id)
                .FirstOrDefaultAsync();
            var additionalIds = matchedVocabIds.ToList();
            if (scaleLeafId.HasValue) additionalIds.Add(scaleLeafId.Value);

            var content = ResourceBankItemContent.Serialize(new ReadingPassageContent(
                topic, passageText, Summary: null, PrimarySkill: "Reading", TopicTagsJson: null,
                wordCount, estimatedReadingMinutes,
                AttributionText: "CEFR-J synthetic reading passage dataset (data/cerfj-reading.json).",
                QualityScore: null));

            try
            {
                await seeder.SeedOneAsync(source.Id, cefrLevel, content, topic, leafId, "reading_fill_in_blanks",
                    PublishedResourceType.ReadingPassage, additionalIds.Count > 0 ? additionalIds : null);
            }
            catch (ExerciseValidationException ex)
            {
                Console.Error.WriteLine($"Reading: exercise generation failed for '{key}' ('{topic}') — {ex.Message}. Skipping.");
                continue;
            }

            checkpoint.MarkProcessed(key);
            if (++processed % 100 == 0)
            {
                checkpoint.Save(path);
                Console.WriteLine($"Reading: {processed}/{lines.Count} processed.");
            }
        }

        checkpoint.Save(path);
        Console.WriteLine($"Reading seeding complete. {processed} leaves processed this run.");
        return 0;
    }

    /// <summary>Strips the occasional "Here is a reading passage about X:" preamble line (present on
    /// 3 of 3,053 passages) and the **bold** target-vocabulary markers — this pipeline's deterministic
    /// cloze composer HTML-encodes passage text verbatim with no markdown rendering, so literal
    /// asterisks would otherwise show up in the student-facing exercise.</summary>
    private static string CleanReadingPassageText(string raw)
    {
        var text = System.Text.RegularExpressions.Regex.Replace(
            raw, @"^Here is a reading passage about[^\n]*:\s*\n+", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return text.Replace("**", "").Trim();
    }

    /// <summary>Loads every vocabulary leaf's headword (splitting "color/colour"-style slash variants
    /// into separate lookup keys) into an in-memory dictionary once, so matching each reading
    /// passage's target_vocabulary list doesn't need a DB round-trip per word. ~9,775 leaves — cheap
    /// to hold in memory for the duration of this one seeding run.</summary>
    private static async Task<Dictionary<string, List<Guid>>> BuildVocabularyHeadwordLookupAsync(LinguaCoachDbContext db)
    {
        var vocabLeaves = await db.SkillGraphNodes
            .Where(n => n.Skill == CurriculumSkillConstants.Vocabulary && n.ParentNodeId != null)
            .Select(n => new { n.Id, n.Title })
            .ToListAsync();

        var lookup = new Dictionary<string, List<Guid>>();
        foreach (var leaf in vocabLeaves)
        {
            var headwordPart = leaf.Title.Contains(" (") ? leaf.Title[..leaf.Title.IndexOf(" (")] : leaf.Title;
            foreach (var variant in headwordPart.Split('/'))
            {
                var slug = SlugHeadword(variant);
                if (slug.Length == 0) continue;
                if (!lookup.TryGetValue(slug, out var ids)) lookup[slug] = ids = [];
                ids.Add(leaf.Id);
            }
        }
        return lookup;
    }

    private static string SlugHeadword(string word) => word.Trim().ToLowerInvariant();

    /// <summary>Domain-agnostic prerequisite-edge loader (2026-07-30) — used for grammar's
    /// hand-curated curriculum-design edges, and for speaking/listening's topic-scoped CEFR-level
    /// chaining and reading's CEFR-scale chaining (requested after the reseed shipped with zero
    /// edges in SkillGraphPrerequisiteEdge; see docs/reviews for the underlying gap analysis). Each
    /// entry in the seed file is a {Node, Prerequisite, Reason} triple keyed by SkillGraphNode.Key.
    /// Offline verification (dedup + cycle check) already ran when authoring each seed file — this
    /// pass adds a defensive re-check against the live DB anyway, since edges accumulate across runs.</summary>
    private static async Task<int> SeedPrerequisitesAsync(LinguaCoachDbContext db, string path)
    {
        var entries = JsonSerializer.Deserialize<List<PrerequisiteEdgeSeed>>(await File.ReadAllTextAsync(path), JsonOptions)
            ?? throw new InvalidOperationException("Empty/invalid prerequisites seed file.");

        var allKeys = entries.Select(e => e.Node).Concat(entries.Select(e => e.Prerequisite)).Distinct().ToList();
        var idByKey = await db.SkillGraphNodes
            .Where(n => allKeys.Contains(n.Key))
            .ToDictionaryAsync(n => n.Key, n => n.Id);

        var existingEdges = new HashSet<(Guid Node, Guid Prerequisite)>(
            (await db.SkillGraphPrerequisiteEdges.Select(e => new { e.NodeId, e.PrerequisiteNodeId }).ToListAsync())
                .Select(e => (e.NodeId, e.PrerequisiteNodeId)));

        var created = 0;
        var skippedMissingKey = 0;
        var skippedExisting = 0;
        foreach (var entry in entries)
        {
            if (!idByKey.TryGetValue(entry.Node, out var nodeId) || !idByKey.TryGetValue(entry.Prerequisite, out var prereqId))
            {
                Console.Error.WriteLine($"Prerequisites: missing SkillGraphNode for '{entry.Node}' or '{entry.Prerequisite}'. Skipping.");
                skippedMissingKey++;
                continue;
            }

            if (!existingEdges.Add((nodeId, prereqId)))
            {
                skippedExisting++;
                continue;
            }

            db.SkillGraphPrerequisiteEdges.Add(new SkillGraphPrerequisiteEdge(nodeId, prereqId));
            created++;
            if (created % 200 == 0)
            {
                await db.SaveChangesAsync();
                Console.WriteLine($"Prerequisites: {created} edges created so far.");
            }
        }

        await db.SaveChangesAsync();
        Console.WriteLine($"Prerequisites seeding complete. {created} edges created, {skippedExisting} already existed, {skippedMissingKey} skipped (missing node).");
        return 0;
    }

    private static string Slug(string key) =>
        key.ToLowerInvariant().Replace('.', '_').Replace(' ', '_');

    private static async Task<Dictionary<string, Guid>> UpsertContainersAsync(
        LinguaCoachDbContext db, IEnumerable<(string Key, string Title, string CefrLevel, int DifficultyBand, string? Skill)> containers)
        => await UpsertContainersAsync(db, containers.Select(c =>
            (c.Key, c.Title, c.CefrLevel, c.DifficultyBand, c.Skill, ParentKey: (string?)null, Description: (string?)null,
             CefrConfidence: (string?)null, CefrSource: (string?)null, NodeType: (string?)null, RoutingEligible: false)));

    /// <summary>Container-of-container overload (2026-07-30, Adverbs topical hierarchy) — lets a
    /// container declare its own <paramref name="containers"/>' ParentKey, e.g. a topic container
    /// ("Adverbs") parenting subtopic containers ("Adverbs of frequency"). Two passes: first upsert
    /// every container with no parent (so every Key resolves to an Id), then a second pass assigns
    /// parents — only resolves one level of nesting, since that's all this codebase needs today;
    /// deeper chains would need topological ordering instead of two fixed passes.</summary>
    private static async Task<Dictionary<string, Guid>> UpsertContainersAsync(
        LinguaCoachDbContext db,
        IEnumerable<(string Key, string Title, string CefrLevel, int DifficultyBand, string? Skill, string? ParentKey, string? Description)> containers)
        => await UpsertContainersAsync(db, containers.Select(c =>
            (c.Key, c.Title, c.CefrLevel, c.DifficultyBand, c.Skill, c.ParentKey, c.Description,
             CefrConfidence: (string?)null, CefrSource: (string?)null, NodeType: (string?)null, RoutingEligible: false)));

    /// <summary>Provenance overload (Phase GSG-1, 2026-07-31) — carries the audit-derived CEFR
    /// confidence/source/node-type/routing-eligibility classification through to
    /// <see cref="SkillGraphNode.SetProvenanceAndType"/> for every container. Grammar-only today
    /// (the other domains' overloads above default these to null/false).</summary>
    private static async Task<Dictionary<string, Guid>> UpsertContainersAsync(
        LinguaCoachDbContext db,
        IEnumerable<(string Key, string Title, string CefrLevel, int DifficultyBand, string? Skill, string? ParentKey, string? Description,
            string? CefrConfidence, string? CefrSource, string? NodeType, bool RoutingEligible)> containers)
    {
        var list = containers.ToList();
        var result = new Dictionary<string, Guid>();
        foreach (var c in list)
        {
            var id = await UpsertLeafAsync(db, c.Key, c.Title, c.CefrLevel, c.DifficultyBand, c.Skill, parentId: null, description: c.Description,
                cefrConfidence: c.CefrConfidence, cefrSource: c.CefrSource, nodeType: c.NodeType, routingEligible: c.RoutingEligible);
            result[c.Key] = id;
        }

        foreach (var c in list.Where(x => x.ParentKey is not null))
        {
            if (!result.TryGetValue(c.ParentKey!, out var parentId)) continue;
            var node = await db.SkillGraphNodes.FirstAsync(n => n.Id == result[c.Key]);
            if (node.ParentNodeId != parentId) node.AssignParent(parentId);
        }
        await db.SaveChangesAsync();
        return result;
    }

    private static async Task<Guid> UpsertLeafAsync(
        LinguaCoachDbContext db, string key, string title, string cefrLevel, int difficultyBand,
        string? skill, Guid? parentId, string? descriptionForAi = null, string? description = null,
        string? cefrConfidence = null, string? cefrSource = null, string? nodeType = null, bool routingEligible = false,
        string? subskill = null)
    {
        var resolvedDescription = description ?? $"{title}.";
        var existing = await db.SkillGraphNodes.FirstOrDefaultAsync(n => n.Key == key);
        if (existing is not null)
        {
            // Content re-seed (2026-07-30) — UpdateCore refuses to run once a node is Approved (see
            // its own doc comment: reject first to reopen editing), and every seeded node ends up
            // Approved. Without this reject/edit/re-approve round trip, editing a title/description
            // in the seed JSON and re-running the seeder silently had no effect on existing nodes.
            if (existing.Title != title || existing.Description != resolvedDescription
                || existing.CefrLevel != cefrLevel.ToUpperInvariant() || existing.DifficultyBand != difficultyBand
                || existing.Subskill != subskill)
            {
                if (existing.ReviewStatus == AdminReviewStatus.Approved)
                    existing.Reject("Content re-seed", null);
                existing.UpdateCore(title, resolvedDescription, cefrLevel, skill, subskill, difficultyBand, existing.DescriptionForAi);
            }
            if (existing.ReviewStatus != AdminReviewStatus.Approved)
                existing.Approve(null);
            if (parentId.HasValue && existing.ParentNodeId != parentId)
                existing.AssignParent(parentId);
            // Phase GSG-1 (2026-07-31) — provenance/type/routing-eligibility is ungated supplementary
            // metadata (same reasoning as UpdateTags), applied whenever the seed JSON supplies a
            // nodeType. Skipped when nodeType is null so non-grammar domains (which never pass these)
            // and not-yet-backfilled entries don't get stamped with a meaningless default.
            if (nodeType is not null)
            {
                existing.SetProvenanceAndType(
                    ParseCefrConfidence(cefrConfidence), cefrSource, ParseNodeType(nodeType) ?? Domain.Enums.SkillGraphNodeType.Skill,
                    routingEligible);
            }
            await db.SaveChangesAsync();
            return existing.Id;
        }

        var node = new SkillGraphNode(key, title, resolvedDescription, cefrLevel, skill,
            subskill: subskill, difficultyBand: difficultyBand, descriptionForAi: descriptionForAi);
        db.SkillGraphNodes.Add(node);
        await db.SaveChangesAsync(); // assign Id before AssignParent/Approve

        if (parentId.HasValue)
            node.AssignParent(parentId);
        if (nodeType is not null)
        {
            node.SetProvenanceAndType(
                ParseCefrConfidence(cefrConfidence), cefrSource, ParseNodeType(nodeType) ?? Domain.Enums.SkillGraphNodeType.Skill,
                routingEligible);
        }
        node.Approve(null);
        await db.SaveChangesAsync();
        return node.Id;
    }

    /// <summary>Phase GSG-1 (2026-07-31) — parses the seed JSON's plain-string provenance fields
    /// into the domain enum. Defaults to Unknown on anything unrecognized rather than throwing, so a
    /// typo in hand-edited JSON degrades to "not routing eligible" instead of crashing the seeder.</summary>
    private static Domain.Enums.CefrConfidence ParseCefrConfidence(string? s) => s?.Trim().ToLowerInvariant() switch
    {
        "attested" => Domain.Enums.CefrConfidence.Attested,
        "fallback" => Domain.Enums.CefrConfidence.Fallback,
        "inherited" => Domain.Enums.CefrConfidence.Inherited,
        "curated" => Domain.Enums.CefrConfidence.Curated,
        _ => Domain.Enums.CefrConfidence.Unknown,
    };

    private static Domain.Enums.SkillGraphNodeType? ParseNodeType(string? s) => s?.Trim().ToLowerInvariant() switch
    {
        "topic" => Domain.Enums.SkillGraphNodeType.Topic,
        "concept" => Domain.Enums.SkillGraphNodeType.Concept,
        "skill" => Domain.Enums.SkillGraphNodeType.Skill,
        "variant" => Domain.Enums.SkillGraphNodeType.Variant,
        "broadreference" => Domain.Enums.SkillGraphNodeType.BroadReference,
        _ => null,
    };

    private static async Task<CefrResourceSource> GetOrCreateSourceAsync(LinguaCoachDbContext db, string name)
    {
        var existing = await db.CefrResourceSources.FirstOrDefaultAsync(s => s.Name == name);
        if (existing is not null) return existing;

        var source = new CefrResourceSource(name, licenseType: "Educational", allowsStudentDisplay: true, allowsCommercialUse: false);
        db.CefrResourceSources.Add(source);
        await db.SaveChangesAsync();
        return source;
    }
}

/// <summary>Shared per-leaf pipeline: ResourceBankItem -> Lesson -> Exercise -> Module (all via the
/// existing deterministic handlers) -> direct ModuleSkillGraphNodeLink. No AI auto-tagging.</summary>
public sealed class LeafContentSeeder(
    LinguaCoachDbContext db,
    IGenerateLessonFromResourcesHandler lessonHandler,
    IGenerateActivitiesFromLessonHandler activitiesHandler)
{
    public async Task SeedOneAsync(
        Guid sourceId, string cefrLevel, string contentJson, string resourceTitle, Guid skillGraphNodeId,
        string activityType, PublishedResourceType resourceType, IReadOnlyList<Guid>? additionalSkillGraphNodeIds = null)
    {
        // Idempotency guard against process interruption (e.g. a hard-killed run) leaving the local
        // checkpoint file out of sync with the DB — if this leaf's node already has a Module linked,
        // trust the DB over the checkpoint and skip, rather than creating a duplicate content chain.
        var alreadyComplete = await db.ModuleSkillGraphNodeLinks
            .AnyAsync(l => l.SkillGraphNodeId == skillGraphNodeId);
        if (alreadyComplete) return;

        var resource = new ResourceBankItem(resourceType, sourceId, cefrLevel, contentJson);
        db.ResourceBankItems.Add(resource);
        await db.SaveChangesAsync();

        var lessonResult = await lessonHandler.HandleAsync(new GenerateLessonFromResourcesRequest(
            Resources: [new LessonResourceLinkInput(resourceType.ToString(), resource.Id, "Primary")],
            Notes: "Full content reseed (2026-07-29) — bulk-seeded via ContentSeeder tool, deterministic composer."));
        var lesson = await db.Lessons.FirstAsync(l => l.Id == lessonResult.Lesson.Id);
        lesson.Approve(null, "Bulk-seeded — deterministic composer, auto-approved.");

        var activitiesResult = await activitiesHandler.HandleAsync(new GenerateActivitiesFromLessonRequest(
            LessonId: lesson.Id,
            Specs: [new ActivityGenerationSpec(activityType, 1)],
            Notes: "Full content reseed (2026-07-29) — bulk-seeded via ContentSeeder tool, deterministic composer."));

        foreach (var activity in activitiesResult.Activities)
        {
            var exercise = await db.Exercises.FirstAsync(e => e.Id == activity.Id);
            exercise.Approve(null, "Bulk-seeded — deterministic composer, auto-approved.");
        }

        var module = await db.Modules.FirstAsync(m => m.Id == activitiesResult.ModuleId);
        module.Approve(null, "Bulk-seeded — deterministic composer, auto-approved.");
        await db.SaveChangesAsync();

        var alreadyLinked = await db.ModuleSkillGraphNodeLinks
            .AnyAsync(l => l.ModuleId == module.Id && l.SkillGraphNodeId == skillGraphNodeId);
        if (!alreadyLinked)
        {
            db.ModuleSkillGraphNodeLinks.Add(new ModuleSkillGraphNodeLink(module.Id, skillGraphNodeId, confidence: null));
            await db.SaveChangesAsync();
        }

        foreach (var additionalId in additionalSkillGraphNodeIds ?? [])
        {
            var alreadyLinkedAdditional = await db.ModuleSkillGraphNodeLinks
                .AnyAsync(l => l.ModuleId == module.Id && l.SkillGraphNodeId == additionalId);
            if (!alreadyLinkedAdditional)
            {
                db.ModuleSkillGraphNodeLinks.Add(new ModuleSkillGraphNodeLink(module.Id, additionalId, confidence: null));
                await db.SaveChangesAsync();
            }
        }
    }
}

// ── Seed JSON shapes (Plan Phase C) ──────────────────────────────────────────────────────────────

public sealed record GrammarSeedFile(
    int Version, List<GrammarSeedContainer> Containers, List<GrammarSeedLeaf> Leaves, List<string>? VersionNotes = null);
public sealed record GrammarSeedContainer(
    string Key, string Title, string CefrLevel, int DifficultyBand = 1, string? ParentKey = null, string? Description = null,
    string? CefrConfidence = null, string? CefrSource = null, string? NodeType = null, bool RoutingEligible = false);
/// <summary>Curated-content shape (2026-07-31 rebuild) — <see cref="LessonBody"/>/<see cref="Examples"/>/
/// <see cref="CommonMistakes"/>/<see cref="Exercises"/> carry full original lesson + multi-question
/// practice content, seeded via <see cref="RichContentSeeder"/> rather than the single-generic-exercise
/// <see cref="LeafContentSeeder"/> path (retired for this domain — no CEFR-J CSV import runs today).</summary>
public sealed record GrammarSeedLeaf(
    string Key, string Title, string CefrLevel, int DifficultyBand, string? ParentKey,
    string LessonBody, List<string>? Examples, List<string>? CommonMistakes, List<SeedExercise> Exercises,
    string? Description = null, string? Subskill = null,
    string? CefrConfidence = null, string? CefrSource = null, string? NodeType = null, bool RoutingEligible = false,
    string? ImageUrl = null);

public sealed record VocabularySeedFile(int Version, List<VocabularySeedContainer> Containers, List<VocabularySeedLeaf> Leaves, List<string>? VersionNotes = null);
public sealed record VocabularySeedContainer(string Key, string Title, string CefrLevel, string? ParentKey = null);
/// <summary>Curated-content shape (2026-07-31 rebuild) — see <see cref="GrammarSeedLeaf"/>'s doc
/// comment; same reasoning applies here (a leaf is a topic-scoped vocabulary set, e.g. "Numbers
/// 0-10," not a single CEFR-J headword row).</summary>
public sealed record VocabularySeedLeaf(
    string Key, string Title, string CefrLevel, int DifficultyBand, string? ParentKey,
    string LessonBody, List<string>? Examples, List<string>? CommonMistakes, List<SeedExercise> Exercises,
    string? Subskill = null, string? ImageUrl = null);

public sealed record PronunciationSeedFile(int Version, List<PronunciationSeedContainer> Containers, List<PronunciationSeedLeaf> Leaves, List<string>? VersionNotes = null);
public sealed record PronunciationSeedContainer(string Key, string Title, string CefrLevel, string? ParentKey = null);
public sealed record PronunciationSeedLeaf(
    string Key, string Title, string CefrLevel, int DifficultyBand, string? ParentKey,
    string LessonBody, List<string>? Examples, List<string>? CommonMistakes, List<SeedExercise> Exercises,
    string? ImageUrl = null);

/// <summary>Fixed/functional social-language items ("introducing yourself," "spelling and the
/// alphabet") — classified under Speaking (2026-07-31 container/leaf redesign), not Vocabulary.
/// A genuinely new domain (no prior seed file covered this content shape).</summary>
public sealed record FunctionalLanguageSeedFile(int Version, List<FunctionalLanguageSeedContainer> Containers, List<FunctionalLanguageSeedLeaf> Leaves, List<string>? VersionNotes = null);
public sealed record FunctionalLanguageSeedContainer(string Key, string Title, string CefrLevel, string? ParentKey = null);
public sealed record FunctionalLanguageSeedLeaf(
    string Key, string Title, string CefrLevel, int DifficultyBand, string? ParentKey,
    string LessonBody, List<string>? Examples, List<string>? CommonMistakes, List<SeedExercise> Exercises,
    string? Subskill = null, string? ImageUrl = null);

/// <summary>2026-08-04 — curated Reading-comprehension content, same shape/rationale as
/// Pronunciation/FunctionalLanguage above. CLI domain keyword is "reading-comprehension," not
/// "reading" — that name is already taken by the legacy CEFR-J-CSV-driven <c>SeedReadingAsync</c>
/// path, untouched and unrelated to this curated pipeline.</summary>
public sealed record ReadingSeedFile(int Version, List<ReadingSeedContainer> Containers, List<ReadingSeedLeaf> Leaves, List<string>? VersionNotes = null);
public sealed record ReadingSeedContainer(string Key, string Title, string CefrLevel, string? ParentKey = null);
public sealed record ReadingSeedLeaf(
    string Key, string Title, string CefrLevel, int DifficultyBand, string? ParentKey,
    string LessonBody, List<string>? Examples, List<string>? CommonMistakes, List<SeedExercise> Exercises,
    string? Subskill = null, string? ImageUrl = null);

/// <summary>2026-08-04 — curated Writing content, same shape/rationale as the domains above. No
/// prior "writing" CLI domain exists, so no naming collision.</summary>
public sealed record WritingSeedFile(int Version, List<WritingSeedContainer> Containers, List<WritingSeedLeaf> Leaves, List<string>? VersionNotes = null);
public sealed record WritingSeedContainer(string Key, string Title, string CefrLevel, string? ParentKey = null);
public sealed record WritingSeedLeaf(
    string Key, string Title, string CefrLevel, int DifficultyBand, string? ParentKey,
    string LessonBody, List<string>? Examples, List<string>? CommonMistakes, List<SeedExercise> Exercises,
    string? Subskill = null, string? ImageUrl = null);

/// <summary>2026-08-04 — curated Listening-comprehension content (text-transcript based — no
/// audio/video is ever bundled, per the seeding rules' copyright exclusion). CLI domain keyword
/// is "listening-comprehension," not "listening" — that name is already taken by the legacy
/// CEFR-J-CSV-driven <c>SeedListeningAsync</c> path (which also does real TTS audio generation,
/// untouched and unrelated to this curated pipeline).</summary>
public sealed record ListeningComprehensionSeedFile(int Version, List<ListeningComprehensionSeedContainer> Containers, List<ListeningComprehensionSeedLeaf> Leaves, List<string>? VersionNotes = null);
public sealed record ListeningComprehensionSeedContainer(string Key, string Title, string CefrLevel, string? ParentKey = null);
public sealed record ListeningComprehensionSeedLeaf(
    string Key, string Title, string CefrLevel, int DifficultyBand, string? ParentKey,
    string LessonBody, List<string>? Examples, List<string>? CommonMistakes, List<SeedExercise> Exercises,
    string? Subskill = null, string? ImageUrl = null);

public sealed record CefrScalesSeedFile(List<CefrScaleSeedContainer> Containers, List<CefrScaleSeedLeaf> Leaves);
public sealed record CefrScaleSeedContainer(string Key, string Title, string CefrLevel, string Skill);
public sealed record CefrScaleSeedLeaf(
    string Key, string Title, string CefrLevel, string Skill, string ParentKey, string Descriptor);

public sealed record SpeakingSeedFile(List<SpeakingSeedContainer> Containers, List<SpeakingSeedLeaf> Leaves);
public sealed record SpeakingSeedContainer(string Key, string Title, string CefrLevel);
public sealed record SpeakingSeedLeaf(
    string Key, string Title, string PromptText, int SuggestedDurationSeconds, string CefrLevel,
    string ParentKey, string ActivityType, string ScaleLeafKey);

public sealed record ListeningSeedFile(List<ListeningSeedContainer> Containers, List<ListeningSeedLeaf> Leaves);
public sealed record ListeningSeedContainer(string Key, string Title, string CefrLevel);
public sealed record ListeningSeedLeaf(
    string Key, string Title, string Transcript, string CefrLevel,
    string ParentKey, string ActivityType, string ScaleLeafKey);

public sealed record PrerequisiteEdgeSeed(string Node, string Prerequisite, string Reason);

/// <summary>Resumability — a processed-keys file next to each input, so a crashed/interrupted run
/// doesn't redo already-seeded items. Mirrors the pattern used earlier this session for the
/// vocabulary categorization batches.</summary>
public sealed class Checkpoint
{
    private readonly HashSet<string> _processedKeys;
    private readonly string _checkpointPath;

    private Checkpoint(HashSet<string> processedKeys, string checkpointPath)
    {
        _processedKeys = processedKeys;
        _checkpointPath = checkpointPath;
    }

    public static Checkpoint Load(string seedFilePath)
    {
        var checkpointPath = seedFilePath + ".checkpoint.json";
        var keys = File.Exists(checkpointPath)
            ? JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(checkpointPath)) ?? []
            : [];
        return new Checkpoint(keys, checkpointPath);
    }

    public bool Contains(string key) => _processedKeys.Contains(key);
    public void MarkProcessed(string key) => _processedKeys.Add(key);
    public void Save(string _) => File.WriteAllText(_checkpointPath, JsonSerializer.Serialize(_processedKeys));
}

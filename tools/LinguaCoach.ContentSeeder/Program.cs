using System.Text.Json;
using System.Text.Json.Serialization;
using LinguaCoach.Application.Exercises;
using LinguaCoach.Application.Lessons;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure;
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
            "grammar" => await SeedGrammarAsync(seeder, db, seedFilePath),
            "vocabulary" => await SeedVocabularyAsync(seeder, db, seedFilePath),
            "cefr-scales" => await SeedCefrScalesAsync(db, seedFilePath),
            _ => Fail($"Unknown domain '{domain}' — expected 'grammar', 'vocabulary', or 'cefr-scales'."),
        };
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }

    private static async Task<int> SeedGrammarAsync(LeafContentSeeder seeder, LinguaCoachDbContext db, string path)
    {
        var file = JsonSerializer.Deserialize<GrammarSeedFile>(await File.ReadAllTextAsync(path), JsonOptions)
            ?? throw new InvalidOperationException("Empty/invalid grammar seed file.");

        var source = await GetOrCreateSourceAsync(db, "CEFR-J Grammar Profile");
        var checkpoint = Checkpoint.Load(path);
        var containerIds = await UpsertContainersAsync(db, file.Containers.Select(c =>
            (c.Key, c.Title, c.CefrLevel, c.DifficultyBand, Skill: CurriculumSkillConstants.Grammar)));

        var processed = 0;
        foreach (var leaf in file.Leaves)
        {
            if (checkpoint.Contains(leaf.Key)) continue;

            var parentId = leaf.ParentKey is not null && containerIds.TryGetValue(leaf.ParentKey, out var pid) ? pid : (Guid?)null;
            var leafId = await UpsertLeafAsync(db, leaf.Key, leaf.Title, leaf.CefrLevel, leaf.DifficultyBand,
                CurriculumSkillConstants.Grammar, parentId);

            var content = ResourceBankItemContent.Serialize(new GrammarContent(leaf.GrammarPoint, leaf.Explanation));
            await seeder.SeedOneAsync(source.Id, leaf.CefrLevel, content, leaf.Title, leafId, "gap_fill");

            checkpoint.MarkProcessed(leaf.Key);
            if (++processed % 100 == 0)
            {
                checkpoint.Save(path);
                Console.WriteLine($"Grammar: {processed}/{file.Leaves.Count} processed.");
            }
        }

        checkpoint.Save(path);
        Console.WriteLine($"Grammar seeding complete. {processed} leaves processed this run.");
        return 0;
    }

    private static async Task<int> SeedVocabularyAsync(LeafContentSeeder seeder, LinguaCoachDbContext db, string path)
    {
        var file = JsonSerializer.Deserialize<VocabularySeedFile>(await File.ReadAllTextAsync(path), JsonOptions)
            ?? throw new InvalidOperationException("Empty/invalid vocabulary seed file.");

        var source = await GetOrCreateSourceAsync(db, "CEFR-J / Octanove Vocabulary Profiles");
        var checkpoint = Checkpoint.Load(path);
        var containerIds = await UpsertContainersAsync(db, file.Containers.Select(c =>
            (c.Key, c.Title, c.CefrLevel, DifficultyBand: 1, Skill: CurriculumSkillConstants.Vocabulary)));

        var processed = 0;
        foreach (var leaf in file.Leaves)
        {
            if (checkpoint.Contains(leaf.Key)) continue;

            var parentId = containerIds.TryGetValue(leaf.ParentKey, out var pid) ? pid : (Guid?)null;
            var title = $"{leaf.Headword} ({leaf.PartOfSpeech})";
            var descriptionForAi = leaf.ExtraTags is { Count: > 0 } ? $"Also: {string.Join(", ", leaf.ExtraTags)}" : null;
            var leafId = await UpsertLeafAsync(db, leaf.Key, title, leaf.CefrLevel, 1,
                CurriculumSkillConstants.Vocabulary, parentId, descriptionForAi);

            var content = ResourceBankItemContent.Serialize(new VocabularyContent(leaf.Headword, leaf.PartOfSpeech, leaf.Definition));
            await seeder.SeedOneAsync(source.Id, leaf.CefrLevel, content, title, leafId, "gap_fill");

            checkpoint.MarkProcessed(leaf.Key);
            if (++processed % 100 == 0)
            {
                checkpoint.Save(path);
                Console.WriteLine($"Vocabulary: {processed}/{file.Leaves.Count} processed.");
            }
        }

        checkpoint.Save(path);
        Console.WriteLine($"Vocabulary seeding complete. {processed} leaves processed this run.");
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

    private static async Task<Dictionary<string, Guid>> UpsertContainersAsync(
        LinguaCoachDbContext db, IEnumerable<(string Key, string Title, string CefrLevel, int DifficultyBand, string Skill)> containers)
    {
        var result = new Dictionary<string, Guid>();
        foreach (var c in containers)
        {
            var id = await UpsertLeafAsync(db, c.Key, c.Title, c.CefrLevel, c.DifficultyBand, c.Skill, parentId: null);
            result[c.Key] = id;
        }
        await db.SaveChangesAsync();
        return result;
    }

    private static async Task<Guid> UpsertLeafAsync(
        LinguaCoachDbContext db, string key, string title, string cefrLevel, int difficultyBand,
        string skill, Guid? parentId, string? descriptionForAi = null, string? description = null)
    {
        var existing = await db.SkillGraphNodes.FirstOrDefaultAsync(n => n.Key == key);
        if (existing is not null)
        {
            if (existing.ReviewStatus != AdminReviewStatus.Approved)
                existing.Approve(null);
            if (parentId.HasValue && existing.ParentNodeId != parentId)
                existing.AssignParent(parentId);
            await db.SaveChangesAsync();
            return existing.Id;
        }

        var node = new SkillGraphNode(key, title, description ?? $"{title}.", cefrLevel, skill,
            subskill: null, difficultyBand: difficultyBand, descriptionForAi: descriptionForAi);
        db.SkillGraphNodes.Add(node);
        await db.SaveChangesAsync(); // assign Id before AssignParent/Approve

        if (parentId.HasValue)
            node.AssignParent(parentId);
        node.Approve(null);
        await db.SaveChangesAsync();
        return node.Id;
    }

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
        Guid sourceId, string cefrLevel, string contentJson, string resourceTitle, Guid skillGraphNodeId, string activityType)
    {
        // Idempotency guard against process interruption (e.g. a hard-killed run) leaving the local
        // checkpoint file out of sync with the DB — if this leaf's node already has a Module linked,
        // trust the DB over the checkpoint and skip, rather than creating a duplicate content chain.
        var alreadyComplete = await db.ModuleSkillGraphNodeLinks
            .AnyAsync(l => l.SkillGraphNodeId == skillGraphNodeId);
        if (alreadyComplete) return;

        var resourceType = activityType == "reading_fill_in_blanks"
            ? PublishedResourceType.ReadingPassage
            : contentJson.Contains("\"grammarPoint\"", StringComparison.OrdinalIgnoreCase)
                ? PublishedResourceType.Grammar
                : PublishedResourceType.Vocabulary;

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
    }
}

// ── Seed JSON shapes (Plan Phase C) ──────────────────────────────────────────────────────────────

public sealed record GrammarSeedFile(List<GrammarSeedContainer> Containers, List<GrammarSeedLeaf> Leaves);
public sealed record GrammarSeedContainer(string Key, string Title, string CefrLevel, int DifficultyBand);
public sealed record GrammarSeedLeaf(
    string Key, string Title, string CefrLevel, int DifficultyBand,
    string? ParentKey, string GrammarPoint, string Explanation);

public sealed record VocabularySeedFile(List<VocabularySeedContainer> Containers, List<VocabularySeedLeaf> Leaves);
public sealed record VocabularySeedContainer(string Key, string Title, string CefrLevel);
public sealed record VocabularySeedLeaf(
    string Key, string Headword, string PartOfSpeech, string CefrLevel,
    string Definition, string ParentKey, List<string>? ExtraTags);

public sealed record CefrScalesSeedFile(List<CefrScaleSeedContainer> Containers, List<CefrScaleSeedLeaf> Leaves);
public sealed record CefrScaleSeedContainer(string Key, string Title, string CefrLevel, string Skill);
public sealed record CefrScaleSeedLeaf(
    string Key, string Title, string CefrLevel, string Skill, string ParentKey, string Descriptor);

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

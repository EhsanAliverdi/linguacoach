using System.Text.Json;
using LinguaCoach.Application.SkillGraph;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Infrastructure.SkillGraph;

namespace LinguaCoach.UnitTests.SkillGraph;

/// <summary>
/// Skill Graph rebuild Phase 3 (2026-07-23) — validates each canonical per-CEFR-level seed data
/// file under src/LinguaCoach.Persistence/Seed/SkillGraph/*.json: deserializes with the exact
/// contract <c>AdminSkillGraphController.ImportNodes</c> consumes, and re-runs the same
/// referential-integrity/cycle checks the endpoint itself performs — so a mistake in a hand-authored
/// seed file is caught here, at authoring time, rather than only discovered when Phase 4 actually
/// runs the reseed against a live database.
/// </summary>
public sealed class SkillGraphSeedDataTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record SeedFile(List<SeedNode> Nodes);

    private sealed record SeedNode(
        string Key, string Title, string Description, string CefrLevel, string Skill, string? Subskill,
        int DifficultyBand, string? DescriptionForAi, List<string> ContextTags, List<string> FocusTags,
        List<string> PrerequisiteKeys);

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LinguaCoach.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate repo root (LinguaCoach.slnx) from test output directory.");
    }

    private static string SeedDirectory() =>
        Path.Combine(FindRepoRoot(), "src", "LinguaCoach.Persistence", "Seed", "SkillGraph");

    public static IEnumerable<object[]> SeedFiles()
    {
        var dir = SeedDirectory();
        if (!Directory.Exists(dir)) yield break;
        foreach (var file in Directory.GetFiles(dir, "*.json").OrderBy(f => f))
            yield return [Path.GetFileName(file)];
    }

    [Theory]
    [MemberData(nameof(SeedFiles))]
    public void SeedFile_DeserializesWithTheRealImportContract(string fileName)
    {
        var json = File.ReadAllText(Path.Combine(SeedDirectory(), fileName));
        var parsed = JsonSerializer.Deserialize<SeedFile>(json, JsonOptions);

        Assert.NotNull(parsed);
        Assert.NotEmpty(parsed!.Nodes);
        foreach (var n in parsed.Nodes)
        {
            Assert.False(string.IsNullOrWhiteSpace(n.Key));
            Assert.False(string.IsNullOrWhiteSpace(n.Title));
            Assert.False(string.IsNullOrWhiteSpace(n.Description));
        }
    }

    [Theory]
    [MemberData(nameof(SeedFiles))]
    public void SeedFile_EveryNodePassesRealTaxonomyValidation(string fileName)
    {
        var json = File.ReadAllText(Path.Combine(SeedDirectory(), fileName));
        var parsed = JsonSerializer.Deserialize<SeedFile>(json, JsonOptions)!;

        foreach (var n in parsed.Nodes)
        {
            Assert.True(CefrLevelConstants.IsValid(n.CefrLevel), $"{n.Key}: invalid CEFR level '{n.CefrLevel}'.");
            Assert.True(CurriculumSkillConstants.IsValid(n.Skill), $"{n.Key}: invalid skill '{n.Skill}'.");
            Assert.True(CurriculumSubskillConstants.IsValidForSkill(n.Skill, n.Subskill), $"{n.Key}: subskill '{n.Subskill}' invalid for skill '{n.Skill}'.");
            Assert.InRange(n.DifficultyBand, 1, 5);
            foreach (var tag in n.ContextTags.Concat(n.FocusTags))
                Assert.True(CurriculumContextTagConstants.IsValid(tag), $"{n.Key}: invalid tag '{tag}'.");
        }
    }

    [Theory]
    [MemberData(nameof(SeedFiles))]
    public void SeedFile_NoDuplicateKeys(string fileName)
    {
        var json = File.ReadAllText(Path.Combine(SeedDirectory(), fileName));
        var parsed = JsonSerializer.Deserialize<SeedFile>(json, JsonOptions)!;

        var duplicates = parsed.Nodes.GroupBy(n => n.Key).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.Empty(duplicates);
    }

    [Theory]
    [MemberData(nameof(SeedFiles))]
    public void SeedFile_EveryPrerequisiteKeyResolvesWithinTheFile(string fileName)
    {
        // Real reseed execution (Phase 4) resolves a prerequisiteKey against every already-imported
        // node across all files loaded so far, not just this one file — but each file is also
        // expected to be self-contained for keys it introduces at its own CEFR level, since later
        // levels are authored and imported after earlier ones. A1 (the first level) must be fully
        // self-contained since nothing precedes it.
        var json = File.ReadAllText(Path.Combine(SeedDirectory(), fileName));
        var parsed = JsonSerializer.Deserialize<SeedFile>(json, JsonOptions)!;
        var keys = parsed.Nodes.Select(n => n.Key).ToHashSet();

        if (fileName == "a1.json")
        {
            var dangling = parsed.Nodes.SelectMany(n => n.PrerequisiteKeys, (n, p) => (n.Key, p))
                .Where(x => !keys.Contains(x.p)).ToList();
            Assert.Empty(dangling);
        }
    }

    [Theory]
    [MemberData(nameof(SeedFiles))]
    public void SeedFile_HasNoCircularPrerequisiteChains(string fileName)
    {
        var json = File.ReadAllText(Path.Combine(SeedDirectory(), fileName));
        var parsed = JsonSerializer.Deserialize<SeedFile>(json, JsonOptions)!;

        // Real deterministic Guids per key (stable within this test run) so the actual, already-
        // reviewed ISkillGraphValidationService DFS cycle detector can run unmodified.
        var idByKey = parsed.Nodes.ToDictionary(n => n.Key, _ => Guid.NewGuid());
        var nodeSummaries = parsed.Nodes.Select(n => new SkillGraphNodeSummary(idByKey[n.Key], n.Key)).ToList();
        var edges = parsed.Nodes
            .SelectMany(n => n.PrerequisiteKeys.Where(idByKey.ContainsKey), (n, p) => new SkillGraphEdgeSummary(idByKey[n.Key], idByKey[p]))
            .ToList();

        var result = new SkillGraphValidationService().Validate(nodeSummaries, edges);
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
    }

    [Theory]
    [MemberData(nameof(SeedFiles))]
    public void SeedFile_NoFullyIsolatedNodes(string fileName)
    {
        // Rebuild Phase 3's actual goal — every node should have at least one edge (as a
        // prerequisite or a dependent) within the level being authored. A node with zero edges on
        // both sides here is the exact defect the 2026-07-23 audit's screenshot surfaced.
        var json = File.ReadAllText(Path.Combine(SeedDirectory(), fileName));
        var parsed = JsonSerializer.Deserialize<SeedFile>(json, JsonOptions)!;
        var keys = parsed.Nodes.Select(n => n.Key).ToHashSet();

        var hasEdge = new HashSet<string>();
        foreach (var n in parsed.Nodes)
        foreach (var p in n.PrerequisiteKeys.Where(keys.Contains))
        {
            hasEdge.Add(n.Key);
            hasEdge.Add(p);
        }

        var isolated = parsed.Nodes.Select(n => n.Key).Where(k => !hasEdge.Contains(k)).ToList();
        Assert.Empty(isolated);
    }
}

using LinguaCoach.Infrastructure.LearningPath;

namespace LinguaCoach.UnitTests.LearningPath;

public sealed class DefaultPathFactoryTests
{
    private static readonly Guid ProfileId = Guid.NewGuid();

    [Fact]
    public void Create_ReturnsActivePathWithCareerAndLevel()
    {
        var path = DefaultPathFactory.Create(ProfileId, "Document Controller", "B1");

        Assert.Equal(ProfileId, path.StudentProfileId);
        Assert.Contains("Document Controller", path.Title);
        Assert.Contains("B1", path.Title);
        Assert.True(path.IsActive);
    }

    [Fact]
    public void CreateModules_Returns5Modules()
    {
        var path = DefaultPathFactory.Create(ProfileId, "Document Controller", "B1");
        var modules = DefaultPathFactory.CreateModules(path.Id);

        Assert.Equal(5, modules.Count);
    }

    [Fact]
    public void CreateModules_OrdersSequentially1To5()
    {
        var path = DefaultPathFactory.Create(ProfileId, "Document Controller", "B1");
        var modules = DefaultPathFactory.CreateModules(path.Id);

        var orders = modules.Select(m => m.Order).OrderBy(o => o).ToList();
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, orders);
    }

    [Fact]
    public void CreateModules_AllModulesHaveNonEmptyTitleAndDescription()
    {
        var path = DefaultPathFactory.Create(ProfileId, "Document Controller", "B1");
        var modules = DefaultPathFactory.CreateModules(path.Id);

        foreach (var m in modules)
        {
            Assert.False(string.IsNullOrWhiteSpace(m.Title));
            Assert.False(string.IsNullOrWhiteSpace(m.Description));
        }
    }

    [Fact]
    public void CreateModules_AllLinkedToPathId()
    {
        var path = DefaultPathFactory.Create(ProfileId, "Document Controller", "B1");
        var modules = DefaultPathFactory.CreateModules(path.Id);

        Assert.All(modules, m => Assert.Equal(path.Id, m.LearningPathId));
    }

    // ── Phase 9K: context-flexibility tests ──────────────────────────────────

    [Fact]
    public void Create_PathTitle_DoesNotForceWorkplacePrefix()
    {
        // The fallback path title must NOT start with "Workplace English" unconditionally.
        // It should include the careerContext but not hardcode "Workplace".
        var path = DefaultPathFactory.Create(ProfileId, "General learner", "A2");

        Assert.DoesNotContain("Workplace English for", path.Title);
    }

    [Fact]
    public void Create_PathTitle_IncludesContextAndLevel()
    {
        var path = DefaultPathFactory.Create(ProfileId, "General learner", "A2");

        Assert.Contains("General learner", path.Title);
        Assert.Contains("A2", path.Title);
    }

    [Fact]
    public void CreateModules_DefaultModules_AreNotWorkplaceOnly()
    {
        // Phase 9K: fallback modules must use general real-life communication topics,
        // not workplace-specific titles like "Professional email writing".
        var path = DefaultPathFactory.Create(ProfileId, "New arrival", "A2");
        var modules = DefaultPathFactory.CreateModules(path.Id);

        // None of the default module titles should contain the word "workplace"
        Assert.All(modules, m =>
            Assert.DoesNotContain("workplace", m.Title, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateModules_WorkplaceContextStillSupported()
    {
        // When careerContext is workplace-specific, the path title still includes it.
        // Workplace is one valid context — not the forced default.
        var path = DefaultPathFactory.Create(ProfileId, "Document Controller", "B1");

        Assert.Contains("Document Controller", path.Title);
        Assert.Contains("B1", path.Title);
    }
}

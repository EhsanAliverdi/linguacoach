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
}

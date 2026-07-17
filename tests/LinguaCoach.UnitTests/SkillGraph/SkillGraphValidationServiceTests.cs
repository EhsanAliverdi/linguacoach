using LinguaCoach.Application.SkillGraph;
using LinguaCoach.Infrastructure.SkillGraph;

namespace LinguaCoach.UnitTests.SkillGraph;

public sealed class SkillGraphValidationServiceTests
{
    private readonly SkillGraphValidationService _sut = new();

    [Fact]
    public void Validate_NoIssues_ReturnsValid()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var nodes = new[] { new SkillGraphNodeSummary(a, "a"), new SkillGraphNodeSummary(b, "b") };
        var edges = new[] { new SkillGraphEdgeSummary(b, a) }; // b requires a — no cycle

        var result = _sut.Validate(nodes, edges);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_DuplicateKey_ReturnsError()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var nodes = new[] { new SkillGraphNodeSummary(a, "same_key"), new SkillGraphNodeSummary(b, "same_key") };

        var result = _sut.Validate(nodes, []);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == SkillGraphValidationCodes.DuplicateKey);
    }

    [Fact]
    public void Validate_DirectCycle_ReturnsError()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var nodes = new[] { new SkillGraphNodeSummary(a, "a"), new SkillGraphNodeSummary(b, "b") };
        var edges = new[]
        {
            new SkillGraphEdgeSummary(a, b), // a requires b
            new SkillGraphEdgeSummary(b, a), // b requires a — cycle
        };

        var result = _sut.Validate(nodes, edges);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == SkillGraphValidationCodes.PrereqCircular);
    }

    [Fact]
    public void Validate_IndirectThreeNodeCycle_ReturnsError()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var nodes = new[]
        {
            new SkillGraphNodeSummary(a, "a"), new SkillGraphNodeSummary(b, "b"), new SkillGraphNodeSummary(c, "c"),
        };
        var edges = new[]
        {
            new SkillGraphEdgeSummary(a, b),
            new SkillGraphEdgeSummary(b, c),
            new SkillGraphEdgeSummary(c, a), // closes the cycle a→b→c→a
        };

        var result = _sut.Validate(nodes, edges);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == SkillGraphValidationCodes.PrereqCircular);
    }

    [Fact]
    public void Validate_EdgeReferencingNodeOutsideSet_IsIgnored()
    {
        var a = Guid.NewGuid();
        var outside = Guid.NewGuid();
        var nodes = new[] { new SkillGraphNodeSummary(a, "a") };
        var edges = new[] { new SkillGraphEdgeSummary(a, outside) };

        var result = _sut.Validate(nodes, edges);

        Assert.True(result.IsValid);
    }
}

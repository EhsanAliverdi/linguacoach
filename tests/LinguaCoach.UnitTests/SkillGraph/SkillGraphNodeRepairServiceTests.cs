using FluentAssertions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Infrastructure.AdminRepair;
using LinguaCoach.Infrastructure.SkillGraph;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.UnitTests.SkillGraph;

/// <summary>
/// Sprint 14.1 — covers the deterministic diagnose/summary/list paths of
/// SkillGraphNodeRepairService (no AI call involved). The AI-calling RepairAsync/RepairAllAsync
/// paths reuse the same shared AdminRepairFieldGenerator every other repair service (Module/
/// Lesson/Exercise/ResourceBank) already uses — none of those have dedicated AI-mocked unit tests
/// either, since the generator itself is the reusable, already-integration-tested piece.
/// </summary>
public sealed class SkillGraphNodeRepairServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly SkillGraphNodeRepairService _sut;

    public SkillGraphNodeRepairServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        // AdminRepairFieldGenerator is never exercised by these tests (no repair call), so a
        // real instance with null AI dependencies is fine — same pattern would break only if
        // RepairAsync were called, which none of these tests do.
        _sut = new SkillGraphNodeRepairService(_db, null!);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private SkillGraphNode SeedNode(string title, string? contextTagsJson = null, string? focusTagsJson = null)
    {
        var node = new SkillGraphNode(
            $"grammar.{title.ToLowerInvariant().Replace(' ', '_')}.a1", title, "A description.", "A1", "grammar",
            contextTagsJson: contextTagsJson, focusTagsJson: focusTagsJson);
        _db.SkillGraphNodes.Add(node);
        _db.SaveChanges();
        return node;
    }

    [Fact]
    public async Task DiagnoseAsync_flags_missing_context_and_focus_tags()
    {
        var node = SeedNode("Present simple");

        var issues = await _sut.DiagnoseAsync(node.Id);

        issues.Should().Contain(i => i.Code == "missing_context_tags" && i.AutoFixable);
        issues.Should().Contain(i => i.Code == "missing_focus_tags" && i.AutoFixable);
    }

    [Fact]
    public async Task DiagnoseAsync_reports_no_issues_when_both_tag_arrays_are_populated()
    {
        var node = SeedNode("Present simple", contextTagsJson: "[\"workplace\"]", focusTagsJson: "[\"pronunciation\"]");

        var issues = await _sut.DiagnoseAsync(node.Id);

        issues.Should().BeEmpty();
    }

    [Fact]
    public async Task DiagnoseAsync_treats_an_empty_JSON_array_as_missing()
    {
        var node = SeedNode("Present simple", contextTagsJson: "[]", focusTagsJson: "[]");

        var issues = await _sut.DiagnoseAsync(node.Id);

        issues.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetIssuesSummaryAsync_counts_only_active_nodes_with_at_least_one_missing_tag_set()
    {
        SeedNode("Untagged one");
        SeedNode("Untagged two");
        SeedNode("Fully tagged", contextTagsJson: "[\"workplace\"]", focusTagsJson: "[\"pronunciation\"]");

        var summary = await _sut.GetIssuesSummaryAsync();

        summary.TotalItems.Should().Be(3);
        summary.ItemsWithIssues.Should().Be(2);
    }

    [Fact]
    public async Task ListWithIssuesAsync_returns_only_the_nodes_missing_tags()
    {
        var untagged = SeedNode("Untagged one");
        SeedNode("Fully tagged", contextTagsJson: "[\"workplace\"]", focusTagsJson: "[\"pronunciation\"]");

        var result = await _sut.ListWithIssuesAsync();

        result.Should().ContainSingle().Which.Id.Should().Be(untagged.Id);
    }

    [Fact]
    public async Task DiagnoseAsync_throws_for_a_node_that_does_not_exist()
    {
        var act = async () => await _sut.DiagnoseAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}

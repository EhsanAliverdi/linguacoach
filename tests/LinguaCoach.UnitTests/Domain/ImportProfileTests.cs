using FluentAssertions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using Xunit;

namespace LinguaCoach.UnitTests.Domain;

/// <summary>
/// Mandatory Import Execution Plan addendum (2026-07-15) — no material AI/STT/TTS/background
/// processing may begin until a plan reaches Approved. These tests pin the state machine that
/// enforces that gate.
/// </summary>
public sealed class ImportProfileTests
{
    private static ImportProfile CreateDraft() =>
        new(Guid.NewGuid(), version: 1, profileJson: "{}", sampleAssetIds: Array.Empty<Guid>(),
            estimatedCandidateCount: 10, createdAtUtc: DateTimeOffset.UtcNow);

    [Fact]
    public void New_plan_starts_as_Draft()
    {
        CreateDraft().Status.Should().Be(ImportProfileStatus.Draft);
    }

    [Fact]
    public void Version_greater_than_one_requires_a_change_reason()
    {
        var act = () => new ImportProfile(
            Guid.NewGuid(), version: 2, profileJson: "{}", sampleAssetIds: Array.Empty<Guid>(),
            estimatedCandidateCount: 10, createdAtUtc: DateTimeOffset.UtcNow, changeReason: null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Cannot_approve_a_plan_that_has_not_been_submitted_for_approval()
    {
        var plan = CreateDraft();

        var act = () => plan.Approve(Guid.NewGuid(), DateTimeOffset.UtcNow, approvedCostCeiling: 50m);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Approve_requires_going_through_SubmitForApproval_first()
    {
        var plan = CreateDraft();
        plan.SubmitForApproval();

        plan.Approve(Guid.NewGuid(), DateTimeOffset.UtcNow, approvedCostCeiling: 50m);

        plan.Status.Should().Be(ImportProfileStatus.Approved);
        plan.ApprovedCostCeiling.Should().Be(50m);
    }

    [Fact]
    public void Reject_requires_a_reason()
    {
        var plan = CreateDraft();
        plan.SubmitForApproval();

        var act = () => plan.Reject(Guid.NewGuid(), DateTimeOffset.UtcNow, reason: "");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Rejected_plan_cannot_start_executing()
    {
        var plan = CreateDraft();
        plan.SubmitForApproval();
        plan.Reject(Guid.NewGuid(), DateTimeOffset.UtcNow, "Cost too high.");

        var act = () => plan.MarkExecuting();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cannot_execute_a_plan_that_has_not_been_approved()
    {
        var plan = CreateDraft();

        var act = () => plan.MarkExecuting();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void PauseForCostApproval_then_resume_via_revised_ceiling_returns_to_Executing()
    {
        var plan = CreateDraft();
        plan.SubmitForApproval();
        plan.Approve(Guid.NewGuid(), DateTimeOffset.UtcNow, approvedCostCeiling: 100m);
        plan.MarkExecuting();

        plan.PauseForCostApproval("Projected cost now $150, above the $100 ceiling.");
        plan.Status.Should().Be(ImportProfileStatus.PausedForCostApproval);

        plan.ApproveRevisedCeilingAndResume(200m);

        plan.Status.Should().Be(ImportProfileStatus.Executing);
        plan.ApprovedCostCeiling.Should().Be(200m);
        plan.PauseReason.Should().BeNull();
    }

    [Fact]
    public void Superseded_plan_cannot_execute()
    {
        var plan = CreateDraft();
        plan.Supersede();

        var act = () => plan.MarkExecuting();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Completed_plan_cannot_be_cancelled()
    {
        var plan = CreateDraft();
        plan.SubmitForApproval();
        plan.Approve(Guid.NewGuid(), DateTimeOffset.UtcNow, 50m);
        plan.MarkExecuting();
        plan.MarkCompleted();

        var act = () => plan.Cancel();

        act.Should().Throw<InvalidOperationException>();
    }
}

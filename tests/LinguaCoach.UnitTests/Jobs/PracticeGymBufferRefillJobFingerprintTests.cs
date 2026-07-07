using FluentAssertions;
using LinguaCoach.Infrastructure.Jobs;

namespace LinguaCoach.UnitTests.Jobs;

/// <summary>
/// Covers PracticeGymBufferRefillJob.BuildQueueSlotFingerprint — a queue-slot uniqueness
/// key only. These tests intentionally do NOT assert anything about content-level dedup,
/// because no such capability exists yet (see docs/reviews/2026-07-08-bank-first-ai-teaching-clean-architecture-plan.md, Phase B).
/// </summary>
public sealed class PracticeGymBufferRefillJobFingerprintTests
{
    private static readonly Guid StudentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime QueuedAt = new(2026, 7, 8, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void BuildQueueSlotFingerprint_SameInputs_ProducesSameFingerprint()
    {
        var first = PracticeGymBufferRefillJob.BuildQueueSlotFingerprint(
            StudentId, "email_reply", "B1", "intermediate_workplace", QueuedAt, 0);
        var second = PracticeGymBufferRefillJob.BuildQueueSlotFingerprint(
            StudentId, "email_reply", "B1", "intermediate_workplace", QueuedAt, 0);

        first.Should().Be(second);
    }

    [Fact]
    public void BuildQueueSlotFingerprint_DifferentStudent_ProducesDifferentFingerprint()
    {
        var otherStudent = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var first = PracticeGymBufferRefillJob.BuildQueueSlotFingerprint(
            StudentId, "email_reply", "B1", "intermediate_workplace", QueuedAt, 0);
        var second = PracticeGymBufferRefillJob.BuildQueueSlotFingerprint(
            otherStudent, "email_reply", "B1", "intermediate_workplace", QueuedAt, 0);

        first.Should().NotBe(second);
    }

    [Fact]
    public void BuildQueueSlotFingerprint_DifferentPattern_ProducesDifferentFingerprint()
    {
        var first = PracticeGymBufferRefillJob.BuildQueueSlotFingerprint(
            StudentId, "email_reply", "B1", "intermediate_workplace", QueuedAt, 0);
        var second = PracticeGymBufferRefillJob.BuildQueueSlotFingerprint(
            StudentId, "vocabulary_match", "B1", "intermediate_workplace", QueuedAt, 0);

        first.Should().NotBe(second);
    }

    [Fact]
    public void BuildQueueSlotFingerprint_DifferentSlotIndex_ProducesDifferentFingerprint()
    {
        var first = PracticeGymBufferRefillJob.BuildQueueSlotFingerprint(
            StudentId, "email_reply", "B1", "intermediate_workplace", QueuedAt, 0);
        var second = PracticeGymBufferRefillJob.BuildQueueSlotFingerprint(
            StudentId, "email_reply", "B1", "intermediate_workplace", QueuedAt, 1);

        first.Should().NotBe(second);
    }

    [Fact]
    public void BuildQueueSlotFingerprint_DifferentRunTimestamp_ProducesDifferentFingerprint()
    {
        var first = PracticeGymBufferRefillJob.BuildQueueSlotFingerprint(
            StudentId, "email_reply", "B1", "intermediate_workplace", QueuedAt, 0);
        var second = PracticeGymBufferRefillJob.BuildQueueSlotFingerprint(
            StudentId, "email_reply", "B1", "intermediate_workplace", QueuedAt.AddSeconds(1), 0);

        first.Should().NotBe(second);
    }
}

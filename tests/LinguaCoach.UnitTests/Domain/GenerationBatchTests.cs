using FluentAssertions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.Domain;

public sealed class GenerationBatchTests
{
    [Fact]
    public void MarkCancelledByAdmin_MarksFailedWithAdminReason()
    {
        var batch = new GenerationBatch(
            Guid.NewGuid(),
            GenerationTriggerReason.ManualAdmin,
            requestedSessionCount: 4);
        batch.MarkRunning();

        batch.MarkCancelledByAdmin();

        batch.Status.Should().Be(GenerationBatchStatus.Failed);
        batch.FailureReason.Should().Be(GenerationBatch.AdminCancelledFailureReason);
        batch.CompletedAtUtc.Should().NotBeNull();
    }
}

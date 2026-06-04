using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// A thematic group of activities within a LearningPath.
/// Example: "Email writing for approvals", "Telephone etiquette".
/// </summary>
public sealed class LearningModule : BaseEntity
{
    public Guid LearningPathId { get; private set; }

    public string Title { get; private set; }
    public string Description { get; private set; }

    // Display order within the parent LearningPath.
    public int Order { get; private set; }

    // Null until the student explicitly confirms module completion.
    public DateTime? CompletedAt { get; private set; }

    public bool IsCompleted => CompletedAt.HasValue;

    public void MarkCompleted() => CompletedAt ??= DateTime.UtcNow;

    public IReadOnlyList<LearningActivity> Activities => _activities.AsReadOnly();
    private readonly List<LearningActivity> _activities = [];

    private LearningModule()
    {
        Title = string.Empty;
        Description = string.Empty;
    }

    public LearningModule(Guid learningPathId, string title, string description, int order)
    {
        if (learningPathId == Guid.Empty) throw new ArgumentException("LearningPathId must not be empty.", nameof(learningPathId));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(title));
        if (order < 0) throw new ArgumentOutOfRangeException(nameof(order), "Order must be non-negative.");

        LearningPathId = learningPathId;
        Title = title.Trim();
        Description = description?.Trim() ?? string.Empty;
        Order = order;
    }
}

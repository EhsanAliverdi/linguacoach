using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>Phase H5 — links a <see cref="Module"/> to one of the
/// <see cref="Exercise"/>s it's built from.</summary>
public sealed class ModuleExerciseLink : BaseEntity
{
    public Guid ModuleId { get; private set; }
    public Guid ExerciseId { get; private set; }
    public ModuleExerciseRole Role { get; private set; }
    public int SortOrder { get; private set; }
    public bool Required { get; private set; }
    public string? SnapshotTitle { get; private set; }

    private ModuleExerciseLink() { }

    public ModuleExerciseLink(
        Guid moduleId, Guid exerciseId, ModuleExerciseRole role,
        int sortOrder, bool required = true, string? snapshotTitle = null)
    {
        if (moduleId == Guid.Empty)
            throw new ArgumentException("ModuleId must not be empty.", nameof(moduleId));
        if (exerciseId == Guid.Empty)
            throw new ArgumentException("ExerciseId must not be empty.", nameof(exerciseId));

        ModuleId = moduleId;
        ExerciseId = exerciseId;
        Role = role;
        SortOrder = sortOrder;
        Required = required;
        SnapshotTitle = snapshotTitle?.Trim();
    }
}

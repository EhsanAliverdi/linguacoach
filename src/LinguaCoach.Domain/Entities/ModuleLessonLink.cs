using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>Phase H5 — links a <see cref="Module"/> to one of the <see cref="Lesson"/>s
/// it's built from. Reuses <see cref="LessonResourceRole"/> (Primary/Supporting) — the same
/// "what part does this play" concept H3/H4's resource links already use.</summary>
public sealed class ModuleLessonLink : BaseEntity
{
    public Guid ModuleId { get; private set; }
    public Guid LessonId { get; private set; }
    public LessonResourceRole Role { get; private set; }
    public int SortOrder { get; private set; }
    public string? SnapshotTitle { get; private set; }

    private ModuleLessonLink() { }

    public ModuleLessonLink(
        Guid moduleId, Guid lessonId, LessonResourceRole role, int sortOrder, string? snapshotTitle = null)
    {
        if (moduleId == Guid.Empty)
            throw new ArgumentException("ModuleId must not be empty.", nameof(moduleId));
        if (lessonId == Guid.Empty)
            throw new ArgumentException("LessonId must not be empty.", nameof(lessonId));

        ModuleId = moduleId;
        LessonId = lessonId;
        Role = role;
        SortOrder = sortOrder;
        SnapshotTitle = snapshotTitle?.Trim();
    }
}

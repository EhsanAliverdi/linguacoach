using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Phase 2 (2026-07-15 exercise pipeline boundary consolidation) — no schema change (source_mode
    /// is a plain varchar column, not a DB-level enum/CHECK constraint), but the
    /// <c>ExerciseSourceMode.GeneratedFromResources</c> value was removed from the C# enum, so any
    /// existing row still storing that string would fail to deserialize. Converts every such row to
    /// <c>GeneratedFromLesson</c> — safe because the prior migration
    /// (Phase_2_RequireExerciseLessonId) already guaranteed every row has a real LessonId, so
    /// "generated from resources with no Lesson" no longer describes any row's actual shape.
    /// </summary>
    public partial class Phase_2_ConvertGeneratedFromResourcesSourceMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE exercises
                SET source_mode = 'GeneratedFromLesson'
                WHERE source_mode = 'GeneratedFromResources';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Irreversible by design — there is no reliable way to tell, after the fact, which
            // GeneratedFromLesson rows were originally GeneratedFromResources.
        }
    }
}

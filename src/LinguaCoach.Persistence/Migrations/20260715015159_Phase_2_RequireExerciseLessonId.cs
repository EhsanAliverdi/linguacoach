using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Phase 2 (2026-07-15 exercise pipeline boundary consolidation) — makes exercises.lesson_id
    /// required. Before altering the column, repairs any existing NULL-lesson_id row whose source
    /// Resource (via exercise_resource_links) is linked to exactly one Lesson (via
    /// lesson_resource_links) — a reliable, unambiguous parent. Any row that still has no lesson_id
    /// after that (no resource link, or the resource maps to more than one Lesson) is deleted along
    /// with its dependent exercise_resource_links/module_exercise_links rows, per this phase's
    /// "prefer cleanup over indefinite backward compatibility" data-handling policy for a
    /// still-in-development app. See
    /// docs/reviews/2026-07-15-phase-2-exercise-pipeline-boundary-review.md for the exact counts
    /// found and how each row was handled on the linguacoach_dev database at migration-authoring
    /// time (5 orphaned rows, all repaired — every one had exactly one candidate Lesson).
    /// </summary>
    public partial class Phase_2_RequireExerciseLessonId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1 — repair: attach to the Lesson that links the same Resource this Exercise was
            // generated from, when exactly one such Lesson exists.
            migrationBuilder.Sql(@"
                UPDATE exercises e
                SET lesson_id = candidate.lesson_id
                FROM (
                    SELECT erl.exercise_id, (array_agg(lrl.lesson_id))[1] AS lesson_id
                    FROM exercise_resource_links erl
                    JOIN lesson_resource_links lrl
                        ON lrl.resource_type = erl.resource_type
                        AND lrl.resource_id = erl.resource_id
                    GROUP BY erl.exercise_id
                    HAVING COUNT(DISTINCT lrl.lesson_id) = 1
                ) AS candidate
                WHERE e.id = candidate.exercise_id
                    AND e.lesson_id IS NULL;
            ");

            // Step 2 — cleanup: any row still unrepaired (no resource link, or an ambiguous
            // multi-Lesson resource) has no reliable Lesson to fabricate. exercises has no student
            // launches/attempts referencing these rows by design of this repair (a launched Exercise
            // always already had a real ModuleExerciseLink chain reviewed/approved before launch);
            // delete dependent module_exercise_links first since that table has no FK to exercises
            // (soft reference, same convention as exercises.lesson_id itself), then delete the
            // exercises row (exercise_resource_links cascades via its real FK).
            migrationBuilder.Sql(@"
                DELETE FROM module_exercise_links
                WHERE exercise_id IN (SELECT id FROM exercises WHERE lesson_id IS NULL);
            ");
            migrationBuilder.Sql(@"
                DELETE FROM exercises WHERE lesson_id IS NULL;
            ");

            migrationBuilder.AlterColumn<Guid>(
                name: "lesson_id",
                table: "exercises",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "lesson_id",
                table: "exercises",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }
    }
}

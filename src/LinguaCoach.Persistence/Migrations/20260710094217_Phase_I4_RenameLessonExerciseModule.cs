using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Phase I4 Pass 1 — pure rename (no data-model change): LearnItem → Lesson,
    /// ActivityDefinition → Exercise, ModuleDefinition → Module. Uses direct
    /// RenameTable/RenameColumn/RenameIndex operations (not Drop+Create) so existing rows,
    /// PK/FK constraints, and indexes survive the migration untouched — Postgres preserves
    /// constraint wiring across a table/column rename automatically.
    /// </summary>
    public partial class Phase_I4_RenameLessonExerciseModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Tables ──────────────────────────────────────────────────────────────────
            migrationBuilder.RenameTable(name: "learn_items", newName: "lessons");
            migrationBuilder.RenameTable(name: "activity_definitions", newName: "exercises");
            migrationBuilder.RenameTable(name: "module_definitions", newName: "modules");
            migrationBuilder.RenameTable(name: "learn_item_resource_links", newName: "lesson_resource_links");
            migrationBuilder.RenameTable(name: "activity_resource_links", newName: "exercise_resource_links");
            migrationBuilder.RenameTable(name: "module_definition_learn_item_links", newName: "module_lesson_links");
            migrationBuilder.RenameTable(name: "module_definition_activity_links", newName: "module_exercise_links");
            migrationBuilder.RenameTable(name: "student_activity_definition_launches", newName: "student_exercise_launches");

            // ── FK/id columns ───────────────────────────────────────────────────────────
            migrationBuilder.RenameColumn(name: "learn_item_id", table: "lesson_resource_links", newName: "lesson_id");
            migrationBuilder.RenameColumn(name: "activity_definition_id", table: "exercise_resource_links", newName: "exercise_id");

            migrationBuilder.RenameColumn(name: "learn_item_id", table: "exercises", newName: "lesson_id");

            migrationBuilder.RenameColumn(name: "learn_item_id", table: "module_lesson_links", newName: "lesson_id");
            migrationBuilder.RenameColumn(name: "module_definition_id", table: "module_lesson_links", newName: "module_id");

            migrationBuilder.RenameColumn(name: "activity_definition_id", table: "module_exercise_links", newName: "exercise_id");
            migrationBuilder.RenameColumn(name: "module_definition_id", table: "module_exercise_links", newName: "module_id");

            migrationBuilder.RenameColumn(name: "activity_definition_id", table: "student_exercise_launches", newName: "exercise_id");
            migrationBuilder.RenameColumn(name: "learn_item_id", table: "student_exercise_launches", newName: "lesson_id");
            migrationBuilder.RenameColumn(name: "module_definition_id", table: "student_exercise_launches", newName: "module_id");

            migrationBuilder.RenameColumn(name: "module_definition_id", table: "student_daily_module_assignments", newName: "module_id");
            migrationBuilder.RenameColumn(name: "module_definition_id", table: "student_practice_gym_module_assignments", newName: "module_id");

            // ── Indexes (renamed to match new table/column names) ─────────────────────────
            migrationBuilder.RenameIndex(name: "ix_learn_items_cefr_level", table: "lessons", newName: "ix_lessons_cefr_level");
            migrationBuilder.RenameIndex(name: "ix_learn_items_created_at", table: "lessons", newName: "ix_lessons_created_at");
            migrationBuilder.RenameIndex(name: "ix_learn_items_review_status", table: "lessons", newName: "ix_lessons_review_status");
            migrationBuilder.RenameIndex(name: "ix_learn_items_skill", table: "lessons", newName: "ix_lessons_skill");
            migrationBuilder.RenameIndex(name: "ix_learn_items_subskill", table: "lessons", newName: "ix_lessons_subskill");

            migrationBuilder.RenameIndex(name: "ix_activity_definitions_activity_type", table: "exercises", newName: "ix_exercises_activity_type");
            migrationBuilder.RenameIndex(name: "ix_activity_definitions_cefr_level", table: "exercises", newName: "ix_exercises_cefr_level");
            migrationBuilder.RenameIndex(name: "ix_activity_definitions_created_at", table: "exercises", newName: "ix_exercises_created_at");
            migrationBuilder.RenameIndex(name: "ix_activity_definitions_learn_item", table: "exercises", newName: "ix_exercises_lesson");
            migrationBuilder.RenameIndex(name: "ix_activity_definitions_review_status", table: "exercises", newName: "ix_exercises_review_status");
            migrationBuilder.RenameIndex(name: "ix_activity_definitions_skill", table: "exercises", newName: "ix_exercises_skill");
            migrationBuilder.RenameIndex(name: "ix_activity_definitions_subskill", table: "exercises", newName: "ix_exercises_subskill");

            migrationBuilder.RenameIndex(name: "ix_module_definitions_cefr_level", table: "modules", newName: "ix_modules_cefr_level");
            migrationBuilder.RenameIndex(name: "ix_module_definitions_created_at", table: "modules", newName: "ix_modules_created_at");
            migrationBuilder.RenameIndex(name: "ix_module_definitions_review_status", table: "modules", newName: "ix_modules_review_status");
            migrationBuilder.RenameIndex(name: "ix_module_definitions_skill", table: "modules", newName: "ix_modules_skill");
            migrationBuilder.RenameIndex(name: "ix_module_definitions_subskill", table: "modules", newName: "ix_modules_subskill");

            migrationBuilder.RenameIndex(name: "ix_learn_item_resource_links_learn_item", table: "lesson_resource_links", newName: "ix_lesson_resource_links_lesson");
            migrationBuilder.RenameIndex(name: "ix_learn_item_resource_links_resource", table: "lesson_resource_links", newName: "ix_lesson_resource_links_resource");

            migrationBuilder.RenameIndex(name: "ix_activity_resource_links_activity", table: "exercise_resource_links", newName: "ix_exercise_resource_links_activity");
            migrationBuilder.RenameIndex(name: "ix_activity_resource_links_resource", table: "exercise_resource_links", newName: "ix_exercise_resource_links_resource");

            migrationBuilder.RenameIndex(name: "ix_module_definition_learn_item_links_learn_item", table: "module_lesson_links", newName: "ix_module_lesson_links_lesson");
            migrationBuilder.RenameIndex(name: "ix_module_definition_learn_item_links_module", table: "module_lesson_links", newName: "ix_module_lesson_links_module");

            migrationBuilder.RenameIndex(name: "ix_module_definition_activity_links_activity", table: "module_exercise_links", newName: "ix_module_exercise_links_activity");
            migrationBuilder.RenameIndex(name: "ix_module_definition_activity_links_module", table: "module_exercise_links", newName: "ix_module_exercise_links_module");

            migrationBuilder.RenameIndex(name: "ix_activity_definition_launches_activity", table: "student_exercise_launches", newName: "ix_exercise_launches_activity");
            migrationBuilder.RenameIndex(name: "ix_activity_definition_launches_learning_activity", table: "student_exercise_launches", newName: "ix_exercise_launches_learning_activity");
            migrationBuilder.RenameIndex(name: "ix_activity_definition_launches_module", table: "student_exercise_launches", newName: "ix_exercise_launches_module");
            migrationBuilder.RenameIndex(name: "ix_activity_definition_launches_student_launched", table: "student_exercise_launches", newName: "ix_exercise_launches_student_launched");
            migrationBuilder.RenameIndex(name: "IX_student_activity_definition_launches_learn_item_id", table: "student_exercise_launches", newName: "IX_student_exercise_launches_lesson_id");

            migrationBuilder.RenameIndex(name: "IX_student_daily_module_assignments_module_definition_id", table: "student_daily_module_assignments", newName: "IX_student_daily_module_assignments_module_id");
            migrationBuilder.RenameIndex(name: "IX_student_practice_gym_module_assignments_module_definition_id", table: "student_practice_gym_module_assignments", newName: "IX_student_practice_gym_module_assignments_module_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(name: "IX_student_practice_gym_module_assignments_module_id", table: "student_practice_gym_module_assignments", newName: "IX_student_practice_gym_module_assignments_module_definition_id");
            migrationBuilder.RenameIndex(name: "IX_student_daily_module_assignments_module_id", table: "student_daily_module_assignments", newName: "IX_student_daily_module_assignments_module_definition_id");

            migrationBuilder.RenameIndex(name: "IX_student_exercise_launches_lesson_id", table: "student_exercise_launches", newName: "IX_student_activity_definition_launches_learn_item_id");
            migrationBuilder.RenameIndex(name: "ix_exercise_launches_student_launched", table: "student_exercise_launches", newName: "ix_activity_definition_launches_student_launched");
            migrationBuilder.RenameIndex(name: "ix_exercise_launches_module", table: "student_exercise_launches", newName: "ix_activity_definition_launches_module");
            migrationBuilder.RenameIndex(name: "ix_exercise_launches_learning_activity", table: "student_exercise_launches", newName: "ix_activity_definition_launches_learning_activity");
            migrationBuilder.RenameIndex(name: "ix_exercise_launches_activity", table: "student_exercise_launches", newName: "ix_activity_definition_launches_activity");

            migrationBuilder.RenameIndex(name: "ix_module_exercise_links_module", table: "module_exercise_links", newName: "ix_module_definition_activity_links_module");
            migrationBuilder.RenameIndex(name: "ix_module_exercise_links_activity", table: "module_exercise_links", newName: "ix_module_definition_activity_links_activity");

            migrationBuilder.RenameIndex(name: "ix_module_lesson_links_module", table: "module_lesson_links", newName: "ix_module_definition_learn_item_links_module");
            migrationBuilder.RenameIndex(name: "ix_module_lesson_links_lesson", table: "module_lesson_links", newName: "ix_module_definition_learn_item_links_learn_item");

            migrationBuilder.RenameIndex(name: "ix_exercise_resource_links_resource", table: "exercise_resource_links", newName: "ix_activity_resource_links_resource");
            migrationBuilder.RenameIndex(name: "ix_exercise_resource_links_activity", table: "exercise_resource_links", newName: "ix_activity_resource_links_activity");

            migrationBuilder.RenameIndex(name: "ix_lesson_resource_links_resource", table: "lesson_resource_links", newName: "ix_learn_item_resource_links_resource");
            migrationBuilder.RenameIndex(name: "ix_lesson_resource_links_lesson", table: "lesson_resource_links", newName: "ix_learn_item_resource_links_learn_item");

            migrationBuilder.RenameIndex(name: "ix_modules_subskill", table: "modules", newName: "ix_module_definitions_subskill");
            migrationBuilder.RenameIndex(name: "ix_modules_skill", table: "modules", newName: "ix_module_definitions_skill");
            migrationBuilder.RenameIndex(name: "ix_modules_review_status", table: "modules", newName: "ix_module_definitions_review_status");
            migrationBuilder.RenameIndex(name: "ix_modules_created_at", table: "modules", newName: "ix_module_definitions_created_at");
            migrationBuilder.RenameIndex(name: "ix_modules_cefr_level", table: "modules", newName: "ix_module_definitions_cefr_level");

            migrationBuilder.RenameIndex(name: "ix_exercises_subskill", table: "exercises", newName: "ix_activity_definitions_subskill");
            migrationBuilder.RenameIndex(name: "ix_exercises_skill", table: "exercises", newName: "ix_activity_definitions_skill");
            migrationBuilder.RenameIndex(name: "ix_exercises_review_status", table: "exercises", newName: "ix_activity_definitions_review_status");
            migrationBuilder.RenameIndex(name: "ix_exercises_lesson", table: "exercises", newName: "ix_activity_definitions_learn_item");
            migrationBuilder.RenameIndex(name: "ix_exercises_created_at", table: "exercises", newName: "ix_activity_definitions_created_at");
            migrationBuilder.RenameIndex(name: "ix_exercises_cefr_level", table: "exercises", newName: "ix_activity_definitions_cefr_level");
            migrationBuilder.RenameIndex(name: "ix_exercises_activity_type", table: "exercises", newName: "ix_activity_definitions_activity_type");

            migrationBuilder.RenameIndex(name: "ix_lessons_subskill", table: "lessons", newName: "ix_learn_items_subskill");
            migrationBuilder.RenameIndex(name: "ix_lessons_skill", table: "lessons", newName: "ix_learn_items_skill");
            migrationBuilder.RenameIndex(name: "ix_lessons_review_status", table: "lessons", newName: "ix_learn_items_review_status");
            migrationBuilder.RenameIndex(name: "ix_lessons_created_at", table: "lessons", newName: "ix_learn_items_created_at");
            migrationBuilder.RenameIndex(name: "ix_lessons_cefr_level", table: "lessons", newName: "ix_learn_items_cefr_level");

            migrationBuilder.RenameColumn(name: "module_id", table: "student_practice_gym_module_assignments", newName: "module_definition_id");
            migrationBuilder.RenameColumn(name: "module_id", table: "student_daily_module_assignments", newName: "module_definition_id");

            migrationBuilder.RenameColumn(name: "module_id", table: "student_exercise_launches", newName: "module_definition_id");
            migrationBuilder.RenameColumn(name: "lesson_id", table: "student_exercise_launches", newName: "learn_item_id");
            migrationBuilder.RenameColumn(name: "exercise_id", table: "student_exercise_launches", newName: "activity_definition_id");

            migrationBuilder.RenameColumn(name: "module_id", table: "module_exercise_links", newName: "module_definition_id");
            migrationBuilder.RenameColumn(name: "exercise_id", table: "module_exercise_links", newName: "activity_definition_id");

            migrationBuilder.RenameColumn(name: "module_id", table: "module_lesson_links", newName: "module_definition_id");
            migrationBuilder.RenameColumn(name: "lesson_id", table: "module_lesson_links", newName: "learn_item_id");

            migrationBuilder.RenameColumn(name: "lesson_id", table: "exercises", newName: "learn_item_id");

            migrationBuilder.RenameColumn(name: "exercise_id", table: "exercise_resource_links", newName: "activity_definition_id");
            migrationBuilder.RenameColumn(name: "lesson_id", table: "lesson_resource_links", newName: "learn_item_id");

            migrationBuilder.RenameTable(name: "student_exercise_launches", newName: "student_activity_definition_launches");
            migrationBuilder.RenameTable(name: "module_exercise_links", newName: "module_definition_activity_links");
            migrationBuilder.RenameTable(name: "module_lesson_links", newName: "module_definition_learn_item_links");
            migrationBuilder.RenameTable(name: "exercise_resource_links", newName: "activity_resource_links");
            migrationBuilder.RenameTable(name: "lesson_resource_links", newName: "learn_item_resource_links");
            migrationBuilder.RenameTable(name: "modules", newName: "module_definitions");
            migrationBuilder.RenameTable(name: "exercises", newName: "activity_definitions");
            migrationBuilder.RenameTable(name: "lessons", newName: "learn_items");
        }
    }
}

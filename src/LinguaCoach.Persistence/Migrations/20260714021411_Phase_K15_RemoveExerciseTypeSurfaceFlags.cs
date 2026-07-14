using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase_K15_RemoveExerciseTypeSurfaceFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "supports_practice_gym",
                table: "exercise_type_definitions");

            migrationBuilder.DropColumn(
                name: "supports_today_lesson",
                table: "exercise_type_definitions");

            // Phase K15 — "Enabled" now specifically means "usable for Lesson-based Exercise
            // generation" (see ExerciseTypeDefinitionSeeder). None of the pre-existing legacy/
            // pattern catalog rows have a real composer wired to that pipeline yet — only the
            // 3 new "BankFirst" rows (gap_fill/multiple_choice_single/short_answer) do. The
            // seeder's SyncCatalogMetadata deliberately never touches IsEnabled on existing rows
            // (so a future admin's manual toggle survives redeploys), so this one-time data fix
            // is required here to make existing databases match the new default posture.
            migrationBuilder.Sql(
                "UPDATE exercise_type_definitions SET is_enabled = false WHERE category <> 'BankFirst';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "supports_practice_gym",
                table: "exercise_type_definitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "supports_today_lesson",
                table: "exercise_type_definitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}

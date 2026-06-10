using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T33_ExercisePatternEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "exercise_pattern_key",
                table: "learning_activities",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "exercise_patterns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    primary_skill = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    secondary_skills_json = table.Column<string>(type: "text", nullable: false),
                    compatible_kinds_json = table.Column<string>(type: "text", nullable: false),
                    activity_type = table.Column<int>(type: "integer", nullable: false),
                    interaction_mode = table.Column<int>(type: "integer", nullable: false),
                    marking_mode = table.Column<int>(type: "integer", nullable: false),
                    estimated_minutes = table.Column<int>(type: "integer", nullable: false),
                    ai_generate_prompt_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ai_evaluate_prompt_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    requires_audio = table.Column<bool>(type: "boolean", nullable: false),
                    workplace_context = table.Column<bool>(type: "boolean", nullable: false),
                    teaching_purpose = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercise_patterns", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_exercise_patterns_active",
                table: "exercise_patterns",
                column: "is_active",
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "ix_exercise_patterns_key",
                table: "exercise_patterns",
                column: "key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exercise_patterns");

            migrationBuilder.DropColumn(
                name: "exercise_pattern_key",
                table: "learning_activities");
        }
    }
}

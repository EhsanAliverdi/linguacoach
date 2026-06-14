using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T44_ExerciseTypeCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "exercise_type_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    primary_skill = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    secondary_skills_json = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    implementation_status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    renderer_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    evaluator_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    generation_prompt_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    legacy_activity_type = table.Column<int>(type: "integer", nullable: true),
                    exercise_pattern_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    estimated_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    requires_audio = table.Column<bool>(type: "boolean", nullable: false),
                    requires_image = table.Column<bool>(type: "boolean", nullable: false),
                    supports_practice_gym = table.Column<bool>(type: "boolean", nullable: false),
                    supports_today_lesson = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercise_type_definitions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_exercise_type_definitions_key",
                table: "exercise_type_definitions",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_exercise_type_definitions_skill_enabled",
                table: "exercise_type_definitions",
                columns: new[] { "primary_skill", "is_enabled" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "exercise_type_definitions");
        }
    }
}

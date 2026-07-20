using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint7_RetireCurriculumObjectiveAndModuleObjectiveKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "curriculum_objectives");

            migrationBuilder.DropColumn(
                name: "objective_key",
                table: "modules");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "objective_key",
                table: "modules",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "curriculum_objectives",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    admin_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cefr_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    context_tags_json = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    description = table.Column<string>(type: "text", nullable: false),
                    difficulty_band = table.Column<int>(type: "integer", nullable: false),
                    example_prompts = table.Column<string>(type: "text", nullable: true),
                    focus_tags_json = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_exam_inspired = table.Column<bool>(type: "boolean", nullable: false),
                    is_reviewable = table.Column<bool>(type: "boolean", nullable: false),
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    prerequisite_keys_json = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    primary_skill = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    recommended_order = table.Column<int>(type: "integer", nullable: false),
                    secondary_skills_json = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    subskill = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    teaching_notes = table.Column<string>(type: "text", nullable: true),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_curriculum_objectives", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_curriculum_objectives_cefr_skill_active",
                table: "curriculum_objectives",
                columns: new[] { "cefr_level", "primary_skill", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_curriculum_objectives_key",
                table: "curriculum_objectives",
                column: "key",
                unique: true);
        }
    }
}

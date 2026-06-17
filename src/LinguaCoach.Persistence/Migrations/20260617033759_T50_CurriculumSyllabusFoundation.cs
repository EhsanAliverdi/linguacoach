using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T50_CurriculumSyllabusFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "curriculum_objectives",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    cefr_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    primary_skill = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    secondary_skills_json = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    context_tags_json = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    focus_tags_json = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    prerequisite_keys_json = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    recommended_order = table.Column<int>(type: "integer", nullable: false),
                    difficulty_band = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_reviewable = table.Column<bool>(type: "boolean", nullable: false),
                    is_exam_inspired = table.Column<bool>(type: "boolean", nullable: false),
                    teaching_notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "curriculum_objectives");
        }
    }
}

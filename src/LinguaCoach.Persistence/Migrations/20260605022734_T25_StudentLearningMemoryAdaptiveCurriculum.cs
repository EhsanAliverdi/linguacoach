using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T25_StudentLearningMemoryAdaptiveCurriculum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "covered_scenarios_json",
                table: "user_learning_summaries",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "journey_summary",
                table: "user_learning_summaries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "next_focus_json",
                table: "user_learning_summaries",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "recurring_mistakes_json",
                table: "user_learning_summaries",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "strong_skills_json",
                table: "user_learning_summaries",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "weak_skills_json",
                table: "user_learning_summaries",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "difficulty",
                table: "learning_modules",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fingerprint_json",
                table: "learning_modules",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "focus_skill",
                table: "learning_modules",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reason",
                table: "learning_modules",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "student_skill_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    skill_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_weak = table.Column<bool>(type: "boolean", nullable: false),
                    last_updated_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_skill_profiles", x => x.id);
                    table.ForeignKey(
                        name: "FK_student_skill_profiles_student_profiles_student_profile_id",
                        column: x => x.student_profile_id,
                        principalTable: "student_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_student_skill_profiles_student_key",
                table: "student_skill_profiles",
                columns: new[] { "student_profile_id", "skill_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "student_skill_profiles");

            migrationBuilder.DropColumn(
                name: "covered_scenarios_json",
                table: "user_learning_summaries");

            migrationBuilder.DropColumn(
                name: "journey_summary",
                table: "user_learning_summaries");

            migrationBuilder.DropColumn(
                name: "next_focus_json",
                table: "user_learning_summaries");

            migrationBuilder.DropColumn(
                name: "recurring_mistakes_json",
                table: "user_learning_summaries");

            migrationBuilder.DropColumn(
                name: "strong_skills_json",
                table: "user_learning_summaries");

            migrationBuilder.DropColumn(
                name: "weak_skills_json",
                table: "user_learning_summaries");

            migrationBuilder.DropColumn(
                name: "difficulty",
                table: "learning_modules");

            migrationBuilder.DropColumn(
                name: "fingerprint_json",
                table: "learning_modules");

            migrationBuilder.DropColumn(
                name: "focus_skill",
                table: "learning_modules");

            migrationBuilder.DropColumn(
                name: "reason",
                table: "learning_modules");
        }
    }
}

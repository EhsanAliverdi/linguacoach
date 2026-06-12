using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T42_StudentSkillScorePercent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "score_percent",
                table: "student_skill_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 50);

            // Preserve existing weak/strong classification: weak -> 40, strong -> 60.
            migrationBuilder.Sql(
                "UPDATE student_skill_profiles SET score_percent = CASE WHEN is_weak THEN 40 ELSE 60 END;");

            migrationBuilder.DropColumn(
                name: "is_weak",
                table: "student_skill_profiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_weak",
                table: "student_skill_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                "UPDATE student_skill_profiles SET is_weak = (score_percent < 50);");

            migrationBuilder.DropColumn(
                name: "score_percent",
                table: "student_skill_profiles");
        }
    }
}

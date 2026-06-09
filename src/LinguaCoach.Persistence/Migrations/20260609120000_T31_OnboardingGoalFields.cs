using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T31_OnboardingGoalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Student-authored learning goal and difficult situations text (any language).
            // IF NOT EXISTS guards make this idempotent on databases that may have the
            // columns already from a manual hotfix.
            migrationBuilder.Sql(
                "ALTER TABLE student_profiles ADD COLUMN IF NOT EXISTS learning_goal_description character varying(1000);");

            migrationBuilder.Sql(
                "ALTER TABLE student_profiles ADD COLUMN IF NOT EXISTS difficult_situations_text character varying(1000);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "learning_goal_description",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "difficult_situations_text",
                table: "student_profiles");
        }
    }
}

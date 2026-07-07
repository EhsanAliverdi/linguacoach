using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T_StudentLearningEventCurriculumObjectiveKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "curriculum_objective_key",
                table: "student_learning_events",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_student_learning_events_student_objective",
                table: "student_learning_events",
                columns: new[] { "student_profile_id", "curriculum_objective_key" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_student_learning_events_student_objective",
                table: "student_learning_events");

            migrationBuilder.DropColumn(
                name: "curriculum_objective_key",
                table: "student_learning_events");
        }
    }
}

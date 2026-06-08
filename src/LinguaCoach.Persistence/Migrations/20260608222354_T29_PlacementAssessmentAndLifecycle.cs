using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T29_PlacementAssessmentAndLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "lifecycle_stage",
                table: "student_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 2); // OnboardingRequired — sensible default for any new/blank row

            // Backfill existing rows: students who completed onboarding move to PlacementRequired (4);
            // others remain at OnboardingRequired (2). OnboardingStatus: 0=NotStarted,1=InProgress,2=Complete.
            migrationBuilder.Sql(
                "UPDATE student_profiles SET lifecycle_stage = 4 WHERE onboarding_status = 2;");

            migrationBuilder.AddColumn<int>(
                name: "preferred_session_duration_minutes",
                table: "student_profiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "professional_experience_level",
                table: "student_profiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "role_familiarity",
                table: "student_profiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "workplace_seniority",
                table: "student_profiles",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "placement_assessments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    current_section_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    result_json = table.Column<string>(type: "text", nullable: true),
                    overall_estimated_level = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    skill_levels_json = table.Column<string>(type: "text", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_placement_assessments", x => x.id);
                    table.ForeignKey(
                        name: "FK_placement_assessments_student_profiles_student_profile_id",
                        column: x => x.student_profile_id,
                        principalTable: "student_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "placement_answers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    placement_assessment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    question_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    response_text = table.Column<string>(type: "text", nullable: true),
                    selected_option = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    score = table.Column<double>(type: "double precision", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_placement_answers", x => x.id);
                    table.ForeignKey(
                        name: "FK_placement_answers_placement_assessments_placement_assessmen~",
                        column: x => x.placement_assessment_id,
                        principalTable: "placement_assessments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_placement_answers_placement_assessment_id",
                table: "placement_answers",
                column: "placement_assessment_id");

            migrationBuilder.CreateIndex(
                name: "ix_placement_assessments_student_profile_id",
                table: "placement_assessments",
                column: "student_profile_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "placement_answers");

            migrationBuilder.DropTable(
                name: "placement_assessments");

            migrationBuilder.DropColumn(
                name: "lifecycle_stage",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "preferred_session_duration_minutes",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "professional_experience_level",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "role_familiarity",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "workplace_seniority",
                table: "student_profiles");
        }
    }
}

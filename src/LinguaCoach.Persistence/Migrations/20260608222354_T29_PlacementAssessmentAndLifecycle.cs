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
            // Use IF NOT EXISTS guards because these columns may already exist on databases
            // that had them applied via raw SQL before this migration was added to the history.
            migrationBuilder.Sql(
                "ALTER TABLE student_profiles ADD COLUMN IF NOT EXISTS lifecycle_stage integer NOT NULL DEFAULT 2;");

            // Backfill existing rows: students who completed onboarding move to PlacementRequired (4);
            // others remain at OnboardingRequired (2). OnboardingStatus: 0=NotStarted,1=InProgress,2=Complete.
            migrationBuilder.Sql(
                "UPDATE student_profiles SET lifecycle_stage = 4 WHERE onboarding_status = 2;");

            migrationBuilder.Sql(
                "ALTER TABLE student_profiles ADD COLUMN IF NOT EXISTS preferred_session_duration_minutes integer;");

            migrationBuilder.Sql(
                "ALTER TABLE student_profiles ADD COLUMN IF NOT EXISTS professional_experience_level integer;");

            migrationBuilder.Sql(
                "ALTER TABLE student_profiles ADD COLUMN IF NOT EXISTS role_familiarity integer;");

            migrationBuilder.Sql(
                "ALTER TABLE student_profiles ADD COLUMN IF NOT EXISTS workplace_seniority integer;");

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS placement_assessments (
                    id uuid NOT NULL,
                    student_profile_id uuid NOT NULL,
                    status integer NOT NULL,
                    started_at_utc timestamp with time zone,
                    completed_at_utc timestamp with time zone,
                    current_section_key character varying(50) NOT NULL,
                    result_json text,
                    overall_estimated_level character varying(5),
                    skill_levels_json text,
                    updated_at_utc timestamp with time zone NOT NULL,
                    created_at timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_placement_assessments" PRIMARY KEY (id),
                    CONSTRAINT "FK_placement_assessments_student_profiles_student_profile_id"
                        FOREIGN KEY (student_profile_id) REFERENCES student_profiles(id) ON DELETE CASCADE
                );
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS placement_answers (
                    id uuid NOT NULL,
                    placement_assessment_id uuid NOT NULL,
                    section_key character varying(50) NOT NULL,
                    question_key character varying(100) NOT NULL,
                    response_text text,
                    selected_option character varying(500),
                    score double precision,
                    created_at timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_placement_answers" PRIMARY KEY (id),
                    CONSTRAINT "FK_placement_answers_placement_assessments_placement_assessmen~"
                        FOREIGN KEY (placement_assessment_id) REFERENCES placement_assessments(id) ON DELETE CASCADE
                );
                """);

            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_placement_answers_placement_assessment_id ON placement_answers(placement_assessment_id);");

            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_placement_assessments_student_profile_id ON placement_assessments(student_profile_id);");
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

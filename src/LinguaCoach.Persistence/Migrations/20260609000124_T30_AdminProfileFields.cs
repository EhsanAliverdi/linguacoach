using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T30_AdminProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // All ADD COLUMN calls use IF NOT EXISTS because T29 may have already added
            // the shared columns (lifecycle_stage etc.) on databases that ran T29 before T30.
            migrationBuilder.Sql(
                "ALTER TABLE student_profiles ADD COLUMN IF NOT EXISTS career_context character varying(500);");

            migrationBuilder.Sql(
                "ALTER TABLE student_profiles ADD COLUMN IF NOT EXISTS display_name character varying(150);");

            migrationBuilder.Sql(
                "ALTER TABLE student_profiles ADD COLUMN IF NOT EXISTS first_name character varying(100);");

            migrationBuilder.Sql(
                "ALTER TABLE student_profiles ADD COLUMN IF NOT EXISTS last_name character varying(100);");

            migrationBuilder.Sql(
                "ALTER TABLE student_profiles ADD COLUMN IF NOT EXISTS learning_goal character varying(500);");

            // These were already added by T29 on databases that ran T29; guard them.
            migrationBuilder.Sql(
                "ALTER TABLE student_profiles ADD COLUMN IF NOT EXISTS lifecycle_stage integer NOT NULL DEFAULT 0;");

            migrationBuilder.Sql(
                "ALTER TABLE student_profiles ADD COLUMN IF NOT EXISTS preferred_session_duration_minutes integer;");

            migrationBuilder.Sql(
                "ALTER TABLE student_profiles ADD COLUMN IF NOT EXISTS professional_experience_level integer;");

            migrationBuilder.Sql(
                "ALTER TABLE student_profiles ADD COLUMN IF NOT EXISTS role_familiarity integer;");

            migrationBuilder.Sql(
                "ALTER TABLE student_profiles ADD COLUMN IF NOT EXISTS workplace_seniority integer;");

            // These tables were already created by T29; guard them.
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
                name: "career_context",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "display_name",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "first_name",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "last_name",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "learning_goal",
                table: "student_profiles");

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

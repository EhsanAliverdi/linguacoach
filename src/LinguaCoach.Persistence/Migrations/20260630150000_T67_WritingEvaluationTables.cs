using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T67_WritingEvaluationTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "writing_evaluations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    activity_attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    learning_activity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    provider_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    model_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    overall_score = table.Column<double>(type: "double precision", nullable: true),
                    grammar_score = table.Column<double>(type: "double precision", nullable: true),
                    vocabulary_score = table.Column<double>(type: "double precision", nullable: true),
                    coherence_score = table.Column<double>(type: "double precision", nullable: true),
                    task_completion_score = table.Column<double>(type: "double precision", nullable: true),
                    feedback_text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    suggested_improvement = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    corrected_text = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_writing_evaluations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_writing_evaluations_activity",
                table: "writing_evaluations",
                column: "learning_activity_id");

            migrationBuilder.CreateIndex(
                name: "ix_writing_evaluations_attempt",
                table: "writing_evaluations",
                column: "activity_attempt_id");

            migrationBuilder.CreateIndex(
                name: "ix_writing_evaluations_status",
                table: "writing_evaluations",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_writing_evaluations_student",
                table: "writing_evaluations",
                column: "student_profile_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "writing_evaluations");
        }
    }
}

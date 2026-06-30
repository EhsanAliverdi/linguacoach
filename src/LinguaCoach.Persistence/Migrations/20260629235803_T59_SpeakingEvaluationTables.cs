using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T59_SpeakingEvaluationTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "speaking_evaluation_applied_signals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    signal_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    confidence = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    score_used = table.Column<double>(type: "double precision", nullable: true),
                    skill_affected = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    applied_rule_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    dry_run_outcome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    learning_event_id = table.Column<Guid>(type: "uuid", nullable: true),
                    applied_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_speaking_evaluation_applied_signals", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "speaking_evaluations",
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
                    transcript = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    overall_score = table.Column<double>(type: "double precision", nullable: true),
                    fluency_score = table.Column<double>(type: "double precision", nullable: true),
                    pronunciation_score = table.Column<double>(type: "double precision", nullable: true),
                    completeness_score = table.Column<double>(type: "double precision", nullable: true),
                    relevance_score = table.Column<double>(type: "double precision", nullable: true),
                    feedback_text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    suggested_improvement = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_speaking_evaluations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_speaking_applied_signals_evaluation_unique",
                table: "speaking_evaluation_applied_signals",
                column: "evaluation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_speaking_applied_signals_student",
                table: "speaking_evaluation_applied_signals",
                column: "student_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_speaking_evaluations_attempt",
                table: "speaking_evaluations",
                column: "activity_attempt_id");

            migrationBuilder.CreateIndex(
                name: "ix_speaking_evaluations_status",
                table: "speaking_evaluations",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_speaking_evaluations_student",
                table: "speaking_evaluations",
                column: "student_profile_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "speaking_evaluation_applied_signals");

            migrationBuilder.DropTable(
                name: "speaking_evaluations");
        }
    }
}

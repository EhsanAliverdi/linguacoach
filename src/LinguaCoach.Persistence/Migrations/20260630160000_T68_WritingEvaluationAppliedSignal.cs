using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T68_WritingEvaluationAppliedSignal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "writing_evaluation_applied_signals",
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
                    table.PrimaryKey("PK_writing_evaluation_applied_signals", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_writing_applied_signals_evaluation_unique",
                table: "writing_evaluation_applied_signals",
                column: "evaluation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_writing_applied_signals_student",
                table: "writing_evaluation_applied_signals",
                column: "student_profile_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "writing_evaluation_applied_signals");
        }
    }
}

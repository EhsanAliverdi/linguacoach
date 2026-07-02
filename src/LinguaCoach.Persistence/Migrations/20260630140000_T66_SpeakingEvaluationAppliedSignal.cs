using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T66_SpeakingEvaluationAppliedSignal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Guarded with IF NOT EXISTS: this migration's table
            // (speaking_evaluation_applied_signals) is also created by
            // T59_SpeakingEvaluationTables — see the identical note in
            // T65_SpeakingEvaluationFoundation.cs.
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS speaking_evaluation_applied_signals (
    id uuid NOT NULL,
    evaluation_id uuid NOT NULL,
    attempt_id uuid NOT NULL,
    student_profile_id uuid NOT NULL,
    activity_id uuid NOT NULL,
    signal_type character varying(50) NOT NULL,
    confidence character varying(20) NOT NULL,
    score_used double precision,
    skill_affected character varying(100) NOT NULL,
    applied_rule_version character varying(20) NOT NULL,
    dry_run_outcome character varying(100) NOT NULL,
    reason character varying(500) NOT NULL,
    learning_event_id uuid,
    applied_at_utc timestamp with time zone NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT ""PK_speaking_evaluation_applied_signals"" PRIMARY KEY (id)
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_speaking_applied_signals_evaluation_unique ON speaking_evaluation_applied_signals (evaluation_id);
CREATE INDEX IF NOT EXISTS ix_speaking_applied_signals_student ON speaking_evaluation_applied_signals (student_profile_id);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS speaking_evaluation_applied_signals;");
        }
    }
}

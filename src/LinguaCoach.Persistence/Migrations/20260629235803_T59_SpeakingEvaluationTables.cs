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
            // Guarded with IF NOT EXISTS: T65_SpeakingEvaluationFoundation and
            // T66_SpeakingEvaluationAppliedSignal create the same two tables. All
            // three previously had no .Designer.cs, so EF never applied any of them
            // anywhere and whichever runs first (by filename-timestamp order, which
            // differs per environment's migration history) must not conflict with
            // the other two. See T65/T66 for the identical note.
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

CREATE TABLE IF NOT EXISTS speaking_evaluations (
    id uuid NOT NULL,
    activity_attempt_id uuid NOT NULL,
    student_profile_id uuid NOT NULL,
    learning_activity_id uuid NOT NULL,
    status integer NOT NULL DEFAULT 0,
    provider_name character varying(100),
    model_name character varying(100),
    started_at_utc timestamp with time zone,
    completed_at_utc timestamp with time zone,
    failed_at_utc timestamp with time zone,
    failure_reason character varying(500),
    transcript character varying(4000),
    overall_score double precision,
    fluency_score double precision,
    pronunciation_score double precision,
    completeness_score double precision,
    relevance_score double precision,
    feedback_text character varying(2000),
    suggested_improvement character varying(500),
    retry_count integer NOT NULL DEFAULT 0,
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT ""PK_speaking_evaluations"" PRIMARY KEY (id)
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_speaking_applied_signals_evaluation_unique ON speaking_evaluation_applied_signals (evaluation_id);
CREATE INDEX IF NOT EXISTS ix_speaking_applied_signals_student ON speaking_evaluation_applied_signals (student_profile_id);
CREATE INDEX IF NOT EXISTS ix_speaking_evaluations_attempt ON speaking_evaluations (activity_attempt_id);
CREATE INDEX IF NOT EXISTS ix_speaking_evaluations_status ON speaking_evaluations (status);
CREATE INDEX IF NOT EXISTS ix_speaking_evaluations_student ON speaking_evaluations (student_profile_id);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TABLE IF EXISTS speaking_evaluation_applied_signals;
DROP TABLE IF EXISTS speaking_evaluations;
");
        }
    }
}

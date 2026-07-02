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
            // Guarded with IF NOT EXISTS: T68_PendingModelChanges (a separate
            // migration, timestamped later) creates this exact same table. This
            // migration had no .Designer.cs, so EF Core never discovered or applied
            // it on any environment -- T68_PendingModelChanges is what actually ran.
            // See the identical note in T68_PendingModelChanges.cs and
            // docs/reviews/2026-07-02-phase-20f-production-placement-readiness-p0-unblocker-review.md.
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS writing_evaluation_applied_signals (
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
    CONSTRAINT ""PK_writing_evaluation_applied_signals"" PRIMARY KEY (id)
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_writing_applied_signals_evaluation_unique ON writing_evaluation_applied_signals (evaluation_id);
CREATE INDEX IF NOT EXISTS ix_writing_applied_signals_student ON writing_evaluation_applied_signals (student_profile_id);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS writing_evaluation_applied_signals;");
        }
    }
}

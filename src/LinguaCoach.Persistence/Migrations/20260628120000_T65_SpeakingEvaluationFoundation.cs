using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T65_SpeakingEvaluationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Guarded with IF NOT EXISTS: this migration's table (speaking_evaluations)
            // is also created by T59_SpeakingEvaluationTables. Because this migration
            // previously had no .Designer.cs file, EF Core never discovered or applied
            // it on any environment, so which of the two migrations "wins" the race
            // depends on filename-timestamp order relative to whichever one an
            // environment applies first. Making both idempotent makes the outcome safe
            // regardless of order. See
            // docs/reviews/2026-07-02-phase-20f-production-placement-readiness-p0-unblocker-review.md.
            migrationBuilder.Sql(@"
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

CREATE INDEX IF NOT EXISTS ix_speaking_evaluations_attempt ON speaking_evaluations (activity_attempt_id);
CREATE INDEX IF NOT EXISTS ix_speaking_evaluations_student ON speaking_evaluations (student_profile_id);
CREATE INDEX IF NOT EXISTS ix_speaking_evaluations_status ON speaking_evaluations (status);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS speaking_evaluations;");
        }
    }
}

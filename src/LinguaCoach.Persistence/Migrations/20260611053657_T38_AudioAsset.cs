using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T38_AudioAsset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "course_sequence_number",
                table: "learning_sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "generation_batch_id",
                table: "learning_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "generation_status",
                table: "learning_sessions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "ready_at_utc",
                table: "learning_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "student_profile_id",
                table: "learning_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "audio_assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    learning_session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    learning_activity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    activity_attempt_id = table.Column<Guid>(type: "uuid", nullable: true),
                    asset_type = table.Column<int>(type: "integer", nullable: false),
                    object_key = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    duration_seconds = table.Column<double>(type: "double precision", nullable: true),
                    transcript_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    speaker_profile_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    speaker_profile_json = table.Column<string>(type: "text", nullable: true),
                    provider_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    model_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    generation_status = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audio_assets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "generation_batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trigger_reason = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    requested_session_count = table.Column<int>(type: "integer", nullable: false),
                    completed_session_count = table.Column<int>(type: "integer", nullable: false),
                    summary_snapshot_json = table.Column<string>(type: "text", nullable: true),
                    prompt_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    provider_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    model_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failure_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_generation_batches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lesson_generation_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ready_lesson_buffer_size = table.Column<int>(type: "integer", nullable: false),
                    refill_threshold = table.Column<int>(type: "integer", nullable: false),
                    refill_batch_size = table.Column<int>(type: "integer", nullable: false),
                    max_generation_attempts = table.Column<int>(type: "integer", nullable: false),
                    generation_timeout_seconds = table.Column<int>(type: "integer", nullable: false),
                    tts_timeout_seconds = table.Column<int>(type: "integer", nullable: false),
                    max_concurrent_generation_jobs = table.Column<int>(type: "integer", nullable: false),
                    max_concurrent_tts_jobs = table.Column<int>(type: "integer", nullable: false),
                    enable_background_generation = table.Column<bool>(type: "boolean", nullable: false),
                    enable_tts_generation = table.Column<bool>(type: "boolean", nullable: false),
                    practice_gym_ready_exercises_per_type = table.Column<int>(type: "integer", nullable: false),
                    practice_gym_refill_threshold_per_type = table.Column<int>(type: "integer", nullable: false),
                    practice_gym_refill_count_per_type = table.Column<int>(type: "integer", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lesson_generation_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "practice_activity_cache",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pattern_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    cefr_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    domain_complexity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    skill_focus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    content_fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    learning_activity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_practice_activity_cache", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "generation_job_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    generation_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_type = table.Column<int>(type: "integer", nullable: false),
                    target_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_retry_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_generation_job_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_generation_job_items_generation_batches_generation_batch_id",
                        column: x => x.generation_batch_id,
                        principalTable: "generation_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_learning_sessions_student_sequence",
                table: "learning_sessions",
                columns: new[] { "student_profile_id", "course_sequence_number" },
                unique: true,
                filter: "student_profile_id IS NOT NULL AND course_sequence_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_audio_assets_activity",
                table: "audio_assets",
                column: "learning_activity_id");

            migrationBuilder.CreateIndex(
                name: "ix_audio_assets_attempt",
                table: "audio_assets",
                column: "activity_attempt_id");

            migrationBuilder.CreateIndex(
                name: "ix_audio_assets_status",
                table: "audio_assets",
                column: "generation_status");

            migrationBuilder.CreateIndex(
                name: "ix_audio_assets_student",
                table: "audio_assets",
                column: "student_profile_id");

            migrationBuilder.CreateIndex(
                name: "ux_audio_assets_tts_fingerprint",
                table: "audio_assets",
                columns: new[] { "learning_activity_id", "transcript_hash", "speaker_profile_hash", "provider_name", "model_name" },
                unique: true,
                filter: "learning_activity_id IS NOT NULL AND transcript_hash IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_generation_batches_status",
                table: "generation_batches",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_generation_batches_student",
                table: "generation_batches",
                column: "student_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_generation_job_items_batch",
                table: "generation_job_items",
                column: "generation_batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_generation_job_items_status",
                table: "generation_job_items",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_practice_cache_student_pattern_status",
                table: "practice_activity_cache",
                columns: new[] { "student_profile_id", "pattern_key", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_practice_cache_fingerprint",
                table: "practice_activity_cache",
                columns: new[] { "student_profile_id", "pattern_key", "cefr_level", "domain_complexity", "content_fingerprint" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audio_assets");

            migrationBuilder.DropTable(
                name: "generation_job_items");

            migrationBuilder.DropTable(
                name: "lesson_generation_settings");

            migrationBuilder.DropTable(
                name: "practice_activity_cache");

            migrationBuilder.DropTable(
                name: "generation_batches");

            migrationBuilder.DropIndex(
                name: "ux_learning_sessions_student_sequence",
                table: "learning_sessions");

            migrationBuilder.DropColumn(
                name: "course_sequence_number",
                table: "learning_sessions");

            migrationBuilder.DropColumn(
                name: "generation_batch_id",
                table: "learning_sessions");

            migrationBuilder.DropColumn(
                name: "generation_status",
                table: "learning_sessions");

            migrationBuilder.DropColumn(
                name: "ready_at_utc",
                table: "learning_sessions");

            migrationBuilder.DropColumn(
                name: "student_profile_id",
                table: "learning_sessions");
        }
    }
}

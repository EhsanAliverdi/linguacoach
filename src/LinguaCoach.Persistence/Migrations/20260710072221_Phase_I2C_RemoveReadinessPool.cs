using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase_I2C_RemoveReadinessPool : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_activity_feedback_signals_student_activity_readiness_items_~",
                table: "activity_feedback_signals");

            migrationBuilder.DropForeignKey(
                name: "FK_student_activity_usage_logs_student_activity_readiness_item~",
                table: "student_activity_usage_logs");

            migrationBuilder.DropTable(
                name: "student_activity_readiness_items");

            migrationBuilder.DropIndex(
                name: "IX_student_activity_usage_logs_student_activity_readiness_item~",
                table: "student_activity_usage_logs");

            migrationBuilder.DropIndex(
                name: "IX_activity_feedback_signals_student_activity_readiness_item_id",
                table: "activity_feedback_signals");

            migrationBuilder.DropColumn(
                name: "student_activity_readiness_item_id",
                table: "student_activity_usage_logs");

            migrationBuilder.DropColumn(
                name: "student_activity_readiness_item_id",
                table: "activity_feedback_signals");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "student_activity_readiness_item_id",
                table: "student_activity_usage_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "student_activity_readiness_item_id",
                table: "activity_feedback_signals",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "student_activity_readiness_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    activity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    admin_review_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    admin_review_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    admin_review_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "NotRequired"),
                    admin_reviewed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    admin_reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    consumed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    context_tags_json = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    curriculum_objective_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    curriculum_objective_title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    difficulty_band = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    difficulty_preference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    focus_tags_json = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    form_io_schema_snapshot_json = table.Column<string>(type: "jsonb", nullable: true),
                    generated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    generated_by_model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    generated_by_provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_lower_level_content = table.Column<bool>(type: "boolean", nullable: false),
                    last_evaluated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    learning_activity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    learning_session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    original_cefr_level_snapshot = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    pattern_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    personalization_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    preferred_session_duration_minutes = table.Column<int>(type: "integer", nullable: true),
                    primary_skill = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    requires_admin_review = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    reserved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    routing_explanation = table.Column<string>(type: "text", nullable: true),
                    routing_reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    scoring_rules_snapshot_json = table.Column<string>(type: "jsonb", nullable: true),
                    secondary_skills_json = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    session_exercise_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_bank_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    stale_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subskill = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    support_language_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    support_language_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    target_cefr_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    translation_help_preference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    validation_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_activity_readiness_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_student_activity_readiness_items_placement_item_definitions~",
                        column: x => x.source_bank_item_id,
                        principalTable: "placement_item_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_student_activity_usage_logs_student_activity_readiness_item~",
                table: "student_activity_usage_logs",
                column: "student_activity_readiness_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_activity_feedback_signals_student_activity_readiness_item_id",
                table: "activity_feedback_signals",
                column: "student_activity_readiness_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_readiness_items_activity_id",
                table: "student_activity_readiness_items",
                column: "learning_activity_id");

            migrationBuilder.CreateIndex(
                name: "ix_readiness_items_admin_review_status",
                table: "student_activity_readiness_items",
                columns: new[] { "requires_admin_review", "admin_review_status" });

            migrationBuilder.CreateIndex(
                name: "ix_readiness_items_session_id",
                table: "student_activity_readiness_items",
                column: "learning_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_readiness_items_student_status_priority",
                table: "student_activity_readiness_items",
                columns: new[] { "student_id", "status", "priority", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_readiness_items_student_status_source",
                table: "student_activity_readiness_items",
                columns: new[] { "student_id", "status", "source" });

            migrationBuilder.CreateIndex(
                name: "IX_student_activity_readiness_items_source_bank_item_id",
                table: "student_activity_readiness_items",
                column: "source_bank_item_id");

            migrationBuilder.AddForeignKey(
                name: "FK_activity_feedback_signals_student_activity_readiness_items_~",
                table: "activity_feedback_signals",
                column: "student_activity_readiness_item_id",
                principalTable: "student_activity_readiness_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_student_activity_usage_logs_student_activity_readiness_item~",
                table: "student_activity_usage_logs",
                column: "student_activity_readiness_item_id",
                principalTable: "student_activity_readiness_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T51_StudentActivityReadinessPool : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "student_activity_readiness_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    target_cefr_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    original_cefr_level_snapshot = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    is_lower_level_content = table.Column<bool>(type: "boolean", nullable: false),
                    routing_reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    routing_explanation = table.Column<string>(type: "text", nullable: true),
                    curriculum_objective_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    curriculum_objective_title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    primary_skill = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    secondary_skills_json = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    context_tags_json = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    focus_tags_json = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    pattern_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    activity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    difficulty_band = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    preferred_session_duration_minutes = table.Column<int>(type: "integer", nullable: true),
                    difficulty_preference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    support_language_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    support_language_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    translation_help_preference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    learning_session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    learning_activity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    session_exercise_id = table.Column<Guid>(type: "uuid", nullable: true),
                    generated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    reserved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    consumed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    stale_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_activity_readiness_items", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_readiness_items_activity_id",
                table: "student_activity_readiness_items",
                column: "learning_activity_id");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "student_activity_readiness_items");
        }
    }
}

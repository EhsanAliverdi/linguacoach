using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase_I2A_RemoveActivityTemplateAndPracticeActivityCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_activity_feedback_signals_activity_templates_source_templat~",
                table: "activity_feedback_signals");

            migrationBuilder.DropForeignKey(
                name: "FK_student_activity_readiness_items_activity_templates_source_~",
                table: "student_activity_readiness_items");

            migrationBuilder.DropForeignKey(
                name: "FK_student_activity_usage_logs_activity_templates_source_templ~",
                table: "student_activity_usage_logs");

            migrationBuilder.DropTable(
                name: "activity_templates");

            migrationBuilder.DropTable(
                name: "practice_activity_cache");

            migrationBuilder.DropIndex(
                name: "IX_student_activity_usage_logs_source_template_id",
                table: "student_activity_usage_logs");

            migrationBuilder.DropIndex(
                name: "IX_student_activity_readiness_items_source_template_id",
                table: "student_activity_readiness_items");

            migrationBuilder.DropIndex(
                name: "IX_activity_feedback_signals_source_template_id",
                table: "activity_feedback_signals");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "activity_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    activity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    asset_requirements_json = table.Column<string>(type: "text", nullable: true),
                    cefr_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    context_tags_json = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    curriculum_objective_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    estimated_duration_seconds = table.Column<int>(type: "integer", nullable: true),
                    focus_tags_json = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    form_io_base_schema_json = table.Column<string>(type: "jsonb", nullable: true),
                    generation_instructions = table.Column<string>(type: "text", nullable: true),
                    is_published = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    pattern_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    previous_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    review_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "NotRequired"),
                    scoring_model_json = table.Column<string>(type: "jsonb", nullable: true),
                    skill = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    subskill = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    validation_rules_json = table.Column<string>(type: "jsonb", nullable: true),
                    version_number = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_templates", x => x.id);
                    table.ForeignKey(
                        name: "FK_activity_templates_activity_templates_previous_version_id",
                        column: x => x.previous_version_id,
                        principalTable: "activity_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "practice_activity_cache",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cefr_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    content_fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    domain_complexity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    learning_activity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pattern_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    skill_focus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    student_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_practice_activity_cache", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_student_activity_usage_logs_source_template_id",
                table: "student_activity_usage_logs",
                column: "source_template_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_activity_readiness_items_source_template_id",
                table: "student_activity_readiness_items",
                column: "source_template_id");

            migrationBuilder.CreateIndex(
                name: "IX_activity_feedback_signals_source_template_id",
                table: "activity_feedback_signals",
                column: "source_template_id");

            migrationBuilder.CreateIndex(
                name: "ix_activity_templates_key_version",
                table: "activity_templates",
                columns: new[] { "key", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_activity_templates_previous_version_id",
                table: "activity_templates",
                column: "previous_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_activity_templates_review_status",
                table: "activity_templates",
                column: "review_status");

            migrationBuilder.CreateIndex(
                name: "ix_activity_templates_skill_level_published",
                table: "activity_templates",
                columns: new[] { "skill", "cefr_level", "is_published" });

            migrationBuilder.CreateIndex(
                name: "ix_practice_cache_student_pattern_status",
                table: "practice_activity_cache",
                columns: new[] { "student_profile_id", "pattern_key", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_practice_cache_fingerprint",
                table: "practice_activity_cache",
                columns: new[] { "student_profile_id", "pattern_key", "cefr_level", "domain_complexity", "content_fingerprint" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_activity_feedback_signals_activity_templates_source_templat~",
                table: "activity_feedback_signals",
                column: "source_template_id",
                principalTable: "activity_templates",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_student_activity_readiness_items_activity_templates_source_~",
                table: "student_activity_readiness_items",
                column: "source_template_id",
                principalTable: "activity_templates",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_student_activity_usage_logs_activity_templates_source_templ~",
                table: "student_activity_usage_logs",
                column: "source_template_id",
                principalTable: "activity_templates",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

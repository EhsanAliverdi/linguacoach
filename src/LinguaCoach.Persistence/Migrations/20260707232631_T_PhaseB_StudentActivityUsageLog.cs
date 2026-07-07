using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T_PhaseB_StudentActivityUsageLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "student_activity_usage_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    learning_activity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    student_activity_readiness_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_bank_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pattern_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    activity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    skill = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    subskill = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    cefr_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    curriculum_objective_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    content_fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    topic_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    scenario_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    passage_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    prompt_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    context_tags_json = table.Column<string>(type: "jsonb", nullable: true),
                    focus_tags_json = table.Column<string>(type: "jsonb", nullable: true),
                    is_intentional_review = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    review_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    consumed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_activity_usage_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_student_activity_usage_logs_activity_templates_source_templ~",
                        column: x => x.source_template_id,
                        principalTable: "activity_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_student_activity_usage_logs_learning_activities_learning_ac~",
                        column: x => x.learning_activity_id,
                        principalTable: "learning_activities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_student_activity_usage_logs_placement_item_definitions_sour~",
                        column: x => x.source_bank_item_id,
                        principalTable: "placement_item_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_student_activity_usage_logs_student_activity_readiness_item~",
                        column: x => x.student_activity_readiness_item_id,
                        principalTable: "student_activity_readiness_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_student_activity_usage_logs_student_profiles_student_profil~",
                        column: x => x.student_profile_id,
                        principalTable: "student_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_student_activity_usage_logs_learning_activity_id",
                table: "student_activity_usage_logs",
                column: "learning_activity_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_activity_usage_logs_source_bank_item_id",
                table: "student_activity_usage_logs",
                column: "source_bank_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_activity_usage_logs_source_template_id",
                table: "student_activity_usage_logs",
                column: "source_template_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_activity_usage_logs_student_activity_readiness_item~",
                table: "student_activity_usage_logs",
                column: "student_activity_readiness_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_usage_logs_student_consumed_at",
                table: "student_activity_usage_logs",
                columns: new[] { "student_profile_id", "consumed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_usage_logs_student_fingerprint",
                table: "student_activity_usage_logs",
                columns: new[] { "student_profile_id", "content_fingerprint" });

            migrationBuilder.CreateIndex(
                name: "ix_usage_logs_student_pattern",
                table: "student_activity_usage_logs",
                columns: new[] { "student_profile_id", "pattern_key" });

            migrationBuilder.CreateIndex(
                name: "ix_usage_logs_student_scenario",
                table: "student_activity_usage_logs",
                columns: new[] { "student_profile_id", "scenario_key" });

            migrationBuilder.CreateIndex(
                name: "ix_usage_logs_student_template",
                table: "student_activity_usage_logs",
                columns: new[] { "student_profile_id", "source_template_id" });

            migrationBuilder.CreateIndex(
                name: "ix_usage_logs_student_topic",
                table: "student_activity_usage_logs",
                columns: new[] { "student_profile_id", "topic_key" });

            migrationBuilder.CreateIndex(
                name: "ux_usage_logs_student_activity",
                table: "student_activity_usage_logs",
                columns: new[] { "student_profile_id", "learning_activity_id" },
                unique: true,
                filter: "learning_activity_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "student_activity_usage_logs");
        }
    }
}

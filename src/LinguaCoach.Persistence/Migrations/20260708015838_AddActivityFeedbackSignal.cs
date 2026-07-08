using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityFeedbackSignal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "activity_feedback_signals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    learning_activity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activity_attempt_id = table.Column<Guid>(type: "uuid", nullable: true),
                    student_activity_usage_log_id = table.Column<Guid>(type: "uuid", nullable: true),
                    student_activity_readiness_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_bank_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pattern_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    skill = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    subskill = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    cefr_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    curriculum_objective_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    difficulty_rating = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    clarity_rating = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    usefulness_rating = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    repeat_preference = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    optional_comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_feedback_signals", x => x.id);
                    table.ForeignKey(
                        name: "FK_activity_feedback_signals_activity_attempts_activity_attemp~",
                        column: x => x.activity_attempt_id,
                        principalTable: "activity_attempts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_activity_feedback_signals_activity_templates_source_templat~",
                        column: x => x.source_template_id,
                        principalTable: "activity_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_activity_feedback_signals_learning_activities_learning_acti~",
                        column: x => x.learning_activity_id,
                        principalTable: "learning_activities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_activity_feedback_signals_placement_item_definitions_source~",
                        column: x => x.source_bank_item_id,
                        principalTable: "placement_item_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_activity_feedback_signals_student_activity_readiness_items_~",
                        column: x => x.student_activity_readiness_item_id,
                        principalTable: "student_activity_readiness_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_activity_feedback_signals_student_activity_usage_logs_stude~",
                        column: x => x.student_activity_usage_log_id,
                        principalTable: "student_activity_usage_logs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_activity_feedback_signals_student_profiles_student_profile_~",
                        column: x => x.student_profile_id,
                        principalTable: "student_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_activity_feedback_signals_activity_attempt_id",
                table: "activity_feedback_signals",
                column: "activity_attempt_id");

            migrationBuilder.CreateIndex(
                name: "IX_activity_feedback_signals_learning_activity_id",
                table: "activity_feedback_signals",
                column: "learning_activity_id");

            migrationBuilder.CreateIndex(
                name: "IX_activity_feedback_signals_source_bank_item_id",
                table: "activity_feedback_signals",
                column: "source_bank_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_activity_feedback_signals_source_template_id",
                table: "activity_feedback_signals",
                column: "source_template_id");

            migrationBuilder.CreateIndex(
                name: "IX_activity_feedback_signals_student_activity_readiness_item_id",
                table: "activity_feedback_signals",
                column: "student_activity_readiness_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_activity_feedback_signals_student_activity_usage_log_id",
                table: "activity_feedback_signals",
                column: "student_activity_usage_log_id");

            migrationBuilder.CreateIndex(
                name: "ix_feedback_signals_student_pattern",
                table: "activity_feedback_signals",
                columns: new[] { "student_profile_id", "pattern_key" });

            migrationBuilder.CreateIndex(
                name: "ux_feedback_signals_student_activity_no_attempt",
                table: "activity_feedback_signals",
                columns: new[] { "student_profile_id", "learning_activity_id" },
                unique: true,
                filter: "activity_attempt_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_feedback_signals_student_attempt",
                table: "activity_feedback_signals",
                columns: new[] { "student_profile_id", "activity_attempt_id" },
                unique: true,
                filter: "activity_attempt_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_feedback_signals");
        }
    }
}

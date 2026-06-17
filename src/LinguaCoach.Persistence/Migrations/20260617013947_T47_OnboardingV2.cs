using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T47_OnboardingV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "onboarding_flow_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_onboarding_flow_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "onboarding_step_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    flow_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    step_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    step_type = table.Column<string>(type: "text", nullable: false),
                    requirement_type = table.Column<string>(type: "text", nullable: false),
                    step_order = table.Column<int>(type: "integer", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    options_json = table.Column<string>(type: "jsonb", nullable: true),
                    validation_metadata_json = table.Column<string>(type: "jsonb", nullable: true),
                    answer_mapping = table.Column<string>(type: "text", nullable: false),
                    assessment_metadata_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_onboarding_step_definitions", x => x.id);
                    table.ForeignKey(
                        name: "FK_onboarding_step_definitions_onboarding_flow_definitions_flo~",
                        column: x => x.flow_definition_id,
                        principalTable: "onboarding_flow_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "student_onboarding_progress",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    flow_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_step_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    completed_step_keys = table.Column<string>(type: "jsonb", nullable: false),
                    percentage_complete = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_complete = table.Column<bool>(type: "boolean", nullable: false),
                    preliminary_cefr_level = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_onboarding_progress", x => x.id);
                    table.ForeignKey(
                        name: "FK_student_onboarding_progress_onboarding_flow_definitions_flo~",
                        column: x => x.flow_definition_id,
                        principalTable: "onboarding_flow_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "student_onboarding_responses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    progress_id = table.Column<Guid>(type: "uuid", nullable: false),
                    step_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    answer_json = table.Column<string>(type: "jsonb", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_onboarding_responses", x => x.id);
                    table.ForeignKey(
                        name: "FK_student_onboarding_responses_student_onboarding_progress_pr~",
                        column: x => x.progress_id,
                        principalTable: "student_onboarding_progress",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_onboarding_flow_definitions_single_active",
                table: "onboarding_flow_definitions",
                column: "is_active",
                unique: true,
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "ix_onboarding_step_definitions_flow_step_key",
                table: "onboarding_step_definitions",
                columns: new[] { "flow_definition_id", "step_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_onboarding_step_definitions_flow_step_order",
                table: "onboarding_step_definitions",
                columns: new[] { "flow_definition_id", "step_order" });

            migrationBuilder.CreateIndex(
                name: "IX_student_onboarding_progress_flow_definition_id",
                table: "student_onboarding_progress",
                column: "flow_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_onboarding_progress_user_id",
                table: "student_onboarding_progress",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_student_onboarding_responses_progress_id",
                table: "student_onboarding_responses",
                column: "progress_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_onboarding_responses_progress_step_key",
                table: "student_onboarding_responses",
                columns: new[] { "progress_id", "step_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "onboarding_step_definitions");

            migrationBuilder.DropTable(
                name: "student_onboarding_responses");

            migrationBuilder.DropTable(
                name: "student_onboarding_progress");

            migrationBuilder.DropTable(
                name: "onboarding_flow_definitions");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T_FormioOnboardingModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "onboarding_category_definitions");

            migrationBuilder.DropTable(
                name: "onboarding_step_definitions");

            migrationBuilder.DropTable(
                name: "student_onboarding_responses");

            migrationBuilder.DropTable(
                name: "student_onboarding_progress");

            migrationBuilder.DropTable(
                name: "onboarding_flow_definitions");

            migrationBuilder.CreateTable(
                name: "student_flow_submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    flow_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    template_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_json = table.Column<string>(type: "jsonb", nullable: false),
                    normalized_answers_json = table.Column<string>(type: "jsonb", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    evaluated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_flow_submissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "student_flow_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    flow_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    active_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_flow_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "student_flow_template_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    form_io_schema_json = table.Column<string>(type: "jsonb", nullable: false),
                    scoring_rules_json = table.Column<string>(type: "jsonb", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_by_admin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_flow_template_versions", x => x.id);
                    table.ForeignKey(
                        name: "FK_student_flow_template_versions_student_flow_templates_templ~",
                        column: x => x.template_id,
                        principalTable: "student_flow_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_student_flow_submissions_student_flow",
                table: "student_flow_submissions",
                columns: new[] { "student_id", "flow_kind" });

            migrationBuilder.CreateIndex(
                name: "ix_student_flow_template_versions_template_status",
                table: "student_flow_template_versions",
                columns: new[] { "template_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_student_flow_template_versions_template_version",
                table: "student_flow_template_versions",
                columns: new[] { "template_id", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_student_flow_templates_flow_kind",
                table: "student_flow_templates",
                column: "flow_kind");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "student_flow_submissions");

            migrationBuilder.DropTable(
                name: "student_flow_template_versions");

            migrationBuilder.DropTable(
                name: "student_flow_templates");

            migrationBuilder.CreateTable(
                name: "onboarding_flow_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_onboarding_flow_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "onboarding_category_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    flow_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_onboarding_category_definitions", x => x.id);
                    table.ForeignKey(
                        name: "FK_onboarding_category_definitions_onboarding_flow_definitions~",
                        column: x => x.flow_definition_id,
                        principalTable: "onboarding_flow_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "onboarding_step_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    flow_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    answer_mapping = table.Column<string>(type: "text", nullable: false),
                    assessment_metadata_json = table.Column<string>(type: "jsonb", nullable: true),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    content_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    options_json = table.Column<string>(type: "jsonb", nullable: true),
                    requirement_type = table.Column<string>(type: "text", nullable: false),
                    step_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    step_order = table.Column<int>(type: "integer", nullable: false),
                    step_type = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    validation_metadata_json = table.Column<string>(type: "jsonb", nullable: true)
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
                    flow_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_step_keys = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    current_step_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_complete = table.Column<bool>(type: "boolean", nullable: false),
                    percentage_complete = table.Column<int>(type: "integer", nullable: false),
                    preliminary_cefr_level = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
                    answer_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    step_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                name: "ix_onboarding_category_definitions_flow_order",
                table: "onboarding_category_definitions",
                columns: new[] { "flow_definition_id", "category_order" });

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
    }
}

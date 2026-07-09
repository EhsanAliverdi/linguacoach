using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase_H4_AddActivityFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "activity_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    instructions = table.Column<string>(type: "text", nullable: false),
                    activity_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    pattern_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    renderer_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    form_schema_json = table.Column<string>(type: "text", nullable: true),
                    answer_key_json = table.Column<string>(type: "text", nullable: true),
                    scoring_rules_json = table.Column<string>(type: "text", nullable: true),
                    feedback_plan_json = table.Column<string>(type: "text", nullable: true),
                    cefr_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    skill = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    subskill = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    context_tags_json = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    focus_tags_json = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    difficulty_band = table.Column<int>(type: "integer", nullable: true),
                    estimated_minutes = table.Column<int>(type: "integer", nullable: true),
                    learn_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    generation_provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    generation_model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    review_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rejected_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
                    review_notes = table.Column<string>(type: "text", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "activity_resource_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    activity_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    snapshot_title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    content_fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_resource_links", x => x.id);
                    table.ForeignKey(
                        name: "FK_activity_resource_links_activity_definitions_activity_defin~",
                        column: x => x.activity_definition_id,
                        principalTable: "activity_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_activity_definitions_activity_type",
                table: "activity_definitions",
                column: "activity_type");

            migrationBuilder.CreateIndex(
                name: "ix_activity_definitions_cefr_level",
                table: "activity_definitions",
                column: "cefr_level");

            migrationBuilder.CreateIndex(
                name: "ix_activity_definitions_created_at",
                table: "activity_definitions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_activity_definitions_learn_item",
                table: "activity_definitions",
                column: "learn_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_activity_definitions_review_status",
                table: "activity_definitions",
                column: "review_status");

            migrationBuilder.CreateIndex(
                name: "ix_activity_definitions_skill",
                table: "activity_definitions",
                column: "skill");

            migrationBuilder.CreateIndex(
                name: "ix_activity_definitions_subskill",
                table: "activity_definitions",
                column: "subskill");

            migrationBuilder.CreateIndex(
                name: "ix_activity_resource_links_activity",
                table: "activity_resource_links",
                column: "activity_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_activity_resource_links_resource",
                table: "activity_resource_links",
                columns: new[] { "resource_type", "resource_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_resource_links");

            migrationBuilder.DropTable(
                name: "activity_definitions");
        }
    }
}

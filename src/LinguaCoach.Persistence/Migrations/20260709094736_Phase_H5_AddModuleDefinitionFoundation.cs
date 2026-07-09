using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase_H5_AddModuleDefinitionFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "module_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    objective_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    cefr_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    skill = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    subskill = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    context_tags_json = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    focus_tags_json = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    difficulty_band = table.Column<int>(type: "integer", nullable: true),
                    estimated_minutes = table.Column<int>(type: "integer", nullable: true),
                    feedback_plan_json = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_module_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "module_definition_activity_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    module_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activity_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    required = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    snapshot_title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_module_definition_activity_links", x => x.id);
                    table.ForeignKey(
                        name: "FK_module_definition_activity_links_module_definitions_module_~",
                        column: x => x.module_definition_id,
                        principalTable: "module_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "module_definition_learn_item_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    module_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    learn_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    snapshot_title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_module_definition_learn_item_links", x => x.id);
                    table.ForeignKey(
                        name: "FK_module_definition_learn_item_links_module_definitions_modul~",
                        column: x => x.module_definition_id,
                        principalTable: "module_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_module_definition_activity_links_activity",
                table: "module_definition_activity_links",
                column: "activity_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_module_definition_activity_links_module",
                table: "module_definition_activity_links",
                column: "module_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_module_definition_learn_item_links_learn_item",
                table: "module_definition_learn_item_links",
                column: "learn_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_module_definition_learn_item_links_module",
                table: "module_definition_learn_item_links",
                column: "module_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_module_definitions_cefr_level",
                table: "module_definitions",
                column: "cefr_level");

            migrationBuilder.CreateIndex(
                name: "ix_module_definitions_created_at",
                table: "module_definitions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_module_definitions_review_status",
                table: "module_definitions",
                column: "review_status");

            migrationBuilder.CreateIndex(
                name: "ix_module_definitions_skill",
                table: "module_definitions",
                column: "skill");

            migrationBuilder.CreateIndex(
                name: "ix_module_definitions_subskill",
                table: "module_definitions",
                column: "subskill");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "module_definition_activity_links");

            migrationBuilder.DropTable(
                name: "module_definition_learn_item_links");

            migrationBuilder.DropTable(
                name: "module_definitions");
        }
    }
}

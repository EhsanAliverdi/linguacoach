using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase_H3_AddLearnItemFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "learn_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    examples_json = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    common_mistakes_json = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    usage_notes = table.Column<string>(type: "text", nullable: true),
                    cefr_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    skill = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    subskill = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    context_tags_json = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    focus_tags_json = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    difficulty_band = table.Column<int>(type: "integer", nullable: true),
                    estimated_minutes = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_learn_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "learn_item_resource_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    learn_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    snapshot_title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    content_fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learn_item_resource_links", x => x.id);
                    table.ForeignKey(
                        name: "FK_learn_item_resource_links_learn_items_learn_item_id",
                        column: x => x.learn_item_id,
                        principalTable: "learn_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_learn_item_resource_links_learn_item",
                table: "learn_item_resource_links",
                column: "learn_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_learn_item_resource_links_resource",
                table: "learn_item_resource_links",
                columns: new[] { "resource_type", "resource_id" });

            migrationBuilder.CreateIndex(
                name: "ix_learn_items_cefr_level",
                table: "learn_items",
                column: "cefr_level");

            migrationBuilder.CreateIndex(
                name: "ix_learn_items_created_at",
                table: "learn_items",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_learn_items_review_status",
                table: "learn_items",
                column: "review_status");

            migrationBuilder.CreateIndex(
                name: "ix_learn_items_skill",
                table: "learn_items",
                column: "skill");

            migrationBuilder.CreateIndex(
                name: "ix_learn_items_subskill",
                table: "learn_items",
                column: "subskill");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "learn_item_resource_links");

            migrationBuilder.DropTable(
                name: "learn_items");
        }
    }
}

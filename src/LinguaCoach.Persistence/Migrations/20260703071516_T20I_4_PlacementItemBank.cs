using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T20I_4_PlacementItemBank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "placement_item_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    cefr_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    item_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    prompt = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    correct_answer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    reading_passage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    listening_audio_script = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    item_order = table.Column<int>(type: "integer", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_placement_item_definitions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_placement_item_definitions_prompt",
                table: "placement_item_definitions",
                column: "prompt",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_placement_item_definitions_skill_level_enabled",
                table: "placement_item_definitions",
                columns: new[] { "skill", "cefr_level", "is_enabled" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "placement_item_definitions");
        }
    }
}

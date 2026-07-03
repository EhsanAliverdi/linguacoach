using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T20I_Unified_Placement_ContentJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "content_json",
                table: "placement_item_definitions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "answer_json",
                table: "placement_assessment_items",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "content_json",
                table: "placement_assessment_items",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "content_json",
                table: "placement_item_definitions");

            migrationBuilder.DropColumn(
                name: "answer_json",
                table: "placement_assessment_items");

            migrationBuilder.DropColumn(
                name: "content_json",
                table: "placement_assessment_items");
        }
    }
}

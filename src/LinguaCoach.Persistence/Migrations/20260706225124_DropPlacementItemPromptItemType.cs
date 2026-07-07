using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropPlacementItemPromptItemType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_placement_item_definitions_prompt",
                table: "placement_item_definitions");

            migrationBuilder.DropColumn(
                name: "item_type",
                table: "placement_item_definitions");

            migrationBuilder.DropColumn(
                name: "prompt",
                table: "placement_item_definitions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "item_type",
                table: "placement_item_definitions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "prompt",
                table: "placement_item_definitions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_placement_item_definitions_prompt",
                table: "placement_item_definitions",
                column: "prompt",
                unique: true);
        }
    }
}

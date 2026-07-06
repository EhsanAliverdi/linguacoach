using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T_FormioPlacementItemBank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "form_io_schema_json",
                table: "placement_item_definitions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "scoring_rules_json",
                table: "placement_item_definitions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "form_io_schema_json",
                table: "placement_assessment_items",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_item_definition_id",
                table: "placement_assessment_items",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "form_io_schema_json",
                table: "placement_item_definitions");

            migrationBuilder.DropColumn(
                name: "scoring_rules_json",
                table: "placement_item_definitions");

            migrationBuilder.DropColumn(
                name: "form_io_schema_json",
                table: "placement_assessment_items");

            migrationBuilder.DropColumn(
                name: "source_item_definition_id",
                table: "placement_assessment_items");
        }
    }
}

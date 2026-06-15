using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPracticeItemCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "default_items_per_practice",
                table: "exercise_type_definitions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "default_options_per_item",
                table: "exercise_type_definitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "max_items_per_practice",
                table: "exercise_type_definitions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "max_options_per_item",
                table: "exercise_type_definitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "min_items_per_practice",
                table: "exercise_type_definitions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "min_options_per_item",
                table: "exercise_type_definitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "default_items_per_practice",
                table: "exercise_type_definitions");

            migrationBuilder.DropColumn(
                name: "default_options_per_item",
                table: "exercise_type_definitions");

            migrationBuilder.DropColumn(
                name: "max_items_per_practice",
                table: "exercise_type_definitions");

            migrationBuilder.DropColumn(
                name: "max_options_per_item",
                table: "exercise_type_definitions");

            migrationBuilder.DropColumn(
                name: "min_items_per_practice",
                table: "exercise_type_definitions");

            migrationBuilder.DropColumn(
                name: "min_options_per_item",
                table: "exercise_type_definitions");
        }
    }
}

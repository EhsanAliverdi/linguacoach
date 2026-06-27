using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T63_PlacementResponseSubmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "evaluation_notes",
                table: "placement_assessment_items",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "duration_seconds",
                table: "placement_assessment_items",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "evaluation_notes",
                table: "placement_assessment_items");

            migrationBuilder.DropColumn(
                name: "duration_seconds",
                table: "placement_assessment_items");
        }
    }
}

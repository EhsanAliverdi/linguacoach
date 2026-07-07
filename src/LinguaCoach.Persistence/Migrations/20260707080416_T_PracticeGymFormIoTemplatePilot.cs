using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T_PracticeGymFormIoTemplatePilot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "form_io_schema_json",
                table: "learning_activities",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "scoring_rules_json",
                table: "learning_activities",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "form_io_schema_json",
                table: "learning_activities");

            migrationBuilder.DropColumn(
                name: "scoring_rules_json",
                table: "learning_activities");
        }
    }
}

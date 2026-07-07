using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T_FormIoQuizAuthoringSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "authoring_schema_json",
                table: "student_flow_template_versions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "authoring_schema_json",
                table: "placement_item_definitions",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "authoring_schema_json",
                table: "student_flow_template_versions");

            migrationBuilder.DropColumn(
                name: "authoring_schema_json",
                table: "placement_item_definitions");
        }
    }
}

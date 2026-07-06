using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFormRendererKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "renderer_kind",
                table: "student_flow_template_versions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "FormIo");

            migrationBuilder.AddColumn<string>(
                name: "renderer_kind",
                table: "placement_item_definitions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "FormIo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "renderer_kind",
                table: "student_flow_template_versions");

            migrationBuilder.DropColumn(
                name: "renderer_kind",
                table: "placement_item_definitions");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint14_1_AddSkillGraphNodeTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "context_tags_json",
                table: "skill_graph_nodes",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "focus_tags_json",
                table: "skill_graph_nodes",
                type: "text",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "context_tags_json",
                table: "skill_graph_nodes");

            migrationBuilder.DropColumn(
                name: "focus_tags_json",
                table: "skill_graph_nodes");
        }
    }
}

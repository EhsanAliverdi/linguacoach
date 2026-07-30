using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillGraphNodeProvenanceAndType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cefr_confidence",
                table: "skill_graph_nodes",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<string>(
                name: "cefr_source",
                table: "skill_graph_nodes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "node_type",
                table: "skill_graph_nodes",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "routing_eligible",
                table: "skill_graph_nodes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_skill_graph_nodes_node_type",
                table: "skill_graph_nodes",
                column: "node_type");

            migrationBuilder.CreateIndex(
                name: "ix_skill_graph_nodes_routing_eligible",
                table: "skill_graph_nodes",
                column: "routing_eligible");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_skill_graph_nodes_node_type",
                table: "skill_graph_nodes");

            migrationBuilder.DropIndex(
                name: "ix_skill_graph_nodes_routing_eligible",
                table: "skill_graph_nodes");

            migrationBuilder.DropColumn(
                name: "cefr_confidence",
                table: "skill_graph_nodes");

            migrationBuilder.DropColumn(
                name: "cefr_source",
                table: "skill_graph_nodes");

            migrationBuilder.DropColumn(
                name: "node_type",
                table: "skill_graph_nodes");

            migrationBuilder.DropColumn(
                name: "routing_eligible",
                table: "skill_graph_nodes");
        }
    }
}

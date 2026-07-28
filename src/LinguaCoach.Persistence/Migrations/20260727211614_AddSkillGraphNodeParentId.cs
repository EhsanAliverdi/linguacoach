using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillGraphNodeParentId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "parent_node_id",
                table: "skill_graph_nodes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_skill_graph_nodes_parent_node_id",
                table: "skill_graph_nodes",
                column: "parent_node_id");

            migrationBuilder.AddForeignKey(
                name: "FK_skill_graph_nodes_skill_graph_nodes_parent_node_id",
                table: "skill_graph_nodes",
                column: "parent_node_id",
                principalTable: "skill_graph_nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_skill_graph_nodes_skill_graph_nodes_parent_node_id",
                table: "skill_graph_nodes");

            migrationBuilder.DropIndex(
                name: "ix_skill_graph_nodes_parent_node_id",
                table: "skill_graph_nodes");

            migrationBuilder.DropColumn(
                name: "parent_node_id",
                table: "skill_graph_nodes");
        }
    }
}

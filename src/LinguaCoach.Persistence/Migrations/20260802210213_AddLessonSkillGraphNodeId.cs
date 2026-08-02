using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLessonSkillGraphNodeId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "skill_graph_node_id",
                table: "lessons",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_lessons_skill_graph_node_id_unique",
                table: "lessons",
                column: "skill_graph_node_id",
                unique: true,
                filter: "skill_graph_node_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_lessons_skill_graph_nodes_skill_graph_node_id",
                table: "lessons",
                column: "skill_graph_node_id",
                principalTable: "skill_graph_nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_lessons_skill_graph_nodes_skill_graph_node_id",
                table: "lessons");

            migrationBuilder.DropIndex(
                name: "ix_lessons_skill_graph_node_id_unique",
                table: "lessons");

            migrationBuilder.DropColumn(
                name: "skill_graph_node_id",
                table: "lessons");
        }
    }
}

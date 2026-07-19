using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint2_AddModuleSkillGraphNodeLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "module_skill_graph_node_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    module_id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill_graph_node_id = table.Column<Guid>(type: "uuid", nullable: false),
                    confidence = table.Column<double>(type: "double precision", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_module_skill_graph_node_links", x => x.id);
                    table.ForeignKey(
                        name: "FK_module_skill_graph_node_links_modules_module_id",
                        column: x => x.module_id,
                        principalTable: "modules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_module_skill_graph_node_links_module",
                table: "module_skill_graph_node_links",
                column: "module_id");

            migrationBuilder.CreateIndex(
                name: "ix_module_skill_graph_node_links_module_node",
                table: "module_skill_graph_node_links",
                columns: new[] { "module_id", "skill_graph_node_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_module_skill_graph_node_links_node",
                table: "module_skill_graph_node_links",
                column: "skill_graph_node_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "module_skill_graph_node_links");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint1_AddSkillGraphFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "skill_graph_nodes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    cefr_level = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    skill = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    subskill = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    difficulty_band = table.Column<int>(type: "integer", nullable: false),
                    description_for_ai = table.Column<string>(type: "text", nullable: true),
                    review_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rejected_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skill_graph_nodes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "skill_graph_prerequisite_edges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    node_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prerequisite_node_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skill_graph_prerequisite_edges", x => x.id);
                    table.ForeignKey(
                        name: "FK_skill_graph_prerequisite_edges_skill_graph_nodes_node_id",
                        column: x => x.node_id,
                        principalTable: "skill_graph_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_skill_graph_prerequisite_edges_skill_graph_nodes_prerequisi~",
                        column: x => x.prerequisite_node_id,
                        principalTable: "skill_graph_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_skill_graph_nodes_cefr_skill_active",
                table: "skill_graph_nodes",
                columns: new[] { "cefr_level", "skill", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_skill_graph_nodes_key",
                table: "skill_graph_nodes",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_skill_graph_nodes_review_status",
                table: "skill_graph_nodes",
                column: "review_status");

            migrationBuilder.CreateIndex(
                name: "ix_skill_graph_prerequisite_edges_node",
                table: "skill_graph_prerequisite_edges",
                column: "node_id");

            migrationBuilder.CreateIndex(
                name: "ix_skill_graph_prerequisite_edges_node_prerequisite",
                table: "skill_graph_prerequisite_edges",
                columns: new[] { "node_id", "prerequisite_node_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_skill_graph_prerequisite_edges_prerequisite",
                table: "skill_graph_prerequisite_edges",
                column: "prerequisite_node_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "skill_graph_prerequisite_edges");

            migrationBuilder.DropTable(
                name: "skill_graph_nodes");
        }
    }
}

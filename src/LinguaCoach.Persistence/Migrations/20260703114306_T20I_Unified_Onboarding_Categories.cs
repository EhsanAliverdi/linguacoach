using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T20I_Unified_Onboarding_Categories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "category_id",
                table: "onboarding_step_definitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "onboarding_category_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    flow_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    category_order = table.Column<int>(type: "integer", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_onboarding_category_definitions", x => x.id);
                    table.ForeignKey(
                        name: "FK_onboarding_category_definitions_onboarding_flow_definitions~",
                        column: x => x.flow_definition_id,
                        principalTable: "onboarding_flow_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_onboarding_category_definitions_flow_order",
                table: "onboarding_category_definitions",
                columns: new[] { "flow_definition_id", "category_order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "onboarding_category_definitions");

            migrationBuilder.DropColumn(
                name: "category_id",
                table: "onboarding_step_definitions");
        }
    }
}

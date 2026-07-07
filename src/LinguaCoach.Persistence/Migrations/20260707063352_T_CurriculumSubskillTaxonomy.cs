using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T_CurriculumSubskillTaxonomy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "subskill",
                table: "student_learning_events",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "subskill",
                table: "student_activity_readiness_items",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "subskill",
                table: "placement_item_definitions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "subskill",
                table: "curriculum_objectives",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "subskill",
                table: "student_learning_events");

            migrationBuilder.DropColumn(
                name: "subskill",
                table: "student_activity_readiness_items");

            migrationBuilder.DropColumn(
                name: "subskill",
                table: "placement_item_definitions");

            migrationBuilder.DropColumn(
                name: "subskill",
                table: "curriculum_objectives");
        }
    }
}

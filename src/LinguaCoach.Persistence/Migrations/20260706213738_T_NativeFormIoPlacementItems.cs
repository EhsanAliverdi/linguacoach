using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T_NativeFormIoPlacementItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "placement_answers");

            migrationBuilder.DropColumn(
                name: "content_json",
                table: "placement_item_definitions");

            migrationBuilder.DropColumn(
                name: "correct_answer",
                table: "placement_item_definitions");

            migrationBuilder.DropColumn(
                name: "listening_audio_script",
                table: "placement_item_definitions");

            migrationBuilder.DropColumn(
                name: "reading_passage",
                table: "placement_item_definitions");

            migrationBuilder.DropColumn(
                name: "correct_answer",
                table: "placement_assessment_items");

            migrationBuilder.DropColumn(
                name: "evaluation_notes",
                table: "placement_assessment_items");

            migrationBuilder.DropColumn(
                name: "listening_audio_script",
                table: "placement_assessment_items");

            migrationBuilder.DropColumn(
                name: "reading_passage",
                table: "placement_assessment_items");

            migrationBuilder.DropColumn(
                name: "response",
                table: "placement_assessment_items");

            migrationBuilder.RenameColumn(
                name: "content_json",
                table: "placement_assessment_items",
                newName: "submission_data_json");

            migrationBuilder.RenameColumn(
                name: "answer_json",
                table: "placement_assessment_items",
                newName: "scoring_rules_json_snapshot");

            migrationBuilder.AddColumn<int>(
                name: "scoring_rules_version",
                table: "placement_item_definitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "normalized_answer_json",
                table: "placement_assessment_items",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "scoring_rules_version_snapshot",
                table: "placement_assessment_items",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "scoring_rules_version",
                table: "placement_item_definitions");

            migrationBuilder.DropColumn(
                name: "normalized_answer_json",
                table: "placement_assessment_items");

            migrationBuilder.DropColumn(
                name: "scoring_rules_version_snapshot",
                table: "placement_assessment_items");

            migrationBuilder.RenameColumn(
                name: "submission_data_json",
                table: "placement_assessment_items",
                newName: "content_json");

            migrationBuilder.RenameColumn(
                name: "scoring_rules_json_snapshot",
                table: "placement_assessment_items",
                newName: "answer_json");

            migrationBuilder.AddColumn<string>(
                name: "content_json",
                table: "placement_item_definitions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "correct_answer",
                table: "placement_item_definitions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "listening_audio_script",
                table: "placement_item_definitions",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reading_passage",
                table: "placement_item_definitions",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "correct_answer",
                table: "placement_assessment_items",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "evaluation_notes",
                table: "placement_assessment_items",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "listening_audio_script",
                table: "placement_assessment_items",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reading_passage",
                table: "placement_assessment_items",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "response",
                table: "placement_assessment_items",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "placement_answers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    placement_assessment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    response_text = table.Column<string>(type: "text", nullable: true),
                    score = table.Column<double>(type: "double precision", nullable: true),
                    section_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    selected_option = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_placement_answers", x => x.id);
                    table.ForeignKey(
                        name: "FK_placement_answers_placement_assessments_placement_assessmen~",
                        column: x => x.placement_assessment_id,
                        principalTable: "placement_assessments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_placement_answers_placement_assessment_id",
                table: "placement_answers",
                column: "placement_assessment_id");
        }
    }
}

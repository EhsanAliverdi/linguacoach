using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T17_WritingScenarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "scenario_id",
                table: "writing_submissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "writing_scenarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    language_pair_id = table.Column<Guid>(type: "uuid", nullable: true),
                    career_profile_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    situation = table.Column<string>(type: "text", nullable: false),
                    learning_goal = table.Column<string>(type: "text", nullable: false),
                    target_phrases_json = table.Column<string>(type: "jsonb", nullable: false),
                    target_vocabulary_json = table.Column<string>(type: "jsonb", nullable: false),
                    example_text = table.Column<string>(type: "text", nullable: false),
                    common_mistake_to_avoid = table.Column<string>(type: "text", nullable: false),
                    difficulty = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_writing_scenarios", x => x.id);
                    table.ForeignKey(
                        name: "FK_writing_scenarios_career_profiles_career_profile_id",
                        column: x => x.career_profile_id,
                        principalTable: "career_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_writing_scenarios_language_pairs_language_pair_id",
                        column: x => x.language_pair_id,
                        principalTable: "language_pairs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_writing_submissions_scenario_id",
                table: "writing_submissions",
                column: "scenario_id");

            migrationBuilder.CreateIndex(
                name: "ix_writing_scenarios_active",
                table: "writing_scenarios",
                column: "is_active",
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "IX_writing_scenarios_career_profile_id",
                table: "writing_scenarios",
                column: "career_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_writing_scenarios_language_pair_id",
                table: "writing_scenarios",
                column: "language_pair_id");

            migrationBuilder.AddForeignKey(
                name: "FK_writing_submissions_writing_scenarios_scenario_id",
                table: "writing_submissions",
                column: "scenario_id",
                principalTable: "writing_scenarios",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_writing_submissions_writing_scenarios_scenario_id",
                table: "writing_submissions");

            migrationBuilder.DropTable(
                name: "writing_scenarios");

            migrationBuilder.DropIndex(
                name: "IX_writing_submissions_scenario_id",
                table: "writing_submissions");

            migrationBuilder.DropColumn(
                name: "scenario_id",
                table: "writing_submissions");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase_E9_AddLeanBankSelectionMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "context_tags_json",
                table: "cefr_vocabulary_entries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "difficulty_band",
                table: "cefr_vocabulary_entries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "focus_tags_json",
                table: "cefr_vocabulary_entries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "subskill",
                table: "cefr_vocabulary_entries",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "context_tags_json",
                table: "cefr_reading_references",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "difficulty_band",
                table: "cefr_reading_references",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "focus_tags_json",
                table: "cefr_reading_references",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "subskill",
                table: "cefr_reading_references",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "context_tags_json",
                table: "cefr_grammar_profile_entries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "difficulty_band",
                table: "cefr_grammar_profile_entries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "focus_tags_json",
                table: "cefr_grammar_profile_entries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "subskill",
                table: "cefr_grammar_profile_entries",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "context_tags_json",
                table: "cefr_vocabulary_entries");

            migrationBuilder.DropColumn(
                name: "difficulty_band",
                table: "cefr_vocabulary_entries");

            migrationBuilder.DropColumn(
                name: "focus_tags_json",
                table: "cefr_vocabulary_entries");

            migrationBuilder.DropColumn(
                name: "subskill",
                table: "cefr_vocabulary_entries");

            migrationBuilder.DropColumn(
                name: "context_tags_json",
                table: "cefr_reading_references");

            migrationBuilder.DropColumn(
                name: "difficulty_band",
                table: "cefr_reading_references");

            migrationBuilder.DropColumn(
                name: "focus_tags_json",
                table: "cefr_reading_references");

            migrationBuilder.DropColumn(
                name: "subskill",
                table: "cefr_reading_references");

            migrationBuilder.DropColumn(
                name: "context_tags_json",
                table: "cefr_grammar_profile_entries");

            migrationBuilder.DropColumn(
                name: "difficulty_band",
                table: "cefr_grammar_profile_entries");

            migrationBuilder.DropColumn(
                name: "focus_tags_json",
                table: "cefr_grammar_profile_entries");

            migrationBuilder.DropColumn(
                name: "subskill",
                table: "cefr_grammar_profile_entries");
        }
    }
}

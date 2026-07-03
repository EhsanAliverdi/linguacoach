using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T20I_5_PlacementItemAudio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "audio_content_type",
                table: "placement_assessment_items",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "audio_storage_key",
                table: "placement_assessment_items",
                type: "character varying(500)",
                maxLength: 500,
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "audio_content_type",
                table: "placement_assessment_items");

            migrationBuilder.DropColumn(
                name: "audio_storage_key",
                table: "placement_assessment_items");

            migrationBuilder.DropColumn(
                name: "listening_audio_script",
                table: "placement_assessment_items");

            migrationBuilder.DropColumn(
                name: "reading_passage",
                table: "placement_assessment_items");
        }
    }
}

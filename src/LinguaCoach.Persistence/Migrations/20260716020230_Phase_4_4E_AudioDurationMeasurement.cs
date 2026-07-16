using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase_4_4E_AudioDurationMeasurement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "audio_duration_measured_at_utc",
                table: "import_assets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "audio_duration_measurement_checksum",
                table: "import_assets",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "audio_duration_measurement_error",
                table: "import_assets",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "audio_duration_measurement_status",
                table: "import_assets",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "NotMeasured");

            migrationBuilder.AddColumn<decimal>(
                name: "audio_duration_seconds",
                table: "import_assets",
                type: "numeric(10,3)",
                precision: 10,
                scale: 3,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "audio_duration_measured_at_utc",
                table: "import_assets");

            migrationBuilder.DropColumn(
                name: "audio_duration_measurement_checksum",
                table: "import_assets");

            migrationBuilder.DropColumn(
                name: "audio_duration_measurement_error",
                table: "import_assets");

            migrationBuilder.DropColumn(
                name: "audio_duration_measurement_status",
                table: "import_assets");

            migrationBuilder.DropColumn(
                name: "audio_duration_seconds",
                table: "import_assets");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T15_PerModelTestResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_test_error",
                table: "ai_provider_credentials");

            migrationBuilder.DropColumn(
                name: "last_test_ok",
                table: "ai_provider_credentials");

            migrationBuilder.DropColumn(
                name: "last_tested_at",
                table: "ai_provider_credentials");

            migrationBuilder.AddColumn<string>(
                name: "model_tests",
                table: "ai_provider_credentials",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "model_tests",
                table: "ai_provider_credentials");

            migrationBuilder.AddColumn<string>(
                name: "last_test_error",
                table: "ai_provider_credentials",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "last_test_ok",
                table: "ai_provider_credentials",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_tested_at",
                table: "ai_provider_credentials",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}

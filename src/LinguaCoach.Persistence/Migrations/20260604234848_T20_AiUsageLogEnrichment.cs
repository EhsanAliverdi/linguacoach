using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T20_AiUsageLogEnrichment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "student_profile_id",
                table: "ai_usage_logs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "correlation_id",
                table: "ai_usage_logs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "duration_ms",
                table: "ai_usage_logs",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "failure_reason",
                table: "ai_usage_logs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "feature_key",
                table: "ai_usage_logs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "is_fallback",
                table: "ai_usage_logs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "was_successful",
                table: "ai_usage_logs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "fallback_enabled",
                table: "ai_provider_configs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "fallback_model_name",
                table: "ai_provider_configs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fallback_provider_name",
                table: "ai_provider_configs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_ai_usage_logs_feature_key",
                table: "ai_usage_logs",
                column: "feature_key");

            migrationBuilder.CreateIndex(
                name: "ix_ai_usage_logs_provider_name",
                table: "ai_usage_logs",
                column: "provider_name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ai_usage_logs_feature_key",
                table: "ai_usage_logs");

            migrationBuilder.DropIndex(
                name: "ix_ai_usage_logs_provider_name",
                table: "ai_usage_logs");

            migrationBuilder.DropColumn(
                name: "correlation_id",
                table: "ai_usage_logs");

            migrationBuilder.DropColumn(
                name: "duration_ms",
                table: "ai_usage_logs");

            migrationBuilder.DropColumn(
                name: "failure_reason",
                table: "ai_usage_logs");

            migrationBuilder.DropColumn(
                name: "feature_key",
                table: "ai_usage_logs");

            migrationBuilder.DropColumn(
                name: "is_fallback",
                table: "ai_usage_logs");

            migrationBuilder.DropColumn(
                name: "was_successful",
                table: "ai_usage_logs");

            migrationBuilder.DropColumn(
                name: "fallback_enabled",
                table: "ai_provider_configs");

            migrationBuilder.DropColumn(
                name: "fallback_model_name",
                table: "ai_provider_configs");

            migrationBuilder.DropColumn(
                name: "fallback_provider_name",
                table: "ai_provider_configs");

            migrationBuilder.AlterColumn<Guid>(
                name: "student_profile_id",
                table: "ai_usage_logs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}

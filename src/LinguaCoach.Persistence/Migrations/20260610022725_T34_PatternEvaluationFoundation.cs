using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T34_PatternEvaluationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "completed",
                table: "activity_attempts",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "evaluation_result_json",
                table: "activity_attempts",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "marking_mode",
                table: "activity_attempts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "max_score",
                table: "activity_attempts",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "passed",
                table: "activity_attempts",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "percentage",
                table: "activity_attempts",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "submitted_answer_json",
                table: "activity_attempts",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "completed",
                table: "activity_attempts");

            migrationBuilder.DropColumn(
                name: "evaluation_result_json",
                table: "activity_attempts");

            migrationBuilder.DropColumn(
                name: "marking_mode",
                table: "activity_attempts");

            migrationBuilder.DropColumn(
                name: "max_score",
                table: "activity_attempts");

            migrationBuilder.DropColumn(
                name: "passed",
                table: "activity_attempts");

            migrationBuilder.DropColumn(
                name: "percentage",
                table: "activity_attempts");

            migrationBuilder.DropColumn(
                name: "submitted_answer_json",
                table: "activity_attempts");
        }
    }
}

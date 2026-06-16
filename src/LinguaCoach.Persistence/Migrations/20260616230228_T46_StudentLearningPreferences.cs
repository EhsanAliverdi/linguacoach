using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T46_StudentLearningPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "custom_focus_area",
                table: "student_profiles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "custom_learning_goal",
                table: "student_profiles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "difficulty_preference",
                table: "student_profiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "focus_areas",
                table: "student_profiles",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "learning_goals",
                table: "student_profiles",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "learning_preferences_updated_at",
                table: "student_profiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "preferred_name",
                table: "student_profiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "support_language_code",
                table: "student_profiles",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "support_language_name",
                table: "student_profiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "translation_help_preference",
                table: "student_profiles",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "custom_focus_area",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "custom_learning_goal",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "difficulty_preference",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "focus_areas",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "learning_goals",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "learning_preferences_updated_at",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "preferred_name",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "support_language_code",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "support_language_name",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "translation_help_preference",
                table: "student_profiles");
        }
    }
}

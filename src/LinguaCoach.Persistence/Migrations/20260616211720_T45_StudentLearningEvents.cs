using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T45_StudentLearningEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "student_learning_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    activity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    session_exercise_id = table.Column<Guid>(type: "uuid", nullable: true),
                    activity_attempt_id = table.Column<Guid>(type: "uuid", nullable: true),
                    exercise_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    pattern_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    primary_skill = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    secondary_skills_json = table.Column<string>(type: "text", nullable: true),
                    learning_goal_context = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    cefr_level_at_event = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    concepts_taught_json = table.Column<string>(type: "text", nullable: true),
                    concepts_practised_json = table.Column<string>(type: "text", nullable: true),
                    mistake_tags_json = table.Column<string>(type: "text", nullable: true),
                    score = table.Column<double>(type: "double precision", nullable: true),
                    normalized_score = table.Column<double>(type: "double precision", nullable: true),
                    outcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    metadata_json = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_learning_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_student_learning_events_student_profiles_student_profile_id",
                        column: x => x.student_profile_id,
                        principalTable: "student_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_student_learning_events_student",
                table: "student_learning_events",
                column: "student_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_learning_events_student_pattern",
                table: "student_learning_events",
                columns: new[] { "student_profile_id", "pattern_key" });

            migrationBuilder.CreateIndex(
                name: "ix_student_learning_events_student_time",
                table: "student_learning_events",
                columns: new[] { "student_profile_id", "occurred_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "student_learning_events");
        }
    }
}

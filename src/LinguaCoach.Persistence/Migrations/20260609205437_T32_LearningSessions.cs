using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T32_LearningSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "learning_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    learning_module_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    topic = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    session_goal = table.Column<string>(type: "text", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    focus_skill = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    secondary_skills_json = table.Column<string>(type: "text", nullable: true),
                    order = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    generated_from_memory_snapshot_json = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learning_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_learning_sessions_learning_modules_learning_module_id",
                        column: x => x.learning_module_id,
                        principalTable: "learning_modules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_exercises",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    learning_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    exercise_pattern_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    primary_skill = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    secondary_skills_json = table.Column<string>(type: "text", nullable: true),
                    estimated_minutes = table.Column<int>(type: "integer", nullable: false),
                    instructions = table.Column<string>(type: "text", nullable: false),
                    learning_activity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_exercises", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_exercises_learning_activities_learning_activity_id",
                        column: x => x.learning_activity_id,
                        principalTable: "learning_activities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_session_exercises_learning_sessions_learning_session_id",
                        column: x => x.learning_session_id,
                        principalTable: "learning_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_learning_sessions_module_order",
                table: "learning_sessions",
                columns: new[] { "learning_module_id", "order" });

            migrationBuilder.CreateIndex(
                name: "ix_learning_sessions_status",
                table: "learning_sessions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_session_exercises_learning_activity_id",
                table: "session_exercises",
                column: "learning_activity_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_exercises_session_order",
                table: "session_exercises",
                columns: new[] { "learning_session_id", "order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "session_exercises");

            migrationBuilder.DropTable(
                name: "learning_sessions");
        }
    }
}

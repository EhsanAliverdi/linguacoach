using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T18_LearningActivities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "learning_paths",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    learner_context_summary = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learning_paths", x => x.id);
                    table.ForeignKey(
                        name: "FK_learning_paths_student_profiles_student_profile_id",
                        column: x => x.student_profile_id,
                        principalTable: "student_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "learning_modules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    learning_path_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learning_modules", x => x.id);
                    table.ForeignKey(
                        name: "FK_learning_modules_learning_paths_learning_path_id",
                        column: x => x.learning_path_id,
                        principalTable: "learning_paths",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "learning_activities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    learning_module_id = table.Column<Guid>(type: "uuid", nullable: true),
                    activity_type = table.Column<int>(type: "integer", nullable: false),
                    source = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    difficulty = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ai_generated_content_json = table.Column<string>(type: "jsonb", nullable: false),
                    source_writing_scenario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learning_activities", x => x.id);
                    table.ForeignKey(
                        name: "FK_learning_activities_learning_modules_learning_module_id",
                        column: x => x.learning_module_id,
                        principalTable: "learning_modules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "activity_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    learning_activity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_content = table.Column<string>(type: "text", nullable: false),
                    audio_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    feedback_json = table.Column<string>(type: "jsonb", nullable: false),
                    score = table.Column<double>(type: "double precision", nullable: true),
                    prompt_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_attempts", x => x.id);
                    table.ForeignKey(
                        name: "FK_activity_attempts_learning_activities_learning_activity_id",
                        column: x => x.learning_activity_id,
                        principalTable: "learning_activities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_activity_attempts_student_profiles_student_profile_id",
                        column: x => x.student_profile_id,
                        principalTable: "student_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_activity_attempts_activity",
                table: "activity_attempts",
                column: "learning_activity_id");

            migrationBuilder.CreateIndex(
                name: "ix_activity_attempts_student",
                table: "activity_attempts",
                column: "student_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_learning_activities_learning_module_id",
                table: "learning_activities",
                column: "learning_module_id");

            migrationBuilder.CreateIndex(
                name: "ix_learning_activities_source",
                table: "learning_activities",
                column: "source");

            migrationBuilder.CreateIndex(
                name: "ix_learning_activities_type_active",
                table: "learning_activities",
                columns: new[] { "activity_type", "is_active" },
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "ix_learning_modules_path_order",
                table: "learning_modules",
                columns: new[] { "learning_path_id", "order" });

            migrationBuilder.CreateIndex(
                name: "ix_learning_paths_student_active",
                table: "learning_paths",
                columns: new[] { "student_profile_id", "is_active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_attempts");

            migrationBuilder.DropTable(
                name: "learning_activities");

            migrationBuilder.DropTable(
                name: "learning_modules");

            migrationBuilder.DropTable(
                name: "learning_paths");
        }
    }
}

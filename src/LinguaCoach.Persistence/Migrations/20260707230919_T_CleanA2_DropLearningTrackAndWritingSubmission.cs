using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T_CleanA2_DropLearningTrackAndWritingSubmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_student_profiles_learning_tracks_learning_track_id",
                table: "student_profiles");

            migrationBuilder.DropTable(
                name: "learning_tracks");

            migrationBuilder.DropTable(
                name: "writing_submissions");

            migrationBuilder.DropIndex(
                name: "ix_student_profiles_learning_track_id",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "learning_track_id",
                table: "student_profiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "learning_track_id",
                table: "student_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "learning_tracks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    language_pair_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learning_tracks", x => x.id);
                    table.ForeignKey(
                        name: "FK_learning_tracks_language_pairs_language_pair_id",
                        column: x => x.language_pair_id,
                        principalTable: "language_pairs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "writing_submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    corrected_text = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    feedback_json = table.Column<string>(type: "jsonb", nullable: false),
                    original_text = table.Column<string>(type: "text", nullable: false),
                    prompt_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    scenario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    scenario_title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    score = table.Column<double>(type: "double precision", nullable: true),
                    student_profile_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_writing_submissions", x => x.id);
                    table.ForeignKey(
                        name: "FK_writing_submissions_student_profiles_student_profile_id",
                        column: x => x.student_profile_id,
                        principalTable: "student_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_writing_submissions_writing_scenarios_scenario_id",
                        column: x => x.scenario_id,
                        principalTable: "writing_scenarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "learning_tracks",
                columns: new[] { "id", "created_at", "description", "language_pair_id", "name" },
                values: new object[] { new Guid("30000000-0000-0000-0000-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Role-specific English for professional workplace communication.", new Guid("20000000-0000-0000-0000-000000000001"), "Workplace English" });

            migrationBuilder.CreateIndex(
                name: "ix_student_profiles_learning_track_id",
                table: "student_profiles",
                column: "learning_track_id");

            migrationBuilder.CreateIndex(
                name: "ix_learning_tracks_language_pair_id",
                table: "learning_tracks",
                column: "language_pair_id");

            migrationBuilder.CreateIndex(
                name: "IX_writing_submissions_scenario_id",
                table: "writing_submissions",
                column: "scenario_id");

            migrationBuilder.CreateIndex(
                name: "ix_writing_submissions_student_created",
                table: "writing_submissions",
                columns: new[] { "student_profile_id", "created_at" });

            migrationBuilder.AddForeignKey(
                name: "FK_student_profiles_learning_tracks_learning_track_id",
                table: "student_profiles",
                column: "learning_track_id",
                principalTable: "learning_tracks",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

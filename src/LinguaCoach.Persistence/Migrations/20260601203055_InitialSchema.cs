using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_prompts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_prompts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "languages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    direction = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_languages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "language_pairs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_language_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_language_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_language_pairs", x => x.id);
                    table.ForeignKey(
                        name: "FK_language_pairs_languages_source_language_id",
                        column: x => x.source_language_id,
                        principalTable: "languages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_language_pairs_languages_target_language_id",
                        column: x => x.target_language_id,
                        principalTable: "languages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "career_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    language_pair_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_career_profiles", x => x.id);
                    table.ForeignKey(
                        name: "FK_career_profiles_language_pairs_language_pair_id",
                        column: x => x.language_pair_id,
                        principalTable: "language_pairs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "learning_tracks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    language_pair_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
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
                name: "student_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    onboarding_status = table.Column<int>(type: "integer", nullable: false),
                    last_completed_step = table.Column<int>(type: "integer", nullable: false),
                    language_pair_id = table.Column<Guid>(type: "uuid", nullable: true),
                    learning_track_id = table.Column<Guid>(type: "uuid", nullable: true),
                    career_profile_id = table.Column<Guid>(type: "uuid", nullable: true),
                    skill_focus = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_profiles", x => x.id);
                    table.ForeignKey(
                        name: "FK_student_profiles_career_profiles_career_profile_id",
                        column: x => x.career_profile_id,
                        principalTable: "career_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_student_profiles_language_pairs_language_pair_id",
                        column: x => x.language_pair_id,
                        principalTable: "language_pairs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_student_profiles_learning_tracks_learning_track_id",
                        column: x => x.learning_track_id,
                        principalTable: "learning_tracks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_usage_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    input_tokens = table.Column<int>(type: "integer", nullable: false),
                    output_tokens = table.Column<int>(type: "integer", nullable: false),
                    cost_usd = table.Column<decimal>(type: "numeric(10,6)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_usage_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_ai_usage_logs_student_profiles_student_profile_id",
                        column: x => x.student_profile_id,
                        principalTable: "student_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "languages",
                columns: new[] { "id", "code", "created_at", "direction", "name" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "fa", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, "Persian" },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "en", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0, "English" }
                });

            migrationBuilder.InsertData(
                table: "language_pairs",
                columns: new[] { "id", "created_at", "is_active", "source_language_id", "target_language_id" },
                values: new object[] { new Guid("20000000-0000-0000-0000-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, new Guid("10000000-0000-0000-0000-000000000001"), new Guid("10000000-0000-0000-0000-000000000002") });

            migrationBuilder.InsertData(
                table: "career_profiles",
                columns: new[] { "id", "created_at", "description", "language_pair_id", "name" },
                values: new object[] { new Guid("40000000-0000-0000-0000-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Role-specific English for document control professionals in construction and engineering projects.", new Guid("20000000-0000-0000-0000-000000000001"), "Document Controller" });

            migrationBuilder.InsertData(
                table: "learning_tracks",
                columns: new[] { "id", "created_at", "description", "language_pair_id", "name" },
                values: new object[] { new Guid("30000000-0000-0000-0000-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Role-specific English for professional workplace communication.", new Guid("20000000-0000-0000-0000-000000000001"), "Workplace English" });

            migrationBuilder.CreateIndex(
                name: "ix_ai_prompts_key_version",
                table: "ai_prompts",
                columns: new[] { "key", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ai_usage_logs_student_profile_id",
                table: "ai_usage_logs",
                column: "student_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_usage_logs_student_profile_id_created_at",
                table: "ai_usage_logs",
                columns: new[] { "student_profile_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_career_profiles_language_pair_id",
                table: "career_profiles",
                column: "language_pair_id");

            migrationBuilder.CreateIndex(
                name: "ix_language_pairs_source_target",
                table: "language_pairs",
                columns: new[] { "source_language_id", "target_language_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_language_pairs_target_language_id",
                table: "language_pairs",
                column: "target_language_id");

            migrationBuilder.CreateIndex(
                name: "ix_languages_code",
                table: "languages",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_learning_tracks_language_pair_id",
                table: "learning_tracks",
                column: "language_pair_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_profiles_career_profile_id",
                table: "student_profiles",
                column: "career_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_profiles_language_pair_id",
                table: "student_profiles",
                column: "language_pair_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_profiles_learning_track_id",
                table: "student_profiles",
                column: "learning_track_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_profiles_user_id",
                table: "student_profiles",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_student_profiles_user_id_onboarding_status",
                table: "student_profiles",
                columns: new[] { "user_id", "onboarding_status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_prompts");

            migrationBuilder.DropTable(
                name: "ai_usage_logs");

            migrationBuilder.DropTable(
                name: "student_profiles");

            migrationBuilder.DropTable(
                name: "career_profiles");

            migrationBuilder.DropTable(
                name: "learning_tracks");

            migrationBuilder.DropTable(
                name: "language_pairs");

            migrationBuilder.DropTable(
                name: "languages");
        }
    }
}

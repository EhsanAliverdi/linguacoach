using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T3_VocabularyAndSpeakingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "max_input_tokens",
                table: "ai_prompts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "max_output_tokens",
                table: "ai_prompts",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "curriculum_word_lists",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    career_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    language_pair_id = table.Column<Guid>(type: "uuid", nullable: false),
                    word = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    definition = table.Column<string>(type: "text", nullable: false),
                    example_sentence = table.Column<string>(type: "text", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    tags = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_curriculum_word_lists", x => x.id);
                    table.ForeignKey(
                        name: "FK_curriculum_word_lists_career_profiles_career_profile_id",
                        column: x => x.career_profile_id,
                        principalTable: "career_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_curriculum_word_lists_language_pairs_language_pair_id",
                        column: x => x.language_pair_id,
                        principalTable: "language_pairs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "speaking_scenarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    career_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    language_pair_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    goal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    max_turns = table.Column<int>(type: "integer", nullable: false),
                    target_phrases = table.Column<string>(type: "text", nullable: false),
                    rubric = table.Column<string>(type: "text", nullable: false),
                    difficulty_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_speaking_scenarios", x => x.id);
                    table.ForeignKey(
                        name: "FK_speaking_scenarios_career_profiles_career_profile_id",
                        column: x => x.career_profile_id,
                        principalTable: "career_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_speaking_scenarios_language_pairs_language_pair_id",
                        column: x => x.language_pair_id,
                        principalTable: "language_pairs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_learning_summaries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recent_weaknesses = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    recent_progress = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_learning_summaries", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_learning_summaries_student_profiles_student_profile_id",
                        column: x => x.student_profile_id,
                        principalTable: "student_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vocabulary_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    language_pair_id = table.Column<Guid>(type: "uuid", nullable: false),
                    word = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    definition = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    recognition_count = table.Column<int>(type: "integer", nullable: false),
                    recall_count = table.Column<int>(type: "integer", nullable: false),
                    usage_count = table.Column<int>(type: "integer", nullable: false),
                    exposure_count = table.Column<int>(type: "integer", nullable: false),
                    correct_count = table.Column<int>(type: "integer", nullable: false),
                    incorrect_count = table.Column<int>(type: "integer", nullable: false),
                    last_seen = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_practised = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    next_review_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ease_factor = table.Column<double>(type: "double precision", nullable: false),
                    mastery_score = table.Column<double>(type: "double precision", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vocabulary_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_vocabulary_entries_language_pairs_language_pair_id",
                        column: x => x.language_pair_id,
                        principalTable: "language_pairs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vocabulary_entries_student_profiles_student_profile_id",
                        column: x => x.student_profile_id,
                        principalTable: "student_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "speaking_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scenario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    cefr_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    career_context = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    max_turns = table.Column<int>(type: "integer", nullable: false),
                    current_turn = table.Column<int>(type: "integer", nullable: false),
                    overall_score = table.Column<double>(type: "double precision", nullable: true),
                    session_summary = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_speaking_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_speaking_sessions_speaking_scenarios_scenario_id",
                        column: x => x.scenario_id,
                        principalTable: "speaking_scenarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_speaking_sessions_student_profiles_student_profile_id",
                        column: x => x.student_profile_id,
                        principalTable: "student_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "speaking_turns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    speaking_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    turn_number = table.Column<int>(type: "integer", nullable: false),
                    ai_question = table.Column<string>(type: "text", nullable: false),
                    user_transcript = table.Column<string>(type: "text", nullable: true),
                    user_audio_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ai_reply = table.Column<string>(type: "text", nullable: false),
                    feedback_json = table.Column<string>(type: "jsonb", nullable: false),
                    mistakes_json = table.Column<string>(type: "jsonb", nullable: false),
                    pronunciation_score = table.Column<double>(type: "double precision", nullable: true),
                    grammar_score = table.Column<double>(type: "double precision", nullable: true),
                    vocabulary_score = table.Column<double>(type: "double precision", nullable: true),
                    fluency_score = table.Column<double>(type: "double precision", nullable: true),
                    turn_summary = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_speaking_turns", x => x.id);
                    table.ForeignKey(
                        name: "FK_speaking_turns_speaking_sessions_speaking_session_id",
                        column: x => x.speaking_session_id,
                        principalTable: "speaking_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_curriculum_word_lists_career_lang_priority",
                table: "curriculum_word_lists",
                columns: new[] { "career_profile_id", "language_pair_id", "priority" });

            migrationBuilder.CreateIndex(
                name: "IX_curriculum_word_lists_language_pair_id",
                table: "curriculum_word_lists",
                column: "language_pair_id");

            migrationBuilder.CreateIndex(
                name: "ix_speaking_scenarios_career_lang",
                table: "speaking_scenarios",
                columns: new[] { "career_profile_id", "language_pair_id" });

            migrationBuilder.CreateIndex(
                name: "IX_speaking_scenarios_language_pair_id",
                table: "speaking_scenarios",
                column: "language_pair_id");

            migrationBuilder.CreateIndex(
                name: "ix_speaking_sessions_scenario_id",
                table: "speaking_sessions",
                column: "scenario_id");

            migrationBuilder.CreateIndex(
                name: "ix_speaking_sessions_student_status",
                table: "speaking_sessions",
                columns: new[] { "student_profile_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_speaking_turns_session_turn",
                table: "speaking_turns",
                columns: new[] { "speaking_session_id", "turn_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_learning_summaries_student_profile_id",
                table: "user_learning_summaries",
                column: "student_profile_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vocabulary_entries_language_pair_id",
                table: "vocabulary_entries",
                column: "language_pair_id");

            migrationBuilder.CreateIndex(
                name: "ix_vocabulary_entries_student_lang",
                table: "vocabulary_entries",
                columns: new[] { "student_profile_id", "language_pair_id" });

            migrationBuilder.CreateIndex(
                name: "ix_vocabulary_entries_student_next_review",
                table: "vocabulary_entries",
                columns: new[] { "student_profile_id", "next_review_date" });

            migrationBuilder.CreateIndex(
                name: "ix_vocabulary_entries_student_status",
                table: "vocabulary_entries",
                columns: new[] { "student_profile_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "curriculum_word_lists");

            migrationBuilder.DropTable(
                name: "speaking_turns");

            migrationBuilder.DropTable(
                name: "user_learning_summaries");

            migrationBuilder.DropTable(
                name: "vocabulary_entries");

            migrationBuilder.DropTable(
                name: "speaking_sessions");

            migrationBuilder.DropTable(
                name: "speaking_scenarios");

            migrationBuilder.DropColumn(
                name: "max_input_tokens",
                table: "ai_prompts");

            migrationBuilder.DropColumn(
                name: "max_output_tokens",
                table: "ai_prompts");
        }
    }
}

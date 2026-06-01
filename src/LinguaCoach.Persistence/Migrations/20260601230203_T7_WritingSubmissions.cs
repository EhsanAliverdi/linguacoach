using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T7_WritingSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "writing_submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scenario_title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    original_text = table.Column<string>(type: "text", nullable: false),
                    corrected_text = table.Column<string>(type: "text", nullable: false),
                    feedback_json = table.Column<string>(type: "jsonb", nullable: false),
                    score = table.Column<double>(type: "double precision", nullable: true),
                    prompt_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
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
                });

            migrationBuilder.CreateIndex(
                name: "ix_writing_submissions_student_created",
                table: "writing_submissions",
                columns: new[] { "student_profile_id", "created_at" });

            // Seed the writing exercise prompt template.
            // Variables: {{sourceLanguageName}}, {{targetLanguageName}}, {{userLevel}},
            //            {{careerProfile}}, {{scenario}}, {{targetVocabulary}}, {{targetPhrases}}, {{userDraft}}
            migrationBuilder.InsertData(
                table: "ai_prompts",
                columns: new[] { "id", "key", "content", "version", "is_active", "max_input_tokens", "max_output_tokens", "created_at" },
                values: new object[]
                {
                    new Guid("50000000-0000-0000-0000-000000000001"),
                    "writing.exercise.v1",
                    @"You are an expert English writing coach for {{sourceLanguageName}}-speaking professionals learning {{targetLanguageName}}.

The student's approximate level is {{userLevel}}.
Career context: {{careerProfile}}.
Scenario: {{scenario}}.

Target vocabulary to use or check: {{targetVocabulary}}.
Target phrases to encourage: {{targetPhrases}}.

The student has written the following draft:
---
{{userDraft}}
---

Evaluate the draft and return ONLY valid JSON (no markdown, no explanation outside JSON) matching this exact structure:
{
  ""overallScore"": <number 0-100>,
  ""correctedEmail"": ""<a corrected, professional version of their email>"",
  ""feedbackInSourceLanguage"": ""<2-3 sentences of encouraging, specific feedback written in {{sourceLanguageName}}>"",
  ""grammarIssues"": [""<specific issue 1>"", ""<specific issue 2>""],
  ""vocabularyIssues"": [""<specific issue 1>""],
  ""toneIssues"": [""<specific issue 1>""],
  ""suggestedPhrases"": [""<phrase 1>"", ""<phrase 2>""],
  ""mistakesToTrack"": [""<short mistake description 1>"", ""<short mistake description 2>""]
}

Rules:
- overallScore must be a number between 0 and 100.
- correctedEmail must be a complete, polished professional email.
- feedbackInSourceLanguage must be warm, specific, and written entirely in {{sourceLanguageName}}.
- All arrays may be empty [] if there are no issues.
- Do not include any text outside the JSON object.",
                    1,
                    true,
                    800,
                    600,
                    new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData("ai_prompts", "id", new Guid("50000000-0000-0000-0000-000000000001"));
            migrationBuilder.DropTable(name: "writing_submissions");
        }
    }
}

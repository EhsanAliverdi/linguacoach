using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class T10_CefrAssessment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cefr_level",
                table: "student_profiles",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.InsertData(
                table: "ai_prompts",
                columns: new[] { "id", "key", "content", "version", "is_active", "max_input_tokens", "max_output_tokens", "created_at" },
                values: new object[]
                {
                    new System.Guid("50000000-0000-0000-0000-000000000002"),
                    "cefr.assessment.v1",
                    @"You are an expert English language assessor. Your task is to determine a learner's CEFR level based on a short writing sample.

The student's native language is {{sourceLanguageName}} and they are learning {{targetLanguageName}}.
Career context: {{careerProfile}}.

The student wrote the following text:
---
{{studentSample}}
---

Assess the writing and return ONLY valid JSON (no markdown, no explanation outside JSON) matching this exact structure:
{
  ""level"": ""<one of: A1, A2, B1, B2, C1, C2>"",
  ""rationale"": ""<1-2 sentences explaining the level in {{sourceLanguageName}}>"",
  ""strengths"": [""<strength 1>"", ""<strength 2>""],
  ""areasForImprovement"": [""<area 1>"", ""<area 2>""]
}

Rules:
- level must be exactly one of: A1, A2, B1, B2, C1, C2 (uppercase, no suffix).
- rationale must be written in {{sourceLanguageName}}.
- All arrays may be empty [] if there is nothing to report.
- Do not include any text outside the JSON object.",
                    1,
                    true,
                    600,
                    300,
                    new System.DateTime(2026, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "cefr_level", table: "student_profiles");
            migrationBuilder.DeleteData("ai_prompts", "id", new System.Guid("50000000-0000-0000-0000-000000000002"));
        }
    }
}

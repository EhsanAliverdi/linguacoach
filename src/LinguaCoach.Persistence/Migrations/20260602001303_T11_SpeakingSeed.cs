using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinguaCoach.Persistence.Migrations
{
    file static class T11Seeds
    {
        internal static readonly Guid FaEnPairId = new("20000000-0000-0000-0000-000000000001");
        internal static readonly Guid DocumentControllerProfileId = new("40000000-0000-0000-0000-000000000001");
        internal static readonly Guid DocControllerScenarioId = new("70000000-0000-0000-0000-000000000001");
        internal static readonly Guid SpeakingPromptId = new("50000000-0000-0000-0000-000000000003");
        internal static readonly DateTime SeedDate = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    /// <inheritdoc />
    public partial class T11_SpeakingSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ai_prompts",
                columns: new[] { "id", "key", "content", "version", "is_active", "max_input_tokens", "max_output_tokens", "created_at" },
                values: new object[]
                {
                    T11Seeds.SpeakingPromptId,
                    "speaking.turn.v1",
                    @"You are an English speaking coach for a {{sourceLanguageName}}-speaking professional at {{cefrLevel}} level.
Career context: {{careerContext}}.
Scenario goal: {{scenarioGoal}}.

Previous turn summary: {{previousTurnSummary}}
Student said: {{userTranscript}}

Evaluate what the student said and respond as their conversation partner. Return ONLY valid JSON:
{
  ""aiReply"": ""<your next question or response to continue the conversation>"",
  ""pronunciationScore"": null,
  ""grammarScore"": <0-100>,
  ""vocabularyScore"": <0-100>,
  ""fluencyScore"": <0-100>,
  ""feedback"": ""<1-2 sentences of specific encouraging feedback in {{sourceLanguageName}}>"",
  ""mistakes"": [""<mistake 1>""],
  ""turnSummary"": ""<max 150 chars: what was discussed this turn>""
}

Rules:
- aiReply must continue the conversation naturally toward the scenario goal.
- pronunciationScore is always null (no audio in this MVP).
- feedback must be in {{sourceLanguageName}}.
- mistakes array may be empty.
- turnSummary must be at most 150 characters.
- Do not include any text outside the JSON object.",
                    1,
                    true,
                    700,
                    400,
                    T11Seeds.SeedDate
                });

            migrationBuilder.InsertData(
                table: "speaking_scenarios",
                columns: new[] { "id", "career_profile_id", "language_pair_id", "title", "goal", "max_turns", "target_phrases", "rubric", "difficulty_level", "created_at" },
                values: new object[]
                {
                    T11Seeds.DocControllerScenarioId,
                    T11Seeds.DocumentControllerProfileId,
                    T11Seeds.FaEnPairId,
                    "Document approval follow-up call",
                    "Practice a professional telephone conversation following up on a pending document approval with a project manager.",
                    6,
                    "I wanted to follow up on,Could you confirm,I appreciate your time,As per our previous conversation,I look forward to hearing from you",
                    "Evaluate professional tone, clarity of purpose, use of target phrases, and ability to handle responses politely.",
                    "B1",
                    T11Seeds.SeedDate
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData("speaking_scenarios", "id", T11Seeds.DocControllerScenarioId);
            migrationBuilder.DeleteData("ai_prompts", "id", T11Seeds.SpeakingPromptId);
        }
    }
}

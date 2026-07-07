using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>Covers StudentOnboardingFlowService's CEFR quick-check scoring after it was upgraded
/// from its bespoke {"correctAnswerKey"} shape onto the shared placement ComponentScoringRule
/// shape (ScoringRulesDocument), so onboarding and placement's Quiz-tab-authored answers use one
/// model. Confirms both the legacy pre-upgrade flat scoring-rules shape (never re-migrated, read
/// compatibly forever) and the new unified shape still score correctly.</summary>
public sealed class OnboardingQuickCheckScoringTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public OnboardingQuickCheckScoringTests(ApiTestFactory factory) => _factory = factory;

    private static object RadioQuizSchema(string correctAnswer) => new
    {
        components = new object[]
        {
            new { type = "textfield", key = "preferred_name", label = "Name" },
            new
            {
                type = "radio", key = "assessment_q1", label = "Pick one",
                values = new[] { new { label = "am", value = "A" }, new { label = "is", value = "B" } },
                quiz = new { enabled = true, rule = new { kind = "single_choice", correctAnswer } },
            },
        },
    };

    private async Task<Guid> CreateAndPublishTemplateAsync(System.Net.Http.HttpClient adminClient, string? scoringRulesJson, string? authoringSchemaJson)
    {
        var createResp = await adminClient.PostAsJsonAsync("/api/admin/onboarding/templates", new
        {
            name = $"Quick-check scoring test {Guid.NewGuid():N}",
            description = (string?)null,
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var createBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var templateId = createBody.GetProperty("templateId").GetGuid();

        var schemaJson = JsonSerializer.Serialize(new
        {
            components = new object[]
            {
                new { type = "textfield", key = "preferred_name", label = "Name" },
                new
                {
                    type = "radio", key = "assessment_q1", label = "Pick one",
                    values = new[] { new { label = "am", value = "A" }, new { label = "is", value = "B" } },
                },
            },
        });

        var draftResp = await adminClient.PutAsJsonAsync($"/api/admin/onboarding/templates/{templateId}/draft", new
        {
            formIoSchemaJson = schemaJson,
            scoringRulesJson,
            authoringSchemaJson,
        });
        Assert.Equal(HttpStatusCode.OK, draftResp.StatusCode);

        var publishResp = await adminClient.PostAsync($"/api/admin/onboarding/templates/{templateId}/publish", null);
        Assert.Equal(HttpStatusCode.OK, publishResp.StatusCode);

        return templateId;
    }

    private async Task<string?> SubmitAsync(string studentAnswer)
    {
        var (studentToken, _) = await _factory.CreateStudentAndGetTokenAsync($"quickcheck_{Guid.NewGuid():N}@test.com");
        var studentClient = _factory.CreateClient();
        studentClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", studentToken);

        var submissionJson = JsonSerializer.Serialize(new { preferred_name = "Alex", assessment_q1 = studentAnswer });
        var resp = await studentClient.PostAsJsonAsync("/api/onboarding/submit", new { submissionJson });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("success").GetBoolean());
        return body.TryGetProperty("preliminaryCefrLevel", out var cefr) && cefr.ValueKind == JsonValueKind.String
            ? cefr.GetString()
            : null;
    }

    [Fact]
    public async Task LegacyFlatScoringRulesShape_CorrectAnswer_ScoresAsC2()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

        var legacyScoringRules = JsonSerializer.Serialize(new { assessment_q1 = new { correctAnswerKey = "B" } });
        await CreateAndPublishTemplateAsync(adminClient, legacyScoringRules, authoringSchemaJson: null);

        var cefr = await SubmitAsync(studentAnswer: "B");
        Assert.Equal("C2", cefr);
    }

    [Fact]
    public async Task LegacyFlatScoringRulesShape_IncorrectAnswer_ScoresAsA1()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

        var legacyScoringRules = JsonSerializer.Serialize(new { assessment_q1 = new { correctAnswerKey = "B" } });
        await CreateAndPublishTemplateAsync(adminClient, legacyScoringRules, authoringSchemaJson: null);

        var cefr = await SubmitAsync(studentAnswer: "A");
        Assert.Equal("A1", cefr);
    }

    [Fact]
    public async Task QuizTabAuthoringSchema_CorrectAnswer_ScoresAsC2()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

        var authoringSchema = JsonSerializer.Serialize(RadioQuizSchema("B"));
        await CreateAndPublishTemplateAsync(adminClient, scoringRulesJson: null, authoringSchemaJson: authoringSchema);

        var cefr = await SubmitAsync(studentAnswer: "B");
        Assert.Equal("C2", cefr);
    }

    [Fact]
    public async Task QuizTabAuthoringSchema_IncorrectAnswer_ScoresAsA1()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

        var authoringSchema = JsonSerializer.Serialize(RadioQuizSchema("B"));
        await CreateAndPublishTemplateAsync(adminClient, scoringRulesJson: null, authoringSchemaJson: authoringSchema);

        var cefr = await SubmitAsync(studentAnswer: "A");
        Assert.Equal("A1", cefr);
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>End-to-end regression coverage for the Form.io builder's Quiz tab: an admin submits
/// ONE annotated schema (quiz data embedded per-component) via AuthoringSchemaJson, and the
/// server — never the client — is the sole authority splitting it into a student-safe schema and
/// a backend-only scoring-rules document. These tests assert on the raw HTTP response body
/// string, not just DTO shape, so a future change that accidentally reintroduces a leak fails
/// loudly. Placement already had <c>StudentPlacementControllerTests.GetNext_NeverLeaksScoringRulesOrCorrectAnswerToStudent</c>
/// for the pre-Quiz-tab path; this class covers the new AuthoringSchemaJson path for both flows.</summary>
public sealed class QuizAnnotationLeakRegressionTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public QuizAnnotationLeakRegressionTests(ApiTestFactory factory) => _factory = factory;

    private static object QuizAnnotatedSchema(string questionText, string correctAnswer) => new
    {
        components = new object[]
        {
            new
            {
                type = "radio", key = "answer", label = questionText,
                values = new[] { new { label = "am", value = "A" }, new { label = "is", value = "B" } },
                quiz = new { enabled = true, rule = new { kind = "single_choice", correctAnswer } },
            },
        },
    };

    // ── Placement: admin add via AuthoringSchemaJson never leaks quiz/correctAnswer into FormIoSchemaJson ──

    [Fact]
    public async Task Placement_AddViaAuthoringSchema_NeverLeaksQuizOrCorrectAnswerIntoStudentSchema()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);
        var questionText = $"Which is correct {Guid.NewGuid():N}";
        var authoringSchema = JsonSerializer.Serialize(QuizAnnotatedSchema(questionText, "A"));

        var resp = await client.PostAsJsonAsync("/api/admin/placement-items", new
        {
            skill = "grammar", cefrLevel = "A1", itemOrder = 2000, isEnabled = true,
            // Placeholder — ignored by the server whenever authoringSchemaJson is present.
            formIoSchemaJson = "{}", scoringRulesJson = "{}",
            authoringSchemaJson = authoringSchema,
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var raw = await resp.Content.ReadAsStringAsync();
        var body = JsonSerializer.Deserialize<JsonElement>(raw);

        var studentSchemaJson = body.GetProperty("formIoSchemaJson").GetString()!;
        Assert.DoesNotContain("quiz", studentSchemaJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correctAnswer", studentSchemaJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(questionText, studentSchemaJson); // the question itself is still there

        // Backend-only scoring rules DO contain the correct answer — expected, admin-visible only.
        var scoringRulesJson = body.GetProperty("scoringRulesJson").GetString()!;
        Assert.Contains("correctAnswer", scoringRulesJson, StringComparison.OrdinalIgnoreCase);

        // The authoring schema (admin-only, for re-editing) keeps the quiz annotation.
        var authoringSchemaJson = body.GetProperty("authoringSchemaJson").GetString()!;
        Assert.Contains("quiz", authoringSchemaJson, StringComparison.OrdinalIgnoreCase);
    }

    // ── Onboarding: admin save-draft via AuthoringSchemaJson never leaks quiz/correctAnswer to the student ──

    [Fact]
    public async Task Onboarding_SaveDraftViaAuthoringSchema_NeverLeaksQuizOrCorrectAnswerToStudent()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var adminClient = ClientWithToken(adminToken);
        var questionText = $"Which is correct {Guid.NewGuid():N}";
        var authoringSchema = JsonSerializer.Serialize(QuizAnnotatedSchema(questionText, "B"));

        var createResp = await adminClient.PostAsJsonAsync("/api/admin/onboarding/templates", new
        {
            name = $"Quiz leak test {Guid.NewGuid():N}",
            description = (string?)null,
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var createBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var templateId = createBody.GetProperty("templateId").GetGuid();

        var draftResp = await adminClient.PutAsJsonAsync($"/api/admin/onboarding/templates/{templateId}/draft", new
        {
            formIoSchemaJson = "{\"components\":[]}", // placeholder — ignored when authoringSchemaJson is present
            scoringRulesJson = (string?)null,
            authoringSchemaJson = authoringSchema,
        });
        Assert.Equal(HttpStatusCode.OK, draftResp.StatusCode);

        var publishResp = await adminClient.PostAsync($"/api/admin/onboarding/templates/{templateId}/publish", null);
        Assert.Equal(HttpStatusCode.OK, publishResp.StatusCode);

        var (studentToken, _) = await _factory.CreateStudentAndGetTokenAsync($"quiz_leak_{Guid.NewGuid():N}@test.com");
        var studentClient = ClientWithToken(studentToken);

        var activeResp = await studentClient.GetAsync("/api/onboarding/active");
        Assert.Equal(HttpStatusCode.OK, activeResp.StatusCode);
        var raw = await activeResp.Content.ReadAsStringAsync();

        Assert.DoesNotContain("quiz", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correctAnswer", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("scoringRulesJson", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authoringSchemaJson", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(questionText, raw); // the question itself is still there
    }

    private System.Net.Http.HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

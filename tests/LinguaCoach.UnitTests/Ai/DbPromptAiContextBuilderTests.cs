using FluentAssertions;
using LinguaCoach.Application.Ai;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.UnitTests.Ai;

public sealed class DbPromptAiContextBuilderTests
{
    [Fact]
    public async Task BuildAsync_InsertsLearnerPreferencesBeforeReturnInstructions()
    {
        using var setup = BuildSetup();
        setup.Db.AiPrompts.Add(new AiPrompt(
            "activity_generate_test",
            """
            You are creating an activity.

            Student level: {{cefrLevel}}

            Return ONLY valid JSON.
            """,
            maxInputTokens: 300,
            maxOutputTokens: 100));
        await setup.Db.SaveChangesAsync();

        var request = await setup.Builder.BuildAsync(
            "activity_generate_test",
            new Dictionary<string, string>
            {
                ["cefrLevel"] = "B1",
                ["learnerPreferences"] = """
                Learner preferences:
                - Preferred name: Ehsan
                - Goals: Travel English
                - Support language: Persian
                """
            });

        request.RenderedPrompt.Should().Contain("Learner preferences:");
        request.RenderedPrompt.Should().Contain("Preference behaviour rules:");
        request.RenderedPrompt.IndexOf("Learner preferences:", StringComparison.Ordinal)
            .Should().BeLessThan(request.RenderedPrompt.IndexOf("Return ONLY", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BuildAsync_WhenPreferencesPushPromptOverBudget_Throws()
    {
        using var setup = BuildSetup();
        setup.Db.AiPrompts.Add(new AiPrompt(
            "activity_generate_test",
            "Prompt {{cefrLevel}}. Return ONLY valid JSON.",
            maxInputTokens: 20,
            maxOutputTokens: 100));
        await setup.Db.SaveChangesAsync();

        Func<Task> act = async () => await setup.Builder.BuildAsync(
            "activity_generate_test",
            new Dictionary<string, string>
            {
                ["cefrLevel"] = "B1",
                ["learnerPreferences"] = new string('x', 500)
            });

        await act.Should().ThrowAsync<TokenBudgetExceededException>();
    }

    private static BuilderSetup BuildSetup()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new LinguaCoachDbContext(options);
        db.Database.EnsureCreated();

        return new BuilderSetup(connection, db, new DbPromptAiContextBuilder(db));
    }

    private sealed record BuilderSetup(
        SqliteConnection Connection,
        LinguaCoachDbContext Db,
        DbPromptAiContextBuilder Builder) : IDisposable
    {
        public void Dispose()
        {
            Db.Dispose();
            Connection.Dispose();
        }
    }
}

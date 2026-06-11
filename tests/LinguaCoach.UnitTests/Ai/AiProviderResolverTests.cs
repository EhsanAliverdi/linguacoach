using FluentAssertions;
using LinguaCoach.Application.Ai;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.UnitTests.Ai;

public sealed class AiProviderResolverTests
{
    [Fact]
    public void ResolveLlm_WhenCategoryConfigured_ReturnsCategoryProvider()
    {
        using var setup = BuildResolver();
        setup.Db.AiConfigCategories.Add(new AiConfigCategory("llm.default", "Default LLM", "gemini", "gemini-2.5-flash"));
        setup.Db.AiConfigCategories.Add(new AiConfigCategory("llm.generation", "Content Generation", "openai", "gpt-4o-mini"));
        var openAi = new AiProviderCredential("openai");
        openAi.SetApiKey("sk-test");
        setup.Db.AiProviderCredentials.Add(openAi);
        setup.Db.SaveChanges();

        var selection = setup.Resolver.ResolveLlm("activity_generate_listen_and_answer", "llm.generation").Primary;

        selection.ProviderName.Should().Be("openai");
        selection.ModelName.Should().Be("gpt-4o-mini");
        selection.Provider.Should().BeOfType<OpenAiProvider>();
    }

    [Fact]
    public void ResolveLlm_WhenCategoryEmpty_FallsBackToDefaultLlm()
    {
        using var setup = BuildResolver();
        setup.Db.AiConfigCategories.Add(new AiConfigCategory("llm.default", "Default LLM", "gemini", "gemini-2.5-flash"));
        setup.Db.AiConfigCategories.Add(new AiConfigCategory("llm.evaluation", "Evaluation", null, null));
        var gemini = new AiProviderCredential("gemini");
        gemini.SetApiKey("gemini-test");
        setup.Db.AiProviderCredentials.Add(gemini);
        setup.Db.SaveChanges();

        var selection = setup.Resolver.ResolveLlm("activity_evaluate_email_reply", "llm.evaluation").Primary;

        selection.ProviderName.Should().Be("gemini");
        selection.ModelName.Should().Be("gemini-2.5-flash");
    }

    [Fact]
    public void ResolveLlm_WhenNoCategoryOrDefault_ThrowsUnavailable()
    {
        using var setup = BuildResolver();

        var act = () => setup.Resolver.ResolveLlm("activity_generate_listen_and_answer", "llm.generation");

        act.Should().Throw<AiConfigurationUnavailableException>()
            .WithMessage("*not configured*");
    }

    [Fact]
    public void ResolveLlm_WhenSelectedApiKeyMissing_ThrowsUnavailable()
    {
        using var setup = BuildResolver();
        setup.Db.AiConfigCategories.Add(new AiConfigCategory("llm.default", "Default LLM", "gemini", "gemini-2.5-flash"));
        setup.Db.SaveChanges();

        var act = () => setup.Resolver.ResolveLlm("activity_evaluate_email_reply", "llm.evaluation");

        act.Should().Throw<AiConfigurationUnavailableException>()
            .WithMessage("*API key is not configured*");
    }

    private static ResolverSetup BuildResolver()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new LinguaCoachDbContext(options);
        db.Database.EnsureCreated();

        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(db);
        services.AddSingleton(NullLogger<OpenAiProvider>.Instance);
        services.AddSingleton(NullLogger<GeminiProvider>.Instance);
        services.AddSingleton(NullLogger<AnthropicProvider>.Instance);
        services.AddSingleton(NullLogger<QwenProvider>.Instance);
        services.AddSingleton<OpenAiProvider>();
        services.AddHttpClient<GeminiProvider>();
        services.AddSingleton<AnthropicProvider>();
        services.AddSingleton<QwenProvider>();
        var provider = services.BuildServiceProvider();

        var resolver = new AiProviderResolver(
            configuration,
            provider,
            NullLogger<AiProviderResolver>.Instance);

        return new ResolverSetup(connection, db, resolver);
    }

    private sealed record ResolverSetup(
        SqliteConnection Connection,
        LinguaCoachDbContext Db,
        AiProviderResolver Resolver) : IDisposable
    {
        public void Dispose()
        {
            Db.Dispose();
            Connection.Dispose();
        }
    }
}

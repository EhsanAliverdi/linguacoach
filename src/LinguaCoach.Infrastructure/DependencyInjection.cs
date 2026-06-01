using LinguaCoach.Application.Admin;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.Auth;
using LinguaCoach.Application.Dashboard;
using LinguaCoach.Application.Onboarding;
using LinguaCoach.Application.Reference;
using LinguaCoach.Application.Speaking;
using LinguaCoach.Application.Writing;
using LinguaCoach.Infrastructure.Admin;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Infrastructure.Auth;
using LinguaCoach.Infrastructure.Dashboard;
using LinguaCoach.Infrastructure.Onboarding;
using LinguaCoach.Infrastructure.Reference;
using LinguaCoach.Infrastructure.Speaking;
using LinguaCoach.Infrastructure.Writing;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Auth
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<ILoginHandler, LoginHandler>();
        services.AddScoped<IChangePasswordHandler, ChangePasswordHandler>();

        // Admin
        services.AddScoped<ICreateStudentHandler, CreateStudentHandler>();

        // Onboarding
        services.AddScoped<IOnboardingHandler, OnboardingHandler>();
        services.AddScoped<IOnboardingStatusQuery, OnboardingHandler>();

        // Dashboard
        services.AddScoped<IDashboardQueryHandler, DashboardQueryHandler>();

        // Reference data
        services.AddScoped<IReferenceQueryService, ReferenceQueryService>();

        // AI — context builder and OpenAI provider
        services.AddScoped<IAiContextBuilder, DbPromptAiContextBuilder>();
        services.AddScoped<IAiProvider, OpenAiProvider>();

        // Writing exercise
        services.AddScoped<IGetWritingExerciseHandler, WritingExerciseHandler>();
        services.AddScoped<ISubmitWritingDraftHandler, WritingExerciseHandler>();

        // STT/TTS — no-op stubs for MVP; interfaces registered so DI doesn't fail
        services.AddScoped<ISpeechToTextService, NoOpSpeechToTextService>();
        services.AddScoped<ITextToSpeechService, NoOpTextToSpeechService>();

        return services;
    }
}

using System.Data.Common;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Identity;
using LinguaCoach.Persistence.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// WebApplicationFactory that replaces PostgreSQL with SQLite in-memory for tests.
/// A single shared connection is kept open for the lifetime of the factory so
/// the in-memory database survives between requests within one test instance.
/// </summary>
public class ApiTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestJwtKey = "test-jwt-signing-key-at-least-32-characters-long!";

    // One connection per factory instance — shared across all requests/scopes.
    private SqliteConnection? _connection;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();
        // Prime the host so EnsureCreated runs before any test accesses the DB.
        await EnsureCreatedAsync();
    }

    private static void RemoveAll<T>(IServiceCollection services)
    {
        var descriptors = services.Where(d => d.ServiceType == typeof(T)).ToList();
        foreach (var d in descriptors) services.Remove(d);
    }

    public new async Task DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Program.cs registers SQLite when env=Testing. We replace the placeholder
        // DbContextOptions with one that uses the single shared open connection,
        // so the in-memory database survives across requests.
        builder.ConfigureServices(services =>
        {
            RemoveAll<DbContextOptions<LinguaCoachDbContext>>(services);
            RemoveAll<DbContextOptions>(services);

            var connDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbConnection));
            if (connDescriptor is not null) services.Remove(connDescriptor);

            services.AddSingleton<DbConnection>(_ => _connection!);

            services.AddDbContext<LinguaCoachDbContext>((sp, options) =>
            {
                var conn = sp.GetRequiredService<DbConnection>();
                options.UseSqlite(conn);
            });

            // Replace the real IFileStorageService with the in-memory fake for all tests.
            RemoveAll<LinguaCoach.Application.Storage.IFileStorageService>(services);
            RemoveAll<LinguaCoach.Infrastructure.Storage.FakeFileStorageService>(services);
            services.AddSingleton<LinguaCoach.Infrastructure.Storage.FakeFileStorageService>();
            services.AddSingleton<LinguaCoach.Application.Storage.IFileStorageService>(
                sp => sp.GetRequiredService<LinguaCoach.Infrastructure.Storage.FakeFileStorageService>());
        });

        builder.UseSetting("Jwt:Key", TestJwtKey);
        builder.UseSetting("Jwt:Issuer", "linguacoach");
        builder.UseSetting("Jwt:Audience", "linguacoach");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "DataSource=:memory:");
    }

    /// <summary>Ensures DB schema exists and default seed data is present.</summary>
    public async Task EnsureCreatedAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        await db.Database.EnsureCreatedAsync();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApiTestFactory>>();
        await DefaultAiSeeder.SeedAsync(db, logger);
        await LinguaCoach.Persistence.Seed.ExercisePatternSeeder.SeedAsync(db, logger);
        await LinguaCoach.Persistence.Seed.ExerciseTypeDefinitionSeeder.SeedAsync(db, logger);
        await LinguaCoach.Persistence.Seed.CurriculumObjectiveSeeder.SeedAsync(db, logger);
        await LinguaCoach.Persistence.Seed.UsageGovernanceSeeder.SeedAsync(db);
        await LinguaCoach.Persistence.Seed.OnboardingFlowSeeder.SeedAsync(db);
        await LinguaCoach.Persistence.Seed.PlacementItemBankSeeder.SeedAsync(db);
    }

    public async Task<string> CreateAdminAndGetTokenAsync()
    {
        await EnsureCreatedAsync();
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        const string adminEmail = "admin@test.linguacoach.com";
        var existing = await userManager.FindByEmailAsync(adminEmail);
        if (existing is not null)
        {
            var ts = scope.ServiceProvider.GetRequiredService<LinguaCoach.Application.Auth.ITokenService>();
            return ts.GenerateToken(existing.Id, existing.Email!, existing.Role);
        }

        var admin = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            Role = LinguaCoach.Domain.Enums.UserRole.Admin,
            EmailConfirmed = true,
            MustChangePassword = false
        };
        await userManager.CreateAsync(admin, "Admin@1234");

        var svc = scope.ServiceProvider.GetRequiredService<LinguaCoach.Application.Auth.ITokenService>();
        return svc.GenerateToken(admin.Id, admin.Email!, admin.Role);
    }

    public async Task<(string Token, Guid UserId)> CreateStudentAndGetTokenAsync(string email = "student@test.linguacoach.com")
    {
        await EnsureCreatedAsync();
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            var ts = scope.ServiceProvider.GetRequiredService<LinguaCoach.Application.Auth.ITokenService>();
            return (ts.GenerateToken(existing.Id, existing.Email!, existing.Role), existing.Id);
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            Role = LinguaCoach.Domain.Enums.UserRole.Student,
            EmailConfirmed = true,
            MustChangePassword = false
        };
        await userManager.CreateAsync(user, "Student@1234");

        var profile = new LinguaCoach.Domain.Entities.StudentProfile(user.Id);
        db.StudentProfiles.Add(profile);
        await db.SaveChangesAsync();

        var svc = scope.ServiceProvider.GetRequiredService<LinguaCoach.Application.Auth.ITokenService>();
        return (svc.GenerateToken(user.Id, user.Email!, user.Role), user.Id);
    }
}

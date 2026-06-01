using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LinguaCoach.Persistence;

/// <summary>
/// Used by EF Core CLI tools (dotnet ef migrations add / database update).
/// Reads LINGUACOACH_CONNSTR from the environment; falls back to a localhost
/// default for convenience. Never used at application runtime.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<LinguaCoachDbContext>
{
    public LinguaCoachDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("LINGUACOACH_CONNSTR")
            ?? "Host=localhost;Database=linguacoach_dev;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new LinguaCoachDbContext(options);
    }
}

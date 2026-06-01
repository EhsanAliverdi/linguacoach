using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LinguaCoach.Persistence;

/// <summary>
/// Used by EF Core CLI tools (dotnet ef migrations add / database update).
/// Connection string is for local development only — never used at runtime.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<LinguaCoachDbContext>
{
    public LinguaCoachDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseNpgsql("Host=localhost;Database=linguacoach_dev;Username=postgres;Password=postgres")
            .Options;

        return new LinguaCoachDbContext(options);
    }
}

using System.Reflection;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LinguaCoach.ArchitectureTests;

/// <summary>
/// Phase 20F regression coverage: production had a standing outage
/// (placement start, the Phase 20D readiness audit, and two background jobs
/// all threw PostgresException) because five migration classes
/// (T62, T63, T65, T66, T67) had no accompanying .Designer.cs file. EF
/// Core's migration discovery finds migrations via the [Migration("id")]
/// attribute, which the code generator places on the Designer.cs partial
/// class -- not the main migration file -- so those five migrations were
/// never applied on any environment, ever, even though their .cs files
/// with correct Up()/Down() methods existed and compiled cleanly.
/// See docs/reviews/2026-07-02-phase-20f-production-placement-readiness-p0-unblocker-review.md.
/// </summary>
public class MigrationDiscoveryTests
{
    private static Type[] GetAllMigrationTypes() =>
        typeof(LinguaCoachDbContext).Assembly
            .GetTypes()
            .Where(t => typeof(Migration).IsAssignableFrom(t) && !t.IsAbstract)
            .ToArray();

    [Fact]
    public void Every_migration_class_has_a_Migration_attribute()
    {
        var migrationTypes = GetAllMigrationTypes();

        Assert.NotEmpty(migrationTypes);

        var missingAttribute = migrationTypes
            .Where(t => t.GetCustomAttribute<MigrationAttribute>() is null)
            .Select(t => t.FullName)
            .ToList();

        Assert.True(missingAttribute.Count == 0,
            "The following Migration-derived classes have no [Migration(\"id\")] attribute, " +
            "which means EF Core will never discover or apply them (this is exactly the Phase 20F " +
            "production incident -- a missing .Designer.cs, which is where this attribute normally " +
            "lives, silently makes a migration invisible to `dotnet ef database update`): " +
            string.Join(", ", missingAttribute));
    }

    [Fact]
    public void Every_migration_attribute_id_is_unique()
    {
        var ids = GetAllMigrationTypes()
            .Select(t => t.GetCustomAttribute<MigrationAttribute>()?.Id)
            .Where(id => id is not null)
            .Cast<string>()
            .ToList();

        var duplicates = ids
            .GroupBy(id => id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.True(duplicates.Count == 0,
            "Duplicate [Migration] ids found (two migration classes would collide in " +
            "__EFMigrationsHistory): " + string.Join(", ", duplicates));
    }
}

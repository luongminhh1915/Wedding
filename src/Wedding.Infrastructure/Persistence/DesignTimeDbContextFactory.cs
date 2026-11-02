using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Wedding.Infrastructure.Persistence;
/// <summary>Scaffold SQL Server migrations without starting the API, workers, or seeding a database.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // Scaffolding does not connect. Only an explicit deployment-only variable selects a real
        // target for EF migration tools; it never reads or silently targets a developer's API DB.
        var connection = Environment.GetEnvironmentVariable("WEDDING_MIGRATION_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection))
            connection = "Server=localhost;Database=WeddingDesignOnly;Integrated Security=true;TrustServerCertificate=true";
        return new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).Options);
    }
}

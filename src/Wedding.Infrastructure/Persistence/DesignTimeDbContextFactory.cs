using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Wedding.Infrastructure.Persistence;
/// <summary>Scaffold SQL Server migrations without starting the API, workers, or seeding a database.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlServer("Server=localhost;Database=WeddingDesignOnly;Integrated Security=true;TrustServerCertificate=true")
        .Options);
}

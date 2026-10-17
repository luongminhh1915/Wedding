using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Infrastructure.Persistence;
using Wedding.Infrastructure.Services;

namespace Wedding.Tests;

public class DatabaseStartupTests
{
    private const string LocalSql = "Server=localhost;Database=WeddingStartupPolicyOnly;Integrated Security=true;TrustServerCertificate=true";

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void DeployedEnvironmentsRequireSqlServerAndSeparateMigrations(string environment)
    {
        var valid = Policy("SqlServer", LocalSql, environment: environment);
        Assert.False(valid.ApplyMigrations);
        Assert.False(valid.SeedDemoData);
        Assert.Equal(LocalSql, valid.ConnectionString);
        Assert.Throws<InvalidOperationException>(() => Policy("Sqlite", "Data Source=local.db", environment: environment));
        Assert.Throws<InvalidOperationException>(() => Policy("SqlServer", LocalSql, seed: true, environment: environment));
        Assert.Throws<InvalidOperationException>(() => Policy("SqlServer", LocalSql, migrate: true, environment: environment));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void MissingConnectionNeverFallsBackToAnEmptyDatabase(string? connection) =>
        Assert.Throws<InvalidOperationException>(() => Policy("SqlServer", connection));

    [Theory]
    [InlineData("Server=localhost")]
    [InlineData("Database=Wedding")]
    [InlineData("Server=localhost;Database=master")]
    [InlineData("Server=localhost;Database=MoDeL")]
    [InlineData("Server=localhost;Database=msdb")]
    [InlineData("Server=localhost;Database=tempdb")]
    [InlineData("Server=localhost;Database=\" master \"")]
    [InlineData("Server=localhost;Database=Wedding;AttachDbFilename=C:\\temporary\\wedding.mdf")]
    public void SqlServerTargetMustBeAnExplicitApplicationDatabase(string connection) =>
        Assert.Throws<InvalidOperationException>(() => Policy("SqlServer", connection));

    [Fact]
    public void UnspecifiedProviderDefaultsToSqlServerButNeverEnablesSeedingOrMigrations()
    {
        var policy = Policy(null, LocalSql);
        Assert.Equal("SqlServer", policy.Provider);
        Assert.False(policy.ApplyMigrations);
        Assert.False(policy.SeedDemoData);
        Assert.Throws<InvalidOperationException>(() => Policy("PostgreSQL", LocalSql));
    }

    [Fact]
    public async Task ExplicitLocalSqliteRestartsPreserveUserDataEvenWhenDemoSeedIsRequested()
    {
        var directory = Directory.CreateTempSubdirectory("WeddingStartupTest_");
        var path = Path.Combine(directory.FullName, "persistent.db");
        var connection = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ConnectionString;
        var user = User.Create("A real local customer", "retained@example.invalid", "0901111111", "keep-this-hash", UserRole.Customer);
        var budget = BudgetItem.Create(user.Id, null, "Retained custom budget", 12345678m, 123m, "Never seed over this");
        try
        {
            using (var first = Provider(Policy("Sqlite", connection, migrate: true)))
            {
                await DbInitializer.SeedAsync(first);
                using var scope = first.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Assert.Empty(await db.Users.ToListAsync());
                db.Users.Add(user); db.BudgetItems.Add(budget);
                await db.SaveChangesAsync();
            }
            using (var restart = Provider(Policy("Sqlite", connection, migrate: true, seed: true)))
            {
                await DbInitializer.SeedAsync(restart);
                await DbInitializer.SeedAsync(restart);
                using var scope = restart.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Assert.Equal("keep-this-hash", (await db.Users.SingleAsync()).PasswordHash);
                Assert.Equal(123m, (await db.BudgetItems.SingleAsync()).ActualCost);
                Assert.Empty(await db.Vendors.ToListAsync());
            }
        }
        finally
        {
            // This exact file was created in a unique temporary directory by this test.
            Assert.StartsWith("WeddingStartupTest_", directory.Name);
            if (File.Exists(path)) File.Delete(path);
            directory.Delete();
        }
    }

    [Fact]
    public async Task StaleSqliteSchemaIsReportedAndNeverDeleted()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "CREATE TABLE Users (Id TEXT PRIMARY KEY); INSERT INTO Users VALUES ('retained-sentinel');";
            await command.ExecuteNonQueryAsync();
        }
        var policy = Policy("Sqlite", connection.ConnectionString, migrate: true);
        var services = new ServiceCollection().AddSingleton(policy).AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
        using var provider = services.BuildServiceProvider();
        await Assert.ThrowsAsync<SqliteException>(() => DbInitializer.SeedAsync(provider));
        await using var retained = connection.CreateCommand();
        retained.CommandText = "SELECT Id FROM Users";
        Assert.Equal("retained-sentinel", await retained.ExecuteScalarAsync());
    }

    [SqlServerFact]
    public async Task SqlServerStartupAndProductionRestartsKeepAllCustomerRows()
    {
        var name = $"WeddingPlanTest_Startup_{Guid.NewGuid():N}";
        var source = Environment.GetEnvironmentVariable("WEDDING_TEST_SQLSERVER");
        var connection = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(source) || source == "1"
            ? "Server=localhost;Integrated Security=true;TrustServerCertificate=true;Connection Timeout=5" : source) { InitialCatalog = name }.ConnectionString;
        var user = User.Create("Persistent SQL customer", "retained@example.invalid", "0901111111", "original-hash", UserRole.Customer);
        var profile = WeddingProfile.Create(user.Id);
        profile.Update("Groom", "Bride", DateTime.UtcNow.AddMonths(1), "Hồ Chí Minh", 100000000m, 100, "Classic", null);
        profile.CompleteOnboarding();
        var budget = BudgetItem.Create(user.Id, null, "Retained budget", 15000000m, 125000m, "Customer supplied");
        try
        {
            using (var development = Provider(Policy("SqlServer", connection, migrate: true)))
            {
                await DbInitializer.SeedAsync(development);
                using var scope = development.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Assert.Empty(await db.Users.ToListAsync());
                db.Users.Add(user); db.WeddingProfiles.Add(profile); db.BudgetItems.Add(budget);
                await db.SaveChangesAsync();
                await DbInitializer.SeedAsync(development);
            }
            // Simulates several redeploys: no migrations or demo seeding, only readiness checks.
            for (var restart = 0; restart < 3; restart++)
            {
                using var production = Provider(Policy("SqlServer", connection, environment: "Production"));
                await DbInitializer.SeedAsync(production);
                using var scope = production.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Assert.Equal("original-hash", (await db.Users.SingleAsync()).PasswordHash);
                Assert.Equal(profile.OnboardingCompletedAt, (await db.WeddingProfiles.SingleAsync()).OnboardingCompletedAt);
                Assert.Equal(125000m, (await db.BudgetItems.SingleAsync()).ActualCost);
                Assert.Empty(await db.Vendors.ToListAsync());
                Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            }
        }
        finally
        {
            Assert.StartsWith("WeddingPlanTest_Startup_", name);
            await using var cleanup = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).Options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    private static DatabaseStartupPolicy Policy(string? provider, string? connection, bool migrate = false, bool seed = false, string environment = "Development")
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = provider, ["ConnectionStrings:DefaultConnection"] = connection,
            ["Database:ApplyMigrations"] = migrate.ToString(), ["Database:SeedDemoData"] = seed.ToString()
        }).Build();
        return DatabaseStartupPolicy.FromConfiguration(config, new TestEnvironment { EnvironmentName = environment });
    }

    private static ServiceProvider Provider(DatabaseStartupPolicy policy)
    {
        var services = new ServiceCollection().AddSingleton(policy).AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            if (policy.IsSqlite) options.UseSqlite(policy.ConnectionString);
            else options.UseSqlServer(policy.ConnectionString);
        });
        return services.BuildServiceProvider();
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Wedding.Tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

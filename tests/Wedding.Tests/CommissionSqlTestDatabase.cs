using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Wedding.Infrastructure.Persistence;

namespace Wedding.Tests;

/// <summary>A fresh SQL Server database owned only by this test invocation, never by the application.</summary>
internal sealed class CommissionSqlTestDatabase : IAsyncDisposable
{
    private const string Prefix = "WeddingPlanTest_Commission5_";
    private readonly DbContextOptions<ApplicationDbContext> options;
    private bool created;
    public string Name { get; }
    public ApplicationDbContext Db { get; }

    private CommissionSqlTestDatabase(string name, string connectionString)
    {
        Name = name;
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connectionString).Options;
        Db = new ApplicationDbContext(options);
    }

    public void AssertTarget()
    {
        var target = new SqlConnectionStringBuilder(Db.Database.GetConnectionString());
        Assert.Equal("localhost", target.DataSource, ignoreCase: true);
        Assert.True(target.IntegratedSecurity);
        Assert.Empty(target.AttachDBFilename);
        Assert.Equal(Name, target.InitialCatalog);
        Assert.StartsWith(Prefix, Name);
        Assert.True(Guid.TryParseExact(Name[Prefix.Length..], "N", out _));
    }

    public ApplicationDbContext CreateContext()
    {
        AssertTarget();
        Assert.True(created);
        return new ApplicationDbContext(options);
    }

    public static async Task<CommissionSqlTestDatabase> CreateAsync(bool migrate = true)
    {
        // Opt-in only: never interpret the variable as a connection string or a DB name.
        Assert.Equal("1", Environment.GetEnvironmentVariable("WEDDING_TEST_SQLSERVER"));
        var name = Prefix + Guid.NewGuid().ToString("N");
        var target = new SqlConnectionStringBuilder
        {
            DataSource = "localhost", InitialCatalog = name, IntegratedSecurity = true,
            TrustServerCertificate = true, ConnectTimeout = 5
        };
        var owned = new CommissionSqlTestDatabase(name, target.ConnectionString);
        owned.AssertTarget();
        var master = new SqlConnectionStringBuilder(target.ConnectionString) { InitialCatalog = "master" };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using (var exists = connection.CreateCommand())
        {
            exists.CommandText = "SELECT DB_ID(@database)";
            exists.Parameters.AddWithValue("@database", name);
            Assert.Equal(DBNull.Value, await exists.ExecuteScalarAsync());
        }
        await using (var create = connection.CreateCommand())
        {
            create.CommandText = $"CREATE DATABASE [{name}]";
            await create.ExecuteNonQueryAsync();
            owned.created = true;
        }
        try
        {
            if (migrate) await owned.Db.Database.MigrateAsync();
            return owned;
        }
        catch
        {
            await owned.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        AssertTarget();
        try
        {
            // No cleanup unless this fixture successfully created this exact GUID target.
            if (created) await Db.Database.EnsureDeletedAsync();
        }
        finally { await Db.DisposeAsync(); }
    }
}

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Wedding.Infrastructure.Persistence;

/// <summary>
/// Resolves database configuration once, before any connection is opened. A deployed API only
/// connects to an explicitly configured SQL Server database; schema upgrades are a separate step.
/// </summary>
public sealed record DatabaseStartupPolicy(string Provider, string ConnectionString, bool ApplyMigrations, bool SeedDemoData)
{
    public bool IsSqlite => Provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase);

    public static DatabaseStartupPolicy FromConfiguration(IConfiguration configuration, IHostEnvironment environment)
    {
        var provider = configuration["Database:Provider"] ?? configuration["DatabaseProvider"] ?? "SqlServer";
        var connection = configuration.GetConnectionString("DefaultConnection");
        var migrate = configuration.GetValue("Database:ApplyMigrations", false);
        var seed = configuration.GetValue("Database:SeedDemoData", false);
        var isDevelopment = environment.IsDevelopment();

        if (!provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)
            && !provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Database:Provider must be SqlServer or explicitly Sqlite for Development only.");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Set ConnectionStrings__DefaultConnection to the intended database. No fallback database will be created.");
        if (!isDevelopment)
        {
            if (!provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Deployed environments require SQL Server. SQLite is only supported for explicit local Development.");
            if (seed)
                throw new InvalidOperationException("Database:SeedDemoData must be false outside Development. Demo accounts must not be added to a customer database.");
            if (migrate)
                throw new InvalidOperationException("Database:ApplyMigrations must be false outside Development. Apply reviewed migrations in a separate deployment step first.");
        }

        if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            var parsed = new SqlConnectionStringBuilder(connection);
            if (string.IsNullOrWhiteSpace(parsed.DataSource) || string.IsNullOrWhiteSpace(parsed.InitialCatalog))
                throw new InvalidOperationException("The SQL Server connection must specify both Server and Database explicitly.");
            if (new[] { "master", "model", "msdb", "tempdb" }.Contains(parsed.InitialCatalog.Trim(), StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("A SQL Server system database cannot be used as the application database.");
            if (!string.IsNullOrWhiteSpace(parsed.AttachDBFilename))
                throw new InvalidOperationException("Use a named SQL Server database, not an attached local database file.");
        }

        return new(provider, connection, migrate, seed);
    }
}

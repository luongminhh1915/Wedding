using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

var phase = "Init";
try
{
    phase = "ReadPrivateConfig";
    var configText = await File.ReadAllTextAsync(".deployment/private/home-cms-release-20261029/wedding.production.after.json");
    using var config = JsonDocument.Parse(configText);
    var connStr = config.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString();
    if (string.IsNullOrWhiteSpace(connStr)) throw new InvalidOperationException("Connection string is missing.");

    var builder = new SqlConnectionStringBuilder(connStr);
    if (builder.DataSource != "sql1004.site4now.net" || builder.InitialCatalog != "db_ad0009_weddingmatch")
    {
        throw new InvalidOperationException($"Unexpected target host or DB: {builder.DataSource}/{builder.InitialCatalog}");
    }
    builder.ConnectTimeout = 30;

    phase = "OpenConnection";
    await using var connection = new SqlConnection(builder.ConnectionString);
    await connection.OpenAsync();

    phase = "PreCheckMigrationState";
    await using (var checkCmd = connection.CreateCommand())
    {
        checkCmd.CommandText = @"
            SELECT COUNT(*), MAX(MigrationId), COL_LENGTH('dbo.Users','LoginName'), COL_LENGTH('dbo.Vendors','OperatingRegions')
            FROM dbo.__EFMigrationsHistory;";
        await using var reader = await checkCmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("Could not read migrations history.");
        var count = reader.GetInt32(0);
        var latest = reader.GetString(1);
        var hasLogin = !reader.IsDBNull(2);
        var hasOperating = !reader.IsDBNull(3);
        if (count != 20 || latest != "20261027152038_OptionalSocialContactEmail" || hasLogin || hasOperating)
        {
            throw new InvalidOperationException($"Pre-check failed. Count={count}, Latest={latest}, hasLogin={hasLogin}, hasOperating={hasOperating}");
        }
    }

    phase = "CaptureTableRowCountsBefore";
    var tables = new[] { "Users", "Vendors", "Categories", "Listings", "Vouchers", "AuditLogs" };
    var beforeCounts = new Dictionary<string, int>();
    foreach (var table in tables)
    {
        await using var countCmd = connection.CreateCommand();
        countCmd.CommandText = $"SELECT COUNT(*) FROM dbo.[{table}];";
        var count = (int)await countCmd.ExecuteScalarAsync();
        beforeCounts[table] = count;
    }

    phase = "ReadMigrationSql";
    var sqlScript = await File.ReadAllTextAsync("artifacts/vendor-studio-deploy-20261030/migration21.sql");
    // Strip GO statements and trim
    var cleanSql = sqlScript.Replace("\r\nGO", "").Replace("\nGO", "").Trim();

    phase = "ExecuteMigration21Transaction";
    await using (var applyCmd = connection.CreateCommand())
    {
        applyCmd.CommandText = cleanSql;
        applyCmd.CommandTimeout = 60;
        await applyCmd.ExecuteNonQueryAsync();
    }

    phase = "PostCheckMigrationState";
    int postCount;
    string postLatest;
    bool postHasLogin;
    bool postHasOperating;
    await using (var verifyCmd = connection.CreateCommand())
    {
        verifyCmd.CommandText = @"
            SELECT COUNT(*), MAX(MigrationId), COL_LENGTH('dbo.Users','LoginName'), COL_LENGTH('dbo.Vendors','OperatingRegions')
            FROM dbo.__EFMigrationsHistory;";
        await using var reader = await verifyCmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("Could not read post-migration history.");
        postCount = reader.GetInt32(0);
        postLatest = reader.GetString(1);
        postHasLogin = !reader.IsDBNull(2);
        postHasOperating = !reader.IsDBNull(3);
        if (postCount != 21 || postLatest != "20261029152219_AdminVendorContactAndOperatingAreas" || !postHasLogin || !postHasOperating)
        {
            throw new InvalidOperationException($"Post-check failed. Count={postCount}, Latest={postLatest}, hasLogin={postHasLogin}, hasOperating={postHasOperating}");
        }
    }

    phase = "VerifyTableRowCountsAfter";
    var afterCounts = new Dictionary<string, int>();
    foreach (var table in tables)
    {
        await using var countCmd = connection.CreateCommand();
        countCmd.CommandText = $"SELECT COUNT(*) FROM dbo.[{table}];";
        var count = (int)await countCmd.ExecuteScalarAsync();
        afterCounts[table] = count;
        if (count != beforeCounts[table])
        {
            throw new InvalidOperationException($"Table {table} row count changed from {beforeCounts[table]} to {count}!");
        }
    }

    var result = new
    {
        status = "Migration21AppliedSuccessfully",
        targetHost = builder.DataSource,
        targetDatabase = builder.InitialCatalog,
        previousMigrationCount = 20,
        currentMigrationCount = postCount,
        latestMigration = postLatest,
        hasLoginName = postHasLogin,
        hasOperatingRegions = postHasOperating,
        tableCountsVerified = true,
        rowCounts = afterCounts,
        appliedUtc = DateTime.UtcNow
    };

    var resultJson = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync("artifacts/vendor-studio-deploy-20261030/live-schema-after.json", resultJson);
    Console.WriteLine(resultJson);
}
catch (Exception ex)
{
    var err = new
    {
        status = "MigrationFailed",
        phase,
        exceptionType = ex.GetType().Name,
        message = ex.Message
    };
    var errJson = JsonSerializer.Serialize(err, new JsonSerializerOptions { WriteIndented = true });
    Console.Error.WriteLine(errJson);
    Environment.ExitCode = 1;
}


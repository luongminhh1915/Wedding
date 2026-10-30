using Microsoft.Data.SqlClient;
using System.Text.Json;
var phase = "ReadConfiguration";
try {
    using var config = JsonDocument.Parse(File.ReadAllText(".deployment/private/vendor-studio-release-20261030/wedding.production.before.json"));
    var builder = new SqlConnectionStringBuilder(config.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString());
    if (builder.DataSource != "sql1004.site4now.net" || builder.InitialCatalog != "db_ad0009_weddingmatch") throw new InvalidOperationException("TargetMismatch");
    builder.ApplicationIntent = ApplicationIntent.ReadOnly; builder.ConnectTimeout = 20;
    phase = "ReadLiveMetadata";
    await using var connection = new SqlConnection(builder.ConnectionString); await connection.OpenAsync();
    async Task<object?> Scalar(string query) { await using var command = new SqlCommand(query, connection); return await command.ExecuteScalarAsync(); }
    var counts = new Dictionary<string, object?>();
    foreach (var table in new[] {"Users","Vendors","Categories","Listings","Vouchers","AuditLogs","BookingContracts"}) counts[table] = await Scalar($"SELECT COUNT_BIG(*) FROM dbo.[{table}]");
    var result = new {
        inspectedUtc = DateTime.UtcNow, readOnlyQueries = true,
        migrationCount = await Scalar("SELECT COUNT(*) FROM dbo.__EFMigrationsHistory"),
        latestMigration = await Scalar("SELECT MAX(MigrationId) FROM dbo.__EFMigrationsHistory"),
        loginNameColumnLength = await Scalar("SELECT COL_LENGTH('dbo.Users','LoginName')"),
        operatingRegionsColumnLength = await Scalar("SELECT COL_LENGTH('dbo.Vendors','OperatingRegions')"),
        loginNameUniqueFilteredIndex = await Scalar("SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.Users') AND name='IX_Users_LoginName' AND is_unique=1 AND has_filter=1 AND is_disabled=0"),
        socialEmailCheckEnabledTrusted = await Scalar("SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.Users') AND name='CK_Users_SocialEmail' AND is_disabled=0 AND is_not_trusted=0"),
        socialEmailCheckSupportsVendor = await Scalar("SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.Users') AND name='CK_Users_SocialEmail' AND definition LIKE '%VendorOwner%' AND definition LIKE '%LoginName%'"),
        rowsWithLoginName = await Scalar("SELECT COUNT(*) FROM dbo.Users WHERE LoginName IS NOT NULL"),
        rowsWithOperatingRegions = await Scalar("SELECT COUNT(*) FROM dbo.Vendors WHERE OperatingRegions IS NOT NULL"),
        rowCounts = counts
    };
    var output = JsonSerializer.Serialize(result, new JsonSerializerOptions {WriteIndented=true});
    File.WriteAllText("artifacts/vendor-studio-deploy-20261030/live-schema-independent-20261030.json",output); Console.WriteLine(output);
} catch(Exception ex) { Console.Error.WriteLine(JsonSerializer.Serialize(new {status="ReadOnlyAuditFailed", phase, exceptionType=ex.GetType().Name})); Environment.ExitCode=1; }

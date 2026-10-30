using Microsoft.Data.SqlClient;
using System.Text.Json;
var phase="ReadPrivateConfig";
try {
    using var config=JsonDocument.Parse(File.ReadAllText(".deployment/private/home-cms-release-20261029/wedding.production.after.json"));
    var builder=new SqlConnectionStringBuilder(config.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString());
    if(builder.DataSource!="sql1004.site4now.net"||builder.InitialCatalog!="db_ad0009_weddingmatch")throw new InvalidOperationException();
    builder.ApplicationIntent=ApplicationIntent.ReadOnly;builder.ConnectTimeout=15;
    phase="OpenReadOnlyConnection";
    await using var connection=new SqlConnection(builder.ConnectionString);await connection.OpenAsync();
    await using var command=connection.CreateCommand();
    command.CommandText="SELECT COUNT(*), MAX(MigrationId), COL_LENGTH('dbo.Users','LoginName'), COL_LENGTH('dbo.Vendors','OperatingRegions') FROM dbo.__EFMigrationsHistory;";
    phase="ReadMigrationMetadata";
    await using var reader=await command.ExecuteReaderAsync();if(!await reader.ReadAsync())throw new InvalidOperationException();
    var result=new {migrationCount=reader.GetInt32(0),latestMigration=reader.GetString(1),hasLoginName=!reader.IsDBNull(2),hasOperatingRegions=!reader.IsDBNull(3),readOnlyQueries=true};
    var json=JsonSerializer.Serialize(result);File.WriteAllText("artifacts/vendor-studio-deploy-20261030/live-schema-before.json",json);Console.WriteLine(json);
}catch(Exception e){Console.Error.WriteLine(JsonSerializer.Serialize(new{status="ReadOnlyInspectionFailed",phase,exceptionType=e.GetType().Name}));Environment.ExitCode=1;}

$ErrorActionPreference='Stop'
$phase='LoadPrivateConfig'
try {
    $config=Get-Content -LiteralPath '.deployment/private/home-cms-release-20261029/wedding.production.after.json' -Raw | ConvertFrom-Json
    $phase='BuildConnection'
    $builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $builder.ConnectionString=[string]$config.ConnectionStrings.DefaultConnection
    if($builder.DataSource -cne 'sql1004.site4now.net' -or $builder.InitialCatalog -cne 'db_ad0009_weddingmatch'){throw 'Unexpected target.'}
    $builder.ApplicationIntent='ReadOnly'
    $builder.ConnectTimeout=15
    $connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
    $phase='OpenReadOnlyConnection'
    $connection.Open()
    $command=$connection.CreateCommand()
    $command.CommandText="SELECT COUNT(*), MAX(MigrationId), COL_LENGTH('dbo.Users','LoginName'), COL_LENGTH('dbo.Vendors','OperatingRegions') FROM dbo.__EFMigrationsHistory;"
    $phase='ReadMigrationMetadata'
    $reader=$command.ExecuteReader()
    if(-not $reader.Read()){throw 'No metadata.'}
    $result=[ordered]@{migrationCount=$reader.GetInt32(0);latestMigration=$reader.GetString(1);hasLoginName=(-not $reader.IsDBNull(2));hasOperatingRegions=(-not $reader.IsDBNull(3));readOnlyQueries=$true}
    $reader.Dispose();$command.Dispose();$connection.Dispose()
    $result | ConvertTo-Json
}catch {[ordered]@{status='ReadOnlyInspectionFailed';phase=$phase;exceptionType=$_.Exception.GetType().Name} | ConvertTo-Json;exit 1}


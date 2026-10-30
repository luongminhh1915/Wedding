$ErrorActionPreference='Stop'
$repo='D:\Gitgithubhub\Wedding'
$backup=Join-Path $repo '.deployment\private\vendor-studio-release-20261030\database-schema21-before-release.bak'
$resultPath=Join-Path $repo 'artifacts\vendor-studio-deploy-20261030\restore-schema21-result.json'
if(-not(Test-Path -LiteralPath $backup -PathType Leaf)){throw 'Expected fresh private backup is missing'}
$key=[guid]::NewGuid().ToString('N')
$name='WeddingPlanTest_StudioRestore_'+$key
if($name -cnotmatch '^WeddingPlanTest_StudioRestore_[a-f0-9]{32}$'){throw 'Unsafe test target'}
$connection=[System.Data.SqlClient.SqlConnection]::new('Server=localhost;Database=master;Integrated Security=true;Encrypt=true;TrustServerCertificate=true;Pooling=false')
function Q([string]$s){return "N'"+$s.Replace("'","''")+"'"}
function Query([string]$sql){$cmd=$connection.CreateCommand();$cmd.CommandTimeout=120;$cmd.CommandText=$sql;try{$dt=[System.Data.DataTable]::new();$r=$cmd.ExecuteReader();try{$dt.Load($r)}finally{$r.Dispose()};return ,$dt}finally{$cmd.Dispose()}}
function Execute([string]$sql){$cmd=$connection.CreateCommand();$cmd.CommandTimeout=120;$cmd.CommandText=$sql;try{[void]$cmd.ExecuteNonQuery()}finally{$cmd.Dispose()}}
$phase='OpenLocalhostMaster';$restoreAttempted=$false;$markerWritten=$false
$result=[ordered]@{Target=$name;BackupSha256=(Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash;ReadOnlyProduction=$true;BackupVerified=$false;RestorePassed=$false;CheckDbPassed=$false;Schema21Verified=$false;NoMigrationApplied=$true;CleanupPassed=$false}
try{
    $connection.Open()
    $phase='ValidateUniqueTarget'
    if((Query "SELECT DB_ID($(Q $name)) AS Id").Rows[0].Id -isnot [DBNull]){throw 'Existing test target refused'}
    $phase='VerifyBackupIdentity'
    $header=Query "RESTORE HEADERONLY FROM DISK=$(Q $backup)"
    if($header.Rows.Count -ne 1 -or $header.Rows[0].DatabaseName -cne 'db_ad0009_weddingmatch' -or [int]$header.Rows[0].BackupType -ne 1){throw 'Unexpected backup identity/type'}
    Execute "RESTORE VERIFYONLY FROM DISK=$(Q $backup)"
    $result.BackupVerified=$true
    $files=Query "RESTORE FILELISTONLY FROM DISK=$(Q $backup)"
    if($files.Rows.Count -lt 2 -or $files.Rows.Count -gt 4){throw 'Unexpected backup file layout'}
    $defaults=Query "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000)) AS DataPath,CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(4000)) AS LogPath"
    $moves=@();$i=0
    foreach($file in $files.Rows){
        $i++;if($file.Type -notin @('D','L')){throw 'Unsupported backup file type'}
        $base=if($file.Type -eq 'D'){$defaults.Rows[0].DataPath}else{$defaults.Rows[0].LogPath}
        $dest=Join-Path $base ($name+'_'+$i+$(if($file.Type -eq 'D'){'.mdf'}else{'.ldf'}))
        if(Test-Path -LiteralPath $dest){throw 'Existing GUID restore file refused'}
        $moves+="MOVE $(Q ([string]$file.LogicalName)) TO $(Q $dest)"
    }
    $phase='RestoreOnlyNewGuidTarget'
    $restoreAttempted=$true
    Execute "RESTORE DATABASE [$name] FROM DISK=$(Q $backup) WITH $($moves -join ','),RECOVERY"
    $result.RestorePassed=$true
    $connection.ChangeDatabase($name)
    $phase='MarkOwnedRestore'
    Execute "EXEC sys.sp_addextendedproperty @name=N'WeddingMatch.StudioRestoreOwner',@value=$(Q $key)"
    $markerWritten=$true
    $phase='CheckRestoredDatabase'
    Execute "DBCC CHECKDB ([$name]) WITH NO_INFOMSGS"
    $result.CheckDbPassed=$true
    $phase='CheckSchema21'
    $state=Query "SELECT (SELECT COUNT(*) FROM dbo.__EFMigrationsHistory) AS MigrationCount,(SELECT MAX(MigrationId) FROM dbo.__EFMigrationsHistory) AS LatestMigration,COL_LENGTH(N'dbo.Users',N'LoginName') AS LoginNameLength,COL_LENGTH(N'dbo.Vendors',N'OperatingRegions') AS OperatingLength,(SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Users') AND name=N'IX_Users_LoginName' AND is_unique=1 AND has_filter=1 AND is_disabled=0) AS IndexCount,(SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.Users') AND name=N'CK_Users_SocialEmail' AND is_disabled=0 AND is_not_trusted=0 AND definition LIKE N'%VendorOwner%' AND definition LIKE N'%LoginName%') AS CheckCount"
    $s=$state.Rows[0]
    if($s.MigrationCount -ne 21 -or $s.LatestMigration -cne '20261029152219_AdminVendorContactAndOperatingAreas' -or $s.LoginNameLength -ne 160 -or $s.OperatingLength -ne 2000 -or $s.IndexCount -ne 1 -or $s.CheckCount -ne 1){throw 'Restored schema metadata mismatch'}
    $result.Schema21Verified=$true
    $result.MigrationCount=21
    $phase='ReadAggregateCounts'
    $counts=Query "SELECT t.name AS TableName,SUM(p.rows) AS [RowCount] FROM sys.tables t JOIN sys.partitions p ON p.object_id=t.object_id AND p.index_id IN(0,1) WHERE t.is_ms_shipped=0 GROUP BY t.name ORDER BY t.name"
    $result.UserTableCount=$counts.Rows.Count
    $result.TotalRows=[long](($counts.Rows|ForEach-Object{[long]$_.RowCount}|Measure-Object -Sum).Sum)
    $result.KeyTableCounts=[ordered]@{}
    foreach($table in @('Users','Vendors','Categories','Listings','BookingContracts','Commissions','PaymentTransactions','AuditLogs')){
        $r=@($counts.Rows|Where-Object{$_.TableName -ceq $table});if($r.Count -eq 1){$result.KeyTableCounts[$table]=[long]$r[0].RowCount}
    }
    $result.Status='RestoredSchema21Verified'
}catch{
    $result.Status='Failed';$result.FailurePhase=$phase;$result.ExceptionType=$_.Exception.GetType().Name
}finally{
    try{
        if($restoreAttempted -and $connection.State -eq 'Open'){
            $connection.ChangeDatabase('master')
            $exists=(Query "SELECT DB_ID($(Q $name)) AS Id").Rows[0].Id -isnot [DBNull]
            if($exists){
                if(-not $markerWritten){throw 'No ownership marker; cleanup refused for manual review'}
                $marker=Query "SELECT CAST(value AS nvarchar(100)) AS OwnerKey FROM [$name].sys.extended_properties WHERE class=0 AND name=N'WeddingMatch.StudioRestoreOwner'"
                if($marker.Rows.Count -ne 1 -or $marker.Rows[0].OwnerKey -cne $key){throw 'Ownership marker mismatch; cleanup refused'}
                Execute "DROP DATABASE [$name]"
            }
            $result.CleanupPassed=(Query "SELECT DB_ID($(Q $name)) AS Id").Rows[0].Id -is [DBNull]
        }
    }catch{$result.CleanupFailure=$_.Exception.GetType().Name;$result.Status='CleanupRequiresReview'}
    $connection.Dispose()
    $result.CompletedUtc=[DateTime]::UtcNow.ToString('o')
    $result|ConvertTo-Json -Depth 4|Set-Content -LiteralPath $resultPath
}
$result|ConvertTo-Json -Depth 4 -Compress
if(-not($result.BackupVerified -and $result.RestorePassed -and $result.CheckDbPassed -and $result.Schema21Verified -and $result.CleanupPassed)){exit 1}

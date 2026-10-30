$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$qaRepo = 'D:\Gitgithubhub\Wedding'
$ready = Get-Content -LiteralPath (Join-Path $qaRepo 'artifacts\readiness\20261020-060533-c2159465\ready.json') -Raw | ConvertFrom-Json
if ($ready.Status -ne 'LOCAL_GATES_PASSED' -or $ready.PublishedRemotely) { throw 'Final source gate missing.' }
$migrationFile = Join-Path $ready.ReleaseRoot 'sql\migrate.sql'
if (!(Test-Path -LiteralPath $migrationFile)) { throw 'Reviewed migration artifact missing.' }
$backup = 'D:\sql\dev\MSSQL16.MSSQLSERVER\MSSQL\Backup\WeddingMatch_Dev_schema15_before_runtime_refresh_20261020_0612utc.bak'
if (Test-Path -LiteralPath $backup) { throw 'Backup target already exists; refusing overwrite.' }
$currentApi = Get-CimInstance Win32_Process -Filter 'ProcessId=31392'
if (!$currentApi -or $currentApi.CommandLine -notlike '*artifacts/category-final-tests/Wedding.WebApi.dll*' -or $currentApi.CommandLine -notlike '*Database=WeddingMatch_Dev;*') { throw 'Expected owned API process changed; refusing stop/upgrade.' }
function SqlChecked([string]$sql, [string]$database = 'WeddingMatch_Dev') {
    & sqlcmd -S localhost -E -d $database -b -I -Q $sql
    if ($LASTEXITCODE -ne 0) { throw 'SQL step failed; no automatic restore/reset/downgrade performed.' }
}
SqlChecked "IF DB_NAME()<>N'WeddingMatch_Dev' OR (SELECT COUNT(*) FROM __EFMigrationsHistory)<>15 OR (SELECT MAX(MigrationId) FROM __EFMigrationsHistory)<>N'20261020055151_PrivateServiceAgreements' THROW 51000,'Unexpected database/schema',1;"
Stop-Process -Id 31392
function Fingerprint([string]$file) {
    & sqlcmd -S localhost -E -d WeddingMatch_Dev -b -I -h -1 -W -s '|' -i (Join-Path $qaRepo 'artifacts\refactor-main-fingerprint.sql') -o $file
    if ($LASTEXITCODE -ne 0) { throw 'Fingerprint failed.' }
}
$before = Join-Path $qaRepo 'artifacts\refactor-main-before.txt'
$after = Join-Path $qaRepo 'artifacts\refactor-main-after.txt'
Fingerprint $before
SqlChecked "BACKUP DATABASE [WeddingMatch_Dev] TO DISK=N'$backup' WITH COPY_ONLY,CHECKSUM; RESTORE VERIFYONLY FROM DISK=N'$backup' WITH CHECKSUM;" 'master'
# The first guarded attempt detected that schema15 was already present. Do not replay,
# downgrade, or claim this backup was taken before that independently applied upgrade.
SqlChecked "IF (SELECT COUNT(*) FROM __EFMigrationsHistory)<>15 OR (SELECT MAX(MigrationId) FROM __EFMigrationsHistory)<>N'20261020055151_PrivateServiceAgreements' THROW 51000,'Unexpected upgraded schema',1;"
Fingerprint $after
if ((Get-Content -LiteralPath $before -Raw) -cne (Get-Content -LiteralPath $after -Raw)) { throw 'Existing row fingerprint changed; preserve backup and investigate. No automatic rollback.' }
[ordered]@{ database='WeddingMatch_Dev'; beforeMigrations=15; afterMigrations=15; schemaAlreadyApplied=$true; migrationsExecutedByThisScript=$false; businessTables=55; originalRowsAndColumnsUnchanged=$true; backup=$backup; backupChecksumVerified=$true; beforeFile=$before; afterFile=$after; performedUtc=[DateTime]::UtcNow.ToString('o') } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $qaRepo 'artifacts\refactor-main-preservation.json') -Encoding utf8NoBOM
Write-Output 'PASS: schema15 already present, no migration replay, backup checksum+VERIFYONLY and unchanged fingerprints. Restart exact owned API on latest source next.'

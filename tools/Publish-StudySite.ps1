#requires -Version 7.0
<#
.SYNOPSIS
Build an IIS-ready, same-origin study site and an offline SQL Server schema script.
.DESCRIPTION
Never connects to a database or a hosting account. Never deletes an old artifact.
The ZIP deliberately contains neither user files nor deployment credentials.
#>
[CmdletBinding()]
param(
    [string]$OutputRoot = '',
    [switch]$InstallClientDependencies
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $OutputRoot) { $OutputRoot = Join-Path $repoRoot 'artifacts/study-deploy' }
$outputBase = [IO.Path]::GetFullPath($OutputRoot)
$releaseName = "wedding-match-$([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))-$([Guid]::NewGuid().ToString('N').Substring(0,8))"
$releaseRoot = Join-Path $outputBase $releaseName
$rawPublish = Join-Path $releaseRoot 'build/api'
$frontend = Join-Path $releaseRoot 'build/client'
$site = Join-Path $releaseRoot 'site'
$migration = Join-Path $releaseRoot 'sql/migrate.sql'
$archive = Join-Path $releaseRoot 'WeddingMatch-site.zip'
$webProject = Join-Path $repoRoot 'src/Wedding.WebApi/Wedding.WebApi.csproj'
$dataProject = Join-Path $repoRoot 'src/Wedding.Infrastructure/Wedding.Infrastructure.csproj'
$manifest = Join-Path $repoRoot '.config/dotnet-tools.json'
$npmCommand = if ($IsWindows) { 'npm.cmd' } else { 'npm' }
foreach ($required in @('dotnet', 'node', $npmCommand)) {
    if (-not (Get-Command $required -ErrorAction SilentlyContinue)) { throw "Missing $required. Install the project prerequisites first." }
}
if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) { throw 'Missing the version-pinned .config/dotnet-tools.json.' }
if (Test-Path -LiteralPath $releaseRoot) { throw 'Staging directory already exists; refusing to overwrite it.' }
$null = New-Item -ItemType Directory -Path $releaseRoot
foreach ($folder in @($site, (Split-Path $migration -Parent))) { $null = New-Item -ItemType Directory -Path $folder }

function Invoke-Checked {
    param([string]$Command, [string[]]$Arguments)
    & $Command @Arguments | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "$Command failed with exit code $LASTEXITCODE. This release is incomplete; do not deploy it." }
}

Push-Location $repoRoot
$previousMigrationConnection = [Environment]::GetEnvironmentVariable('WEDDING_MIGRATION_CONNECTION', 'Process')
try {
    if ($InstallClientDependencies) {
        Push-Location (Join-Path $repoRoot 'client')
        try { Invoke-Checked $npmCommand @('ci') } finally { Pop-Location }
    }
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot 'client/node_modules/vite/bin/vite.js'))) {
        throw 'Missing frontend dependencies. Run again with -InstallClientDependencies.'
    }
    Invoke-Checked 'node' @((Join-Path $PSScriptRoot 'build-study-client.mjs'), $frontend)
    Invoke-Checked 'dotnet' @('tool', 'restore', '--tool-manifest', $manifest)
    # EF reads target metadata before its implicit build; restore explicitly so a
    # developer's old net8 obj/project.assets.json cannot block the net10 release.
    Invoke-Checked 'dotnet' @('restore', $webProject)

    # The design-time factory uses this deliberately non-routable target. EF's script
    # command generates SQL offline; no API, seed, migration update, or worker runs.
    $env:WEDDING_MIGRATION_CONNECTION = 'Server=schema-only.invalid;Database=WeddingSchemaOnly;User Id=offline;Password=not-a-real-credential;Encrypt=true;Connect Timeout=1'
    Invoke-Checked 'dotnet' @('tool', 'run', 'dotnet-ef', '--', 'migrations', 'script', '--idempotent', '--project', $dataProject, '--startup-project', $webProject, '--configuration', 'Release', '--output', $migration)
    # The host imports through sqlcmd, but does not otherwise stop on statement
    # errors. Stop immediately instead of applying later batches to a broken schema.
    $generatedSql = Get-Content -LiteralPath $migration -Raw
    # Filtered indexes require these session options. In particular sqlcmd starts
    # with QUOTED_IDENTIFIER OFF, unlike SSMS; make the file self-contained.
    $sqlPreamble = @'
:ON ERROR EXIT
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
GO

'@
    # UTF-16 LE + BOM is auto-detected by Windows sqlcmd, including the host's
    # Import runner where we cannot supply -f. Preserve Vietnamese schema seed text.
    ($sqlPreamble + "`r`n" + $generatedSql) | Set-Content -LiteralPath $migration -Encoding unicode
    Invoke-Checked 'dotnet' @('publish', $webProject, '--configuration', 'Release', '--output', $rawPublish, '--no-self-contained', '-p:UseAppHost=false')

    # Do not mirror the publish directory: Web SDK content may contain local .db,
    # App_Data, uploaded photos, appsettings secrets, and financial demo JSON files.
    # Only dependency binaries, runtime assets, and the generated IIS launcher enter
    # the release. Static assets come solely from this frontend build.
    foreach ($file in Get-ChildItem -LiteralPath $rawPublish -File -Recurse) {
        $relative = [IO.Path]::GetRelativePath($rawPublish, $file.FullName)
        $parts = $relative -split '[\\/]'
        $rootBinary = $parts.Count -eq 1 -and ($file.Extension -in @('.dll', '.exe') -or $file.Name -match '\.(deps|runtimeconfig)\.json$')
        $runtimeAsset = $parts.Count -gt 1 -and $parts[0] -eq 'runtimes'
        $satellite = $parts.Count -eq 2 -and $parts[0] -match '^[a-z]{2}(-[a-zA-Z0-9]+)?$' -and $file.Name.EndsWith('.resources.dll')
        if (-not ($rootBinary -or $runtimeAsset -or $satellite)) { continue }
        $target = Join-Path $site $relative
        $null = New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force
        Copy-Item -LiteralPath $file.FullName -Destination $target
    }
    $staticRoot = Join-Path $site 'wwwroot'
    $null = New-Item -ItemType Directory -Path $staticRoot
    Get-ChildItem -LiteralPath $frontend -Force | Copy-Item -Destination $staticRoot -Recurse

    # Generate a known launcher rather than copying local IIS configuration/secrets.
    $iisLauncher = @'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers><add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" /></handlers>
      <aspNetCore processPath="dotnet" arguments=".\Wedding.WebApi.dll" stdoutLogEnabled="false" hostingModel="inprocess">
        <environmentVariables><environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" /></environmentVariables>
      </aspNetCore>
    </system.webServer>
  </location>
</configuration>
'@
    $iisLauncher | Set-Content -LiteralPath (Join-Path $site 'web.config') -Encoding utf8NoBOM

    # Intentionally unusable until ../private/wedding.production.json (or host
    # environment variables) supplies secrets. The private file is outside this ZIP
    # and outside /wwwroot, so extracting a later release does not replace it.
    $safeSettings = [ordered]@{
        Database = @{ Provider = 'SqlServer'; ApplyMigrations = $false; SeedDemoData = $false }
        ConnectionStrings = @{ DefaultConnection = '' }
        JwtSettings = @{ Secret = ''; Issuer = 'WeddingPlatform'; Audience = 'WeddingPlatformClient'; ExpiryMinutes = 1440 }
        Uploads = @{ InvitationRoot = '../private/uploads/invitations'; ListingRoot = '../private/uploads/listings' }
        Logging = @{ LogLevel = @{ Default = 'Information'; 'Microsoft.AspNetCore' = 'Warning'; 'Microsoft.EntityFrameworkCore.Database.Command' = 'Warning' } }
        AllowedHosts = '*'
    }
    $safeSettings | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $site 'appsettings.json') -Encoding utf8NoBOM

    foreach ($requiredFile in @('Wedding.WebApi.dll', 'Wedding.WebApi.runtimeconfig.json', 'web.config', 'wwwroot/index.html')) {
        if (-not (Test-Path -LiteralPath (Join-Path $site $requiredFile) -PathType Leaf)) { throw "Incomplete release: missing $requiredFile." }
    }
    foreach ($file in Get-ChildItem -LiteralPath $site -File -Recurse -Force) {
        $relative = [IO.Path]::GetRelativePath($site, $file.FullName).Replace('\', '/')
        if ($relative -match '(^|/)(App_Data|uploads|private|node_modules|\.git|data)(/|$)' -or $file.Name -match '(^\.env($|\.)|\.db($|-)|\.(bak|bacpac|publishsettings|user|pfx|pem|key)$|^appsettings\.(?!json$).+\.json$|^advance_payments\.json$)') {
            throw "Unsafe release content detected: $relative. No ZIP was produced."
        }
    }
    $sqlText = Get-Content -LiteralPath $migration -Raw
    if (-not $sqlText.Contains('__EFMigrationsHistory') -or -not $sqlText.Contains('IF NOT EXISTS')) { throw 'Migration script is not idempotent SQL Server EF output.' }
    Compress-Archive -Path (Join-Path $site '*') -DestinationPath $archive -CompressionLevel Optimal
    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
    [ordered]@{
        CreatedUtc = [DateTime]::UtcNow.ToString('O')
        SiteZip = [IO.Path]::GetFileName($archive)
        SiteZipSha256 = $hash
        MigrationSql = 'sql/migrate.sql'
        MigrationSqlSha256 = (Get-FileHash -LiteralPath $migration -Algorithm SHA256).Hash
        ApiTarget = ([xml](Get-Content -LiteralPath $webProject -Raw)).Project.PropertyGroup.TargetFramework
        IncludesUserData = $false
        IncludesProductionSecrets = $false
        PublishedRemotely = $false
    } | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $releaseRoot 'release.json') -Encoding utf8NoBOM
    & (Join-Path $PSScriptRoot 'Verify-StudyPackage.ps1') -ReleaseRoot $releaseRoot
    Write-Host "Prepared locally: $releaseRoot"
    Write-Host 'Upload WeddingMatch-site.zip only. Review sql/migrate.sql separately; this script did not run it.'
    Write-Host 'Configure SQL Server/JWT secrets in the host, preserve private uploads, and verify HTTPS before inviting users.'
} finally {
    [Environment]::SetEnvironmentVariable('WEDDING_MIGRATION_CONNECTION', $previousMigrationConnection, 'Process')
    Pop-Location
}

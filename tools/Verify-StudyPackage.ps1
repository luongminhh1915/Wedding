#requires -Version 7.0
<# .SYNOPSIS Read-only validation of a locally prepared release; no API or DB access. #>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$ReleaseRoot)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$releasePath = [IO.Path]::GetFullPath($ReleaseRoot)
$manifest = Get-Content -LiteralPath (Join-Path $releasePath 'release.json') -Raw | ConvertFrom-Json
if ($manifest.SiteZip -ne 'WeddingMatch-site.zip' -or $manifest.MigrationSql -ne 'sql/migrate.sql') { throw 'Unexpected artifact names.' }
if ($manifest.IncludesUserData -or $manifest.IncludesProductionSecrets -or $manifest.PublishedRemotely) { throw 'Unsafe release metadata.' }
$zip = Join-Path $releasePath $manifest.SiteZip
$sql = Join-Path $releasePath $manifest.MigrationSql
if ((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash -ne $manifest.SiteZipSha256 -or (Get-FileHash -LiteralPath $sql -Algorithm SHA256).Hash -ne $manifest.MigrationSqlSha256) {
    throw 'Artifact integrity check failed.'
}
$sqlBytes = [IO.File]::ReadAllBytes($sql)
if ($sqlBytes.Length -lt 2 -or $sqlBytes[0] -ne 0xFF -or $sqlBytes[1] -ne 0xFE) { throw 'Migration must have a UTF-16 LE BOM for host sqlcmd auto-detection.' }
$migrationText = Get-Content -LiteralPath $sql -Raw
if (-not $migrationText.StartsWith(':ON ERROR EXIT')) { throw 'Migration must stop on its first sqlcmd error.' }
$preamble = ($migrationText -split '(?im)^\s*GO\s*$', 2)[0]
foreach ($requiredOption in @('XACT_ABORT ON', 'ANSI_NULLS ON', 'QUOTED_IDENTIFIER ON', 'ANSI_PADDING ON', 'ANSI_WARNINGS ON', 'CONCAT_NULL_YIELDS_NULL ON', 'ARITHABORT ON', 'NUMERIC_ROUNDABORT OFF')) {
    if ($preamble -notmatch "(?im)^\s*SET\s+$([regex]::Escape($requiredOption))\s*;") { throw "Migration is missing required session option: $requiredOption." }
}
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try {
    $names = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
    foreach ($name in $names) {
        if ($name -match '(^/|:|(^|/)\.\.(/|$)|(^|/)(App_Data|uploads|private|node_modules|\.git|data)(/|$)|(^|/)\.env($|\.)|\.db($|-)|\.(bak|bacpac|publishsettings|user|pfx|pem|key)$|(^|/)appsettings\.(?!json$).+\.json$|(^|/)advance_payments\.json$)') {
            throw "Forbidden ZIP entry: $name"
        }
    }
    foreach ($required in @('web.config', 'appsettings.json', 'Wedding.WebApi.dll', 'Wedding.WebApi.runtimeconfig.json', 'wwwroot/index.html')) {
        if ($required -notin $names) { throw "ZIP is missing $required." }
    }
    function Read-ZipText([string]$name) {
        $stream = $archive.GetEntry($name).Open()
        $reader = [IO.StreamReader]::new($stream)
        try { return $reader.ReadToEnd() } finally { $reader.Dispose(); $stream.Dispose() }
    }
    $settings = Read-ZipText 'appsettings.json' | ConvertFrom-Json
    if ($settings.Database.Provider -ne 'SqlServer' -or $settings.Database.ApplyMigrations -or $settings.Database.SeedDemoData -or $settings.ConnectionStrings.DefaultConnection -or $settings.JwtSettings.Secret) {
        throw 'ZIP configuration must be fail-closed SQL Server without seeding, migration or secrets.'
    }
    $launcher = [xml](Read-ZipText 'web.config')
    $webServer = $launcher.configuration.location.'system.webServer'
    $iisApp = $webServer.aspNetCore
    if ($iisApp.processPath -ne 'dotnet' -or $iisApp.arguments -ne '.\Wedding.WebApi.dll' -or $iisApp.stdoutLogEnabled -ne 'false') { throw 'Unexpected IIS launcher.' }
    $auth = $webServer.security.authentication
    if ($auth.anonymousAuthentication.enabled -ne 'true' -or $auth.basicAuthentication.enabled -ne 'false') { throw 'IIS launcher must allow public anonymous access and disable Basic authentication.' }
    $variables = @($iisApp.environmentVariables.environmentVariable)
    if ($variables.Count -ne 1 -or $variables[0].name -ne 'ASPNETCORE_ENVIRONMENT' -or $variables[0].value -ne 'Production') { throw 'IIS launcher must contain Production only; no credentials.' }
    $runtime = Read-ZipText 'Wedding.WebApi.runtimeconfig.json' | ConvertFrom-Json
    if ($runtime.runtimeOptions.tfm -ne $manifest.ApiTarget) { throw 'Runtime target does not match release metadata.' }
    foreach ($name in $names | Where-Object { $_ -like 'wwwroot/assets/*.js' }) {
        if ((Read-ZipText $name).Contains('http://localhost:5008')) { throw 'Frontend contains the local API endpoint instead of same-origin deployment.' }
    }
    Write-Host "Verified ZIP: $($names.Count) entries, SQL Server, $($manifest.ApiTarget), no data or production credentials."
} finally { $archive.Dispose() }

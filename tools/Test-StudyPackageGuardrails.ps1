#requires -Version 7.0
<# .SYNOPSIS Adversarial checks on synthetic copies only; never modifies the release. #>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$ReleaseRoot)
$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath($ReleaseRoot)
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$testRoot = Join-Path $repo "artifacts/package-guardrails-$([Guid]::NewGuid().ToString('N'))"
$null = New-Item -ItemType Directory -Path $testRoot
$passed = 0
foreach ($case in @('checksum', 'database-file', 'development-config', 'localhost-client', 'sql-session-options', 'sql-encoding')) {
    $fixture = Join-Path $testRoot $case
    $null = New-Item -ItemType Directory -Path (Join-Path $fixture 'sql')
    $metadata = Get-Content -LiteralPath (Join-Path $source 'release.json') -Raw | ConvertFrom-Json
    Copy-Item -LiteralPath (Join-Path $source 'sql/migrate.sql') -Destination (Join-Path $fixture 'sql/migrate.sql')
    $zip = Join-Path $fixture 'WeddingMatch-site.zip'
    if ($case -in @('database-file', 'development-config')) {
        # No real database or user record is ever put into this fixture.
        $archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
        try {
            $entry = if ($case -eq 'database-file') { 'accidental-backup.db' } else { 'appsettings.Development.json' }
            $writer = [IO.StreamWriter]::new($archive.CreateEntry($entry).Open())
            try { $writer.Write('synthetic test, not a database') } finally { $writer.Dispose() }
        } finally { $archive.Dispose() }
        $expected = 'Forbidden ZIP entry'
    } else {
        Copy-Item -LiteralPath (Join-Path $source 'WeddingMatch-site.zip') -Destination $zip
        if ($case -eq 'localhost-client') {
            $archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Update)
            try {
                $writer = [IO.StreamWriter]::new($archive.CreateEntry('wwwroot/assets/synthetic-local-endpoint.js').Open())
                try { $writer.Write('const fakeApi = "http://localhost:5008";') } finally { $writer.Dispose() }
            } finally { $archive.Dispose() }
            $expected = 'local API endpoint'
        } elseif ($case -eq 'sql-session-options') {
            $fixtureSql = Join-Path $fixture 'sql/migrate.sql'
            (Get-Content -LiteralPath $fixtureSql -Raw).Replace('SET QUOTED_IDENTIFIER ON;', '') | Set-Content -LiteralPath $fixtureSql -Encoding unicode
            $expected = 'Migration is missing required session option'
        } elseif ($case -eq 'sql-encoding') {
            $fixtureSql = Join-Path $fixture 'sql/migrate.sql'
            (Get-Content -LiteralPath $fixtureSql -Raw) | Set-Content -LiteralPath $fixtureSql -Encoding utf8NoBOM
            $expected = 'Migration must have a UTF-16 LE BOM'
        } else { $expected = 'Artifact integrity check failed' }
    }
    $metadata.SiteZipSha256 = if ($case -eq 'checksum') { 'intentionally-wrong-checksum' } else { (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash }
    $metadata.MigrationSqlSha256 = (Get-FileHash -LiteralPath (Join-Path $fixture 'sql/migrate.sql') -Algorithm SHA256).Hash
    $metadata | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $fixture 'release.json') -Encoding utf8NoBOM
    $rejected = $false
    try { & (Join-Path $PSScriptRoot 'Verify-StudyPackage.ps1') -ReleaseRoot $fixture }
    catch {
        if (-not $_.Exception.Message.Contains($expected)) { throw }
        $rejected = $true
    }
    if (-not $rejected) { throw "Guardrail failed to reject $case." }
    $passed++
}
Write-Host "Passed $passed packaging rejection checks. Original release unchanged. Synthetic fixtures retained at $testRoot."

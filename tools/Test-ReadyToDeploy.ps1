#requires -Version 7.0
<#
.SYNOPSIS
Run local release gates, prepare an IIS package and test it on disposable SQL Server/HTTPS.
.DESCRIPTION
Never publishes, registers hosting accounts or changes the normal development database.
Requires local SQL Server, sqlcmd, .NET 10 SDK and frontend dependencies. SQL checks are
mandatory: a skipped database test is not reported as a deploy-ready result.
#>
[CmdletBinding()]
param([switch]$InstallClientDependencies)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$npmCommand = if ($IsWindows) { 'npm.cmd' } else { 'npm' }
foreach ($required in @('dotnet', 'node', $npmCommand, 'sqlcmd')) {
    if (-not (Get-Command $required -ErrorAction SilentlyContinue)) { throw "Missing $required. No ready result was produced." }
}
$run = Join-Path $repo ('artifacts/readiness/' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
$null = New-Item -ItemType Directory -Path $run
$previousSqlTests = [Environment]::GetEnvironmentVariable('WEDDING_TEST_SQLSERVER', 'Process')
function Invoke-Checked([string]$Command, [string[]]$Arguments) {
    & $Command @Arguments | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "$Command failed ($LASTEXITCODE). This source/release has not passed the deploy-ready gates." }
}
function Get-SourceFingerprint {
    $inputs = @(
        foreach ($folder in @('src', 'client/src', 'client/public', 'tests', 'tools', '.config')) {
            Get-ChildItem -LiteralPath (Join-Path $repo $folder) -File -Recurse | Where-Object {
                $relative = [IO.Path]::GetRelativePath($repo, $_.FullName).Replace('\', '/')
                $relative -notmatch '(^|/)(bin|obj|node_modules|dist|private|App_Data|uploads)(/|$)' -and
                    ($folder -eq 'client/public' -or $_.Extension -in @('.cs', '.csproj', '.props', '.targets', '.ts', '.tsx', '.mjs', '.ps1', '.css') -or $_.Name -eq 'dotnet-tools.json')
            }
        }
        foreach ($file in @('global.json', 'Wedding.sln', 'client/package.json', 'client/package-lock.json', 'client/index.html', 'client/tsconfig.json', 'client/tsconfig.app.json', 'client/tsconfig.node.json', 'client/vite.config.ts', 'src/Wedding.WebApi/appsettings.json', 'src/Wedding.WebApi/appsettings.Development.json')) {
            if (Test-Path -LiteralPath (Join-Path $repo $file)) { Get-Item -LiteralPath (Join-Path $repo $file) }
        }
    )
    $entries = foreach ($sourceFile in $inputs | Sort-Object FullName -Unique) {
        [IO.Path]::GetRelativePath($repo, $sourceFile.FullName).Replace('\', '/') + ':' + (Get-FileHash -LiteralPath $sourceFile.FullName -Algorithm SHA256).Hash
    }
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($entries -join "`n")))
}
Push-Location $repo
try {
    $sourceFingerprint = Get-SourceFingerprint
    $env:WEDDING_TEST_SQLSERVER = '1'
    # Reachability only, not the application's DB. All writes in tests use unique QA DB names.
    Invoke-Checked 'sqlcmd' @('-S', 'localhost', '-E', '-C', '-b', '-d', 'master', '-Q', 'SET NOCOUNT ON; SELECT 1;')
    Push-Location (Join-Path $repo 'client')
    try {
        if ($InstallClientDependencies) { Invoke-Checked $npmCommand @('ci') }
        Invoke-Checked $npmCommand @('test')
        Invoke-Checked $npmCommand @('run', 'lint')
    } finally { Pop-Location }
    Invoke-Checked 'dotnet' @('test', 'tests/Wedding.Tests/Wedding.Tests.csproj', '--artifacts-path', (Join-Path $run 'test-build'), '--results-directory', (Join-Path $run 'test-results'), '--logger', 'trx;LogFileName=backend.trx', '--verbosity', 'minimal')
    $trx = [xml](Get-Content -LiteralPath (Join-Path $run 'test-results/backend.trx') -Raw)
    $counters = $trx.TestRun.ResultSummary.Counters
    if ([int]$counters.total -eq 0 -or [int]$counters.passed -ne [int]$counters.total) { throw 'Backend tests must all run and pass, without skipped SQL checks.' }
    $publishOutput = Join-Path $run 'deploy'
    & (Join-Path $PSScriptRoot 'Publish-StudySite.ps1') -OutputRoot $publishOutput
    $releases = @(Get-ChildItem -LiteralPath $publishOutput -Directory)
    if ($releases.Count -ne 1) { throw 'Expected exactly one release from this readiness run.' }
    $release = $releases[0].FullName
    & (Join-Path $PSScriptRoot 'Test-StudyPackageGuardrails.ps1') -ReleaseRoot $release
    & (Join-Path $PSScriptRoot 'Verify-StudyDeployment.ps1') -ReleaseRoot $release -IncludeAccountSecurity
    if ((Get-SourceFingerprint) -ne $sourceFingerprint) { throw 'Source files changed during verification. No ready result was produced; wait until edits finish and run again.' }
    $manifest = Get-Content -LiteralPath (Join-Path $release 'release.json') -Raw | ConvertFrom-Json
    [ordered]@{
        Status = 'LOCAL_GATES_PASSED'
        VerifiedUtc = [DateTime]::UtcNow.ToString('O')
        ReleaseRoot = $release
        SiteZipSha256 = $manifest.SiteZipSha256
        SourceSha256 = $sourceFingerprint
        BackendPassed = [int]$counters.passed
        SqlServerTestsEnabled = $true
        PackagedProductionSmokePassed = $true
        AccountSecuritySmokePassed = $true
        PublishedRemotely = $false
        RealHostVerificationStillRequired = @('HTTPS certificate', 'SQL credentials and permissions', 'Persistent uploads', 'Backup restore', 'Resource limits and provider integrations')
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $run 'ready.json') -Encoding utf8NoBOM
    Write-Host "Local deploy gates passed: $release"
    Write-Host 'No public deployment was performed. Only this exact artifact has been verified; repeat after code changes.'
} finally {
    [Environment]::SetEnvironmentVariable('WEDDING_TEST_SQLSERVER', $previousSqlTests, 'Process')
    Pop-Location
}

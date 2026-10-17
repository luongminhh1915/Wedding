#requires -Version 7.0
<#
.SYNOPSIS
Verify a packaged release against a disposable local SQL Server database and local HTTPS API.
.DESCRIPTION
Never touches a hosting account, existing databases, ports 5008/5009, or real uploads.
Self-signed certificate verification is bypassed ONLY for this loopback HTTPS smoke test.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ReleaseRoot,
    [switch]$IncludeAccountSecurity
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$release = [IO.Path]::GetFullPath($ReleaseRoot)
$site = Join-Path $release 'site'
$migration = Join-Path $release 'sql/migrate.sql'
$sqlcmd = (Get-Command sqlcmd -ErrorAction Stop).Source
foreach ($file in @($migration, (Join-Path $release 'release.json'), (Join-Path $site 'Wedding.WebApi.dll'), (Join-Path $site 'wwwroot/index.html'))) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing release file: $file" }
}
$manifest = Get-Content -LiteralPath (Join-Path $release 'release.json') -Raw | ConvertFrom-Json
if ((Get-FileHash -LiteralPath $migration -Algorithm SHA256).Hash -ne $manifest.MigrationSqlSha256) { throw 'Release SQL hash does not match its manifest.' }
if ((Get-Content -LiteralPath $migration -Raw) -notmatch '(?m)^:ON ERROR EXIT') { throw 'Expected the reviewed SQLCMD migration script.' }
foreach ($port in @(5011, 5012)) {
    if (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue) { throw "Port $port is occupied; no process will be stopped." }
}
$database = 'WeddingPlanTest_Study_' + [Guid]::NewGuid().ToString('N')
$userId = [Guid]::NewGuid().ToString('D')
$qaRoot = Join-Path ([IO.Path]::GetTempPath()) ('WeddingStudyDeployment_' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $qaRoot
$databaseCreated = $false
$script:apiProcess = $null
$script:stdoutTask = $null
$script:stderrTask = $null
$script:starts = 0

function Invoke-LocalSql([string]$Query, [string]$Target = 'master') {
    # Each QA query opens a new connection; -I permits writes to filtered-index tables.
    $output = & $sqlcmd -S localhost -E -C -I -b -f 65001 -h -1 -W -s '|' -d $Target -Q "SET NOCOUNT ON; $Query" 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Local QA SQL command failed: $($output -join ' ')" }
    return ($output -join "`n").Trim()
}
function Apply-ReleaseSchema {
    # Deliberately no -f override: the final UTF-16LE BOM release must import
    # Vietnamese text correctly through the host's ordinary SQLCMD defaults.
    $output = & $sqlcmd -S localhost -E -C -b -d $database -i $migration 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Release schema failed against disposable QA DB: $($output -join ' ')" }
}
function Assert-Sql([string]$Expected, [string]$Query) {
    $actual = Invoke-LocalSql $Query $database
    if ($actual -ne $Expected) { throw "QA SQL assertion expected [$Expected], got [$actual]." }
}
function Stop-QAApi {
    if ($null -eq $script:apiProcess) { return }
    if (-not $script:apiProcess.HasExited) { $script:apiProcess.Kill(); $script:apiProcess.WaitForExit(10000) | Out-Null }
    if ($null -ne $script:stdoutTask) { [IO.File]::WriteAllText((Join-Path $qaRoot "api-$script:starts.stdout.log"), $script:stdoutTask.GetAwaiter().GetResult()) }
    if ($null -ne $script:stderrTask) { [IO.File]::WriteAllText((Join-Path $qaRoot "api-$script:starts.stderr.log"), $script:stderrTask.GetAwaiter().GetResult()) }
    $script:apiProcess.Dispose(); $script:apiProcess = $null
}
function Start-QAApi {
    $start = [Diagnostics.ProcessStartInfo]::new((Get-Command dotnet).Source)
    $start.WorkingDirectory = $site
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    $start.ArgumentList.Add((Join-Path $site 'Wedding.WebApi.dll'))
    foreach ($key in @($start.Environment.Keys)) {
        if ($key -match '^(ConnectionStrings|Database|JwtSettings|Uploads|Kestrel|AllowedHosts)(:|__|$)|^(SQLCONNSTR_|SQLAZURECONNSTR_|CUSTOMCONNSTR_|DatabaseProvider$)') { $null = $start.Environment.Remove($key) }
    }
    $start.Environment['WEDDING_PRIVATE_CONFIG'] = Join-Path $qaRoot 'wedding.production.json'
    $start.Environment['ASPNETCORE_ENVIRONMENT'] = 'Production'; $start.Environment['DOTNET_ENVIRONMENT'] = 'Production'
    $start.Environment['ASPNETCORE_URLS'] = 'https://127.0.0.1:5011;http://127.0.0.1:5012'
    $start.Environment['ASPNETCORE_HTTPS_PORT'] = '5011'
    $script:starts++
    $script:apiProcess = [Diagnostics.Process]::Start($start)
    $script:stdoutTask = $script:apiProcess.StandardOutput.ReadToEndAsync()
    $script:stderrTask = $script:apiProcess.StandardError.ReadToEndAsync()
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        if ($script:apiProcess.HasExited) { throw "Packaged API exited. QA diagnostics are retained in $qaRoot." }
        try {
            $ready = Invoke-WebRequest 'https://127.0.0.1:5011/health/ready' -SkipCertificateCheck -SkipHttpErrorCheck -TimeoutSec 2
            if ($ready.StatusCode -eq 200) { return }
        } catch { }
        Start-Sleep -Milliseconds 500
    }
    throw "Packaged API failed readiness. QA diagnostics are retained in $qaRoot."
}
function Assert-Http([string]$Path, [int]$Status, [string]$Type) {
    $response = Invoke-WebRequest ("https://127.0.0.1:5011" + $Path) -SkipCertificateCheck -SkipHttpErrorCheck -TimeoutSec 10
    if ($response.StatusCode -ne $Status) { throw "$Path returned $($response.StatusCode), expected $Status." }
    $contentType = $response.Headers['Content-Type'] -join ''
    if ($Type -eq 'not-html') {
        if ($contentType -match 'text/html' -or $response.Content -match '(?i)<!doctype|<html') { throw "$Path leaked the SPA fallback instead of a non-HTML response." }
    } elseif ($contentType -notmatch "^$Type") { throw "$Path has unexpected Content-Type: $contentType" }
}

try {
    $null = Invoke-LocalSql "CREATE DATABASE [$database];"; $databaseCreated = $true
    Apply-ReleaseSchema; Apply-ReleaseSchema
    Assert-Sql '7|16|0|0' 'SELECT (SELECT COUNT(*) FROM Categories),(SELECT COUNT(*) FROM InvitationTemplates),(SELECT COUNT(*) FROM Users),(SELECT COUNT(*) FROM Vendors);'
    Assert-Sql '1' "SELECT COUNT(*) FROM Categories WHERE Slug='vay-cuoi-vest' AND Name=N'Váy cưới & Vest cưới (Bridal & Suits)';"
    $null = Invoke-LocalSql "INSERT INTO Users(Id,FullName,Email,PhoneNumber,PasswordHash,Role,IsActive,CreatedAt) VALUES('$userId',N'QA persistence sentinel','qa-sentinel@example.invalid','0900000000','qa-only-not-a-password-hash','Customer',0,SYSUTCDATETIME());" $database

    # Ephemeral loopback-only TLS certificate: no development certificate installation/trust-store changes.
    $rsa = [Security.Cryptography.RSA]::Create(2048)
    $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=localhost', $rsa, [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $san = [Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new()
    $san.AddDnsName('localhost'); $san.AddIpAddress([Net.IPAddress]::Loopback)
    $request.CertificateExtensions.Add($san.Build())
    $certificate = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-5), [DateTimeOffset]::UtcNow.AddDays(1))
    $pfxPassword = [Guid]::NewGuid().ToString('N')
    $pfxPath = Join-Path $qaRoot 'loopback-only.pfx'
    [IO.File]::WriteAllBytes($pfxPath, $certificate.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $pfxPassword))
    $certificate.Dispose(); $rsa.Dispose()
    $listingRoot = Join-Path $qaRoot 'private/uploads/listings'
    $null = New-Item -ItemType Directory -Path $listingRoot
    $imageName = [Guid]::NewGuid().ToString('N') + '.png'
    $png = [Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=')
    [IO.File]::WriteAllBytes((Join-Path $listingRoot $imageName), $png)
    $connection = "Server=localhost;Database=$database;Integrated Security=true;Encrypt=true;TrustServerCertificate=true"
    $private = @{
        Database = @{ Provider = 'SqlServer'; ApplyMigrations = $false; SeedDemoData = $false }
        ConnectionStrings = @{ DefaultConnection = $connection }
        JwtSettings = @{ Secret = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48)); Issuer = 'WeddingPlatform'; Audience = 'WeddingPlatformClient' }
        AllowedHosts = 'localhost;127.0.0.1'
        Uploads = @{ ListingRoot = $listingRoot; InvitationRoot = (Join-Path $qaRoot 'private/uploads/invitations') }
        Kestrel = @{ Certificates = @{ Default = @{ Path = $pfxPath; Password = $pfxPassword } } }
        Logging = @{ LogLevel = @{ Default = 'Warning'; 'Microsoft.EntityFrameworkCore.Database.Command' = 'Warning' } }
    }
    $private | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $qaRoot 'wedding.production.json') -Encoding utf8NoBOM
    Start-QAApi
    foreach ($path in @('/', '/combo', '/customer/dashboard')) { Assert-Http $path 200 'text/html' }
    foreach ($path in @('/api/missing', '/uploads/listings/unsafe.html', '/private/wedding.production.json')) { Assert-Http $path 404 'not-html' }
    Assert-Http '/health' 200 'application/json'; Assert-Http '/health/ready' 200 'application/json'
    Assert-Http ("/uploads/listings/" + $imageName) 200 'image/png'
    $login = Invoke-WebRequest 'https://127.0.0.1:5011/api/auth/login' -Method Post -ContentType 'application/json' -Body '{}' -SkipCertificateCheck -SkipHttpErrorCheck
    if ($login.StatusCode -ne 400 -or ($login.Headers['Content-Type'] -join '') -notmatch '^application/') { throw 'Invalid login did not return an API 400 response.' }
    $redirect = Invoke-WebRequest 'http://127.0.0.1:5012/combo' -MaximumRedirection 0 -SkipHttpErrorCheck -ErrorAction SilentlyContinue
    if ($redirect.StatusCode -notin @(307,308) -or ($redirect.Headers['Location'] -join '') -ne 'https://127.0.0.1:5011/combo') { throw 'HTTP did not redirect to loopback HTTPS.' }
    Assert-Http '/api/combos/builder' 200 'application/json'
    Start-Sleep -Seconds 12 # Let startup/background queries settle before measuring the warmed process.
    $script:apiProcess.Refresh()
    Write-Host ("Warmed smoke-process working set: {0:N1} MB; private bytes: {1:N1} MB (single-user smoke, not a concurrent-capacity result)." -f ($script:apiProcess.WorkingSet64 / 1MB), ($script:apiProcess.PrivateMemorySize64 / 1MB))
    if ($script:apiProcess.WorkingSet64 -gt 200MB) { Write-Warning 'Observed working set exceeds 200 MB. Fit on a 256 MB shared host is uncertain; this is not a load test.' }
    Stop-QAApi; Apply-ReleaseSchema; Start-QAApi
    Assert-Sql '7|16|1|0' 'SELECT (SELECT COUNT(*) FROM Categories),(SELECT COUNT(*) FROM InvitationTemplates),(SELECT COUNT(*) FROM Users),(SELECT COUNT(*) FROM Vendors);'
    Assert-Sql 'QA persistence sentinel|qa-only-not-a-password-hash' "SELECT FullName,PasswordHash FROM Users WHERE Id='$userId';"
    Assert-Http ("/uploads/listings/" + $imageName) 200 'image/png'
    $download = Join-Path $qaRoot 'restarted-image.png'
    Invoke-WebRequest ("https://127.0.0.1:5011/uploads/listings/" + $imageName) -SkipCertificateCheck -OutFile $download
    if ((Get-FileHash -LiteralPath $download).Hash -ne (Get-FileHash -LiteralPath (Join-Path $listingRoot $imageName)).Hash) { throw 'Uploaded image bytes changed after restart.' }
    if ($IncludeAccountSecurity) {
        & (Join-Path $PSScriptRoot 'Verify-AccountSecurity.ps1') -DatabaseName $database -QaRoot $qaRoot -ApiProcessId $script:apiProcess.Id
        Start-Sleep -Seconds 2
        $script:apiProcess.Refresh()
        Write-Host ("After account/WebSocket smoke: working set {0:N1} MB; private bytes {1:N1} MB (small functional workload, not a load test)." -f ($script:apiProcess.WorkingSet64 / 1MB), ($script:apiProcess.PrivateMemorySize64 / 1MB))
        if ($script:apiProcess.WorkingSet64 -gt 200MB) { Write-Warning 'Post-security-smoke working set exceeds 200 MB. A 256 MB shared host may need a larger plan; verify real-host resource usage before inviting users.' }
    }
    Write-Host 'PASS: actual SQL migration idempotency; 7 categories / 16 templates / no demo users/vendors; Production SPA, API 404 isolation, readiness, invalid-login JSON, HTTPS redirect; SQL user/hash and private image survive API restart.'
} finally {
    Stop-QAApi
    if ($databaseCreated) {
        if ($database -notmatch '^WeddingPlanTest_Study_[a-f0-9]{32}$') { throw 'Refusing cleanup: unexpected database target.' }
        $null = Invoke-LocalSql "ALTER DATABASE [$database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$database];"
        Write-Host "Removed disposable local QA database: $database"
    }
    # Keep only QA diagnostics/data in a unique temp folder for review; never delete source or user files.
    Write-Host "QA-only temporary certificate/config/diagnostics retained locally: $qaRoot (contains no real credentials or customer data)."
}

#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '../.deployment/private/wedding.production.json'),
    [switch]$TrustHostedSqlCertificate
)

$ErrorActionPreference = 'Stop'
$destination = [IO.Path]::GetFullPath($OutputPath)
if (Test-Path -LiteralPath $destination) { throw 'Configuration already exists. Keep the existing secrets; this tool never overwrites them.' }

function Read-Required([string]$Prompt) {
    $value = (Read-Host $Prompt).Trim()
    if ([string]::IsNullOrWhiteSpace($value)) { throw "$Prompt is required." }
    return $value
}

$siteHost = Read-Required 'Public hostname only (for example wedding-match.runasp.net)'
if ([Uri]::CheckHostName($siteHost) -ne [UriHostNameType]::Dns -or $siteHost.Contains('/') -or $siteHost.Contains(':')) {
    throw 'Use only the hostname assigned by your host, without https:// or a path.'
}
$server = Read-Required 'SQL Server hostname from the hosting panel'
$database = Read-Required 'SQL database name from the hosting panel'
if ($database -in @('master', 'model', 'msdb', 'tempdb')) { throw 'A system database is not an application database.' }
$sqlUser = Read-Required 'SQL database username'
$securePassword = Read-Host 'SQL database password (hidden)' -AsSecureString
if ($securePassword.Length -eq 0) { throw 'SQL password is required.' }
$passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
try {
    # Generic builder quotes values safely without installing or loading a SQL client in PowerShell.
    $connection = New-Object System.Data.Common.DbConnectionStringBuilder
    $connection['Server'] = $server
    $connection['Database'] = $database
    $connection['User Id'] = $sqlUser
    $connection['Password'] = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
    $connection['Encrypt'] = 'true'
    # MonsterASP documents a non-publicly-trusted SQL certificate. Opt in explicitly;
    # traffic is encrypted but certificate identity is not validated in this mode.
    $connection['TrustServerCertificate'] = if ($TrustHostedSqlCertificate) { 'true' } else { 'false' }
    $connection['Max Pool Size'] = '20'
    $connection['Connect Timeout'] = '15'
    $configuration = [ordered]@{
        Database = @{ Provider = 'SqlServer'; ApplyMigrations = $false; SeedDemoData = $false }
        ConnectionStrings = @{ DefaultConnection = $connection.ConnectionString }
        JwtSettings = @{ Secret = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48)); Issuer = 'WeddingPlatform'; Audience = 'WeddingPlatformClient'; ExpiryMinutes = 240 }
        AllowedHosts = $siteHost
        Cors = @{ Origins = @("https://$siteHost") }
        Uploads = @{ InvitationRoot = '../private/uploads/invitations'; InvitationAudioRoot = '../private/uploads/invitation-audio'; ListingRoot = '../private/uploads/listings' }
        Logging = @{ LogLevel = @{ Default = 'Information'; 'Microsoft.AspNetCore' = 'Warning'; 'Microsoft.EntityFrameworkCore.Database.Command' = 'Warning' } }
    }
    $parent = [IO.Path]::GetDirectoryName($destination)
    [IO.Directory]::CreateDirectory($parent) | Out-Null
    $json = $configuration | ConvertTo-Json -Depth 6
    # Credentials are a private deployment input, not a source file or an uploadable public artifact.
    $stream = [IO.File]::Open($destination, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes($json)
        $stream.Write($bytes, 0, $bytes.Length)
    } finally { $stream.Dispose() }
    Write-Host "Private configuration created: $destination"
    Write-Host 'Upload this file ONLY to /private/wedding.production.json, beside the hosting /wwwroot folder.'
    Write-Host 'Never upload it to /wwwroot/wwwroot, Git, chat, or include it in the site ZIP. Keep this file when redeploying.'
    Write-Host 'Request HTTPS from hosting support and verify it before inviting anyone to enter personal data.'
    if ($TrustHostedSqlCertificate) { Write-Warning 'SQL traffic is encrypted, but this hosting-specific setting skips certificate identity validation. Use only the exact server address from your hosting panel.' }
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer)
    $securePassword.Dispose()
    Remove-Variable connection, configuration, json, bytes -ErrorAction SilentlyContinue
}

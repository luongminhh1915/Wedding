#requires -Version 7.0
<#
.SYNOPSIS
Exercise account revocation and real SignalR privacy inside Verify-StudyDeployment's disposable environment.
.DESCRIPTION
This helper refuses non-loopback URLs, non-QA database names and a listener not
owned by the supplied QA process. It never targets a hosting account or real DB.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$DatabaseName,
    [Parameter(Mandatory)][string]$QaRoot,
    [Parameter(Mandatory)][int]$ApiProcessId,
    [string]$BaseUrl = 'https://127.0.0.1:5011'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($BaseUrl -cne 'https://127.0.0.1:5011') { throw 'Account smoke only permits its fixed loopback HTTPS URL.' }
if ($DatabaseName -cnotmatch '^WeddingPlanTest_Study_[a-f0-9]{32}$') { throw 'Account smoke requires its disposable Study QA database.' }
$resolvedQaRoot = [IO.Path]::GetFullPath($QaRoot)
$expectedPrefix = Join-Path ([IO.Path]::GetTempPath()) 'WeddingStudyDeployment_'
if (-not $resolvedQaRoot.StartsWith($expectedPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected QA fixture directory.' }
$listener = @(Get-NetTCPConnection -State Listen -LocalPort 5011 -ErrorAction Stop)
if ($listener.Count -ne 1 -or $listener[0].OwningProcess -ne $ApiProcessId) { throw 'Loopback API listener is not owned by the supplied disposable QA process.' }
$config = Get-Content -LiteralPath (Join-Path $resolvedQaRoot 'wedding.production.json') -Raw | ConvertFrom-Json
$connection = [System.Data.Common.DbConnectionStringBuilder]::new()
# Use CLR accessors: PowerShell's IDictionary adapter otherwise creates a key
# named "ConnectionString" instead of setting the actual CLR property.
$connection.set_ConnectionString($config.ConnectionStrings.DefaultConnection)
if ($connection.get_Item('Database') -cne $DatabaseName -or $connection.get_Item('Server') -cne 'localhost') { throw 'QA private configuration does not target the expected local disposable database.' }
$sqlcmd = (Get-Command sqlcmd -ErrorAction Stop).Source
$node = (Get-Command node -ErrorAction Stop).Source
$password = 'QA-only-' + [Guid]::NewGuid().ToString('N') + '!'
$nextPassword = 'QA-new-' + [Guid]::NewGuid().ToString('N') + '!'
$suffix = [Guid]::NewGuid().ToString('N')

function Invoke-QAApi([string]$Path, [hashtable]$Body) {
    $response = Invoke-WebRequest ($BaseUrl + $Path) -Method Post -ContentType 'application/json' `
        -Body ($Body | ConvertTo-Json -Compress) -SkipCertificateCheck -SkipHttpErrorCheck -TimeoutSec 20
    if ($response.StatusCode -ne 200) { throw "QA setup endpoint $Path returned $($response.StatusCode)." }
    return $response.Content | ConvertFrom-Json
}

$customer = Invoke-QAApi '/api/auth/register-customer' @{
    fullName = 'QA chat couple'; email = "qa-couple-$suffix@example.invalid"; phoneNumber = '0901234567'; password = $password
}
$otherCustomer = Invoke-QAApi '/api/auth/register-customer' @{
    fullName = 'QA unrelated couple'; email = "qa-other-$suffix@example.invalid"; phoneNumber = '0901234568'; password = $password
    role = 'SuperAdmin' # Public registration must ignore an attacker-selected role.
}
if ($customer.user.role -cne 'Customer' -or $otherCustomer.user.role -cne 'Customer') { throw 'Public registration accepted an unexpected actor role.' }
$vendor = Invoke-QAApi '/api/auth/register-vendor' @{
    fullName = 'QA vendor'; email = "qa-vendor-$suffix@example.invalid"; phoneNumber = '0901234569'; password = $password
    brandName = "QA-only shop $suffix"; city = 'HCM'; commissionRate = .08
}
if ($vendor.user.role -cne 'VendorOwner') { throw 'Vendor registration did not preserve its actual actor role.' }
foreach ($id in @($customer.user.id, $otherCustomer.user.id, $vendor.user.id, $vendor.user.vendorId)) {
    if (-not [Guid]::TryParse($id, [ref]([Guid]::Empty))) { throw 'QA API returned an invalid ID.' }
}
$leadId = [Guid]::NewGuid().ToString('D')
$moderatorId = [Guid]::NewGuid().ToString('D')
$moderatorEmail = "qa-moderator-$suffix@example.invalid"
# Copy only the freshly registered QA hash. No built-in password or real user is read.
$sql = @"
SET NOCOUNT ON;
INSERT INTO Users(Id,FullName,Email,PhoneNumber,PasswordHash,SecurityVersion,Role,IsActive,CreatedAt)
SELECT '$moderatorId',N'QA-only moderator','$moderatorEmail','0901234570',PasswordHash,0,'Moderator',1,SYSUTCDATETIME()
FROM Users WHERE Id='$($customer.user.id)' AND Email='qa-couple-$suffix@example.invalid';
IF @@ROWCOUNT <> 1 THROW 51000,'Missing synthetic QA user; refusing setup.',1;
INSERT INTO Leads(Id,CustomerId,VendorId,RawPhoneNumber,MaskedPhoneNumber,IsPhoneUnlocked,Status,SlaDeadline,CreatedAt,Source)
VALUES('$leadId','$($customer.user.id)','$($vendor.user.vendorId)','0901234567','0901***567',0,'New',DATEADD(hour,2,SYSUTCDATETIME()),SYSUTCDATETIME(),'Listing');
"@
$output = & $sqlcmd -S localhost -E -C -I -b -d $DatabaseName -Q $sql 2>&1
if ($LASTEXITCODE -ne 0) { throw "Synthetic QA fixture creation failed: $($output -join ' ')" }
$moderator = Invoke-QAApi '/api/auth/login' @{ email = $moderatorEmail; password = $password }
$secondDevice = Invoke-QAApi '/api/auth/login' @{ email = $customer.user.email; password = $password }
$fixture = @{
    baseUrl = $BaseUrl; leadId = $leadId; customer = $customer; otherCustomer = $otherCustomer
    vendor = $vendor; moderator = $moderator; secondDeviceToken = $secondDevice.token
    password = $password; nextPassword = $nextPassword
}
$fixturePath = Join-Path $resolvedQaRoot 'account-security.synthetic-only.json'
$fixture | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $fixturePath -Encoding utf8NoBOM
& $node (Join-Path $PSScriptRoot 'verify-account-security.mjs') $fixturePath
if ($LASTEXITCODE -ne 0) { throw 'Actual account/SignalR security smoke failed; inspect its redacted step output.' }

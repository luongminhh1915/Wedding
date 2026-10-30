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
    brandName = "QA-only shop $suffix"; city = 'HCM'
    partnershipPolicyAccepted = $true
}
if ($vendor.user.role -cne 'VendorOwner') { throw 'Vendor registration did not preserve its actual actor role.' }
# The generic acknowledgement is not membership, a signed agreement or an active rate.
# Read its version from the canonical policy constant instead of retaining a stale smoke copy.
$policySource = Join-Path $PSScriptRoot '../src/Wedding.Domain/Entities/BusinessGroup.cs'
$versionMatches = [regex]::Matches((Get-Content -LiteralPath $policySource -Raw), 'public const string ServiceCategoryRegistrationVersion = "([^"\r\n]+)";')
if ($versionMatches.Count -ne 1) { throw 'Cannot determine the canonical service-category signup acknowledgement version.' }
$registrationVersion = $versionMatches[0].Groups[1].Value
$policyResponse = Invoke-WebRequest ($BaseUrl + '/api/business-groups/my') -Headers @{ Authorization = "Bearer $($vendor.token)" } -SkipCertificateCheck -SkipHttpErrorCheck -TimeoutSec 20
if ($policyResponse.StatusCode -ne 200) { throw 'Vendor cannot read its persisted business-group policy.' }
$policy = $policyResponse.Content | ConvertFrom-Json
if ($policy.usesLegacyCommissionRate -or $null -ne $policy.legacyCommissionRate -or
    $policy.groups -isnot [Array] -or @($policy.groups).Count -ne 0 -or
    -not $policy.policyAcceptedAt -or $policy.policyVersion -cne $registrationVersion) {
    throw 'New vendor must retain only its generic acknowledgement, without fabricated memberships or a shop-wide legacy rate.'
}
$categoriesResponse = Invoke-WebRequest ($BaseUrl + '/api/categories/my') -Headers @{ Authorization = "Bearer $($vendor.token)" } -SkipCertificateCheck -SkipHttpErrorCheck -TimeoutSec 20
if ($categoriesResponse.StatusCode -ne 200) { throw 'New vendor cannot read open service categories without signup memberships.' }
$eligibility = $categoriesResponse.Content | ConvertFrom-Json
$publicResponse = Invoke-WebRequest ($BaseUrl + '/api/categories') -SkipCertificateCheck -SkipHttpErrorCheck -TimeoutSec 20
if ($publicResponse.StatusCode -ne 200) { throw 'Cannot compare vendor categories with the configured public catalog.' }
$configured = @(($publicResponse.Content | ConvertFrom-Json) | Where-Object { $null -ne $_.businessGroupId })
if ($eligibility.requiresMembershipReview -or $null -ne $eligibility.guidance -or
    $eligibility.categories -isnot [Array] -or @($eligibility.categories).Count -ne 8 -or
    $eligibility.existingCategories -isnot [Array] -or @($eligibility.existingCategories).Count -ne 0 -or
    $eligibility.businessGroupIds -isnot [Array] -or @($eligibility.businessGroupIds).Count -ne 0 -or
    $configured.Count -ne 8 -or @($eligibility.categories.id | Sort-Object -Unique).Count -ne 8 -or
    @($eligibility.categories.businessGroupId | Sort-Object -Unique).Count -ne 5) {
    throw 'New vendor category eligibility must include all eight configured types across five groups, without creating historical memberships or requiring Admin membership review.'
}
foreach ($category in $eligibility.categories) {
    $canonical = @($configured | Where-Object { $_.id -eq $category.id })
    if ($canonical.Count -ne 1 -or -not $category.businessGroupName -or
        $category.businessGroupId -ne $canonical[0].businessGroupId -or
        $category.businessGroupName -cne $canonical[0].businessGroupName -or
        $category.defaultCommissionRate -ne $canonical[0].defaultCommissionRate -or
        $category.requiresCommissionBasisApproval -ne $canonical[0].requiresCommissionBasisApproval) {
        throw 'Vendor service categories must preserve canonical group, rate and basis requirements; signup does not choose them.'
    }
}
foreach ($deniedToken in @('', $customer.token)) {
    $headers = @{}; if ($deniedToken) { $headers.Authorization = "Bearer $deniedToken" }
    $denied = Invoke-WebRequest ($BaseUrl + '/api/business-groups/my') -Headers $headers -SkipCertificateCheck -SkipHttpErrorCheck -TimeoutSec 20
    $expected = if ($deniedToken) { 403 } else { 401 }
    if ($denied.StatusCode -ne $expected) { throw 'Vendor policy endpoint did not enforce its actor boundary.' }
    $deniedCategories = Invoke-WebRequest ($BaseUrl + '/api/categories/my') -Headers $headers -SkipCertificateCheck -SkipHttpErrorCheck -TimeoutSec 20
    if ($deniedCategories.StatusCode -ne $expected) { throw 'Vendor category eligibility endpoint did not enforce its actor boundary.' }
}
Write-Host 'Real HTTPS generic vendor acknowledgement, open canonical categories without memberships, and actor boundaries passed.'
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

# Exercise real HTTP authorization before the account smoke revokes any fixture
# sessions. All requests retain the guarded disposable QA origin/database above.
function Assert-QAAdvanceNoticeAccess([string]$Path, [string]$Token, [int]$ExpectedStatus) {
    $headers = @{}
    if (-not [string]::IsNullOrWhiteSpace($Token)) { $headers.Authorization = "Bearer $Token" }
    $response = Invoke-WebRequest ($BaseUrl + $Path) -Method Get -Headers $headers `
        -SkipCertificateCheck -SkipHttpErrorCheck -MaximumRedirection 0 -TimeoutSec 20
    if ($response.StatusCode -ne $ExpectedStatus) {
        throw "Advance-notice access smoke expected $ExpectedStatus at $Path; received $($response.StatusCode)."
    }
    $contentType = if ($response.Headers.ContainsKey('Content-Type')) { $response.Headers['Content-Type'] -join ',' } else { '' }
    if ($contentType -match '(?i)text/html' -or [string]$response.Content -match '(?is)<(?:!DOCTYPE\s+html|html(?:\s|>))') {
        throw "Advance-notice access smoke received HTML instead of an API response at $Path."
    }
    if ($ExpectedStatus -eq 200) {
        if ($contentType -notmatch '(?i)^application/json(?:\s*;|$)') {
            throw 'Advance-notice vendor list did not return JSON.'
        }
        $notices = ConvertFrom-Json -InputObject ([string]$response.Content) -NoEnumerate -ErrorAction Stop
        if ($notices -isnot [System.Array]) { throw 'Advance-notice vendor list must be a JSON array, including when empty.' }
    }
}
Assert-QAAdvanceNoticeAccess '/api/commissions/advance-requests' '' 401
Assert-QAAdvanceNoticeAccess '/api/commissions/advance-requests' $customer.token 403
Assert-QAAdvanceNoticeAccess '/api/commissions/advance-requests' $moderator.token 403
Assert-QAAdvanceNoticeAccess '/api/commissions/advance-requests' $vendor.token 200
Assert-QAAdvanceNoticeAccess '/api/commissions/admin/advance-requests' $vendor.token 403
Write-Host 'Advance-notice HTTP role isolation and JSON shape passed.'

# These Phase 3A fixtures use only the already-guarded disposable HTTPS API.
# Keep them before account revocation and keep the legacy array endpoint intact.
function Invoke-QAListingSmoke([string]$Path, [string]$Token, [int]$ExpectedStatus, [hashtable]$Body = $null) {
    $headers = @{}
    if (-not [string]::IsNullOrWhiteSpace($Token)) { $headers.Authorization = "Bearer $Token" }
    $requestParameters = @{
        Uri = $BaseUrl + $Path; Method = 'Get'; Headers = $headers
        SkipCertificateCheck = $true; SkipHttpErrorCheck = $true; MaximumRedirection = 0; TimeoutSec = 20
    }
    if ($null -ne $Body) {
        $requestParameters.Method = 'Post'
        $requestParameters.ContentType = 'application/json'
        $requestParameters.Body = $Body | ConvertTo-Json -Compress -Depth 5
    }
    $response = Invoke-WebRequest @requestParameters
    if ($response.StatusCode -ne $ExpectedStatus) {
        throw "Vendor listing HTTPS smoke expected $ExpectedStatus at $Path; received $($response.StatusCode)."
    }
    $contentType = if ($response.Headers.ContainsKey('Content-Type')) { $response.Headers['Content-Type'] -join ',' } else { '' }
    if ($contentType -match '(?i)text/html' -or [string]$response.Content -match '(?is)<(?:!DOCTYPE\s+html|html(?:\s|>))') {
        throw 'Vendor listing HTTPS smoke received HTML instead of an API response.'
    }
    if ($ExpectedStatus -in @(200, 201)) {
        if ($contentType -notmatch '(?i)^application/json(?:\s*;|$)') { throw 'Vendor listing HTTPS smoke requires a JSON response.' }
        return [pscustomobject]@{ Json = (ConvertFrom-Json -InputObject ([string]$response.Content) -NoEnumerate -ErrorAction Stop) }
    }
}

$otherVendor = Invoke-QAApi '/api/auth/register-vendor' @{
    fullName = 'QA separate vendor'; email = "qa-vendor-other-$suffix@example.invalid"; phoneNumber = '0901234571'; password = $password
    brandName = "QA-only separate shop $suffix"; city = 'HCM'
    partnershipPolicyAccepted = $true
}
if ($otherVendor.user.role -cne 'VendorOwner' -or $otherVendor.user.vendorId -eq $vendor.user.vendorId) {
    throw 'Vendor listing smoke needs a separate synthetic shop with its actual VendorOwner role.'
}
$ownListingIds = @()
$foreignListingIds = @()
$ownKeyword = "qa-services-own-$suffix"
$foreignKeyword = "qa-services-foreign-$suffix"
foreach ($listingFixture in @(
    @{ Token = $vendor.token; Keyword = $ownKeyword; ListingCount = 2; Own = $true },
    @{ Token = $otherVendor.token; Keyword = $foreignKeyword; ListingCount = 3; Own = $false }
)) {
    foreach ($number in 1..$listingFixture.ListingCount) {
        $createdListing = (Invoke-QAListingSmoke '/api/listings' $listingFixture.Token 201 @{
            categoryId = '11111111-1111-1111-1111-111111111102'
            title = "$($listingFixture.Keyword) service $number"
            minPrice = 1000000; maxPrice = 2000000; description = 'Synthetic HTTPS pagination fixture only.'
            location = 'QA-only service area'; imageUrls = @("https://example.invalid/qa-listing-$suffix-$number.jpg")
        }).Json
        if (-not [Guid]::TryParse([string]$createdListing.listingId, [ref]([Guid]::Empty))) {
            throw 'Vendor listing create API returned an invalid listing ID.'
        }
        if ($listingFixture.Own) { $ownListingIds += [string]$createdListing.listingId }
        else { $foreignListingIds += [string]$createdListing.listingId }
    }
}

$listingPagePath = '/api/listings/my-listings/page'
$ownPage = (Invoke-QAListingSmoke $listingPagePath $vendor.token 200).Json
foreach ($property in @('items', 'totalCount', 'page', 'pageSize', 'totalPages')) {
    if ($ownPage.PSObject.Properties.Name -notcontains $property) { throw 'Vendor listing page is missing a required response property.' }
}
if ($ownPage.items -isnot [System.Array] -or $ownPage.totalCount -ne 2 -or $ownPage.page -ne 1 -or
    $ownPage.pageSize -ne 10 -or $ownPage.totalPages -ne 1 -or @($ownPage.items).Count -ne 2) {
    throw 'Vendor listing default page/count/shape is incorrect.'
}
foreach ($listingRow in $ownPage.items) {
    if ($listingRow.id -notin $ownListingIds -or $listingRow.id -in $foreignListingIds -or $listingRow.status -cne 'Draft' -or
        $listingRow.PSObject.Properties.Name -notcontains 'updatedAt') {
        throw 'Vendor listing page exposed another shop, wrong lifecycle state, or omitted updatedAt.'
    }
}
$encodedOwnKeyword = [Uri]::EscapeDataString("  $ownKeyword  ")
$firstListingPage = (Invoke-QAListingSmoke "$($listingPagePath)?keyword=$encodedOwnKeyword&status=Draft&page=1&pageSize=1" $vendor.token 200).Json
$secondListingPage = (Invoke-QAListingSmoke "$($listingPagePath)?keyword=$encodedOwnKeyword&status=Draft&page=2&pageSize=1" $vendor.token 200).Json
if ($firstListingPage.totalCount -ne 2 -or $firstListingPage.totalPages -ne 2 -or $firstListingPage.pageSize -ne 1 -or
    @($firstListingPage.items).Count -ne 1 -or $secondListingPage.totalCount -ne 2 -or $secondListingPage.page -ne 2 -or
    @($secondListingPage.items).Count -ne 1 -or $firstListingPage.items[0].id -eq $secondListingPage.items[0].id -or
    $firstListingPage.items[0].id -notin $ownListingIds -or $secondListingPage.items[0].id -notin $ownListingIds) {
    throw 'Vendor listing filtered pages were not distinct, correctly counted own-shop results.'
}
$beyondListingPage = (Invoke-QAListingSmoke "$($listingPagePath)?keyword=$encodedOwnKeyword&status=Draft&page=3&pageSize=1" $vendor.token 200).Json
if ($beyondListingPage.page -ne 3 -or $beyondListingPage.totalCount -ne 2 -or $beyondListingPage.totalPages -ne 2 -or
    @($beyondListingPage.items).Count -ne 0) { throw 'Vendor listing page beyond the last page did not retain accurate metadata and an empty result.' }
$noStatusMatch = (Invoke-QAListingSmoke "$($listingPagePath)?keyword=$encodedOwnKeyword&status=Rejected" $vendor.token 200).Json
if ($noStatusMatch.totalCount -ne 0 -or @($noStatusMatch.items).Count -ne 0) { throw 'Vendor listing keyword/status filters were not combined with AND.' }
foreach ($foreignScope in @(
    @{ Token = $vendor.token; Keyword = $foreignKeyword },
    @{ Token = $otherVendor.token; Keyword = $ownKeyword }
)) {
    $encodedForeignKeyword = [Uri]::EscapeDataString($foreignScope.Keyword)
    $hiddenListingPage = (Invoke-QAListingSmoke "$($listingPagePath)?keyword=$encodedForeignKeyword&status=Draft" $foreignScope.Token 200).Json
    if ($hiddenListingPage.totalCount -ne 0 -or @($hiddenListingPage.items).Count -ne 0) {
        throw 'Vendor listing filtering disclosed another shop draft or count.'
    }
}
$otherListingPage = (Invoke-QAListingSmoke $listingPagePath $otherVendor.token 200).Json
if ($otherListingPage.totalCount -ne 3 -or @($otherListingPage.items).Count -ne 3 -or
    @($otherListingPage.items | Where-Object { $_.id -notin $foreignListingIds }).Count -ne 0) {
    throw 'Second vendor listing page did not remain isolated to its three synthetic drafts.'
}
foreach ($invalidQuery in @('page=0', 'page=-1', 'pageSize=0', 'pageSize=51', 'status=NotAStatus', 'status=999',
    'page=2147483647&pageSize=50', ('keyword=' + ('x' * 201)))) {
    Invoke-QAListingSmoke "$($listingPagePath)?$invalidQuery" $vendor.token 400
}
Invoke-QAListingSmoke $listingPagePath '' 401
Invoke-QAListingSmoke $listingPagePath $customer.token 403
Invoke-QAListingSmoke $listingPagePath $moderator.token 403
$legacyListings = (Invoke-QAListingSmoke '/api/listings/my-listings' $vendor.token 200).Json
if ($legacyListings -isnot [System.Array] -or @($legacyListings).Count -ne 2 -or
    @($legacyListings | Where-Object { $_.id -notin $ownListingIds }).Count -ne 0) {
    throw 'Legacy vendor listing array endpoint changed shape or ownership.'
}
$privateListingPath = '/api/listings/' + $ownListingIds[0]
$ownPreview = (Invoke-QAListingSmoke $privateListingPath $vendor.token 200).Json
if ($ownPreview.id -ne $ownListingIds[0] -or $ownPreview.status -cne 'Draft') { throw 'Owning vendor could not read its saved draft.' }
Invoke-QAListingSmoke $privateListingPath '' 404
Invoke-QAListingSmoke $privateListingPath $customer.token 404
Invoke-QAListingSmoke $privateListingPath $otherVendor.token 404
Write-Host 'Real HTTPS vendor listing paging, filters, role isolation, legacy array and private-draft boundaries passed.'

$fixture = @{
    baseUrl = $BaseUrl; leadId = $leadId; customer = $customer; otherCustomer = $otherCustomer
    vendor = $vendor; moderator = $moderator; secondDeviceToken = $secondDevice.token
    password = $password; nextPassword = $nextPassword
}
$fixturePath = Join-Path $resolvedQaRoot 'account-security.synthetic-only.json'
$fixture | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $fixturePath -Encoding utf8NoBOM
& $node (Join-Path $PSScriptRoot 'verify-account-security.mjs') $fixturePath
if ($LASTEXITCODE -ne 0) { throw 'Actual account/SignalR security smoke failed; inspect its redacted step output.' }

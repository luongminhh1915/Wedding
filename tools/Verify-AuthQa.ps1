param([switch]$ApplyFixtures)
$ErrorActionPreference = 'Stop'
$qaBase = 'http://127.0.0.1:5009'
$qaDatabase = 'WeddingPlanUi_0161333'
$qaListener = Get-NetTCPConnection -LocalPort 5009 -State Listen
$qaProcess = Get-CimInstance Win32_Process -Filter "ProcessId = $($qaListener.OwningProcess)"
if ($qaProcess.CommandLine -notlike '*wedding-auth-jti-final-build*' -or $qaProcess.CommandLine -notlike '*Database=WeddingPlanUi_0161333;*') {
    throw 'Refusing every API except the final auth QA build targeting the exact named QA database.'
}

function Invoke-QaAuthApi {
    param([string]$Method, [string]$Path, $Body = $null, [string]$Token = '', [int]$Expected = 200)
    $qaHeaders = @{}
    if ($Token) { $qaHeaders.Authorization = "Bearer $Token" }
    $qaParameters = @{ Method = $Method; Uri = "$qaBase$Path"; Headers = $qaHeaders; SkipHttpErrorCheck = $true }
    if ($null -ne $Body) { $qaParameters.ContentType = 'application/json'; $qaParameters.Body = $Body | ConvertTo-Json -Depth 8 }
    for ($qaAttempt = 0; $qaAttempt -lt 4; $qaAttempt++) {
        $qaResponse = Invoke-WebRequest @qaParameters
        if ([int]$qaResponse.StatusCode -ne 429) { break }
        # Respect the application's auth limiter; never disable it for testing.
        Write-Output 'Auth limiter reached; waiting briefly before retry.' | Out-Host
        Start-Sleep -Seconds 20
    }
    if ([int]$qaResponse.StatusCode -ne $Expected) { throw "QA $Method $Path returned $($qaResponse.StatusCode), expected $Expected. No credentials or response body are logged." }
    if ($qaResponse.Content) { return $qaResponse.Content | ConvertFrom-Json }
}

$qaFixturePassword = 'QaOnly-Auth-2026!'
$qaFixtures = @(
    @{ Email = 'auth-qa-customer-20261017@example.invalid'; Role = 'Customer'; FullName = '[QA AUTH] Khach moi'; Phone = '0900000411' },
    @{ Email = 'auth-qa-vendor-20261017@example.invalid'; Role = 'VendorOwner'; FullName = '[QA AUTH] NCC moi'; Phone = '0900000412' },
    @{ Email = 'auth-qa-moderator-20261017@example.invalid'; Role = 'Moderator'; FullName = '[QA AUTH] Kiem duyet'; Phone = '0900000413' },
    @{ Email = 'auth-qa-finance-20261017@example.invalid'; Role = 'Finance'; FullName = '[QA AUTH] Ke toan'; Phone = '0900000414' },
    @{ Email = 'auth-qa-care-20261017@example.invalid'; Role = 'CustomerCare'; FullName = '[QA AUTH] CSKH'; Phone = '0900000415' }
)
$qaProviders = Invoke-QaAuthApi GET '/api/auth/providers'
Write-Output "Exact QA target confirmed: $qaDatabase. No tokens/passwords are logged."
if (-not $ApplyFixtures) {
    Write-Output 'Read only. Use -ApplyFixtures to append named fictional accounts and verify all actors in the isolated QA database.'
    return
}
# This bootstrap is a pre-existing fictional seed account in this exact QA database only.
# The utility never creates a SuperAdmin, resets a password, or overwrites an existing account.
$qaAdmin = Invoke-QaAuthApi POST '/api/auth/login' @{ email = 'admin@wedding.com'; password = '123456' }
if ($qaAdmin.user.role -ne 'SuperAdmin') { throw 'QA bootstrap is not SuperAdmin; stopping.' }
$qaUsers = Invoke-QaAuthApi GET '/api/users' -Token $qaAdmin.token
foreach ($qaFixture in $qaFixtures) {
    $qaExisting = @($qaUsers | Where-Object email -eq $qaFixture.Email)
    if ($qaExisting.Count) {
        if ($qaExisting.Count -ne 1 -or $qaExisting[0].role -ne $qaFixture.Role) { throw 'Existing QA identity has an unexpected role; it will not be modified.' }
        continue
    }
    $qaBody = @{ fullName = $qaFixture.FullName; email = $qaFixture.Email; phoneNumber = $qaFixture.Phone; password = $qaFixturePassword }
    if ($qaFixture.Role -eq 'Customer') { $null = Invoke-QaAuthApi POST '/api/auth/register-customer' $qaBody }
    elseif ($qaFixture.Role -eq 'VendorOwner') {
        $qaBody.brandName = '[QA AUTH] Doi tac hu cau 20261017'; $qaBody.city = 'TP. Hồ Chí Minh'; $qaBody.commissionRate = 0.08
        $null = Invoke-QaAuthApi POST '/api/auth/register-vendor' $qaBody
    }
    else { $qaBody.role = $qaFixture.Role; $null = Invoke-QaAuthApi POST '/api/users/staff' $qaBody -Token $qaAdmin.token }
    Write-Output "Appended fictional actor: $($qaFixture.Role)."
}
$qaCustomerToken = ''; $qaVendorToken = ''; $qaFinanceToken = ''
foreach ($qaFixture in $qaFixtures) {
    $qaLogin = Invoke-QaAuthApi POST '/api/auth/login' @{ email = $qaFixture.Email; password = $qaFixturePassword }
    $qaMe = Invoke-QaAuthApi GET '/api/auth/me' -Token $qaLogin.token
    if ($qaLogin.user.role -ne $qaFixture.Role -or $qaMe.role -ne $qaFixture.Role -or $qaMe.id -ne $qaLogin.user.id) { throw 'Actor identity mismatch.' }
    if ($qaFixture.Role -eq 'Customer') { $qaCustomerToken = $qaLogin.token }
    if ($qaFixture.Role -eq 'VendorOwner') { $qaVendorToken = $qaLogin.token }
    if ($qaFixture.Role -eq 'Finance') { $qaFinanceToken = $qaLogin.token }
    Write-Output "PASS login + authoritative /me: $($qaFixture.Role)."
}
$qaAdminMe = Invoke-QaAuthApi GET '/api/auth/me' -Token $qaAdmin.token
if ($qaAdminMe.role -ne 'SuperAdmin' -or $qaAdminMe.id -ne $qaAdmin.user.id) { throw 'Staff creation replaced the admin identity.' }
$null = Invoke-QaAuthApi GET '/api/users' -Token $qaCustomerToken -Expected 403
$null = Invoke-QaAuthApi GET '/api/users' -Token $qaVendorToken -Expected 403
$null = Invoke-QaAuthApi POST '/api/users/staff' @{} -Token $qaFinanceToken -Expected 403
Write-Output 'PASS all six actors, administrator session preserved, cross-role API access denied. No emails, OTP, contracts, payments, password changes or account overwrites.'

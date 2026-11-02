#requires -Version 7.0
<#
.SYNOPSIS
Prepare a new private deployment configuration copy with Google/Facebook or Gmail SMTP settings.
.DESCRIPTION
Both paths must be inside this repository's .deployment/private directory. Existing
files are never overwritten. The input, unrelated keys, SQL/JWT/upload settings and
unknown provider keys are preserved. Facebook App Secret is read only through a
hidden SecureString prompt; it is never a command-line parameter or environment input.
This tool does not connect to SQL, start an app, make network requests or upload files.
Relative paths are resolved from the repository root.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$InputPath,
    [Parameter(Mandatory)][string]$OutputPath,
    [string]$GoogleClientId,
    [string]$FacebookAppId,
    [string]$FacebookGraphApiVersion,
    [switch]$ConfigureGmail,
    [string]$GmailAddress,
    [string]$PasswordResetBaseUrl
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$privateRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot '.deployment/private'))
$pathComparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
$privatePrefix = $privateRoot.TrimEnd([char[]]@('/', '\')) + [IO.Path]::DirectorySeparatorChar
$hasGoogle = $PSBoundParameters.ContainsKey('GoogleClientId')
$hasFacebookId = $PSBoundParameters.ContainsKey('FacebookAppId')
$hasFacebookVersion = $PSBoundParameters.ContainsKey('FacebookGraphApiVersion')
$beforeDocument = $null
$afterDocument = $null
$secureSecret = $null
$secretPointer = [IntPtr]::Zero
$secretText = $null
$gmailSecurePassword = $null
$gmailPasswordPointer = [IntPtr]::Zero
$gmailPasswordText = $null
$originalJson = $null
$candidateJson = $null
$candidateBytes = $null
$changes = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::OrdinalIgnoreCase)

function Assert-NoReparsePoints([string]$FullPath) {
    $relative = [IO.Path]::GetRelativePath($repoRoot, $FullPath)
    $cursor = $repoRoot
    $parts = @('') + @($relative -split '[\\/]')
    foreach ($part in $parts) {
        if ($part) {
            if ($IsWindows -and ($part -match '[. ]$' -or $part -match '^(?i:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)')) {
                throw 'Windows device names and path aliases are not allowed.'
            }
            $cursor = Join-Path $cursor $part
        }
        if (Test-Path -LiteralPath $cursor) {
            $item = Get-Item -LiteralPath $cursor -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw 'Configuration paths must not traverse symbolic links or reparse points.'
            }
        }
    }
}

function Resolve-PrivateJsonPath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { throw 'A private JSON configuration path is required.' }
    $full = [IO.Path]::GetFullPath($Path, $repoRoot)
    if (-not $full.StartsWith($privatePrefix, $pathComparison) -or [IO.Path]::GetExtension($full) -ine '.json') {
        throw 'Input and output must be JSON files inside this repository .deployment/private directory.'
    }
    Assert-NoReparsePoints $full
    return $full
}

function Get-JsonProperty($Element, [string]$Name) {
    foreach ($property in $Element.EnumerateObject()) {
        if ([string]::Equals($property.Name, $Name, [StringComparison]::OrdinalIgnoreCase)) { return $property }
    }
    return $null
}

function Assert-UnambiguousJson($Element) {
    if ($Element.ValueKind -eq [System.Text.Json.JsonValueKind]::Object) {
        $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($property in $Element.EnumerateObject()) {
            if (-not $seen.Add($property.Name)) { throw 'Configuration contains duplicate or case-ambiguous JSON keys.' }
            Assert-UnambiguousJson $property.Value
        }
    } elseif ($Element.ValueKind -eq [System.Text.Json.JsonValueKind]::Array) {
        foreach ($value in $Element.EnumerateArray()) { Assert-UnambiguousJson $value }
    }
}

function Read-JsonObject([string]$Json) {
    try { $document = [System.Text.Json.JsonDocument]::Parse($Json) }
    catch { throw 'Configuration is not valid JSON; no output was written.' }
    if ($document.RootElement.ValueKind -ne [System.Text.Json.JsonValueKind]::Object) {
        $document.Dispose()
        throw 'Configuration must have a JSON object root.'
    }
    try { Assert-UnambiguousJson $document.RootElement }
    catch { $document.Dispose(); throw }
    return $document
}

function Assert-ProductionConfiguration($Root) {
    $database = Get-JsonProperty $Root 'Database'
    if ($null -eq $database -or $database.Value.ValueKind -ne [System.Text.Json.JsonValueKind]::Object) {
        throw 'Production Database settings are required.'
    }
    $provider = Get-JsonProperty $database.Value 'Provider'
    $migrate = Get-JsonProperty $database.Value 'ApplyMigrations'
    $seed = Get-JsonProperty $database.Value 'SeedDemoData'
    if ($null -eq $provider -or $provider.Value.ValueKind -ne [System.Text.Json.JsonValueKind]::String -or $provider.Value.GetString() -ine 'SqlServer' -or
        $null -eq $migrate -or $migrate.Value.ValueKind -ne [System.Text.Json.JsonValueKind]::False -or
        $null -eq $seed -or $seed.Value.ValueKind -ne [System.Text.Json.JsonValueKind]::False) {
        throw 'Require SqlServer with ApplyMigrations=false and SeedDemoData=false.'
    }
    foreach ($sectionName in @('ConnectionStrings', 'JwtSettings')) {
        $section = Get-JsonProperty $Root $sectionName
        if ($null -eq $section -or $section.Value.ValueKind -ne [System.Text.Json.JsonValueKind]::Object) {
            throw 'Production SQL/JWT configuration is required.'
        }
        $key = if ($sectionName -eq 'ConnectionStrings') { 'DefaultConnection' } else { 'Secret' }
        $value = Get-JsonProperty $section.Value $key
        if ($null -eq $value -or $value.Value.ValueKind -ne [System.Text.Json.JsonValueKind]::String -or [string]::IsNullOrWhiteSpace($value.Value.GetString())) {
            throw 'Production SQL/JWT configuration is incomplete.'
        }
    }
}

function Quote-JsonString([string]$Value) { return ConvertTo-Json -InputObject $Value -Compress }

function New-ProviderJson($Original, $Updates) {
    $members = [Collections.Generic.List[string]]::new()
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    if ($null -ne $Original) {
        if ($Original.ValueKind -ne [System.Text.Json.JsonValueKind]::Object) { throw 'Provider settings must be JSON objects.' }
        foreach ($property in $Original.EnumerateObject()) {
            [void]$seen.Add($property.Name)
            $jsonValue = if ($Updates.ContainsKey($property.Name)) { Quote-JsonString $Updates[$property.Name] } else { $property.Value.GetRawText() }
            $members.Add((Quote-JsonString $property.Name) + ':' + $jsonValue)
        }
    }
    foreach ($key in $Updates.Keys) {
        if (-not $seen.Contains($key)) { $members.Add((Quote-JsonString $key) + ':' + (Quote-JsonString $Updates[$key])) }
    }
    return '{' + ($members -join ',') + '}'
}

function Assert-AllowedChanges($Before, $After, $Updates) {
    $expectedRootCount = @($Before.EnumerateObject()).Count
    foreach ($providerName in $Updates.Keys) {
        if ($null -eq (Get-JsonProperty $Before $providerName)) { $expectedRootCount++ }
    }
    if (@($After.EnumerateObject()).Count -ne $expectedRootCount) { throw 'Unexpected root configuration change.' }
    foreach ($property in $Before.EnumerateObject()) {
        $retained = Get-JsonProperty $After $property.Name
        if ($null -eq $retained) { throw 'An existing configuration key was removed.' }
        if (-not $Updates.ContainsKey($property.Name) -and $retained.Value.GetRawText() -cne $property.Value.GetRawText()) {
            throw 'An unrelated configuration value changed.'
        }
    }
    foreach ($providerName in $Updates.Keys) {
        $expected = $Updates[$providerName]
        $oldProvider = Get-JsonProperty $Before $providerName
        $newProvider = Get-JsonProperty $After $providerName
        if ($null -eq $newProvider -or $newProvider.Value.ValueKind -ne [System.Text.Json.JsonValueKind]::Object) { throw 'Provider verification failed.' }
        $expectedCount = $expected.Count
        if ($null -ne $oldProvider) {
            $expectedCount = @($oldProvider.Value.EnumerateObject()).Count
            foreach ($key in $expected.Keys) { if ($null -eq (Get-JsonProperty $oldProvider.Value $key)) { $expectedCount++ } }
            foreach ($property in $oldProvider.Value.EnumerateObject()) {
                $retained = Get-JsonProperty $newProvider.Value $property.Name
                if ($null -eq $retained) { throw 'An existing provider key was removed.' }
                if (-not $expected.ContainsKey($property.Name) -and $retained.Value.GetRawText() -cne $property.Value.GetRawText()) {
                    throw 'An unrelated provider value changed.'
                }
            }
        }
        if (@($newProvider.Value.EnumerateObject()).Count -ne $expectedCount) { throw 'Unexpected provider keys were introduced.' }
        foreach ($key in $expected.Keys) {
            $actual = Get-JsonProperty $newProvider.Value $key
            if ($null -eq $actual -or $actual.Value.ValueKind -ne [System.Text.Json.JsonValueKind]::String -or $actual.Value.GetString() -cne $expected[$key]) {
                throw 'A requested provider setting did not roundtrip correctly.'
            }
        }
    }
}

try {
    if (-not $hasGoogle -and -not $hasFacebookId -and -not $hasFacebookVersion -and -not $ConfigureGmail) { throw 'Specify social settings or ConfigureGmail to update.' }
    if (-not $ConfigureGmail -and ($PSBoundParameters.ContainsKey('GmailAddress') -or $PSBoundParameters.ContainsKey('PasswordResetBaseUrl'))) { throw 'Gmail fields require ConfigureGmail.' }
    if ($hasFacebookId -ne $hasFacebookVersion) { throw 'Facebook App ID and Graph API version must be supplied together.' }
    if ($hasGoogle) {
        if ([string]::IsNullOrWhiteSpace($GoogleClientId)) { throw 'Google Client ID is required when specified.' }
        $GoogleClientId = $GoogleClientId.Trim()
        if ($GoogleClientId -notmatch '^[A-Za-z0-9_-]{6,}\.apps\.googleusercontent\.com$') { throw 'Google Client ID must be a Web OAuth client ID ending in .apps.googleusercontent.com.' }
        $googleUpdates = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
        $googleUpdates.Add('ClientId', $GoogleClientId)
        $changes.Add('Google', $googleUpdates)
    }
    if ($hasFacebookId) {
        if ([string]::IsNullOrWhiteSpace($FacebookAppId) -or [string]::IsNullOrWhiteSpace($FacebookGraphApiVersion)) { throw 'Facebook App ID and Graph API version are required when specified.' }
        $FacebookAppId = $FacebookAppId.Trim()
        $FacebookGraphApiVersion = $FacebookGraphApiVersion.Trim()
        if ($FacebookAppId -notmatch '^\d{5,32}$' -or $FacebookGraphApiVersion -cnotmatch '^v[0-9]{1,3}\.0$') { throw 'Facebook App ID or Graph API version format is invalid; the API requires lowercase vN.0.' }
        $facebookUpdates = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
        $facebookUpdates.Add('AppId', $FacebookAppId)
        $facebookUpdates.Add('GraphApiVersion', $FacebookGraphApiVersion)
        $changes.Add('Facebook', $facebookUpdates)
    }
    if ($ConfigureGmail) {
        $origin = $null
        if (-not [Uri]::TryCreate($PasswordResetBaseUrl, [UriKind]::Absolute, [ref]$origin) -or
            $origin.Scheme -ne 'https' -or $origin.IsLoopback -or $origin.UserInfo -or $origin.AbsolutePath -ne '/' -or $origin.Query -or $origin.Fragment -or
            [Uri]::CheckHostName($origin.Host) -ne [UriHostNameType]::Dns -or -not $origin.IsDefaultPort) {
            throw 'Password reset base URL must be a public HTTPS origin without path, credentials, custom port, query or fragment.'
        }
        if ($PSBoundParameters.ContainsKey('GmailAddress') -and [string]::IsNullOrWhiteSpace($GmailAddress)) { throw 'Gmail address cannot be blank when specified.' }
        $authUpdates = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
        $authUpdates.Add('PasswordResetBaseUrl', $origin.GetLeftPart([UriPartial]::Authority))
        $changes.Add('Auth', $authUpdates)
        $gmailUpdates = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
        $gmailUpdates.Add('Host', 'smtp.gmail.com')
        $gmailUpdates.Add('Port', '587')
        $changes.Add('Smtp', $gmailUpdates)
    }
    $source = Resolve-PrivateJsonPath $InputPath
    $destination = Resolve-PrivateJsonPath $OutputPath
    if ([string]::Equals($source, $destination, $pathComparison)) { throw 'Input and output must be different files.' }
    if (-not [IO.File]::Exists($source)) { throw 'Input configuration file does not exist.' }
    if (Test-Path -LiteralPath $destination) { throw 'Output already exists; refusing to overwrite it.' }
    $originalJson = [IO.File]::ReadAllText($source)
    $beforeDocument = Read-JsonObject $originalJson
    Assert-ProductionConfiguration $beforeDocument.RootElement
    if ($ConfigureGmail) {
        $allowedHostSetting = Get-JsonProperty $beforeDocument.RootElement 'AllowedHosts'
        if ($null -eq $allowedHostSetting -or $allowedHostSetting.Value.ValueKind -ne [System.Text.Json.JsonValueKind]::String -or
            $origin.Host -notin @($allowedHostSetting.Value.GetString().Split(';', [StringSplitOptions]::TrimEntries))) {
            throw 'Reset origin must match an explicit hostname in the existing private AllowedHosts; this tool never changes host bindings.'
        }
    }
    foreach ($providerName in $changes.Keys) {
        $existing = Get-JsonProperty $beforeDocument.RootElement $providerName
        if ($null -ne $existing -and $existing.Value.ValueKind -ne [System.Text.Json.JsonValueKind]::Object) { throw 'Existing provider settings must be JSON objects.' }
    }
    if ($hasFacebookId) {
        $secureSecret = Read-Host 'Facebook App Secret (hidden; server configuration only)' -AsSecureString
        if ($null -eq $secureSecret -or $secureSecret.Length -eq 0) { throw 'Facebook App Secret is required.' }
        $secretPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureSecret)
        $secretText = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($secretPointer)
        if ([string]::IsNullOrWhiteSpace($secretText)) { throw 'Facebook App Secret is required.' }
        $facebookUpdates.Add('AppSecret', $secretText)
    }
    if ($ConfigureGmail) {
        if (-not $PSBoundParameters.ContainsKey('GmailAddress')) { $GmailAddress = Read-Host 'Wedding Match Gmail address (not the account password)' }
        $GmailAddress = $GmailAddress.Trim().ToLowerInvariant()
        if ($GmailAddress -cnotmatch '^[a-z0-9][a-z0-9._+\-]*@(gmail\.com|googlemail\.com)$') { throw 'Enter the actual Gmail account address without a display name.' }
        $gmailSecurePassword = Read-Host 'Gmail App Password (hidden, 16 characters; NOT your Google account password)' -AsSecureString
        if ($null -eq $gmailSecurePassword -or $gmailSecurePassword.Length -eq 0) { throw 'Gmail App Password is required.' }
        $gmailPasswordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($gmailSecurePassword)
        $gmailPasswordText = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($gmailPasswordPointer) -replace '\s', ''
        if ($gmailPasswordText -cnotmatch '^[A-Za-z0-9]{16}$') { throw 'Use the 16-character Google App Password, not the Google account password. Spaces between groups are accepted.' }
        $gmailUpdates.Add('Username', $GmailAddress)
        $gmailUpdates.Add('From', $GmailAddress)
        $gmailUpdates.Add('Password', $gmailPasswordText)
    }
    $rootMembers = [Collections.Generic.List[string]]::new()
    $seenProviders = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($property in $beforeDocument.RootElement.EnumerateObject()) {
        if ($changes.ContainsKey($property.Name)) {
            $jsonValue = New-ProviderJson $property.Value $changes[$property.Name]
            [void]$seenProviders.Add($property.Name)
        } else { $jsonValue = $property.Value.GetRawText() }
        $rootMembers.Add('  ' + (Quote-JsonString $property.Name) + ':' + $jsonValue)
    }
    foreach ($providerName in $changes.Keys) {
        if (-not $seenProviders.Contains($providerName)) {
            $rootMembers.Add('  ' + (Quote-JsonString $providerName) + ':' + (New-ProviderJson $null $changes[$providerName]))
        }
    }
    $candidateJson = "{`n" + ($rootMembers -join ",`n") + "`n}"
    $afterDocument = Read-JsonObject $candidateJson
    Assert-ProductionConfiguration $afterDocument.RootElement
    Assert-AllowedChanges $beforeDocument.RootElement $afterDocument.RootElement $changes
    if ([IO.File]::ReadAllText($source) -cne $originalJson) { throw 'Input changed while preparing settings; no output was written.' }
    Assert-NoReparsePoints $source
    Assert-NoReparsePoints $destination
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))
    Assert-NoReparsePoints $destination
    $candidateBytes = [Text.Encoding]::UTF8.GetBytes($candidateJson)
    $stream = [IO.File]::Open($destination, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $stream.Write($candidateBytes, 0, $candidateBytes.Length); $stream.Flush($true) }
    finally { $stream.Dispose() }
    Write-Host "Private provider configuration prepared: $destination"
    Write-Host 'Original preserved. No SQL, network, upload or app startup performed.'
} finally {
    if ($gmailPasswordPointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($gmailPasswordPointer) }
    if ($null -ne $gmailSecurePassword) { $gmailSecurePassword.Dispose() }
    $gmailPasswordText = $null
    if ($secretPointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($secretPointer) }
    if ($null -ne $secureSecret) { $secureSecret.Dispose() }
    if ($null -ne $beforeDocument) { $beforeDocument.Dispose() }
    if ($null -ne $afterDocument) { $afterDocument.Dispose() }
    foreach ($updates in $changes.Values) { $updates.Clear() }
    $changes.Clear()
    Remove-Variable originalJson,candidateJson,candidateBytes,secretText,secureSecret,secretPointer,jsonValue,rootMembers,googleUpdates,facebookUpdates -ErrorAction SilentlyContinue
}

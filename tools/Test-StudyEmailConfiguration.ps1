#requires -Version 7.0
<# Synthetic private-copy tests only. No real credentials, SMTP/SQL/network or app startup. #>
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runKey=[Guid]::NewGuid().ToString('N')
$fixtureRoot=Join-Path $repoRoot ('.deployment/private/EmailConfigQa_'+$runKey)
$resultRoot=Join-Path $repoRoot 'artifacts/gmail-recovery-20261028'
[void][IO.Directory]::CreateDirectory($fixtureRoot)
[void][IO.Directory]::CreateDirectory($resultRoot)
$tool=Join-Path $PSScriptRoot 'Set-StudyEmailConfiguration.ps1'
$source=Join-Path $fixtureRoot 'input.json'
$original=@'
{"AllowedHosts":"weddingmatch.website;www.weddingmatch.website","Database":{"Provider":"SqlServer","ApplyMigrations":false,"SeedDemoData":false},"ConnectionStrings":{"DefaultConnection":"Server=synthetic.invalid;Database=NoNetwork;Password=synthetic-only"},"JwtSettings":{"Secret":"synthetic-only-not-a-real-signing-key"},"Google":{"ClientId":"1234567890-keep.apps.googleusercontent.com"},"Facebook":{"AppId":"1234567890000","AppSecret":"synthetic-old","GraphApiVersion":"v26.0"},"Uploads":{"Root":"../private/uploads/keep"},"Auth":{"PasswordResetBaseUrl":"https://weddingmatch.website","Retain":true},"Smtp":{"Port":25,"UnknownKeep":{"Amount":1.2345678901234567890123456789}},"UnknownRoot":{"Amount":123456789012345678901234567890}}
'@
[IO.File]::WriteAllText($source,$original)
$originalHash=(Get-FileHash -LiteralPath $source).Hash
$checks=[Collections.Generic.List[string]]::new()
$global:WeddingMailQaSecret='abcd efgh ijkl mnop'
$global:WeddingMailQaPrompts=0
$global:WeddingMailQaAddressPrompts=0
function global:Read-Host { param([string]$Prompt,[switch]$AsSecureString)
    if ($AsSecureString) {
        $global:WeddingMailQaPrompts++
        if (-not $global:WeddingMailQaSecret) { return [Security.SecureString]::new() }
        return ConvertTo-SecureString $global:WeddingMailQaSecret -AsPlainText -Force
    }
    $global:WeddingMailQaAddressPrompts++
    return 'qa-mailer@gmail.com'
}
function Assert([bool]$Condition,[string]$Name) { if (-not $Condition) { throw "Email configuration assertion failed: $Name" } }
function Invoke-Config([hashtable]$Settings) {
    $messages=[Collections.Generic.List[object]]::new();$failure=$null
    try { & $tool @Settings *>&1 | ForEach-Object { $messages.Add($_) } } catch { $failure=$_;$messages.Add($_) }
    $output=$messages|Out-String
    foreach ($secret in @($global:WeddingMailQaSecret,($global:WeddingMailQaSecret -replace '\s',''))) {
        if ($secret) { Assert (-not $output.Contains($secret)) 'secret absent from output/error' }
    }
    if ($failure) { throw $failure }
}
function Reject([hashtable]$Settings,[string]$Name,[bool]$BeforeSecret=$true) {
    $before=$global:WeddingMailQaPrompts;$rejected=$false
    try { Invoke-Config $Settings } catch { $rejected=$true }
    Assert $rejected $Name
    if ($BeforeSecret) { Assert ($global:WeddingMailQaPrompts -eq $before) ($Name+' before secret prompt') }
    if ($Settings.OutputPath -and $Settings.OutputPath -ne $Settings.InputPath -and -not $Name.StartsWith('existing-output')) { Assert (-not [IO.File]::Exists($Settings.OutputPath)) ($Name+' leaves no output') }
    $checks.Add($Name)
}
try {
    $destination=Join-Path $fixtureRoot 'gmail.json'
    Invoke-Config @{InputPath=$source;OutputPath=$destination;GmailAddress='QA-Mailer@gmail.com'}
    $prepared=Get-Content -LiteralPath $destination -Raw|ConvertFrom-Json
    Assert ($prepared.Smtp.Host -ceq 'smtp.gmail.com' -and [int]$prepared.Smtp.Port -eq 587) 'STARTTLS587 target'
    Assert ($prepared.Smtp.Username -ceq 'qa-mailer@gmail.com' -and $prepared.Smtp.From -ceq $prepared.Smtp.Username) 'account equals sender'
    Assert ($prepared.Smtp.Password -ceq 'abcdefghijklmnop') 'grouped hidden app password normalized'
    Assert ($prepared.Auth.PasswordResetBaseUrl -ceq 'https://weddingmatch.website' -and $prepared.Auth.Retain) 'reset origin and unrelated Auth retained'
    Assert ($global:WeddingMailQaPrompts -eq 1) 'exactly one hidden password prompt'
    $checks.Add('gmail-private-copy-hidden-prompt-and-settings')
    $before=[Text.Json.JsonDocument]::Parse($original)
    $after=[Text.Json.JsonDocument]::Parse([IO.File]::ReadAllText($destination))
    try {
        foreach ($name in @('Database','ConnectionStrings','JwtSettings','Google','Facebook','Uploads','UnknownRoot')) {
            Assert ($before.RootElement.GetProperty($name).GetRawText() -ceq $after.RootElement.GetProperty($name).GetRawText()) ('unchanged '+$name)
        }
        Assert ($before.RootElement.GetProperty('Smtp').GetProperty('UnknownKeep').GetRawText() -ceq $after.RootElement.GetProperty('Smtp').GetProperty('UnknownKeep').GetRawText()) 'unknown SMTP precise JSON unchanged'
    } finally { $before.Dispose();$after.Dispose() }
    $checks.Add('SQL-JWT-social-uploads-unknown-precision-preserved')
    $copyHash=(Get-FileHash -LiteralPath $destination).Hash
    Reject @{InputPath=$source;OutputPath=$destination;GmailAddress='qa-mailer@gmail.com'} 'existing-output-never-overwritten'
    Assert ((Get-FileHash -LiteralPath $destination).Hash -eq $copyHash) 'output remains unchanged'
    foreach ($origin in @('http://weddingmatch.website','https://weddingmatch.website/path','https://weddingmatch.website/?x=1','https://weddingmatch.website/#token','https://user@weddingmatch.website','https://weddingmatch.website:8443','https://localhost','https://127.0.0.1','https://unrelated.example.invalid')) {
        Reject @{InputPath=$source;OutputPath=(Join-Path $fixtureRoot ([Guid]::NewGuid().ToString('N')+'.json'));PublicOrigin=$origin;GmailAddress='qa-mailer@gmail.com'} ('unsafe-or-foreign-origin-'+$checks.Count)
    }
    foreach ($address in @('','person@example.invalid','Display Name <qa-mailer@gmail.com>','qa-mailer@gmail.com;other@gmail.com')) {
        Reject @{InputPath=$source;OutputPath=(Join-Path $fixtureRoot ([Guid]::NewGuid().ToString('N')+'.json'));GmailAddress=$address} ('invalid-sender-'+$checks.Count)
    }
    $outside=Join-Path $resultRoot ('must-not-create-'+$runKey+'.json')
    Reject @{InputPath=$source;OutputPath=$outside;GmailAddress='qa-mailer@gmail.com'} 'nonprivate-output-rejected'
    Reject @{InputPath=$source;OutputPath=$source;GmailAddress='qa-mailer@gmail.com'} 'same-file-rejected'
    $unsafe=Join-Path $fixtureRoot 'unsafe-startup.json'
    [IO.File]::WriteAllText($unsafe,$original.Replace('"ApplyMigrations":false','"ApplyMigrations":true'))
    Reject @{InputPath=$unsafe;OutputPath=(Join-Path $fixtureRoot 'unsafe-result.json');GmailAddress='qa-mailer@gmail.com'} 'startup-migrations-rejected'
    $ambiguous=Join-Path $fixtureRoot 'ambiguous.json'
    $originalTrimmed=$original.TrimEnd()
    [IO.File]::WriteAllText($ambiguous,$originalTrimmed.Substring(0,$originalTrimmed.Length-1)+',"smtp":{}}')
    Reject @{InputPath=$ambiguous;OutputPath=(Join-Path $fixtureRoot 'ambiguous-result.json');GmailAddress='qa-mailer@gmail.com'} 'case-ambiguous-keys-rejected'
    foreach ($badSecret in @('','short','abcdefghijklmnopq')) {
        $global:WeddingMailQaSecret=$badSecret
        Reject @{InputPath=$source;OutputPath=(Join-Path $fixtureRoot ([Guid]::NewGuid().ToString('N')+'.json'));GmailAddress='qa-mailer@gmail.com'} ('invalid-app-password-'+$checks.Count) $false
    }
    $global:WeddingMailQaSecret='abcdefghijklmnop'
    $newSource=Join-Path $fixtureRoot 'without-email.json'
    $missing=Get-Content -LiteralPath $source -Raw|ConvertFrom-Json -AsHashtable
    $null=$missing.Remove('Auth');$null=$missing.Remove('Smtp')
    $missing|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $newSource -Encoding utf8NoBOM
    $newOutput=Join-Path $fixtureRoot 'prompted.json'
    Invoke-Config @{InputPath=$newSource;OutputPath=$newOutput}
    $prompted=Get-Content -LiteralPath $newOutput -Raw|ConvertFrom-Json
    Assert ($global:WeddingMailQaAddressPrompts -eq 1 -and $prompted.Smtp.Username -eq 'qa-mailer@gmail.com' -and $prompted.Auth.PasswordResetBaseUrl -eq 'https://weddingmatch.website') 'address prompt and missing sections'
    $checks.Add('interactive-address-and-new-Auth-Smtp-sections')
    Assert ((Get-FileHash -LiteralPath $source).Hash -eq $originalHash) 'original config unchanged after all tests'
    [ordered]@{Status='PASS';Passed=$checks.Count;Failed=0;Checks=@($checks);RealCredentialsUsed=$false;SqlOrNetwork=$false;FixtureRoot=$fixtureRoot}|ConvertTo-Json -Depth 5|Set-Content (Join-Path $resultRoot ('config-test-'+$runKey+'.json')) -Encoding utf8NoBOM
    Write-Output "PASS: $($checks.Count) Gmail private-copy checks; no real credentials, SQL, email or network. Synthetic fixtures retained privately."
} finally {
    Remove-Item Function:\global:Read-Host -ErrorAction SilentlyContinue
    Remove-Variable WeddingMailQaSecret,WeddingMailQaPrompts,WeddingMailQaAddressPrompts -Scope Global -ErrorAction SilentlyContinue
}

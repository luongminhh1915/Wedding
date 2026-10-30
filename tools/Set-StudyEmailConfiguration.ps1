#requires -Version 7.0
<#
.SYNOPSIS
Prepare Gmail SMTP and password-recovery settings in a new private config copy.
.DESCRIPTION
Reuses the existing guarded provider configuration writer. Reads a Google App
Password only through a hidden prompt; never passes it on the command line.
Does not send mail, connect SQL, upload files, deploy or start the application.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$InputPath,
    [Parameter(Mandatory)][string]$OutputPath,
    [string]$PublicOrigin = 'https://weddingmatch.website',
    [string]$GmailAddress
)
$ErrorActionPreference = 'Stop'
$options = @{ InputPath=$InputPath; OutputPath=$OutputPath; ConfigureGmail=$true; PasswordResetBaseUrl=$PublicOrigin }
if ($PSBoundParameters.ContainsKey('GmailAddress')) { $options.GmailAddress=$GmailAddress }
& (Join-Path $PSScriptRoot 'Set-StudySocialConfiguration.ps1') @options

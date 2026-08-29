[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$FilePath,

    [string]$ExpectedSignerThumbprint = '',

    [switch]$RequireTimestamp
)

# ASCII-only: Windows PowerShell 5.1 reads a BOM-less script as ANSI.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$securityModule = Join-Path $PSHOME 'Modules\Microsoft.PowerShell.Security\Microsoft.PowerShell.Security.psd1'
Import-Module -Name $securityModule -ErrorAction Stop

$resolvedPath = (Resolve-Path -LiteralPath $FilePath).Path
$signature = Get-AuthenticodeSignature -FilePath $resolvedPath
if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid -or
    $null -eq $signature.SignerCertificate) {
    throw "'$resolvedPath' does not have a valid Authenticode signature (status: $($signature.Status); $($signature.StatusMessage))."
}

$actualThumbprint = $signature.SignerCertificate.Thumbprint.Replace(' ', '').ToUpperInvariant()
$expectedThumbprint = $ExpectedSignerThumbprint.Replace(' ', '').ToUpperInvariant()
if ($expectedThumbprint -ne '' -and $actualThumbprint -ne $expectedThumbprint) {
    throw "'$resolvedPath' is signed by certificate $actualThumbprint, expected $expectedThumbprint."
}

if ($RequireTimestamp -and $null -eq $signature.TimeStamperCertificate) {
    throw "'$resolvedPath' has no trusted Authenticode timestamp."
}

$timestamp = if ($null -ne $signature.TimeStamperCertificate) {
    $signature.TimeStamperCertificate.Subject
}
else {
    'none'
}
Write-Host "Valid Authenticode signature: $resolvedPath"
Write-Host "  signer: $($signature.SignerCertificate.Subject)"
Write-Host "  thumbprint: $actualThumbprint"
Write-Host "  timestamp signer: $timestamp"

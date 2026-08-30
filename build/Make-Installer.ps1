# ---------------------------------------------------------------------------
# Make-Installer.ps1 - build the Windows installer (Inno Setup).
#
# Steps:
#   1. Version from the exact git tag or build/NEXT_VERSION for development
#      builds (build/Get-GitVersion.ps1 - the same mechanism that stamps the
#      assembly version), split into the numeric part and suffix.
#   2. dotnet publish (SELF-CONTAINED win-x64 by default: the installer ships
#      the .NET 10 Desktop Runtime, so end users need nothing preinstalled)
#      into artifacts\publish\GCodeGenerator.
#   3. Apply the explicit signing policy (see below).
#   4. Compile install\GCodeGenerator.iss with ISCC, passing the version via
#      /D defines; output into artifacts\installer.
#
# SigningMode is deliberately explicit. Unsigned is the current release policy
# until a publicly trusted code-signing certificate is available. Required is
# the fail-closed future policy: it needs a signing command and a pinned signer
# thumbprint. Give -SignCommand (or set the GCODEGEN_SIGN_COMMAND environment
# variable) to a command line that signs one file, with $f standing for the
# file - the same placeholder Inno Setup uses, so one setting covers both the
# app and the installer. Examples:
#
#   -SignCommand 'signtool.exe sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /f C:\keys\cert.pfx /p SECRET $f'
#   -SignCommand 'azuresigntool.exe sign -kvu ... -kvc ... -tr http://timestamp.digicert.com -td SHA256 $f'
#
# Unsigned mode rejects signing inputs instead of silently changing the release
# policy when a stale environment variable is present. Required mode rejects a
# build unless both the command and expected thumbprint are available.
#
# Requires: .NET 10 SDK, git, 64-bit Inno Setup 7 (ISCC.exe) - or -IsccPath.
# ASCII-only on purpose: Windows PowerShell 5.1 reads BOM-less .ps1 as ANSI.
# Compatible with Windows PowerShell 5.1 (no PS7 syntax).
#
# Usage:
#   build\Make-Installer.ps1 [-Configuration Release] [-Runtime win-x64]
#                            [-FrameworkDependent] [-IsccPath <path to ISCC.exe>]
#                            [-SigningMode Unsigned|Required]
#                            [-SignCommand '<command with $f>']
#                            [-ExpectedSignerThumbprint <SHA-1 thumbprint>]
# ---------------------------------------------------------------------------
param(
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [switch]$FrameworkDependent,
    [string]$IsccPath = '',
    [ValidateSet('Unsigned', 'Required')]
    [string]$SigningMode = 'Unsigned',
    [string]$SignCommand = '',
    [string]$ExpectedSignerThumbprint = ''
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$scriptDir = $PSScriptRoot

# --- 1) Version from the git tag -------------------------------------------
$versionFile = Join-Path $env:TEMP ("gcodegen_installer_version_" + [guid]::NewGuid().ToString('N') + '.txt')
try {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass `
        -File (Join-Path $scriptDir 'Get-GitVersion.ps1') `
        -OutFile $versionFile `
        -NextVersionFile (Join-Path $scriptDir 'NEXT_VERSION') | Out-Null
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $versionFile)) {
        throw 'Get-GitVersion.ps1 failed (git unavailable or not a repository?)'
    }
    $version = (Get-Content $versionFile -Raw).Trim()
}
finally {
    if (Test-Path $versionFile) { Remove-Item $versionFile -Force }
}

if ($version -notmatch '^\d+\.\d+\.\d+(-[A-Za-z][A-Za-z0-9]*)?$') {
    throw "Unexpected version format: '$version' (expected X.Y.Z[-suffix])"
}
$numeric = ($version -split '-')[0]
$suffix = ''
$dash = $version.IndexOf('-')
if ($dash -ge 0) { $suffix = $version.Substring($dash) }
Write-Host "Version: $version (numeric: $numeric, suffix: '$suffix')"

# --- 1a) Product metadata from Directory.Build.props -------------------------
# Publisher, product and copyright live in one place for the whole solution:
# the assemblies get them from these properties, and so does the installer.
# Keeping a second copy in the .iss was how they drifted apart before.
$propsPath = Join-Path $repoRoot 'Directory.Build.props'
[xml]$props = Get-Content $propsPath
function Get-BuildProperty([string]$Name) {
    $node = $props.SelectSingleNode("/Project/PropertyGroup/$Name")
    if (-not $node -or -not $node.InnerText.Trim()) {
        throw "Directory.Build.props has no <$Name> - the installer takes product metadata from there."
    }
    return $node.InnerText.Trim()
}
$publisher = Get-BuildProperty 'Company'
$productName = Get-BuildProperty 'Product'
$copyright = Get-BuildProperty 'Copyright'
Write-Host "Publisher: $publisher; product: $productName"

# --- 1b) Inno Setup 7 compiler ---------------------------------------------
# Prefer the known 64-bit Inno Setup 7 location over PATH. Hosted Windows
# images may expose an older Chocolatey ISCC shim even after 7.x is installed,
# and SetupArchitecture is intentionally unavailable in Inno Setup 6.
$defaultIsccPath = 'C:\Program Files\Inno Setup 7\ISCC.exe'
if ($IsccPath -eq '') {
    if (Test-Path -LiteralPath $defaultIsccPath) {
        $IsccPath = $defaultIsccPath
    }
    else {
        $cmd = Get-Command iscc.exe -ErrorAction SilentlyContinue
        if ($cmd) { $IsccPath = $cmd.Source }
    }
}
if ($IsccPath -eq '' -or -not (Test-Path -LiteralPath $IsccPath)) {
    throw "ISCC not found ('$IsccPath'). Install 64-bit Inno Setup 7 (https://jrsoftware.org/isdl.php) or pass -IsccPath."
}

$isccBanner = ((& $IsccPath '/?' 2>&1) | Out-String)
$isccVersion = [regex]::Match(
    $isccBanner,
    'Inno Setup (?<major>\d+) Command-Line Compiler',
    [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
if (-not $isccVersion.Success -or [int]$isccVersion.Groups['major'].Value -lt 7) {
    throw "Inno Setup 7.x or newer is required; selected compiler: '$IsccPath'."
}
Write-Host "Inno Setup compiler: $IsccPath (major $($isccVersion.Groups['major'].Value))"

# --- 2) Publish --------------------------------------------------------------
$publishDir = Join-Path $repoRoot 'artifacts\publish\GCodeGenerator'
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
# Self-contained by default (the installer must work on a clean Windows).
$selfContainedArg = 'true'
if ($FrameworkDependent) { $selfContainedArg = 'false' }
Write-Host "Publishing ($Configuration, $Runtime, self-contained: $selfContainedArg)..."
# RestoreLockedMode: the package set is taken strictly from packages.lock.json
# - the same strictness the build workflows restore with, applied to the very
# output that ships to users. publish has no --locked-mode command-line switch,
# only the MSBuild property. The locks cover publishing under win-x64 because
# the runtime is declared in the csproj files (RuntimeIdentifiers); without it
# a RID restore would be rejected as a mismatch with the lock (NU1004).
& dotnet publish (Join-Path $repoRoot 'GCodeGenerator\GCodeGenerator.csproj') `
    -c $Configuration -r $Runtime --self-contained $selfContainedArg `
    -p:RestoreLockedMode=true -p:DebugSymbols=false -p:DebugType=None `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
if (-not (Test-Path (Join-Path $publishDir 'GCodeGenerator.exe'))) {
    throw "Publish output is missing GCodeGenerator.exe: $publishDir"
}
$publishedSymbols = @(Get-ChildItem -LiteralPath $publishDir -Filter '*.pdb' -File -Recurse)
if ($publishedSymbols.Count -ne 0) {
    throw "Publish output contains debug symbols: $($publishedSymbols.Name -join ', ')"
}

# The installer and portable archive ship the same publish directory. Copy
# product, dependency and self-contained .NET runtime notices before either
# artifact is produced, so the legal terms cannot exist only in the repo.
& (Join-Path $scriptDir 'Copy-ReleaseNotices.ps1') -PublishDirectory $publishDir -RepositoryRoot $repoRoot
if ($LASTEXITCODE -ne 0) { throw 'Copy-ReleaseNotices.ps1 failed' }

# --- 3) Apply the signing policy ---------------------------------------------
$signingEnabled = $SigningMode -eq 'Required'
if ($signingEnabled) {
    # The parameter wins over the environment variable, so a local invocation
    # can still override credentials supplied by a protected build environment.
    if ($SignCommand -eq '') { $SignCommand = $env:GCODEGEN_SIGN_COMMAND }
    if ($ExpectedSignerThumbprint -eq '') {
        $ExpectedSignerThumbprint = $env:GCODEGEN_EXPECTED_SIGNER_THUMBPRINT
    }
    if ($SignCommand -eq '') {
        throw 'SigningMode Required needs -SignCommand or GCODEGEN_SIGN_COMMAND.'
    }
    if ($ExpectedSignerThumbprint -eq '') {
        throw 'SigningMode Required needs -ExpectedSignerThumbprint or GCODEGEN_EXPECTED_SIGNER_THUMBPRINT.'
    }
    Write-Host 'Signing policy: REQUIRED (command and signer thumbprint are pinned).'
}
else {
    $hasSigningInput = $SignCommand -ne '' -or
        $ExpectedSignerThumbprint -ne '' -or
        -not [string]::IsNullOrWhiteSpace($env:GCODEGEN_SIGN_COMMAND) -or
        -not [string]::IsNullOrWhiteSpace($env:GCODEGEN_EXPECTED_SIGNER_THUMBPRINT)
    if ($hasSigningInput) {
        throw 'SigningMode Unsigned cannot be combined with signing parameters or GCODEGEN signing environment variables.'
    }
    Write-Warning 'Signing policy: UNSIGNED. Windows will show an unknown publisher and may block the application according to local reputation policy.'
}

# Runs the signing command for one file: $f is replaced by its quoted path,
# the same placeholder Inno Setup expands for its own SignTool.
function Invoke-SignCommand([string]$Command, [string]$FilePath) {
    $resolved = $Command.Replace('$f', '"' + $FilePath + '"')
    Write-Host "  signing: $FilePath"
    & cmd.exe /c $resolved
    if ($LASTEXITCODE -ne 0) {
        throw "Signing failed for '$FilePath' (exit code $LASTEXITCODE)"
    }

    $verification = @{
        FilePath = $FilePath
        RequireTimestamp = $true
    }
    if ($ExpectedSignerThumbprint -ne '') {
        $verification.ExpectedSignerThumbprint = $ExpectedSignerThumbprint
    }
    & (Join-Path $scriptDir 'Assert-AuthenticodeSignature.ps1') @verification
}

if ($signingEnabled) {
    # Signed here: the app the user launches, not only the installer that
    # delivers it. SmartScreen looks at both, and an unsigned app inside a
    # signed installer warns on every start instead of once.
    # Only the product's own binaries are signed - the .NET runtime files of a
    # self-contained publish arrive already signed by Microsoft.
    Write-Host 'Signing published binaries...'
    $ownBinaries = @('GCodeGenerator.exe', 'GCodeGenerator.dll', 'GCodeGenerator.Core.dll')
    foreach ($name in $ownBinaries) {
        $path = Join-Path $publishDir $name
        if (Test-Path $path) { Invoke-SignCommand $SignCommand $path }
    }
}
else {
    Write-Host 'Published product binaries will remain UNSIGNED.' -ForegroundColor Yellow
}

# --- 4) Compile the installer ------------------------------------------------
$installerDir = Join-Path $repoRoot 'artifacts\installer'
if (Test-Path $installerDir) { Remove-Item $installerDir -Recurse -Force }
New-Item -ItemType Directory -Path $installerDir | Out-Null

$iss = Join-Path $repoRoot 'install\GCodeGenerator.iss'
Write-Host "Compiling installer with ISCC: $IsccPath"
# ISCC syntax: /O<path> (NO separator; relative paths resolve against ISCC's
# CWD, not the .iss dir - so pass an absolute path, quoted when it has spaces).
$outArg = "/O$installerDir"
if ($installerDir -match ' ') { $outArg = '/O"' + $installerDir + '"' }
$isccArgs = @(
    "/DAppVersionNumeric=$numeric",
    "/DAppVersionSuffix=$suffix",
    "/DAppPublisher=$publisher",
    "/DAppProductName=$productName",
    "/DAppCopyright=$copyright",
    $outArg,
    $iss)
if ($signingEnabled) {
    # /S<name>=<command> defines the signing tool, /D<name> switches on the
    # SignTool directives in the .iss (they are inside #ifdef, so an unsigned
    # build compiles the same script unchanged). The name is arbitrary and
    # only ties the two together.
    $isccArgs = @('/DSignToolName=gcodegen', "/Sgcodegen=$SignCommand") + $isccArgs
}
& $IsccPath $isccArgs
if ($LASTEXITCODE -ne 0) { throw 'ISCC failed' }

$setup = Get-ChildItem -Path $installerDir -Filter 'GCodeGenerator-Setup-*.exe' | Select-Object -First 1
if (-not $setup) { throw "Installer not found in $installerDir" }
if ($signingEnabled) {
    $verification = @{
        FilePath = $setup.FullName
        RequireTimestamp = $true
    }
    if ($ExpectedSignerThumbprint -ne '') {
        $verification.ExpectedSignerThumbprint = $ExpectedSignerThumbprint
    }
    & (Join-Path $scriptDir 'Assert-AuthenticodeSignature.ps1') @verification
}
Write-Host ""
Write-Host "Installer: $($setup.FullName) ($([math]::Round($setup.Length / 1MB, 1)) MB)"
if (-not $signingEnabled) {
    Write-Host 'The installer is UNSIGNED - Windows may warn about or block an unknown publisher.' -ForegroundColor Yellow
}

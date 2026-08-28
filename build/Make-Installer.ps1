# ---------------------------------------------------------------------------
# Make-Installer.ps1 - build the Windows installer (Inno Setup).
#
# Steps:
#   1. Version from the git tag (build/Get-GitVersion.ps1 - the same
#      mechanism that stamps the assembly version), split into the numeric
#      part (0.0.1) and the suffix (-rc5).
#   2. dotnet publish (SELF-CONTAINED win-x64 by default: the installer ships
#      the .NET 10 Desktop Runtime, so end users need nothing preinstalled)
#      into artifacts\publish\GCodeGenerator.
#   3. Sign the published executables, if signing is configured (see below).
#   4. Compile install\GCodeGenerator.iss with ISCC, passing the version via
#      /D defines; output into artifacts\installer.
#
# Signing is OPTIONAL and off by default: it needs a code-signing certificate,
# which cannot live in the repository. Give -SignCommand (or set the
# GCODEGEN_SIGN_COMMAND environment variable) to a command line that signs one
# file, with $f standing for the file - the same placeholder Inno Setup uses,
# so one setting covers both the app and the installer. Examples:
#
#   -SignCommand 'signtool.exe sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /f C:\keys\cert.pfx /p SECRET $f'
#   -SignCommand 'azuresigntool.exe sign -kvu ... -kvc ... -tr http://timestamp.digicert.com -td SHA256 $f'
#
# Without it the build produces an unsigned installer exactly as before, and
# says so: Windows SmartScreen warns about unsigned installers, so a release
# meant for other people should be signed.
#
# Requires: .NET 10 SDK, git, Inno Setup 6 (ISCC.exe) - or -IsccPath.
# ASCII-only on purpose: Windows PowerShell 5.1 reads BOM-less .ps1 as ANSI.
# Compatible with Windows PowerShell 5.1 (no PS7 syntax).
#
# Usage:
#   build\Make-Installer.ps1 [-Configuration Release] [-Runtime win-x64]
#                            [-FrameworkDependent] [-IsccPath <path to ISCC.exe>]
#                            [-SignCommand '<command with $f>']
# ---------------------------------------------------------------------------
param(
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [switch]$FrameworkDependent,
    [string]$IsccPath = '',
    [string]$SignCommand = ''
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$scriptDir = $PSScriptRoot

# --- 1) Version from the git tag -------------------------------------------
$versionFile = Join-Path $env:TEMP ("gcodegen_installer_version_" + [guid]::NewGuid().ToString('N') + '.txt')
try {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $scriptDir 'Get-GitVersion.ps1') $versionFile | Out-Null
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
    -p:RestoreLockedMode=true -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
if (-not (Test-Path (Join-Path $publishDir 'GCodeGenerator.exe'))) {
    throw "Publish output is missing GCodeGenerator.exe: $publishDir"
}

# --- 3) Sign the published executables ---------------------------------------
# The parameter wins over the environment variable, so a workflow can export
# the command once and a local build can still override it.
if ($SignCommand -eq '') { $SignCommand = $env:GCODEGEN_SIGN_COMMAND }

# Runs the signing command for one file: $f is replaced by its quoted path,
# the same placeholder Inno Setup expands for its own SignTool.
function Invoke-SignCommand([string]$Command, [string]$FilePath) {
    $resolved = $Command.Replace('$f', '"' + $FilePath + '"')
    Write-Host "  signing: $FilePath"
    & cmd.exe /c $resolved
    if ($LASTEXITCODE -ne 0) {
        throw "Signing failed for '$FilePath' (exit code $LASTEXITCODE)"
    }
}

if ($SignCommand -ne '') {
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
    # Said out loud on purpose: an unsigned installer is met by a SmartScreen
    # warning on every machine it reaches, and that is easy not to notice when
    # the build succeeds either way.
    Write-Host 'Signing is not configured: the installer will be UNSIGNED.' -ForegroundColor Yellow
    Write-Host '  Pass -SignCommand or set GCODEGEN_SIGN_COMMAND to sign the release.'
}

# --- 4) Compile the installer ------------------------------------------------
if ($IsccPath -eq '') {
    $cmd = Get-Command iscc.exe -ErrorAction SilentlyContinue
    if ($cmd) {
        $IsccPath = $cmd.Source
    }
    else {
        $IsccPath = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
    }
}
if (-not (Test-Path $IsccPath)) {
    throw "ISCC not found ('$IsccPath'). Install Inno Setup 6 (https://jrsoftware.org/isdl.php) or pass -IsccPath."
}

$installerDir = Join-Path $repoRoot 'artifacts\installer'
if (Test-Path $installerDir) { Remove-Item $installerDir -Recurse -Force }
New-Item -ItemType Directory -Path $installerDir | Out-Null

$iss = Join-Path $repoRoot 'install\GCodeGenerator.iss'
Write-Host "Compiling installer with ISCC: $IsccPath"
# ISCC syntax: /O<path> (NO separator; relative paths resolve against ISCC's
# CWD, not the .iss dir - so pass an absolute path, quoted when it has spaces).
$outArg = "/O$installerDir"
if ($installerDir -match ' ') { $outArg = '/O"' + $installerDir + '"' }
$isccArgs = @("/DAppVersionNumeric=$numeric", "/DAppVersionSuffix=$suffix", $outArg, $iss)
if ($SignCommand -ne '') {
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
Write-Host ""
Write-Host "Installer: $($setup.FullName) ($([math]::Round($setup.Length / 1MB, 1)) MB)"
if ($SignCommand -eq '') {
    Write-Host "The installer is UNSIGNED - Windows SmartScreen will warn about it." -ForegroundColor Yellow
}

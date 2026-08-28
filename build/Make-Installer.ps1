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
#   3. Compile install\GCodeGenerator.iss with ISCC, passing the version via
#      /D defines; output into artifacts\installer.
#
# Requires: .NET 10 SDK, git, Inno Setup 6 (ISCC.exe) - or -IsccPath.
# ASCII-only on purpose: Windows PowerShell 5.1 reads BOM-less .ps1 as ANSI.
# Compatible with Windows PowerShell 5.1 (no PS7 syntax).
#
# Usage:
#   build\Make-Installer.ps1 [-Configuration Release] [-Runtime win-x64]
#                            [-FrameworkDependent] [-IsccPath <path to ISCC.exe>]
# ---------------------------------------------------------------------------
param(
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [switch]$FrameworkDependent,
    [string]$IsccPath = ''
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
# RestoreLockedMode: состав пакетов берётся строго по packages.lock.json.
# Это та же строгость, с какой пакеты восстанавливают рабочие процессы сборки,
# и относится она к тому самому выводу, который уходит пользователям.
# Ключа командной строки --locked-mode у publish нет - только свойство MSBuild.
# Замки покрывают публикацию под win-x64, потому что среда объявлена
# в csproj (RuntimeIdentifiers); без неё restore под RID отвергался бы
# как расхождение с замком (NU1004).
& dotnet publish (Join-Path $repoRoot 'GCodeGenerator\GCodeGenerator.csproj') `
    -c $Configuration -r $Runtime --self-contained $selfContainedArg `
    -p:RestoreLockedMode=true -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
if (-not (Test-Path (Join-Path $publishDir 'GCodeGenerator.exe'))) {
    throw "Publish output is missing GCodeGenerator.exe: $publishDir"
}

# --- 3) Compile the installer ------------------------------------------------
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
& $IsccPath "/DAppVersionNumeric=$numeric" "/DAppVersionSuffix=$suffix" $outArg $iss
if ($LASTEXITCODE -ne 0) { throw 'ISCC failed' }

$setup = Get-ChildItem -Path $installerDir -Filter 'GCodeGenerator-Setup-*.exe' | Select-Object -First 1
if (-not $setup) { throw "Installer not found in $installerDir" }
Write-Host ""
Write-Host "Installer: $($setup.FullName) ($([math]::Round($setup.Length / 1MB, 1)) MB)"

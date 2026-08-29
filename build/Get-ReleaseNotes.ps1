# ---------------------------------------------------------------------------
# Get-ReleaseNotes.ps1 - release description from CHANGELOG.md.
#
# Prints the section of one version: everything between its heading and the
# next heading of the same level. The section goes into the GitHub Release
# description, so that the release page says what changed for the user -
# not the list of commits, which is written for the developer.
#
# Headings follow Keep a Changelog: "## [1.2.3]" or "## [1.2.3] - 2026-08-28".
# The tag is matched literally, so "1.2.3-rc5" never matches "1.2.3".
#
# A missing changelog, section or section body is a release error. Publishing
# generated commit notes would hide the omission and expose developer-facing
# messages to end users, so every failure is deliberately fail-closed.
#
# ASCII-only on purpose: Windows PowerShell 5.1 reads BOM-less .ps1 as ANSI.
# The changelog itself is UTF-8 and is read as such.
# Compatible with Windows PowerShell 5.1 (no PS7 syntax).
#
# Usage: Get-ReleaseNotes.ps1 -Tag <version> [-Path <changelog>] [-OutFile <path>]
#   -OutFile - additionally write the section to a file (used by the workflow:
#   a multi-line value cannot be passed through a single-line output).
# ---------------------------------------------------------------------------
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Tag,

    [string] $Path,

    [string] $OutFile
)

$ErrorActionPreference = 'Stop'

if (-not $Path) {
    $Path = Join-Path (Split-Path -Parent $PSScriptRoot) 'CHANGELOG.md'
}

if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    throw "Changelog not found: $Path"
}

$lines = Get-Content -LiteralPath $Path -Encoding UTF8

# Escape the tag: a version has dots, and in a regular expression a dot
# matches any character - "1-2-3" would pass for "1.2.3".
$heading = '^##\s+\[' + [regex]::Escape($Tag) + '\]'

$start = -1
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match $heading) {
        $start = $i + 1
        break
    }
}

if ($start -lt 0) {
    throw "Changelog has no section for '$Tag' in $Path"
}

$end = $lines.Count
for ($i = $start; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^##\s') {
        $end = $i
        break
    }
}

$section = @()
if ($end -gt $start) {
    $section = $lines[$start..($end - 1)]
}

# Blank lines around the section carry no meaning and only add empty space
# to the release page.
$firstContentLine = 0
while ($firstContentLine -lt $section.Count -and
       $section[$firstContentLine].Trim().Length -eq 0) {
    $firstContentLine++
}
$lastContentLine = $section.Count - 1
while ($lastContentLine -ge $firstContentLine -and
       $section[$lastContentLine].Trim().Length -eq 0) {
    $lastContentLine--
}
if ($lastContentLine -ge $firstContentLine) {
    $section = @($section[$firstContentLine..$lastContentLine])
}
else {
    $section = @()
}

if ($section.Count -eq 0) {
    throw "Section '$Tag' in $Path is empty"
}

$text = $section -join "`n"

if ($OutFile) {
    $directory = Split-Path -Parent $OutFile
    if ($directory -and -not (Test-Path $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    # UTF8Encoding without BOM: GitHub shows a BOM as a stray character at
    # the very beginning of the release description.
    [System.IO.File]::WriteAllText(
        $OutFile, $text, (New-Object System.Text.UTF8Encoding $false))
}

Write-Output $text

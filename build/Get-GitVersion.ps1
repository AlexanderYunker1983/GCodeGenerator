# ---------------------------------------------------------------------------
# Get-GitVersion.ps1 - product version from git tags (build-time helper).
#
# Prints ONE line: X.Y.Z or X.Y.Z-suffix (e.g. 1.2.3-rc5).
#
# Selection:
#   1. Tag(s) on the current commit (git tag --points-at HEAD); if several -
#      the one with the highest precedence (SemVer):
#      1.2.3 > 1.2.3-rc5 > 1.2.3-beta3 > 1.2.3-alpha2 > 1.2.3-alpha
#      (within a class - by number: alpha2 > alpha, rc10 > rc5).
#   2. Otherwise - the nearest tag in history (git describe --tags --abbrev=0).
#   3. Otherwise (no tags / no git / not a repository) - 0.1.0-alpha.
#
# Tag format: ^\d+\.\d+\.\d+(-[A-Za-z][A-Za-z0-9]*)?$
# (three-part version + optional suffix: -alpha, -alpha2, -beta, -beta3,
# -rc5, ...). Tags outside the format (v1.2.3, 1.2, foo) are ignored with a
# warning on stderr.
#
# ASCII-only on purpose: Windows PowerShell 5.1 reads BOM-less .ps1 as ANSI.
# Compatible with Windows PowerShell 5.1 (no PS7 syntax).
#
# Usage: Get-GitVersion.ps1 [-OutFile <path>]
#   -OutFile - additionally write the version to a file (used by MSBuild:
#   the Exec task of this SDK cannot capture stdout into a property).
# ---------------------------------------------------------------------------
param([string]$OutFile)

$ErrorActionPreference = 'Stop'
$defaultVersion = '0.1.0-alpha'

function Invoke-Git {
    param([string[]]$GitArgs)
    try {
        $out = & git @GitArgs 2>$null
        if ($LASTEXITCODE -ne 0) { return @() }
        return @($out | Where-Object { $_ })
    }
    catch {
        # git not installed / not a repository - empty result.
        return @()
    }
}

function Test-VersionTag {
    param([string]$Tag)
    return [bool]($Tag -match '^\d+\.\d+\.\d+(-[A-Za-z][A-Za-z0-9]*)?$')
}

# Sort key: major.minor.patch.classRank.number (zero-padded, so lexicographic
# comparison == numeric comparison).
# classRank: unknown suffix = 0, alpha = 1, beta = 2, rc = 3,
# no suffix (release) = 4.
function Get-VersionRank {
    param([string]$Tag)
    $m = [regex]::Match($Tag, '^(\d+)\.(\d+)\.(\d+)(?:-([A-Za-z][A-Za-z0-9]*))?$')
    $major = [int]$m.Groups[1].Value
    $minor = [int]$m.Groups[2].Value
    $patch = [int]$m.Groups[3].Value
    $suffix = $m.Groups[4].Value
    $classRank = 4
    $number = 0
    if ($suffix -ne '') {
        $sm = [regex]::Match($suffix, '^([A-Za-z]+)(\d*)$')
        $class = $sm.Groups[1].Value.ToLowerInvariant()
        if ($sm.Groups[2].Value -ne '') { $number = [int]$sm.Groups[2].Value }
        switch ($class) {
            'alpha' { $classRank = 1 }
            'beta'  { $classRank = 2 }
            'rc'    { $classRank = 3 }
            default { $classRank = 0 }
        }
    }
    return '{0:D10}.{1:D10}.{2:D10}.{3:D10}.{4:D10}' -f $major, $minor, $patch, $classRank, $number
}

$tags = @(Invoke-Git @('tag', '--points-at', 'HEAD'))
if ($tags.Count -eq 0) {
    $tags = @(Invoke-Git @('describe', '--tags', '--abbrev=0'))
}

$valid = @($tags | Where-Object { Test-VersionTag $_ })
foreach ($t in $tags) {
    if (-not (Test-VersionTag $t)) {
        # stderr, NOT stdout: stdout is captured as the version by MSBuild
        # (Write-Warning would leak into stdout in redirected child powershell).
        [Console]::Error.WriteLine("Get-GitVersion: tag '$t' does not match X.Y.Z[-suffix] format - skipped")
    }
}

if ($valid.Count -eq 0) {
    $best = $defaultVersion
}
else {
    $best = $null
    $bestRank = ''
    foreach ($t in $valid) {
        $rank = Get-VersionRank $t
        if ($rank -gt $bestRank) {
            $bestRank = $rank
            $best = $t
        }
    }
}

Write-Output $best
if ($OutFile -ne '') {
    Set-Content -Path $OutFile -Value $best -NoNewline -Encoding ASCII
}

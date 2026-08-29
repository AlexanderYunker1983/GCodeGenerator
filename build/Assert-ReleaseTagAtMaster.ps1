# ---------------------------------------------------------------------------
# Assert-ReleaseTagAtMaster.ps1 - require a release tag at origin/master.
#
# Resolves the tag as a commit, so both annotated and lightweight tags work.
# The master ref is refreshed explicitly without fetching or changing tags.
# ASCII-only for Windows PowerShell 5.1 compatibility.
# ---------------------------------------------------------------------------
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Tag
)

$ErrorActionPreference = 'Stop'

if ($Tag -notmatch '^\d+\.\d+\.\d+(-[A-Za-z][A-Za-z0-9]*)?$') {
    throw "Invalid release tag '$Tag'."
}

& git fetch --no-tags origin '+refs/heads/master:refs/remotes/origin/master'
if ($LASTEXITCODE -ne 0) {
    throw 'Cannot refresh origin/master.'
}

$tagOutput = @(& git rev-parse --verify "refs/tags/$($Tag)^{commit}" 2>&1)
$tagExitCode = $LASTEXITCODE
$masterOutput = @(& git rev-parse --verify 'refs/remotes/origin/master^{commit}' 2>&1)
$masterExitCode = $LASTEXITCODE

if ($tagExitCode -ne 0 -or $tagOutput.Count -eq 0 -or
    $masterExitCode -ne 0 -or $masterOutput.Count -eq 0) {
    throw 'Cannot resolve the release tag or origin/master as a commit.'
}

$tagCommit = $tagOutput[-1].ToString().Trim()
$masterCommit = $masterOutput[-1].ToString().Trim()
if ([string]::IsNullOrWhiteSpace($tagCommit) -or
    [string]::IsNullOrWhiteSpace($masterCommit)) {
    throw 'Cannot resolve the release tag or origin/master as a commit.'
}

if ($tagCommit -ne $masterCommit) {
    throw "Release tag points to $tagCommit, but current origin/master is $masterCommit."
}

Write-Host "Release commit is current origin/master: $tagCommit"

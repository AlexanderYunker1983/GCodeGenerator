# ---------------------------------------------------------------------------
# Test-VulnerablePackages.ps1 - fail the build on a known vulnerable package.
#
# Runs "dotnet list package --vulnerable --include-transitive" and reports
# every package the advisory database knows about, direct and transitive
# alike: a vulnerability arrives far more often through a dependency of a
# dependency than through a package the project names itself.
#
# Exit code: 0 - nothing found, 1 - something found (or the listing failed).
#
# The JSON output is parsed instead of the human-readable one: the latter is
# translated, and a check that greps for an English phrase stops working the
# moment the runner speaks another language.
#
# The advisory data comes from the package sources, so the answer is only as
# complete as they are: a private mirror that serves no advisories reports
# nothing. On the build server the source is nuget.org.
#
# ASCII-only on purpose: Windows PowerShell 5.1 reads BOM-less .ps1 as ANSI.
# Compatible with Windows PowerShell 5.1 (no PS7 syntax).
#
# Usage: Test-VulnerablePackages.ps1 [-Solution <path>] [-JsonPath <path>]
#   -JsonPath - read a ready listing instead of running dotnet (tests).
# ---------------------------------------------------------------------------
[CmdletBinding()]
param(
    [string] $Solution,

    [string] $JsonPath
)

$ErrorActionPreference = 'Stop'

# Failures are reported and the exit code is set by hand: Write-Error under
# a Stop preference terminates the script, and the exit code then belongs to
# PowerShell, not to this check. A check whose failure code is accidental is
# a check nobody can rely on.
function Stop-WithError([string] $message) {
    Write-Host "ERROR: $message"
    exit 1
}

if (-not $Solution) {
    $Solution = Join-Path (Split-Path -Parent $PSScriptRoot) 'GCodeGenerator.sln'
}

if ($JsonPath) {
    if (-not (Test-Path $JsonPath)) {
        Stop-WithError "Listing not found: $JsonPath"
    }

    $json = Get-Content -LiteralPath $JsonPath -Encoding UTF8 -Raw
}
else {
    Write-Host "Checking packages of $Solution against the advisory database..."

    # The invariant language keeps the diagnostics of a failure readable in
    # the build log regardless of the machine that produced them.
    $previousLanguage = $env:DOTNET_CLI_UI_LANGUAGE
    $env:DOTNET_CLI_UI_LANGUAGE = 'en'
    try {
        $json = & dotnet list $Solution package --vulnerable --include-transitive --format json 2>&1 | Out-String
        $failed = $LASTEXITCODE -ne 0
    }
    finally {
        $env:DOTNET_CLI_UI_LANGUAGE = $previousLanguage
    }

    if ($failed) {
        Write-Host $json
        Stop-WithError "dotnet list package failed."
    }
}

# A failure of the listing itself must not pass for "nothing found": the
# check would then be green exactly when it stopped working.
try {
    $listing = $json | ConvertFrom-Json
}
catch {
    Write-Host $json
    Stop-WithError "Could not parse the package listing as JSON."
}

if (-not $listing.projects) {
    Stop-WithError "The package listing names no projects - nothing was checked."
}

$found = 0

foreach ($project in $listing.projects) {
    # A clean project carries its path and nothing else: frameworks appear
    # only where there is something to report.
    if (-not $project.frameworks) {
        continue
    }

    foreach ($framework in $project.frameworks) {
        $packages = @()
        if ($framework.topLevelPackages) { $packages += $framework.topLevelPackages }
        if ($framework.transitivePackages) { $packages += $framework.transitivePackages }

        foreach ($package in $packages) {
            foreach ($vulnerability in $package.vulnerabilities) {
                $found++
                Write-Host ("VULNERABLE  {0} {1}  [{2}]  {3}  ({4}, {5})" -f `
                    $package.id,
                    $package.resolvedVersion,
                    $vulnerability.severity,
                    $vulnerability.advisoryurl,
                    (Split-Path -Leaf $project.path),
                    $framework.framework)
            }
        }
    }
}

if ($found -gt 0) {
    Write-Host ""
    Stop-WithError "$found known vulnerability(ies) in the package tree. Update the package and the lock file: dotnet restore GCodeGenerator.sln --force-evaluate"
}

Write-Host "No known vulnerable packages."
exit 0

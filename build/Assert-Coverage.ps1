[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ResultsDirectory,

    [Parameter(Mandatory = $true)]
    [string]$Assembly,

    [Parameter(Mandatory = $true)]
    [ValidateRange(0, 100)]
    [double]$MinimumLinePercent,

    [Parameter(Mandatory = $true)]
    [ValidateRange(0, 100)]
    [double]$MinimumBranchPercent
)

# ASCII-only: Windows PowerShell 5.1 reads a BOM-less script as ANSI.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$reports = @(
    Get-ChildItem -LiteralPath $ResultsDirectory -Filter '*.cobertura.xml' -File -Recurse
)
if ($reports.Count -ne 1) {
    throw "Expected exactly one Cobertura report in '$ResultsDirectory', found $($reports.Count)."
}

[xml]$coverage = Get-Content -LiteralPath $reports[0].FullName
$packages = @($coverage.coverage.packages.package | Where-Object name -EQ $Assembly)
if ($packages.Count -ne 1) {
    throw "Expected exactly one '$Assembly' package in '$($reports[0].FullName)', found $($packages.Count)."
}

$culture = [System.Globalization.CultureInfo]::InvariantCulture
$linePercent = [double]::Parse([string]$packages[0].'line-rate', $culture) * 100
$branchPercent = [double]::Parse([string]$packages[0].'branch-rate', $culture) * 100
if ([double]::IsNaN($linePercent) -or [double]::IsInfinity($linePercent) -or
    [double]::IsNaN($branchPercent) -or [double]::IsInfinity($branchPercent)) {
    throw "Coverage report for '$Assembly' contains a non-finite rate."
}

Write-Host ("{0}: lines {1:N2}% (minimum {2:N2}%), branches {3:N2}% (minimum {4:N2}%)" -f `
    $Assembly, $linePercent, $MinimumLinePercent, $branchPercent, $MinimumBranchPercent)

$failures = @()
if ($linePercent -lt $MinimumLinePercent) {
    $failures += "line coverage $($linePercent.ToString('N2'))% is below $($MinimumLinePercent.ToString('N2'))%"
}
if ($branchPercent -lt $MinimumBranchPercent) {
    $failures += "branch coverage $($branchPercent.ToString('N2'))% is below $($MinimumBranchPercent.ToString('N2'))%"
}
if ($failures.Count -gt 0) {
    throw "${Assembly} coverage gate failed: $($failures -join '; ')."
}

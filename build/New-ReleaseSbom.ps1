[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Project,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$listingOutput = @(& dotnet list $Project package --include-transitive --format json --no-restore 2>&1)
if ($LASTEXITCODE -ne 0) {
    throw "dotnet list package failed:`n$($listingOutput -join [Environment]::NewLine)"
}

$listingText = $listingOutput -join [Environment]::NewLine
$jsonStart = $listingText.IndexOf('{')
$jsonEnd = $listingText.LastIndexOf('}')
if ($jsonStart -lt 0 -or $jsonEnd -le $jsonStart) {
    throw 'dotnet list package returned no JSON document.'
}
$listing = $listingText.Substring($jsonStart, $jsonEnd - $jsonStart + 1) | ConvertFrom-Json

$packages = @{}
foreach ($projectEntry in @($listing.projects)) {
    foreach ($framework in @($projectEntry.frameworks)) {
        foreach ($package in @($framework.topLevelPackages) + @($framework.transitivePackages)) {
            if (-not $package.id -or -not $package.resolvedVersion) {
                throw 'A package in the dependency listing has no id or resolvedVersion.'
            }

            $key = "$($package.id)`n$($package.resolvedVersion)"
            $escapedId = [System.Uri]::EscapeDataString([string]$package.id)
            $escapedVersion = [System.Uri]::EscapeDataString([string]$package.resolvedVersion)
            $purl = "pkg:nuget/$escapedId@$escapedVersion"
            $packages[$key] = [ordered]@{
                type = 'library'
                'bom-ref' = $purl
                name = [string]$package.id
                version = [string]$package.resolvedVersion
                purl = $purl
            }
        }
    }
}

if ($packages.Count -eq 0) {
    throw 'The application dependency listing is empty; refusing to publish an empty SBOM.'
}

$components = @(
    $packages.Values |
        Sort-Object -Property @{ Expression = { $_.name } }, @{ Expression = { $_.version } }
)
$bom = [ordered]@{
    bomFormat = 'CycloneDX'
    specVersion = '1.6'
    version = 1
    metadata = [ordered]@{
        component = [ordered]@{
            type = 'application'
            'bom-ref' = "pkg:generic/GCodeGenerator@$Version"
            name = 'GCodeGenerator'
            version = $Version
        }
    }
    components = $components
}

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)
$outputDirectory = Split-Path -Parent $resolvedOutput
if (-not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory | Out-Null
}
$temporaryPath = "$resolvedOutput.tmp"
try {
    $json = $bom | ConvertTo-Json -Depth 12
    [System.IO.File]::WriteAllText(
        $temporaryPath,
        $json + [Environment]::NewLine,
        [System.Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temporaryPath -Destination $resolvedOutput -Force
}
finally {
    if (Test-Path -LiteralPath $temporaryPath) {
        Remove-Item -LiteralPath $temporaryPath -Force
    }
}

Write-Host "CycloneDX SBOM: $resolvedOutput ($($components.Count) packages)"

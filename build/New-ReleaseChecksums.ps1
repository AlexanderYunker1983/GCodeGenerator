[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Directory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$resolvedDirectory = (Resolve-Path -LiteralPath $Directory).Path
$outputPath = Join-Path $resolvedDirectory 'SHA256SUMS.txt'
$temporaryPath = "$outputPath.tmp"

# Только распространяемые файлы. Release notes намеренно не входят в список:
# они не исполняются и GitHub может сгенерировать их уже после сборки.
$artifacts = @(
    Get-ChildItem -LiteralPath $resolvedDirectory -File |
        Where-Object {
            $_.Name -match '^GCodeGenerator-Setup-.+\.exe$' -or
            $_.Name -match '^GCodeGenerator-.+-portable\.zip$' -or
            $_.Name -match '^GCodeGenerator-.+-sbom\.cdx\.json$'
        } |
        Sort-Object -Property Name
)

if (-not ($artifacts | Where-Object Name -Match '^GCodeGenerator-Setup-.+\.exe$')) {
    throw "Release installer was not found in '$resolvedDirectory'."
}
if (-not ($artifacts | Where-Object Name -Match '^GCodeGenerator-.+-portable\.zip$')) {
    throw "Portable release was not found in '$resolvedDirectory'."
}

$lines = foreach ($artifact in $artifacts) {
    $stream = [System.IO.File]::OpenRead($artifact.FullName)
    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = [System.BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $algorithm.Dispose()
        $stream.Dispose()
    }
    "$hash  $($artifact.Name)"
}

try {
    [System.IO.File]::WriteAllLines(
        $temporaryPath,
        $lines,
        [System.Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temporaryPath -Destination $outputPath -Force
}
finally {
    if (Test-Path -LiteralPath $temporaryPath) {
        Remove-Item -LiteralPath $temporaryPath -Force
    }
}

Write-Host "SHA-256 checksums: $outputPath"

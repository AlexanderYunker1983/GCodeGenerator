[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDirectory,

    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

# ASCII-only: Windows PowerShell 5.1 reads a BOM-less script as ANSI.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$publish = [System.IO.Path]::GetFullPath($PublishDirectory)
$repository = [System.IO.Path]::GetFullPath($RepositoryRoot)
if (-not (Test-Path -LiteralPath $publish -PathType Container)) {
    throw "Publish directory does not exist: '$publish'."
}

$noticeDirectory = Join-Path $publish 'licenses'
New-Item -ItemType Directory -Path $noticeDirectory -Force | Out-Null

function Copy-RequiredFile([string]$Source, [string]$Name) {
    if (-not (Test-Path -LiteralPath $Source -PathType Leaf)) {
        throw "Required release notice is missing: '$Source'."
    }
    Copy-Item -LiteralPath $Source -Destination (Join-Path $noticeDirectory $Name) -Force
}

Copy-RequiredFile (Join-Path $repository 'LICENSE') 'GCodeGenerator-LICENSE.txt'
Copy-RequiredFile (Join-Path $repository 'THIRD-PARTY-NOTICES.md') 'THIRD-PARTY-NOTICES.md'

$dotnetCommand = Get-Command dotnet -ErrorAction Stop
$dotnetRoot = Split-Path -Parent $dotnetCommand.Source
Copy-RequiredFile (Join-Path $dotnetRoot 'LICENSE.txt') 'DOTNET-LICENSE.txt'
Copy-RequiredFile (Join-Path $dotnetRoot 'ThirdPartyNotices.txt') 'DOTNET-THIRD-PARTY-NOTICES.txt'

[xml]$applicationProject = Get-Content (Join-Path $repository 'GCodeGenerator\GCodeGenerator.csproj')
$toolkitReference = $applicationProject.SelectSingleNode(
    "/Project/ItemGroup/PackageReference[@Include='CommunityToolkit.Mvvm']")
if (-not $toolkitReference -or -not $toolkitReference.Version) {
    throw 'CommunityToolkit.Mvvm package version was not found in GCodeGenerator.csproj.'
}

$nugetRoot = $env:NUGET_PACKAGES
if (-not $nugetRoot) {
    $nugetRoot = Join-Path $env:USERPROFILE '.nuget\packages'
}
$toolkitRoot = Join-Path $nugetRoot ("communitytoolkit.mvvm\" + $toolkitReference.Version)
Copy-RequiredFile (Join-Path $toolkitRoot 'License.md') 'COMMUNITYTOOLKIT-LICENSE.md'
Copy-RequiredFile (Join-Path $toolkitRoot 'ThirdPartyNotices.txt') 'COMMUNITYTOOLKIT-THIRD-PARTY-NOTICES.txt'

Write-Host "Release notices: $noticeDirectory"

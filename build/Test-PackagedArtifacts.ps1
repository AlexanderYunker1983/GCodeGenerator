[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$InstallerPath,

    [Parameter(Mandatory = $true)]
    [string]$PortableExePath,

    [Parameter(Mandatory = $true)]
    [string]$WorkRoot,

    [Parameter(Mandatory = $true)]
    [string]$OutputLog,

    [string]$PreviousInstallerPath = '',

    [switch]$RequireAuthenticodeSignature,

    [string]$ExpectedSignerThumbprint = '',

    [ValidateRange(1, 3600)]
    [int]$ProcessTimeoutSeconds = 300,

    [ValidateRange(1, 60)]
    [int]$ApplicationStartupSeconds = 5,

    [ValidateRange(1, 300)]
    [int]$ApplicationCloseTimeoutSeconds = 10
)

# ASCII-only: Windows PowerShell 5.1 reads a BOM-less script as ANSI.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$installer = (Resolve-Path -LiteralPath $InstallerPath).Path
$previousInstaller = if ([string]::IsNullOrWhiteSpace($PreviousInstallerPath)) {
    $null
}
else {
    (Resolve-Path -LiteralPath $PreviousInstallerPath).Path
}
$portableExe = (Resolve-Path -LiteralPath $PortableExePath).Path
$workDirectory = [System.IO.Path]::GetFullPath($WorkRoot)
$logPath = [System.IO.Path]::GetFullPath($OutputLog)
$logDirectory = Split-Path -Parent $logPath

if (Test-Path -LiteralPath $workDirectory) {
    throw "Smoke-test work directory already exists: '$workDirectory'."
}
if (-not (Test-Path -LiteralPath $logDirectory)) {
    New-Item -ItemType Directory -Path $logDirectory | Out-Null
}

$utf8 = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText($logPath, '', $utf8)

function Write-SmokeLog([string]$Message) {
    $line = "[$([DateTime]::UtcNow.ToString('o'))] $Message"
    [System.IO.File]::AppendAllText($logPath, $line + [Environment]::NewLine, $utf8)
    Write-Host $line
}

function Assert-PackagedSignature([string]$FilePath, [string]$Stage) {
    if (-not $RequireAuthenticodeSignature) {
        return
    }

    Write-SmokeLog "${Stage}: verifying Authenticode signature"
    $verification = @{
        FilePath = $FilePath
        RequireTimestamp = $true
    }
    if ($ExpectedSignerThumbprint -ne '') {
        $verification.ExpectedSignerThumbprint = $ExpectedSignerThumbprint
    }
    & (Join-Path $PSScriptRoot 'Assert-AuthenticodeSignature.ps1') @verification
    Write-SmokeLog "${Stage}: signature passed"
}

function Invoke-CheckedProcess([string]$FilePath, [string[]]$Arguments, [string]$Stage) {
    Write-SmokeLog "${Stage}: starting"
    $process = Start-Process -FilePath $FilePath -ArgumentList $Arguments `
        -WindowStyle Hidden -PassThru
    try {
        $processTimeoutMilliseconds = $ProcessTimeoutSeconds * 1000
        if (-not $process.WaitForExit($processTimeoutMilliseconds)) {
            throw "$Stage timed out after $ProcessTimeoutSeconds seconds."
        }
        if ($process.ExitCode -ne 0) {
            throw "$Stage failed with exit code $($process.ExitCode)."
        }
        Write-SmokeLog "${Stage}: passed"
    }
    finally {
        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
        $process.Dispose()
    }
}

function Test-ApplicationStart([string]$Executable, [string]$Stage) {
    Write-SmokeLog "${Stage}: starting"
    $process = Start-Process -FilePath $Executable -WindowStyle Hidden -PassThru
    try {
        Start-Sleep -Seconds $ApplicationStartupSeconds
        if ($process.HasExited) {
            throw "$Stage exited during startup with code $($process.ExitCode)."
        }

        Write-SmokeLog "${Stage}: process remained alive"
        if (-not $process.CloseMainWindow()) {
            throw "$Stage has no closable main window."
        }
        $closeTimeoutMilliseconds = $ApplicationCloseTimeoutSeconds * 1000
        if (-not $process.WaitForExit($closeTimeoutMilliseconds)) {
            throw "$Stage ignored a normal window close request for $ApplicationCloseTimeoutSeconds seconds."
        }
        if ($process.ExitCode -ne 0) {
            throw "$Stage closed with exit code $($process.ExitCode)."
        }
        Write-SmokeLog "${Stage}: passed"
    }
    finally {
        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
        $process.Dispose()
    }
}

$installDirectory = Join-Path $workDirectory 'installed'
$installLog = Join-Path $workDirectory 'install.log'
$upgradeLog = Join-Path $workDirectory 'upgrade.log'
$uninstallLog = Join-Path $workDirectory 'uninstall.log'

try {
    New-Item -ItemType Directory -Path $workDirectory | Out-Null
    Assert-PackagedSignature $installer 'Installer before installation'
    Assert-PackagedSignature $portableExe 'Portable executable'
    $installArguments = @(
        '/VERYSILENT',
        '/SUPPRESSMSGBOXES',
        '/NORESTART',
        '/CURRENTUSER',
        "/DIR=`"$installDirectory`"",
        "/LOG=`"$installLog`""
    )

    $initialInstaller = if ($null -ne $previousInstaller) { $previousInstaller } else { $installer }
    $installStage = if ($null -ne $previousInstaller) { 'Install previous release' } else { 'Install candidate' }
    Invoke-CheckedProcess $initialInstaller $installArguments $installStage
    $installedExe = Join-Path $installDirectory 'GCodeGenerator.exe'
    $uninstaller = Join-Path $installDirectory 'unins000.exe'
    if (-not (Test-Path -LiteralPath $installedExe)) {
        throw "Installed executable is missing: '$installedExe'."
    }
    if (-not (Test-Path -LiteralPath $uninstaller)) {
        throw "Uninstaller is missing: '$uninstaller'."
    }
    if ($null -eq $previousInstaller) {
        foreach ($signedFile in @(
            $installedExe,
            (Join-Path $installDirectory 'GCodeGenerator.dll'),
            (Join-Path $installDirectory 'GCodeGenerator.Core.dll'),
            $uninstaller)) {
            Assert-PackagedSignature $signedFile 'Installed payload'
        }
    }
    $startStage = if ($null -ne $previousInstaller) {
        'Start previous release'
    }
    else {
        'Start installed candidate'
    }
    Test-ApplicationStart $installedExe $startStage

    $installArguments[-1] = "/LOG=`"$upgradeLog`""
    $upgradeStage = if ($null -ne $previousInstaller) {
        'Upgrade previous release to candidate'
    }
    else {
        'Reinstall candidate over existing installation'
    }
    Invoke-CheckedProcess $installer $installArguments $upgradeStage
    foreach ($signedFile in @(
        $installedExe,
        (Join-Path $installDirectory 'GCodeGenerator.dll'),
        (Join-Path $installDirectory 'GCodeGenerator.Core.dll'),
        $uninstaller)) {
        Assert-PackagedSignature $signedFile 'Upgraded payload'
    }
    Test-ApplicationStart $installedExe 'Start upgraded candidate'

    Test-ApplicationStart $portableExe 'Start portable application'

    $uninstallArguments = @(
        '/VERYSILENT',
        '/SUPPRESSMSGBOXES',
        '/NORESTART',
        "/LOG=`"$uninstallLog`""
    )
    Invoke-CheckedProcess $uninstaller $uninstallArguments 'Uninstall'
    if (Test-Path -LiteralPath $installedExe) {
        throw "Installed executable remains after uninstall: '$installedExe'."
    }

    Write-SmokeLog 'Packaged artifact smoke test: PASSED'
}
catch {
    Write-SmokeLog "Packaged artifact smoke test: FAILED: $($_.Exception.Message)"
    foreach ($detailLog in @($installLog, $upgradeLog, $uninstallLog)) {
        if (Test-Path -LiteralPath $detailLog) {
            Write-SmokeLog "----- $(Split-Path -Leaf $detailLog) -----"
            [System.IO.File]::AppendAllText(
                $logPath,
                [System.IO.File]::ReadAllText($detailLog) + [Environment]::NewLine,
                $utf8)
        }
    }
    throw
}
finally {
    # The exact directory was required not to exist and was created above by
    # this script, so recursive cleanup cannot target pre-existing user data.
    if (Test-Path -LiteralPath $workDirectory) {
        Remove-Item -LiteralPath $workDirectory -Recurse -Force
    }
}

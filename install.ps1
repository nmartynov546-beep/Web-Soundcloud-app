[CmdletBinding()]
param(
    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'

$ProjectRoot = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$DistDir = Join-Path $ProjectRoot 'dist'
$InstallDir = Join-Path $env:LOCALAPPDATA 'Programs\SoundCloudDesktop'
$TargetExe = Join-Path $InstallDir 'SoundCloud.exe'

$DesktopDir = [Environment]::GetFolderPath('Desktop')
$StartMenuProgramsDir = [Environment]::GetFolderPath('Programs')
$DesktopShortcut = Join-Path $DesktopDir 'SoundCloud.lnk'
$StartMenuShortcut = Join-Path $StartMenuProgramsDir 'SoundCloud.lnk'

function New-AppShortcut {
    param(
        [Parameter(Mandatory = $true)][string]$ShortcutPath,
        [Parameter(Mandatory = $true)][string]$ExePath,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory
    )

    $parentDir = Split-Path -Parent $ShortcutPath
    if ($parentDir -and -not (Test-Path -LiteralPath $parentDir)) {
        New-Item -ItemType Directory -Path $parentDir -Force | Out-Null
    }

    $wshShell = New-Object -ComObject WScript.Shell
    try {
        $shortcut = $wshShell.CreateShortcut($ShortcutPath)
        $shortcut.TargetPath = $ExePath
        $shortcut.WorkingDirectory = $WorkingDirectory
        $shortcut.IconLocation = "$ExePath,0"
        $shortcut.Description = 'SoundCloud Desktop'
        $shortcut.Save()
    }
    finally {
        [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($wshShell)
    }
}

if ($Uninstall) {
    foreach ($shortcutPath in @($DesktopShortcut, $StartMenuShortcut)) {
        if (Test-Path -LiteralPath $shortcutPath) {
            Remove-Item -LiteralPath $shortcutPath -Force
            Write-Host "Removed shortcut: $shortcutPath"
        }
    }

    if (Test-Path -LiteralPath $InstallDir) {
        Remove-Item -LiteralPath $InstallDir -Recurse -Force
        Write-Host "Removed installation folder: $InstallDir"
    }
    else {
        Write-Host "Installation folder not found: $InstallDir"
    }

    Write-Host 'SoundCloud Desktop uninstalled.'
    exit 0
}

$DistExe = Join-Path $DistDir 'SoundCloud.exe'
if (-not (Test-Path -LiteralPath $DistExe)) {
    Write-Error "Build output not found at '$DistExe'. Run .\build.ps1 first."
    exit 1
}

if (-not (Test-Path -LiteralPath $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

Copy-Item -Path (Join-Path $DistDir '*') -Destination $InstallDir -Recurse -Force

New-AppShortcut -ShortcutPath $DesktopShortcut -ExePath $TargetExe -WorkingDirectory $InstallDir
New-AppShortcut -ShortcutPath $StartMenuShortcut -ExePath $TargetExe -WorkingDirectory $InstallDir

Write-Host "Installed to: $InstallDir"
Write-Host "Created shortcut: $DesktopShortcut"
Write-Host "Created shortcut: $StartMenuShortcut"

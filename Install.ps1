# ================================================================
#  Gakungu Water Management System — Installer
#  Version 1.0  |  Gakungu Community Water Project
#
#  HOW TO USE:
#    Right-click this file → "Run with PowerShell"
#    (or run as Administrator for "All Users" installation)
# ================================================================

param(
    [string]$InstallDir = "",
    [switch]$Silent
)

$ErrorActionPreference = "Stop"
$AppName = "Gakungu Water Management System"
$AppVersion = "1.0"
$ExeName = "GakunguWater.exe"
$RegKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\GakunguWater"

# ── Locate the app files next to this script ─────────────────
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SourceDir = Join-Path $ScriptDir "publish"
$ZipFile = Join-Path $ScriptDir "GakunguWater.zip"

# ── Banner ────────────────────────────────────────────────────
Clear-Host
Write-Host ""
Write-Host "  ██████╗  █████╗ ██╗  ██╗██╗   ██╗███╗   ██╗ ██████╗ ██╗   ██╗" -ForegroundColor Cyan
Write-Host "  ██╔════╝ ██╔══██╗██║ ██╔╝██║   ██║████╗  ██║██╔════╝ ██║   ██║" -ForegroundColor Cyan
Write-Host "  ██║  ███╗███████║█████╔╝ ██║   ██║██╔██╗ ██║██║  ███╗██║   ██║" -ForegroundColor Cyan
Write-Host "  ██║   ██║██╔══██║██╔═██╗ ██║   ██║██║╚██╗██║██║   ██║██║   ██║" -ForegroundColor Cyan
Write-Host "  ╚██████╔╝██║  ██║██║  ██╗╚██████╔╝██║ ╚████║╚██████╔╝╚██████╔╝" -ForegroundColor Cyan
Write-Host "   ╚═════╝ ╚═╝  ╚═╝╚═╝  ╚═╝ ╚═════╝ ╚═╝  ╚═══╝ ╚═════╝  ╚═════╝" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Water Management System — Installer v$AppVersion" -ForegroundColor White
Write-Host "  Gakungu Community Water Project" -ForegroundColor DarkCyan
Write-Host ""
Write-Host ("-" * 65) -ForegroundColor DarkGray

# ── Verify source files exist ────────────────────────────────
if (-not (Test-Path $SourceDir) -and -not (Test-Path $ZipFile)) {
    Write-Host ""
    Write-Host "  ERROR: Cannot find 'publish' folder or 'GakunguWater.zip'" -ForegroundColor Red
    Write-Host "  Make sure this Install.ps1 is in the same folder as the" -ForegroundColor Red
    Write-Host "  'publish' directory or 'GakunguWater.zip'." -ForegroundColor Red
    Write-Host ""
    if (-not $Silent) { Read-Host "  Press Enter to exit" }
    exit 1
}

# ── Choose install directory ──────────────────────────────────
if ($InstallDir -eq "") {
    $Default = "$env:LOCALAPPDATA\Programs\GakunguWater"
    if (-not $Silent) {
        Write-Host ""
        Write-Host "  Install location:" -ForegroundColor Yellow
        Write-Host "  [$Default]" -ForegroundColor Gray
        Write-Host ""
        $Custom = Read-Host "  Press Enter to accept, or type a different path"
        if ($Custom.Trim() -ne "") { $Default = $Custom.Trim() }
    }
    $InstallDir = $Default
}

Write-Host ""
Write-Host "  Installing to: $InstallDir" -ForegroundColor Green
Write-Host ""

# ── Step 1: Create install directory ────────────────────────
Write-Host "  [1/4] Creating installation folder..." -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null

# ── Step 2: Copy / extract files ────────────────────────────
Write-Host "  [2/4] Copying application files..." -ForegroundColor Yellow

if (Test-Path $ZipFile) {
    # Extract from ZIP if available
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($ZipFile, $InstallDir)
    Write-Host "        Extracted from $ZipFile" -ForegroundColor DarkGray
}
else {
    # Copy directly from publish folder
    Copy-Item -Path "$SourceDir\*" -Destination $InstallDir -Recurse -Force
    Write-Host "        Copied from $SourceDir" -ForegroundColor DarkGray
}

$ExePath = Join-Path $InstallDir $ExeName
if (-not (Test-Path $ExePath)) {
    Write-Host ""
    Write-Host "  ERROR: $ExeName not found after copy. Installation failed." -ForegroundColor Red
    exit 1
}

# ── Step 3: Create shortcuts ─────────────────────────────────
Write-Host "  [3/4] Creating shortcuts..." -ForegroundColor Yellow

$WshShell = New-Object -ComObject WScript.Shell

# Desktop shortcut
$DesktopLnk = [System.IO.Path]::Combine([System.Environment]::GetFolderPath('Desktop'), "$AppName.lnk")
$Shortcut = $WshShell.CreateShortcut($DesktopLnk)
$Shortcut.TargetPath = $ExePath
$Shortcut.WorkingDirectory = $InstallDir
$Shortcut.Description = "Gakungu Community Water Management System"
$Shortcut.Save()
Write-Host "        Desktop shortcut created." -ForegroundColor DarkGray

# Start Menu shortcut
$StartMenuDir = [System.IO.Path]::Combine(
    [System.Environment]::GetFolderPath('StartMenu'), "Programs", "Gakungu Water")
New-Item -ItemType Directory -Force -Path $StartMenuDir | Out-Null
$StartLnk = Join-Path $StartMenuDir "$AppName.lnk"
$Shortcut2 = $WshShell.CreateShortcut($StartLnk)
$Shortcut2.TargetPath = $ExePath
$Shortcut2.WorkingDirectory = $InstallDir
$Shortcut2.Description = "Gakungu Community Water Management System"
$Shortcut2.Save()
Write-Host "        Start Menu shortcut created." -ForegroundColor DarkGray

# ── Step 4: Write uninstall registry entry ───────────────────
Write-Host "  [4/4] Registering uninstall entry..." -ForegroundColor Yellow

$UninstallScript = Join-Path $InstallDir "Uninstall.ps1"
@"
# Uninstall Gakungu Water Management System
Remove-Item -Path "$([System.IO.Path]::Combine([System.Environment]::GetFolderPath('Desktop'), "$AppName.lnk"))" -Force -ErrorAction SilentlyContinue
Remove-Item -Path "$StartMenuDir" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "$RegKey" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "$InstallDir" -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "Gakungu Water has been uninstalled." -ForegroundColor Green
"@ | Set-Content $UninstallScript

New-Item -Path $RegKey -Force | Out-Null
Set-ItemProperty -Path $RegKey -Name "DisplayName"     -Value $AppName
Set-ItemProperty -Path $RegKey -Name "DisplayVersion"  -Value $AppVersion
Set-ItemProperty -Path $RegKey -Name "Publisher"       -Value "Gakungu Community Water Project"
Set-ItemProperty -Path $RegKey -Name "InstallLocation" -Value $InstallDir
Set-ItemProperty -Path $RegKey -Name "UninstallString" -Value "powershell -ExecutionPolicy Bypass -File `"$UninstallScript`""
Set-ItemProperty -Path $RegKey -Name "NoModify"        -Value 1 -Type DWord
Set-ItemProperty -Path $RegKey -Name "NoRepair"        -Value 1 -Type DWord
Set-ItemProperty -Path $RegKey -Name "EstimatedSize"  -Value ([math]::Round((Get-ChildItem $InstallDir -Recurse | Measure-Object -Property Length -Sum).Sum / 1KB)) -Type DWord

Write-Host "        Registered in Apps & Features." -ForegroundColor DarkGray

# ── Done ──────────────────────────────────────────────────────
Write-Host ""
Write-Host ("-" * 65) -ForegroundColor DarkGray
Write-Host ""
Write-Host "  ✅  Installation complete!" -ForegroundColor Green
Write-Host ""
Write-Host "  Default login:  admin / Admin@123" -ForegroundColor White
Write-Host "  Change your password immediately after first login." -ForegroundColor DarkYellow
Write-Host ""

if (-not $Silent) {
    $Launch = Read-Host "  Launch Gakungu Water now? (Y/N)"
    if ($Launch -match "^[Yy]") {
        Start-Process $ExePath
    }
}

Write-Host ""

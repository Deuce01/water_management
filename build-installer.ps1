# ============================================================
#  Gakungu Water — Build + Package Installer
#  Run this script from the root "GAKUNGU WATER" folder.
#  Requirements:
#    - .NET 9 SDK  (dotnet --version must show 9.x)
#    - Inno Setup 6  (https://jrsoftware.org/isdl.php)
# ============================================================

$ErrorActionPreference = "Stop"

$ProjectDir   = "$PSScriptRoot\GakunguWater"
$PublishDir   = "$PSScriptRoot\publish"
$SetupScript  = "$PSScriptRoot\setup.iss"
$InnoCompiler = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

Write-Host ""
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  Gakungu Water — Build & Package" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host ""

# ── Step 1: Clean previous publish ──────────────────────────
Write-Host "[1/3] Cleaning previous publish output..." -ForegroundColor Yellow
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }

# ── Step 2: Publish self-contained single-file exe ──────────
Write-Host "[2/3] Publishing self-contained application..." -ForegroundColor Yellow

dotnet publish "$ProjectDir\GakunguWater.csproj" `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    --output "$PublishDir"

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: dotnet publish failed. Aborting." -ForegroundColor Red
    exit 1
}

Write-Host "   Published to: $PublishDir" -ForegroundColor Green

# ── Step 3: Compile Inno Setup installer ────────────────────
Write-Host "[3/3] Compiling installer with Inno Setup..." -ForegroundColor Yellow

if (-not (Test-Path $InnoCompiler)) {
    Write-Host ""
    Write-Host "  Inno Setup not found at:" -ForegroundColor Red
    Write-Host "  $InnoCompiler" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Download and install Inno Setup 6 from:" -ForegroundColor Yellow
    Write-Host "  https://jrsoftware.org/isdl.php" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  Then re-run this script." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  The published files are ready at:" -ForegroundColor Green
    Write-Host "  $PublishDir" -ForegroundColor Green
    exit 0
}

& $InnoCompiler "$SetupScript"

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Inno Setup compilation failed." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "======================================================" -ForegroundColor Green
Write-Host "  SUCCESS!  Installer created in:  .\installer\  " -ForegroundColor Green
Write-Host "======================================================" -ForegroundColor Green
Write-Host ""

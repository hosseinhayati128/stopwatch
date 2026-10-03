<#
.SYNOPSIS
    Cross-platform packaging script for Stopwatch Overlay (Windows & Linux).
.DESCRIPTION
    Builds single-file releases for Windows (win-x64) and Linux (linux-x64),
    bundles install scripts and desktop assets, and prepares distribution packages.
#>

param(
    [ValidateSet("all", "windows", "linux")]
    [string]$Target = "all",

    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Resolve-Path "$PSScriptRoot\.."
$OutputDir = Join-Path $RepoRoot "dist"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Stopwatch Overlay Distribution Packager" -ForegroundColor Cyan
Write-Host " Target: $Target | Config: $Configuration" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if (Test-Path $OutputDir) {
    Remove-Item $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDir | Out-Null

# ----------------- WINDOWS PACKAGING -----------------
if ($Target -in @("all", "windows")) {
    Write-Host ""
    Write-Host "[1/2] Building Windows distribution (win-x64)..." -ForegroundColor Yellow
    $WinOut = Join-Path $OutputDir "windows-x64"
    
    dotnet publish "$RepoRoot\StopwatchOverlay.Desktop\StopwatchOverlay.Desktop.csproj" `
        -c $Configuration `
        -r win-x64 `
        --self-contained false `
        -p:PublishSingleFile=true `
        -o $WinOut

    Write-Host "[OK] Windows publish complete at: $WinOut" -ForegroundColor Green
}

# ----------------- LINUX PACKAGING -----------------
if ($Target -in @("all", "linux")) {
    Write-Host ""
    Write-Host "[2/2] Building Linux distribution (linux-x64 self-contained)..." -ForegroundColor Yellow
    $LinuxOut = Join-Path $OutputDir "linux-x64"
    
    dotnet publish "$RepoRoot\StopwatchOverlay.Desktop\StopwatchOverlay.Desktop.csproj" `
        -c $Configuration `
        -r linux-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $LinuxOut

    # Copy Linux desktop integration files
    $PackagingDir = Join-Path $RepoRoot "packaging\linux"
    if (Test-Path $PackagingDir) {
        Copy-Item "$PackagingDir\stopwatch-overlay.desktop" "$LinuxOut\" -Force
        Copy-Item "$PackagingDir\install.sh" "$LinuxOut\" -Force
        Copy-Item "$PackagingDir\uninstall.sh" "$LinuxOut\" -Force
    }

    $LogoPng = Join-Path $RepoRoot "StopwatchOverlay\project-logo-24.png"
    if (Test-Path $LogoPng) {
        Copy-Item $LogoPng "$LinuxOut\stopwatch-overlay.png" -Force
    }

    # Rename binary to standard lowercase linux command name if present
    $DesktopBin = Join-Path $LinuxOut "StopwatchOverlay.Desktop"
    if (Test-Path $DesktopBin) {
        Copy-Item $DesktopBin "$LinuxOut\stopwatch-overlay" -Force
    }

    Write-Host "[OK] Linux publish complete at: $LinuxOut" -ForegroundColor Green
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " All distribution packages built!" -ForegroundColor Green
Write-Host " Output directory: $OutputDir" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

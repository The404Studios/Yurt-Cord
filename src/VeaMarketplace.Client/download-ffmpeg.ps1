#Requires -Version 5.1
<#
.SYNOPSIS
    Downloads and installs FFmpeg dependencies for YurtCord hardware encoding.

.DESCRIPTION
    This script downloads the latest FFmpeg shared libraries from BtbN's builds
    and extracts the necessary DLLs for hardware-accelerated video encoding.

.NOTES
    Supported hardware encoders:
    - NVENC (NVIDIA GeForce/Quadro GPUs)
    - AMF (AMD Radeon GPUs)
    - QSV (Intel integrated/discrete GPUs)
    - Software fallback (libx264)
#>

[CmdletBinding()]
param(
    [switch]$Force,
    [string]$OutputPath = (Join-Path $PSScriptRoot "ffmpeg")
)

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# Configuration
$FfmpegUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl-shared.zip"
$TempDir = Join-Path $env:TEMP "ffmpeg-download-$(Get-Random)"
$ZipFile = Join-Path $TempDir "ffmpeg.zip"

# Required DLLs for encoding/decoding
$RequiredDlls = @(
    "avcodec-*.dll",
    "avutil-*.dll",
    "avformat-*.dll",
    "avdevice-*.dll",
    "avfilter-*.dll",
    "swscale-*.dll",
    "swresample-*.dll"
)

function Write-Header {
    Write-Host ""
    Write-Host "============================================" -ForegroundColor Cyan
    Write-Host " FFmpeg Dependency Downloader for YurtCord" -ForegroundColor Cyan
    Write-Host "============================================" -ForegroundColor Cyan
    Write-Host ""
}

function Write-Step {
    param([int]$Step, [int]$Total, [string]$Message)
    Write-Host "[$Step/$Total] " -ForegroundColor Yellow -NoNewline
    Write-Host $Message
}

function Test-ExistingInstall {
    if (Test-Path $OutputPath) {
        $existingDlls = Get-ChildItem -Path $OutputPath -Filter "*.dll" -ErrorAction SilentlyContinue
        if ($existingDlls.Count -gt 0) {
            return $true
        }
    }
    return $false
}

try {
    Write-Header

    # Check for existing installation
    if ((Test-ExistingInstall) -and -not $Force) {
        Write-Host "FFmpeg is already installed at: $OutputPath" -ForegroundColor Green
        Write-Host "Use -Force to reinstall." -ForegroundColor Gray
        Write-Host ""
        $existingDlls = Get-ChildItem -Path $OutputPath -Filter "*.dll"
        Write-Host "Installed DLLs ($($existingDlls.Count)):" -ForegroundColor Gray
        $existingDlls | ForEach-Object { Write-Host "  - $($_.Name)" -ForegroundColor Gray }
        exit 0
    }

    # Create directories
    Write-Step 1 5 "Creating directories..."
    New-Item -ItemType Directory -Path $TempDir -Force | Out-Null
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

    # Download FFmpeg
    Write-Step 2 5 "Downloading FFmpeg (this may take a few minutes)..."
    Write-Host "       URL: $FfmpegUrl" -ForegroundColor Gray

    $webClient = New-Object System.Net.WebClient
    $webClient.DownloadFile($FfmpegUrl, $ZipFile)

    $fileSize = (Get-Item $ZipFile).Length / 1MB
    Write-Host "       Downloaded: $([math]::Round($fileSize, 2)) MB" -ForegroundColor Gray

    # Extract ZIP
    Write-Step 3 5 "Extracting archive..."
    Expand-Archive -Path $ZipFile -DestinationPath $TempDir -Force

    # Find extracted folder
    $extractedDir = Get-ChildItem -Path $TempDir -Directory | Where-Object { $_.Name -like "ffmpeg-*" } | Select-Object -First 1
    if (-not $extractedDir) {
        throw "Could not find extracted FFmpeg folder"
    }

    $binDir = Join-Path $extractedDir.FullName "bin"
    if (-not (Test-Path $binDir)) {
        throw "Could not find bin directory in extracted archive"
    }

    # Copy DLLs
    Write-Step 4 5 "Copying DLL files..."
    $copiedCount = 0

    foreach ($pattern in $RequiredDlls) {
        $dlls = Get-ChildItem -Path $binDir -Filter $pattern -ErrorAction SilentlyContinue
        foreach ($dll in $dlls) {
            Copy-Item -Path $dll.FullName -Destination $OutputPath -Force
            Write-Host "       Copied: $($dll.Name)" -ForegroundColor Gray
            $copiedCount++
        }
    }

    # Also copy any other DLLs that might be needed
    $otherDlls = Get-ChildItem -Path $binDir -Filter "*.dll" | Where-Object {
        $name = $_.Name
        -not ($RequiredDlls | Where-Object { $name -like $_ })
    }
    foreach ($dll in $otherDlls) {
        Copy-Item -Path $dll.FullName -Destination $OutputPath -Force
        Write-Host "       Copied: $($dll.Name)" -ForegroundColor Gray
        $copiedCount++
    }

    # Cleanup
    Write-Step 5 5 "Cleaning up temporary files..."
    Remove-Item -Path $TempDir -Recurse -Force -ErrorAction SilentlyContinue

    # Success
    Write-Host ""
    Write-Host "============================================" -ForegroundColor Green
    Write-Host " SUCCESS! FFmpeg installed successfully." -ForegroundColor Green
    Write-Host "============================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "$copiedCount DLL files copied to:" -ForegroundColor White
    Write-Host "  $OutputPath" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Supported hardware encoders:" -ForegroundColor White
    Write-Host "  - h264_nvenc  (NVIDIA GeForce/Quadro)" -ForegroundColor Gray
    Write-Host "  - h264_amf   (AMD Radeon)" -ForegroundColor Gray
    Write-Host "  - h264_qsv   (Intel integrated/discrete)" -ForegroundColor Gray
    Write-Host "  - libx264    (Software fallback)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "YurtCord will automatically detect and use the best available encoder." -ForegroundColor White
    Write-Host ""

} catch {
    Write-Host ""
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Manual download instructions:" -ForegroundColor Yellow
    Write-Host "1. Download from: $FfmpegUrl" -ForegroundColor Gray
    Write-Host "2. Extract the ZIP file" -ForegroundColor Gray
    Write-Host "3. Copy all DLLs from the 'bin' folder to: $OutputPath" -ForegroundColor Gray
    Write-Host ""

    # Cleanup on error
    if (Test-Path $TempDir) {
        Remove-Item -Path $TempDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    exit 1
}

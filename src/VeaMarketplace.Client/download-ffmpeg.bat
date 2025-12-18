@echo off
setlocal enabledelayedexpansion

echo ============================================
echo  FFmpeg Dependency Downloader for Plugin
echo ============================================
echo.

:: Set variables
set "FFMPEG_URL=https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl-shared.zip"
set "DOWNLOAD_DIR=%~dp0ffmpeg-temp"
set "OUTPUT_DIR=%~dp0ffmpeg"
set "ZIP_FILE=%DOWNLOAD_DIR%\ffmpeg.zip"

:: Check for PowerShell (needed for download)
where powershell >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo ERROR: PowerShell is required but not found.
    echo Please install PowerShell or download FFmpeg manually.
    pause
    exit /b 1
)

:: Create directories
echo [1/4] Creating directories...
if not exist "%DOWNLOAD_DIR%" mkdir "%DOWNLOAD_DIR%"
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

:: Download FFmpeg
echo [2/4] Downloading FFmpeg (this may take a few minutes)...
echo       URL: %FFMPEG_URL%
powershell -Command "& {[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -Uri '%FFMPEG_URL%' -OutFile '%ZIP_FILE%' -UseBasicParsing}"

if not exist "%ZIP_FILE%" (
    echo ERROR: Download failed. Please check your internet connection.
    echo You can manually download from: %FFMPEG_URL%
    pause
    exit /b 1
)

echo       Download complete!

:: Extract ZIP
echo [3/4] Extracting files...
powershell -Command "& {Expand-Archive -Path '%ZIP_FILE%' -DestinationPath '%DOWNLOAD_DIR%' -Force}"

:: Copy DLLs to output directory
echo [4/4] Copying DLL files to ffmpeg folder...

:: Find the extracted folder (it has a dynamic name)
for /d %%i in ("%DOWNLOAD_DIR%\ffmpeg-*") do (
    set "EXTRACTED_DIR=%%i"
)

if not defined EXTRACTED_DIR (
    echo ERROR: Could not find extracted FFmpeg folder.
    pause
    exit /b 1
)

:: Copy all DLLs from bin folder
copy /Y "!EXTRACTED_DIR!\bin\*.dll" "%OUTPUT_DIR%\" >nul 2>&1

:: Verify files were copied
set "DLL_COUNT=0"
for %%f in ("%OUTPUT_DIR%\*.dll") do set /a DLL_COUNT+=1

if %DLL_COUNT% equ 0 (
    echo ERROR: No DLL files were copied. Extraction may have failed.
    pause
    exit /b 1
)

:: Cleanup
echo.
echo Cleaning up temporary files...
rmdir /s /q "%DOWNLOAD_DIR%" >nul 2>&1

:: Success
echo.
echo ============================================
echo  SUCCESS! FFmpeg installed successfully.
echo ============================================
echo.
echo %DLL_COUNT% DLL files copied to: %OUTPUT_DIR%
echo.
echo Installed files:
dir /b "%OUTPUT_DIR%\*.dll"
echo.
echo You can now run Plugin with hardware encoding support.
echo.
echo Supported encoders:
echo   - NVENC (NVIDIA GPUs)
echo   - AMF (AMD GPUs)
echo   - QSV (Intel GPUs)
echo   - Software fallback (libx264)
echo.
pause

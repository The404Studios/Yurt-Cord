@echo off
echo Cleaning and rebuilding Yurt Cord solution...
echo.

cd /d "%~dp0"

echo Step 1: Cleaning solution...
dotnet clean VeaMarketplace.sln
if errorlevel 1 goto error

echo.
echo Step 2: Restoring NuGet packages...
dotnet restore VeaMarketplace.sln
if errorlevel 1 goto error

echo.
echo Step 3: Building solution...
dotnet build VeaMarketplace.sln --no-incremental
if errorlevel 1 goto error

echo.
echo ========================================
echo Build completed successfully!
echo ========================================
echo.
echo You can now open the solution in Visual Studio.
echo The designer errors should be resolved.
echo.
pause
exit /b 0

:error
echo.
echo ========================================
echo Build failed! Please check the errors above.
echo ========================================
echo.
pause
exit /b 1

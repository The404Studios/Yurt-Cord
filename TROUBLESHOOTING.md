# Troubleshooting Guide

## Common Issues and Solutions

### ❌ Error: "The name 'LoginView' does not exist in the namespace"

**Symptoms:**
- Visual Studio shows XDG0008 error
- Designer shows errors for XAML files
- IntelliSense doesn't recognize existing views

**Cause:**
This is a Visual Studio designer cache issue that occurs after adding new files or making structural changes.

**Solution 1: Quick Fix (Recommended)**
1. Run the `rebuild.bat` script in the root directory:
   ```cmd
   rebuild.bat
   ```

**Solution 2: Manual Fix**
1. Close Visual Studio completely
2. Delete the following folders:
   - `bin/` directories in all projects
   - `obj/` directories in all projects
   - `.vs/` folder in solution root
3. Open Visual Studio
4. Restore NuGet packages: Right-click solution → Restore NuGet Packages
5. Build → Rebuild Solution

**Solution 3: Command Line**
```cmd
dotnet clean
dotnet restore
dotnet build --no-incremental
```

**Why This Happens:**
- Visual Studio's XAML designer caches type information
- When new files are added, the cache may not update immediately
- The actual code compiles fine - it's only a designer issue

### ✅ Verification
After rebuilding, you should see:
- No red squiggles in XAML files
- Designer preview works
- IntelliSense recognizes all types
- Solution builds without errors

---

## Other Common Issues

### Issue: Project Won't Build

**Error Messages:**
- "Could not copy..."
- "File is being used by another process"
- Assembly load errors

**Solutions:**
1. Close all Visual Studio instances
2. Kill any lingering processes:
   ```cmd
   taskkill /f /im YurtCord.exe
   taskkill /f /im dotnet.exe
   ```
3. Clean and rebuild

### Issue: NuGet Package Errors

**Solution:**
```cmd
dotnet restore --force
dotnet nuget locals all --clear
dotnet restore
```

### Issue: SignalR Connection Fails

**Check:**
1. Is the server running?
   ```cmd
   cd src/VeaMarketplace.Server
   dotnet run
   ```
2. Server should be at: `http://localhost:5000`
3. Check firewall settings
4. Check appsettings.json for correct URLs

### Issue: Voice/Audio Not Working

**Check:**
1. Microphone permissions in Windows Settings
2. Default audio devices in Windows Sound settings
3. NAudio package is installed
4. Audio devices show in settings panel

### Issue: Screen Sharing Not Working

**Check:**
1. Windows display permissions
2. Multiple monitors detected
3. Graphics driver up to date
4. Sufficient system resources

---

## Development Tips

### Hot Reload Issues
If hot reload isn't working:
1. Disable hot reload: Tools → Options → Debugging → Enable Hot Reload
2. Or manually restart the application

### XAML Designer Slow
1. Disable designer: Right-click XAML → Open With → Source Code (Text) Editor
2. Or use lightweight XAML editor

### Build Performance
Speed up builds:
```xml
<!-- Add to Directory.Build.props -->
<PropertyGroup>
  <DebugType>portable</DebugType>
  <DebugSymbols>true</DebugSymbols>
</PropertyGroup>
```

---

## Getting Help

If issues persist:
1. Check error logs in:
   - `%LocalAppData%/VeaMarketplace/`
   - Output window in Visual Studio

2. Enable detailed logging:
   ```json
   // appsettings.Development.json
   {
     "Logging": {
       "LogLevel": {
         "Default": "Debug"
       }
     }
   }
   ```

3. Create an issue on GitHub with:
   - Full error message
   - Steps to reproduce
   - System information
   - Build output

---

## Quick Reference

### Build Commands
```cmd
# Clean
dotnet clean

# Restore packages
dotnet restore

# Build
dotnet build

# Run server
cd src/VeaMarketplace.Server && dotnet run

# Run client
cd src/VeaMarketplace.Client && dotnet run

# Rebuild everything
dotnet build --no-incremental
```

### Project Structure
```
VeaMarketplace/
├── src/
│   ├── VeaMarketplace.Shared/    # Models, DTOs, Enums
│   ├── VeaMarketplace.Server/    # ASP.NET Core backend
│   └── VeaMarketplace.Client/    # WPF desktop app
├── rebuild.bat                    # Quick rebuild script
├── README.md                      # Project overview
├── FEATURES.md                    # Feature documentation
└── TROUBLESHOOTING.md            # This file
```

---

**Last Updated:** December 11, 2025

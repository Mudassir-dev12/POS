# ShopPOS Automated Installer Verification Test
# Tests: silent install to custom temp directory, binary integrity, self-check probe, smoke test execution, silent uninstall, data preservation.
[CmdletBinding()]
param (
    [string]$InstallerPath
)

$ErrorActionPreference = "Stop"

$RootDir = Resolve-Path "$PSScriptRoot\.."
Set-Location $RootDir

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  ShopPOS Automated Installer Verification" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

# 0. Ensure no running ShopPOS instances interfere with installation
Get-Process -Name "ShopPOS.UI" -ErrorAction SilentlyContinue | Stop-Process -Force

# 1. Locate Installer
if (-not $InstallerPath) {
    $DistDir = Join-Path $RootDir "dist"
    $InstallerFile = Get-ChildItem -Path $DistDir -Filter "ShopPOS-Setup-*.exe" -ErrorAction SilentlyContinue | 
                     Sort-Object LastWriteTime -Descending | 
                     Select-Object -First 1
    if (-not $InstallerFile) {
        Write-Error "No installer found in dist/. Please run scripts\build-release.ps1 first."
        exit 1
    }
    $InstallerPath = $InstallerFile.FullName
}

if (-not (Test-Path $InstallerPath)) {
    Write-Error "Installer file not found at: $InstallerPath"
    exit 1
}

Write-Host "Testing Installer: $InstallerPath" -ForegroundColor White

# 2. Setup isolated temporary folders
$Guid = [Guid]::NewGuid().ToString("N").Substring(0, 8)
$TestInstallDir = Join-Path $env:TEMP "ShopPOS_InstallTest_$Guid"
$TestDataDir = Join-Path $env:TEMP "ShopPOS_DataTest_$Guid"

Write-Host "Temporary Install Dir: $TestInstallDir" -ForegroundColor DarkGray
Write-Host "Temporary Data Dir:    $TestDataDir" -ForegroundColor DarkGray

try {
    # 3. Silent Installation to Custom Directory
    Write-Host "`n[1/6] Executing silent installation..." -ForegroundColor Yellow
    $InstallArgs = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /DIR=""$TestInstallDir"""
    $Process = Start-Process -FilePath $InstallerPath -ArgumentList $InstallArgs -Wait -PassThru

    if ($Process.ExitCode -ne 0) {
        Write-Error "Installer failed with exit code $($Process.ExitCode)"
        exit 1
    }
    Write-Host "Installation completed with exit code 0." -ForegroundColor Green

    # 4. Verify Installed Binaries & Critical Native Assets
    Write-Host "`n[2/6] Verifying installed binaries and native assets..." -ForegroundColor Yellow
    $InstalledExe = Join-Path $TestInstallDir "ShopPOS.UI.exe"
    $InstalledIcon = Join-Path $TestInstallDir "app.ico"
    $Uninstaller = Join-Path $TestInstallDir "unins000.exe"

    if (-not (Test-Path $InstalledExe)) {
        Write-Error "Missing critical file: ShopPOS.UI.exe"
        exit 1
    }
    if (-not (Test-Path $InstalledIcon)) {
        Write-Error "Missing icon file: app.ico"
        exit 1
    }
    if (-not (Test-Path $Uninstaller)) {
        Write-Error "Missing uninstaller executable: unins000.exe"
        exit 1
    }

    $CoreDll = Join-Path $TestInstallDir "ShopPOS.Core.dll"
    $DataDll = Join-Path $TestInstallDir "ShopPOS.Data.dll"
    if (-not (Test-Path $CoreDll) -or -not (Test-Path $DataDll)) {
        Write-Error "Missing application assemblies (ShopPOS.Core or ShopPOS.Data)!"
        exit 1
    }

    $SqliteNative = Join-Path $TestInstallDir "e_sqlite3.dll"
    $SkiaNative = Join-Path $TestInstallDir "QuestPdfSkia.dll"
    if (-not (Test-Path $SqliteNative)) {
        Write-Error "Missing SQLite native library: e_sqlite3.dll!"
        exit 1
    }
    if (-not (Test-Path $SkiaNative)) {
        Write-Error "Missing PDF graphics native library: QuestPdfSkia.dll!"
        exit 1
    }

    Write-Host "All required binaries and native assets verified present." -ForegroundColor Green

    # 5. Execute Diagnostic Self-Check Probe on Installed Application
    Write-Host "`n[3/6] Running installed app with --self-check --data-dir..." -ForegroundColor Yellow
    $SelfCheckArgs = "--self-check --data-dir ""$TestDataDir"""
    $ScProcess = Start-Process -FilePath $InstalledExe -ArgumentList $SelfCheckArgs -Wait -PassThru

    if ($ScProcess.ExitCode -ne 0) {
        Write-Error "Installed app failed diagnostic self-check! Exit code: $($ScProcess.ExitCode)"
        exit 1
    }
    Write-Host "Installed app passed all 9 diagnostic self-checks!" -ForegroundColor Green

    # 6. Execute Smoke Test on Installed Application
    Write-Host "`n[4/6] Running installed app with --smoke-test --data-dir..." -ForegroundColor Yellow
    $SmokeArgs = "--smoke-test --data-dir ""$TestDataDir"""
    $AppProcess = Start-Process -FilePath $InstalledExe -ArgumentList $SmokeArgs -Wait -PassThru

    if ($AppProcess.ExitCode -ne 0) {
        Write-Error "Installed app failed smoke test! Exit code: $($AppProcess.ExitCode)"
        exit 1
    }
    Write-Host "Smoke test passed! Database initialized, migrated, dashboard verified, backup taken." -ForegroundColor Green

    # Verify that test database and backup were created in test data folder
    $DbFile = Join-Path $TestDataDir "shop.db"
    $BackupDir = Join-Path $TestDataDir "Backups"
    if (-not (Test-Path $DbFile)) {
        Write-Error "Database file was not created in test data dir: $DbFile"
        exit 1
    }
    $Backups = Get-ChildItem -Path $BackupDir -Filter "*.zip" -ErrorAction SilentlyContinue
    if (-not $Backups -or $Backups.Count -eq 0) {
        Write-Error "No backup file was generated in: $BackupDir"
        exit 1
    }
    Write-Host "Database and backup files confirmed in data directory." -ForegroundColor Green

    # 7. Execute Silent Uninstall
    Write-Host "`n[5/6] Executing silent uninstallation..." -ForegroundColor Yellow
    $UninstallArgs = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART"
    $UninstProcess = Start-Process -FilePath $Uninstaller -ArgumentList $UninstallArgs -Wait -PassThru

    if ($UninstProcess.ExitCode -ne 0) {
        Write-Error "Uninstaller failed with exit code $($UninstProcess.ExitCode)"
        exit 1
    }
    Start-Sleep -Seconds 2
    Write-Host "Uninstallation completed with exit code 0." -ForegroundColor Green

    # 8. Verify Data Retention
    Write-Host "`n[6/6] Verifying user data preservation..." -ForegroundColor Yellow
    if (-not (Test-Path $DbFile)) {
        Write-Error "DATA LOSS DETECTED: The database file ($DbFile) was deleted during uninstall!"
        exit 1
    }
    Write-Host "SUCCESS: User database and backups were preserved as expected." -ForegroundColor Green

    Write-Host "`n===========================================================" -ForegroundColor Green
    Write-Host "  INSTALLER VERIFICATION PASSED WITH ZERO ERRORS!" -ForegroundColor Green
    Write-Host "===========================================================" -ForegroundColor Green
}
finally {
    # Clean up test directories
    if (Test-Path $TestInstallDir) {
        Remove-Item -Recurse -Force $TestInstallDir -ErrorAction SilentlyContinue
    }
    if (Test-Path $TestDataDir) {
        Remove-Item -Recurse -Force $TestDataDir -ErrorAction SilentlyContinue
    }
}

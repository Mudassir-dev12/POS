# Automated test script executed inside Windows Sandbox
$ErrorActionPreference = "Continue"

$DistDir = "C:\dist"
$ResultsDir = "C:\results"

if (-not (Test-Path $ResultsDir)) {
    New-Item -ItemType Directory -Path $ResultsDir -Force | Out-Null
}

$LogFile = Join-Path $ResultsDir "sandbox-test-run.log"
Start-Transcript -Path $LogFile

Write-Host "=================================================="
Write-Host "  ShopPOS Clean Windows Sandbox Verification"
Write-Host "=================================================="

# 1. Locate Installer
$Installer = Get-ChildItem -Path $DistDir -Filter "ShopPOS-Setup-*.exe" | Sort-Object LastWriteTime -Descending | Select-Object -First 1

if (-not $Installer) {
    Write-Error "Installer executable not found in mapped C:\dist!"
    Stop-Transcript
    exit 1
}

Write-Host "Running Installer: $($Installer.FullName)"

# 2. Silent Install
$InstallProc = Start-Process -FilePath $Installer.FullName -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART" -Wait -PassThru
Write-Host "Installer finished with exit code: $($InstallProc.ExitCode)"

$InstalledExe = "C:\Program Files\ShopPOS\ShopPOS.UI.exe"
if (-not (Test-Path $InstalledExe)) {
    Write-Error "ShopPOS.UI.exe was not installed to C:\Program Files\ShopPOS!"
    Stop-Transcript
    exit 1
}

# 3. Execute Self-Check on Clean Machine
Write-Host "`nRunning --self-check probe..."
$SelfCheckProc = Start-Process -FilePath $InstalledExe -ArgumentList "--self-check --data-dir C:\ShopData" -Wait -PassThru
Write-Host "Self-check completed with exit code: $($SelfCheckProc.ExitCode)"

# 4. Copy report to results folder
$ReportFile = "C:\ShopData\Logs\self-check-report.txt"
if (Test-Path $ReportFile) {
    Copy-Item $ReportFile -Destination (Join-Path $ResultsDir "self-check-report.txt") -Force
    Write-Host "`nSelf-Check Report Summary:"
    Get-Content $ReportFile
}

# 5. Execute Smoke Test
Write-Host "`nRunning automated --smoke-test..."
$SmokeProc = Start-Process -FilePath $InstalledExe -ArgumentList "--smoke-test --data-dir C:\ShopData" -Wait -PassThru
Write-Host "Smoke test completed with exit code: $($SmokeProc.ExitCode)"

Write-Host "`nSandbox verification finished."
Stop-Transcript


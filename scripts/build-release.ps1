# ShopPOS Release Build, Verification and Installer Packaging Pipeline
# Requires: .NET 10 SDK, Inno Setup 6+ / 7+
[CmdletBinding()]
param (
    [switch]$SkipTests,
    [string]$SignCertPath = $env:SHOPPOS_CERT_PATH,
    [string]$SignCertPassword = $env:SHOPPOS_CERT_PASSWORD
)

$ErrorActionPreference = "Stop"

$RootDir = Resolve-Path "$PSScriptRoot\.."
Set-Location $RootDir

Write-Host "===========================================================" -ForegroundColor Cyan
Write-Host "  ShopPOS Production Release Build & Packaging Pipeline" -ForegroundColor Cyan
Write-Host "===========================================================" -ForegroundColor Cyan

# 0. Ensure no running ShopPOS instances interfere
Get-Process -Name "ShopPOS.UI" -ErrorAction SilentlyContinue | Stop-Process -Force

# 1. Run Fast Test Suite
if (-not $SkipTests) {
    Write-Host "`n[1/8] Running Fast Test Suite..." -ForegroundColor Yellow
    dotnet test ShopPOS.Tests -c Release --filter "Category!=Slow" --nologo -v q
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Unit/Integration tests failed! Packaging aborted."
        exit $LASTEXITCODE
    }
    Write-Host "All tests passed successfully." -ForegroundColor Green
} else {
    Write-Host "`n[1/8] Skipping tests as requested (-SkipTests)." -ForegroundColor DarkGray
}

# 2. Verify Prerequisites
Write-Host "`n[2/8] Verifying bundled offline prerequisites..." -ForegroundColor Yellow
$PrereqFile = Join-Path $RootDir "prerequisites\vc_redist.x64.exe"
if (-not (Test-Path $PrereqFile)) {
    Write-Error "Missing prerequisite: prerequisites\vc_redist.x64.exe! Please refer to prerequisites\README.md to download the official Microsoft installer."
    exit 1
}

$ExpectedHash = "CC0FF0EB1DC3F5188AE6300FAEF32BF5BEEBA4BDD6E8E445A9184072096B713B"
$ActualHash = (Get-FileHash -Path $PrereqFile -Algorithm SHA256).Hash
if ($ActualHash -ne $ExpectedHash) {
    Write-Warning "Prerequisite vc_redist.x64.exe hash ($ActualHash) does not match expected ($ExpectedHash). Proceeding with caution."
} else {
    Write-Host "Verified Microsoft Visual C++ 2015-2022 Redistributable (x64) checksum." -ForegroundColor Green
}

# 3. Clean previous build artifacts
Write-Host "`n[3/8] Cleaning previous publish and distribution directories..." -ForegroundColor Yellow
$PublishDir = Join-Path $RootDir "publish"
$DistDir = Join-Path $RootDir "dist"

if (Test-Path $PublishDir) {
    Remove-Item -Recurse -Force $PublishDir
}
if (-not (Test-Path $DistDir)) {
    New-Item -ItemType Directory -Path $DistDir | Out-Null
}

# 4. Publish fully self-contained win-x64 app (normal folder distribution)
Write-Host "`n[4/8] Publishing ShopPOS.UI (win-x64, fully self-contained, trimming OFF)..." -ForegroundColor Yellow
dotnet publish ShopPOS.UI\ShopPOS.UI.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=false `
    -p:PublishReadyToRun=false `
    -p:DebugType=none `
    -p:SatelliteResourceLanguages=en `
    -o $PublishDir `
    --nologo -v q

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed!"
    exit $LASTEXITCODE
}
Write-Host "Self-contained publish complete: $PublishDir" -ForegroundColor Green

# 5. Verify presence of required native binaries
Write-Host "`n[5/8] Verifying required native DLLs in publish output..." -ForegroundColor Yellow
$RequiredNatives = @(
    "e_sqlite3.dll",
    "QuestPdfSkia.dll"
)

foreach ($nativeDll in $RequiredNatives) {
    $dllPath = Join-Path $PublishDir $nativeDll
    if (-not (Test-Path $dllPath)) {
        Write-Error "CRITICAL: Native binary '$nativeDll' is missing from publish directory ($dllPath)!"
        exit 1
    }
    $dllSize = (Get-Item $dllPath).Length
    Write-Host "  [OK] $nativeDll ($dllSize bytes)" -ForegroundColor Green
}

# 6. Execute Diagnostic Self-Check on Published Application
Write-Host "`n[6/8] Executing pre-packaging --self-check probe on published binaries..." -ForegroundColor Yellow
$SelfCheckTempDir = Join-Path $env:TEMP "ShopPOS_PublishSelfCheck_$(Get-Random)"
try {
    $ExePath = Join-Path $PublishDir "ShopPOS.UI.exe"
    $SelfCheckProcess = Start-Process -FilePath $ExePath -ArgumentList "--self-check --data-dir ""$SelfCheckTempDir""" -Wait -PassThru

    if ($SelfCheckProcess.ExitCode -ne 0) {
        Write-Error "Published application failed --self-check diagnostic probe! Exit code: $($SelfCheckProcess.ExitCode)"
        exit 1
    }
    Write-Host "Published application passed all 9 diagnostic self-checks!" -ForegroundColor Green
}
finally {
    if (Test-Path $SelfCheckTempDir) {
        Remove-Item -Recurse -Force $SelfCheckTempDir -ErrorAction SilentlyContinue
    }
}

# 7. Build Optional Portable Single-File Executable
Write-Host "`n[7/8] Building & testing optional portable single-file executable (ShopPOS-Portable.exe)..." -ForegroundColor Yellow
$PortableTempDir = Join-Path $env:TEMP "ShopPOS_PortableBuild_$(Get-Random)"
$PortableFinalExe = Join-Path $DistDir "ShopPOS-Portable.exe"
$PortablePassed = $false

try {
    dotnet publish ShopPOS.UI\ShopPOS.UI.csproj `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:PublishTrimmed=false `
        -p:PublishReadyToRun=false `
        -p:DebugType=none `
        -p:SatelliteResourceLanguages=en `
        -o $PortableTempDir `
        --nologo -v q

    if ($LASTEXITCODE -eq 0) {
        $BuiltPortable = Join-Path $PortableTempDir "ShopPOS.UI.exe"
        if (Test-Path $BuiltPortable) {
            # Test portable single-file with --self-check
            $TestPortableData = Join-Path $env:TEMP "ShopPOS_PortableSelfCheck_$(Get-Random)"
            $PortCheckProc = Start-Process -FilePath $BuiltPortable -ArgumentList "--self-check --data-dir ""$TestPortableData""" -Wait -PassThru
            if ($TestPortableData -and (Test-Path $TestPortableData)) {
                Remove-Item -Recurse -Force $TestPortableData -ErrorAction SilentlyContinue
            }

            if ($PortCheckProc.ExitCode -eq 0) {
                Copy-Item -Path $BuiltPortable -Destination $PortableFinalExe -Force
                $PortablePassed = $true
                Write-Host "Portable single-file executable passed self-check: $PortableFinalExe" -ForegroundColor Green
            } else {
                Write-Warning "Portable single-file executable failed self-check (Exit code: $($PortCheckProc.ExitCode)). Shipping installer only."
            }
        }
    }
}
catch {
    Write-Warning "Portable build encountered an error: $_. Shipping installer only."
}
finally {
    if (Test-Path $PortableTempDir) {
        Remove-Item -Recurse -Force $PortableTempDir -ErrorAction SilentlyContinue
    }
}

# 8. Compile Inno Setup Installer
Write-Host "`n[8/8] Locating Inno Setup Compiler & building installer..." -ForegroundColor Yellow
$IsccCandidates = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 7\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 7\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
)

$IsccPath = $null
foreach ($candidate in $IsccCandidates) {
    if (Test-Path $candidate) {
        $IsccPath = $candidate
        break
    }
}

if (-not $IsccPath) {
    $cmd = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($cmd) {
        $IsccPath = $cmd.Source
    }
}

if (-not $IsccPath) {
    Write-Error "Inno Setup Compiler (ISCC.exe) was not found! Please install Inno Setup 6 or 7."
    exit 1
}

Write-Host "Found ISCC at: $IsccPath" -ForegroundColor Green
$IssScript = Join-Path $RootDir "installer\ShopPOS.iss"
& $IsccPath $IssScript

if ($LASTEXITCODE -ne 0) {
    Write-Error "Inno Setup compilation failed!"
    exit $LASTEXITCODE
}

# 9. Verify Generated Installer with test-installer.ps1
Write-Host "`nRunning automated installer verification (test-installer.ps1)..." -ForegroundColor Yellow
$TestInstallerScript = Join-Path $RootDir "scripts\test-installer.ps1"
& $TestInstallerScript
if ($LASTEXITCODE -ne 0) {
    Write-Error "Automated installer verification test failed!"
    exit $LASTEXITCODE
}

# 10. Summary & Output Display
$InstallerExe = Get-ChildItem -Path $DistDir -Filter "ShopPOS-Setup-*.exe" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$InstallerHash = (Get-FileHash -Path $InstallerExe.FullName -Algorithm SHA256).Hash
$InstallerSizeMb = [math]::Round($InstallerExe.Length / 1MB, 2)

Write-Host "`n===========================================================" -ForegroundColor Green
Write-Host "  BUILD & PACKAGING SUCCESSFUL!" -ForegroundColor Green
Write-Host "===========================================================" -ForegroundColor Green
Write-Host "1. Windows Installer: $($InstallerExe.FullName)" -ForegroundColor White
Write-Host "   Size:              $InstallerSizeMb MB" -ForegroundColor White
Write-Host "   SHA-256:           $InstallerHash" -ForegroundColor White

if ($PortablePassed -and (Test-Path $PortableFinalExe)) {
    $PortHash = (Get-FileHash -Path $PortableFinalExe -Algorithm SHA256).Hash
    $PortSizeMb = [math]::Round((Get-Item $PortableFinalExe).Length / 1MB, 2)
    Write-Host "`n2. Portable Single-File: $PortableFinalExe" -ForegroundColor White
    Write-Host "   Size:                 $PortSizeMb MB" -ForegroundColor White
    Write-Host "   SHA-256:              $PortHash" -ForegroundColor White
}
Write-Host "===========================================================" -ForegroundColor Green

[CmdletBinding()]
param(
    [switch]$TestPrint,
    [switch]$Interactive
)

$ErrorActionPreference = "Stop"

Clear-Host
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  ShopPOS - Epson Thermal Receipt Printer Setup & Detector" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Administrator check
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "[!] Administrative permissions required." -ForegroundColor Yellow
    Write-Host "    Elevating permissions..." -ForegroundColor Yellow
    Start-Process powershell -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs
    exit
}

# 2. Print Spooler Service
Write-Host "[1/6] Checking Windows Print Spooler service..." -ForegroundColor Yellow
try {
    $spooler = Get-Service -Name "Spooler" -ErrorAction SilentlyContinue
    if (-not $spooler -or $spooler.Status -ne "Running") {
        Set-Service -Name "Spooler" -StartupType Automatic
        Start-Service -Name "Spooler"
        Write-Host "      Print Spooler started successfully (Set to Automatic)." -ForegroundColor Green
    } else {
        Set-Service -Name "Spooler" -StartupType Automatic
        Write-Host "      Print Spooler is running (Set to Automatic)." -ForegroundColor Green
    }
} catch {
    Write-Host "      [ERROR] Could not start Print Spooler: $($_.Exception.Message)" -ForegroundColor Red
}

# 3. Physical USB Device Detection
Write-Host "[2/6] Detecting connected Epson USB thermal printer..." -ForegroundColor Yellow
$pnpDevice = Get-PnpDevice | Where-Object { $_.InstanceId -like "*04B8*" -or $_.FriendlyName -like "*TM-T88*" -or $_.FriendlyName -like "*EPSON USB*" } | Select-Object -First 1

if ($pnpDevice) {
    Write-Host "      Detected hardware: $($pnpDevice.FriendlyName)" -ForegroundColor Green
    Write-Host "      Hardware ID      : $($pnpDevice.InstanceId)" -ForegroundColor Gray
    Write-Host "      Hardware Status  : $($pnpDevice.Status)" -ForegroundColor Gray
} else {
    Write-Host "      [!] No Epson USB device detected." -ForegroundColor Magenta
    Write-Host "          Ensure the USB cable is firmly connected and the printer power switch is ON." -ForegroundColor Gray
}

# Resolve paths
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (Test-Path (Join-Path $scriptDir "Drivers")) {
    $projectRoot = $scriptDir
} else {
    $projectRoot = Split-Path -Parent $scriptDir
}

$tmusbInf = Join-Path $projectRoot "Drivers\TMUSB64\TMUSB64.inf"
$printerInf = Join-Path $projectRoot "Drivers\Epson_TM-T88V\EA5INSTMT88V.INF"
$driverName = "EPSON TM-T88V Receipt5"
$printerName = "EPSON TM-T88V Receipt5"

# 4. Install TMUSB Kernel Driver
Write-Host "[3/6] Registering Epson TMUSB device controller driver..." -ForegroundColor Yellow
if (Test-Path $tmusbInf) {
    try {
        & pnputil /add-driver "$tmusbInf" /install | Out-Null
        Write-Host "      Epson TMUSB kernel driver is installed & active." -ForegroundColor Green
    } catch {
        Write-Host "      [WARN] TMUSB registration: $($_.Exception.Message)" -ForegroundColor Yellow
    }
} else {
    Write-Host "      [WARN] TMUSB driver not found at: $tmusbInf" -ForegroundColor Yellow
}

# Restart device if needed to apply driver
if ($pnpDevice) {
    try {
        & pnputil /restart-device "$($pnpDevice.InstanceId)" | Out-Null
    } catch {}
}

# 5. Install Spooler Print Driver
Write-Host "[4/6] Registering EPSON TM-T88V Receipt5 print driver..." -ForegroundColor Yellow
if (Test-Path $printerInf) {
    try {
        & pnputil /add-driver "$printerInf" /install | Out-Null
        $installedDrivers = Get-PrinterDriver | Select-Object -ExpandProperty Name
        if ($installedDrivers -notcontains $driverName) {
            Add-PrinterDriver -Name $driverName
        }
        Write-Host "      Print driver '$driverName' registered in Spooler." -ForegroundColor Green
    } catch {
        Write-Host "      [WARN] Print driver registration: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

# 6. Configure Windows Printer Queue
Write-Host "[5/6] Verifying Windows Printer Queue..." -ForegroundColor Yellow
$portName = "USB001"

try {
    $ports = Get-PrinterPort | Select-Object -ExpandProperty Name
    if ($ports -notcontains $portName) {
        Add-PrinterPort -Name $portName -ErrorAction SilentlyContinue
    }
} catch {}

$printers = Get-Printer | Select-Object -ExpandProperty Name
if ($printers -notcontains $printerName) {
    try {
        Add-Printer -Name $printerName -DriverName $driverName -PortName $portName
        Write-Host "      Created printer queue '$printerName' on port '$portName'." -ForegroundColor Green
    } catch {
        Write-Host "      [ERROR] Could not create printer queue: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "      Printer queue '$printerName' is ready." -ForegroundColor Green
}

# 7. Update ShopPOS Database Setting
Write-Host "[6/6] Updating ShopPOS application settings..." -ForegroundColor Yellow
$dbPath = "C:\ShopData\shop.db"
if (Test-Path $dbPath) {
    try {
        $pyScript = "import sqlite3; conn = sqlite3.connect(r'$dbPath'); c = conn.cursor(); c.execute('UPDATE ShopSettings SET ReceiptPrinterName = ?, ReceiptWidthMm = 80', ('$printerName',)); conn.commit(); conn.close()"
        python -c "$pyScript"
        Write-Host "      ShopPOS configured: Default Receipt Printer = '$printerName' (80mm)." -ForegroundColor Green
    } catch {
        Write-Host "      [WARN] Could not update ShopSettings in database: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "  Epson TM-T88V Setup Finished Successfully!" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Printer Name : $printerName" -ForegroundColor White
Write-Host "  Driver Name  : $driverName" -ForegroundColor White
Write-Host "  Port         : $portName" -ForegroundColor White
Write-Host "  App Setting  : Configured in ShopPOS (80mm)" -ForegroundColor White
Write-Host ""

if ($TestPrint) {
    Write-Host "Sending test print to $printerName..." -ForegroundColor Cyan
    rundll32.exe printui.dll,PrintUIEntry /k /n "$printerName"
}

if ($Interactive) {
    Write-Host "Options:" -ForegroundColor Cyan
    Write-Host "  [1] Send Windows Test Page"
    Write-Host "  [2] Open Printer Properties (Ports / Settings)"
    Write-Host "  [3] Exit"
    Write-Host ""
    $choice = Read-Host "Choose an option (1-3) or press Enter to exit"
    if ($choice -eq "1") {
        rundll32.exe printui.dll,PrintUIEntry /k /n "$printerName"
        Write-Host "Test page command sent to printer queue." -ForegroundColor Green
        Start-Sleep -Seconds 2
    } elseif ($choice -eq "2") {
        rundll32.exe printui.dll,PrintUIEntry /p /n "$printerName"
    }
}


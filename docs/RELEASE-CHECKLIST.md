# ShopPOS Release & Packaging Checklist
*Complete Pre-flight, Build, Diagnostic, and Clean-PC Verification Guide*

---

## 1. Supported Operating Systems & System Requirements
- **Supported Versions**: 64-bit Windows 10 Version 1809 (Build 17763) or higher, and 64-bit Windows 11.
- **Architecture**: `win-x64` (64-bit Intel / AMD processors).
- **Dependencies Preinstalled**: **NONE**. The application is 100% self-contained:
  - Bundled .NET 10 Desktop Runtime (no external .NET install needed).
  - Bundled SQLite Native Engine (`e_sqlite3.dll`).
  - Bundled Vector PDF & Skia graphics engine (`QuestPdfSkia.dll`).
  - Bundled Microsoft Visual C++ 2015-2022 Redistributable (silently installed by setup only if missing).
- **Administrative Rights**: Needed **only during installation** (writes to `C:\Program Files\ShopPOS`). The application runs without administrator rights.
- **Data Storage**: Defaults to `C:\ShopData`. If access is restricted, automatically falls back to `%LocalAppData%\ShopPOS\Data`.

---

## 2. Release Build Command

Run the complete automated packaging pipeline from PowerShell:
```powershell
powershell -ExecutionPolicy Bypass -File scripts\build-release.ps1
```

### What the Build Script Does:
1. Runs the test suite (`dotnet test ShopPOS.Tests -c Release --filter "Category!=Slow"`).
2. Verifies `prerequisites\vc_redist.x64.exe` offline binary and SHA-256 hash.
3. Cleans previous artifacts.
4. Publishes fully self-contained win-x64 binaries (trimming OFF).
5. Verifies required native DLLs (`e_sqlite3.dll`, `QuestPdfSkia.dll`).
6. Runs `--self-check` diagnostic probe on published binaries.
7. Builds and tests optional single-file `dist\ShopPOS-Portable.exe`.
8. Compiles `dist\ShopPOS-Setup-v1.0.0.exe` using Inno Setup (LZMA2 ultra solid compression).
9. Runs automated installer test: silent install, installed `--self-check`, `--smoke-test`, silent uninstall, and verifies data retention.
10. Prints final file sizes and SHA-256 checksums.

### Expected Artifact Sizes:
- **Installer**: `dist\ShopPOS-Setup-v1.0.0.exe` (~85 - 95 MB, includes self-contained runtime + offline VC++ redistributable).
- **Portable**: `dist\ShopPOS-Portable.exe` (~90 - 100 MB, single-file self-extracting).

---

## 3. Microsoft Defender SmartScreen Notice
For unsigned release binaries:
- Windows Defender SmartScreen may display: *"Windows protected your PC: Microsoft Defender SmartScreen prevented an unrecognized app from starting."*
- Click **More info**, then click **Run anyway**.
- To prevent this warning in commercial releases, sign the installer using an Authenticode code signing certificate:
  ```powershell
  signtool.exe sign /f "path\to\cert.pfx" /p "<password>" /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 "dist\ShopPOS-Setup-v1.0.0.exe"
  ```

---

## 4. How to Test in Windows Sandbox
Windows Sandbox provides a pristine, disposable Windows environment with no developer tools, .NET, or VC++ runtimes.

1. Double-click `scripts\test-in-sandbox.wsb` (or run `wsb.exe scripts\test-in-sandbox.wsb`).
2. The sandbox boots, maps `dist\` read-only to `C:\dist`, and automatically runs `scripts\sandbox-test.ps1`.
3. The script:
   - Silently installs `ShopPOS-Setup-*.exe`.
   - Executes `C:\Program Files\ShopPOS\ShopPOS.UI.exe --self-check --data-dir C:\ShopData`.
   - Executes `ShopPOS.UI.exe --smoke-test`.
   - Writes the self-check report and execution transcript to `sandbox-results\`.
4. Check `sandbox-results\self-check-report.txt` on the host to verify all checks passed.

*Note: For Windows 10 Home or environments where Windows Sandbox is unavailable, follow the manual steps in `scripts\test-in-vm.md`.*

---

## 5. How to Read Logs and Self-Check Reports

### A. Early Startup Error Log (`startup-error.log`)
If the application encounters a fatal error before main logging or Dependency Injection starts:
- **Location**: `%LocalAppData%\ShopPOS\startup-error.log`
- **Contents**: Formatted header, timestamp, OS version, architecture, plain-language advice, and full stack trace.
- **GUI Display**: Shows an error dialog with the plain-language advice, the log path, and a **Copy details** button.

### B. Diagnostic Self-Check Report (`self-check-report.txt`)
- **Location**: `C:\ShopData\Logs\self-check-report.txt` and `%LocalAppData%\ShopPOS\self-check-report.txt`
- **Generation**:
  - Automatically created when running with CLI flag: `ShopPOS.UI.exe --self-check`
  - In GUI: Settings -> About & Diagnostics -> Click **Run Self Check**
- **Report Interpretation**:
  - `[PASS] 64-Bit Architecture`: Process and OS are 64-bit.
  - `[PASS] .NET Desktop Runtime`: .NET 10.x runtime operational.
  - `[PASS] SQLite Native Engine`: `e_sqlite3.dll` loaded; executes SQLite version query.
  - `[PASS] QuestPDF & SkiaSharp Graphics`: Native graphics engine renders test vector PDF.
  - `[PASS] Data Directory & Storage`: Data directory writable and >= 500 MB free disk space.
  - `[PASS] Printer Subsystem`: Windows Print Spooler responsive.
  - `[PASS] Font Subsystem`: System font families enumerated.
  - `[PASS] Database Engine & Schema`: Database connected and migrations applied.
  - `[PASS] Database Backup Engine`: Online streaming backup verified.

---

## 6. Troubleshooting Table

| Error Message / Symptom | Root Cause | Resolution |
|---|---|---|
| `DllNotFoundException: Unable to load DLL 'e_sqlite3'` | Microsoft Visual C++ 2015-2022 Redistributable (x64) is missing on the target machine. | Reinstall using `ShopPOS-Setup.exe` (which silently installs the runtime), or run `prerequisites\vc_redist.x64.exe`. |
| `DllNotFoundException: Unable to load DLL 'QuestPdfSkia.dll'` | VC++ runtime missing or `QuestPdfSkia.dll` not present in app folder. | Reinstall using setup or install VC++ redistributable. |
| `BadImageFormatException: An attempt was made to load a program with an incorrect format` | 32-bit (x86) Windows OS or 32-bit component conflict. | ShopPOS requires a 64-bit OS. Ensure you are running 64-bit Windows 10/11. |
| `UnauthorizedAccessException: Access to the path 'C:\ShopData' is denied` | Current user account lacks administrative permission to write to `C:\`. | The application automatically falls back to `%LocalAppData%\ShopPOS\Data`. Or specify a custom folder in `appsettings.user.json` or with `--data-dir <path>`. |
| `IOException: The process cannot access the file because it is being used by another process` | Another running instance of ShopPOS or backup task has locked `shop.db`. | Close other running instances. The installer automatically detects running instances via `Global\ShopPOS_SingleInstance_Mutex`. |
| `QuestPDF Exception: Font families not available` | QuestPDF missing fallback fonts. | Handled automatically: `QuestPDF.Settings.UseSystemFonts = true` and `ThrowOnMissingFontFamilies = false` are configured in all report engines. |
| `ShopPOS requires 64-bit Windows 10 Version 1809 (Build 17763) or Windows 11` | Setup launched on unsupported Windows 7, 8, or early Windows 10 builds. | Upgrade target system to Windows 10 build 17763 (1809) or Windows 11. |
| Application does not open; no error on screen | Early boot crash before window creation. | Inspect `%LocalAppData%\ShopPOS\startup-error.log` for full diagnostic details and plain-language guidance. |

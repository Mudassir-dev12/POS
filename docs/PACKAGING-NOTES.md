# ShopPOS Packaging & Clean-PC Deployment Notes

## 1. Executive Summary
This document analyzes the exact root causes of deployment failures observed when running ShopPOS on clean, newly installed Windows 10 or Windows 11 machines (systems without Visual Studio, .NET SDK/Runtime, SQLite, Visual C++ redistributables, or developer tooling installed). It outlines the architecture and packaging pipeline implemented to guarantee 100% offline, zero-dependency, non-admin execution.

---

## 2. Root Causes of Failures on Clean Target PCs

### A. Missing Microsoft Visual C++ 2015-2022 Redistributable
- **Fault**: The application relies on native C/C++ binaries:
  - `e_sqlite3.dll` (SQLitePCLRaw bundle for SQLite database persistence)
  - `QuestPdfSkia.dll` (Skia and HarfBuzz native engine for vector receipt/invoice PDF generation)
- **Symptom**: On a virgin Windows 10/11 PC, attempting to open the database or generate receipts threw `DllNotFoundException` (Win32 Error 126: `ERROR_MOD_NOT_FOUND` / `0x8007007E`) or `TypeInitializationException`.
- **Cause**: The previous build script attempted to copy `vcruntime140.dll` and `msvcp140.dll` directly from the developer's `C:\Windows\System32`. Loose file copying fails on clean machines because modern MSVC runtimes require proper Side-by-Side (SxS) activation, universal CRT (UCRT) integration, and registry registration.
- **Fix**: The installer bundles the official Microsoft `vc_redist.x64.exe` (SHA-256: `CC0FF0EB1DC3F5188AE6300FAEF32BF5BEEBA4BDD6E8E445A9184072096B713B`) in `prerequisites\`. During setup, Inno Setup checks the registry key:
  `HKLM\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64` (`Installed = 1`)
  If missing, it runs `vc_redist.x64.exe /install /quiet /norestart` silently before the application finishes installing.

---

### B. Access Denied on `C:\ShopData` (UAC & Standard User Permissions)
- **Fault**: Clean Windows 10/11 machines enforce strict User Account Control (UAC). Standard (non-admin) users cannot create or write files to the root directory `C:\`.
- **Symptom**: If installed to `C:\Program Files\ShopPOS`, running the application under a standard user account caused `Directory.CreateDirectory(@"C:\ShopData")` to throw `UnauthorizedAccessException: Access to the path 'C:\ShopData' is denied`.
- **Fix**: The application never requires administrative rights to run. In `UserSettingsService.ResolveDataDirectory`, the application checks whether `C:\ShopData` is writable. If access is denied or `C:\ShopData` cannot be created, it automatically falls back to:
  `%LocalAppData%\ShopPOS\Data`
  and logs the active path for the user.

---

### C. Silent Startup Crashes (Lack of Early Bootstrap Exception Handler)
- **Fault**: Any crash occurring prior to Serilog initialization or Dependency Injection setup (e.g., missing assembly, native DLL failure, corrupt config file, path access denied) was swallowed or resulted in an unhelpful Windows Error Reporting dialog with no diagnostic output.
- **Fix**: Added a resilient `BootstrapCrashHandler` at the very entry point of `App.xaml.cs`. It catches all early exceptions, writes full details to `%LocalAppData%\ShopPOS\startup-error.log`, translates exception types into actionable, plain-language guidance (such as "Visual C++ runtime missing: reinstall using setup"), and displays an error dialog with a "Copy details" button.

---

### D. Single-File / Trimming / Native Library Extraction Issues
- **Fault**:
  - `PublishTrimmed=true` is incompatible with WPF, EF Core SQLite, and JSON reflection serializers, stripping required types.
  - Single-file executables (`PublishSingleFile=true`) cannot execute native DLLs in memory unless configured with `IncludeNativeLibrariesForSelfExtract=true` to extract native binaries (`e_sqlite3.dll`, `QuestPdfSkia.dll`) into a per-user temp cache.
- **Fix**:
  - Primary Installer Build: Published as a self-contained folder (`PublishTrimmed=false`, `PublishSingleFile=false`), which is the most reliable distribution model for desktop Windows.
  - Portable Single-File Build: Configured with `IncludeNativeLibrariesForSelfExtract=true` and verified via automated self-check.

---

### E. Missing Runtime Content & Configuration Files
- **Fault**: `googledrive.client.sample.json`, `appsettings.json`, and application icons were not systematically tracked in the publish output.
- **Fix**: Explicit MSBuild `CopyToOutputDirectory` and Inno Setup `[Files]` entries guarantee that all required configurations, icons, and sample files are copied to the application directory.

---

## 3. Deployment Artifact Matrix

| Artifact | Purpose | Target Environment |
|---|---|---|
| `ShopPOS-Setup-<version>.exe` | Primary production installer (Inno Setup 64-bit) | Clean Windows 10 (1809+) & Windows 11 x64 |
| `vc_redist.x64.exe` (bundled) | VC++ 2015-2022 Redistributable (offline silent install) | Only executed if registry check reports missing |
| `ShopPOS-Portable-win-x64.zip` | Standalone portable folder package | Extracted and run anywhere without installation |
| `ShopPOS.UI.exe --self-check` | Pre-flight diagnostic probe | Runs in CI, Sandbox, and by users in Diagnostics UI |


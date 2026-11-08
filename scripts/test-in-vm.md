# Clean Machine & VM Testing Guide for ShopPOS

This guide provides step-by-step instructions for verifying ShopPOS on a virgin, clean Windows 10 or Windows 11 virtual machine (VirtualBox, VMware, Hyper-V, or physical PC) that has **nothing preinstalled** (no .NET SDK/Runtime, no Visual C++ Redistributable, no SQLite, and no developer tools).

---

## Prerequisites for the Test Environment
1. A fresh Windows 10 (Version 1809 or higher, 64-bit) or Windows 11 installation.
2. User account: Standard (non-administrator) account, or Administrator with default UAC enabled.
3. Network connection: Can be **completely offline / disconnected** (ShopPOS is 100% offline).

---

## Test Steps

### Step 1: Copy Installer
- Copy `ShopPOS-Setup-<version>.exe` from `dist\` to the VM (e.g. to the Desktop or Downloads folder).

### Step 2: Install ShopPOS
1. Double-click `ShopPOS-Setup-<version>.exe`.
2. Observe standard Windows UAC elevation prompt ("Do you want to allow this app to make changes to your device?").
3. Verify Setup checks Windows version:
   - On Windows 10 1809+ or Windows 11, setup continues smoothly.
   - On older Windows (e.g. Windows 7/8 or Windows 10 build < 17763), setup halts with:
     `ShopPOS requires 64-bit Windows 10 Version 1809 (Build 17763) or Windows 11.`
4. Observe prerequisites installation:
   - If Microsoft Visual C++ 2015-2022 Redistributable (x64) is missing on the VM, setup silently executes the bundled `vc_redist.x64.exe`.
5. Verify default installation directory is `C:\Program Files\ShopPOS`.
6. Complete setup.

### Step 3: Run Without Admin Rights
1. Launch ShopPOS from the Start Menu or Desktop shortcut.
2. Confirm the application opens **without requiring administrator rights**.
3. Confirm that if `C:\ShopData` cannot be created or written by standard user permissions, the application automatically falls back to `%LocalAppData%\ShopPOS\Data`.

### Step 4: First-Run Setup Wizard
1. The Setup Wizard appears on first launch.
2. Set Shop Name, Currency, and choose "Load demo data to try the software" (or "Start empty").
3. Click "Complete Setup & Launch".
4. Confirm dashboard loads with correct currency formatting.

### Step 5: Core Workflow Verification
1. **POS Sale**: Navigate to Sales (POS). Add products to the cart, select Payment (Cash), and complete checkout.
2. **PDF Receipt**: View and generate a PDF sales receipt. Confirm vector graphics and fonts render properly without missing font exceptions.
3. **Product Stock Edit**: Navigate to Products, edit a product's stock quantity, save. Confirm no database tracking exceptions occur.
4. **Backup**: Navigate to Settings -> Backup & Restore -> Create Backup. Confirm backup creates a `.zip` in the Backups folder.

### Step 6: Diagnostic Self-Check Probe
1. **From GUI**: Open Settings -> About & Diagnostics -> Click "Run Self Check".
   - Confirm all 9 diagnostic checks display `[PASS]`.
2. **From Command Line**: Open Command Prompt / PowerShell:
   ```cmd
   "C:\Program Files\ShopPOS\ShopPOS.UI.exe" --self-check
   ```
   - Confirm output exits with code 0 and prints `[PASS]` for all items.

### Step 7: Uninstall & Data Preservation
1. Open Windows Settings -> Apps -> Installed Apps (or Control Panel -> Programs and Features).
2. Uninstall ShopPOS.
3. When prompted:
   `Do you want to permanently delete your shop database and backup files?`
   Select **NO**.
4. Verify that `C:\Program Files\ShopPOS` is removed, but your shop database (`shop.db`) and backups in `C:\ShopData` (or `%LocalAppData%\ShopPOS\Data`) are **fully preserved**.


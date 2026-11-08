# ShopPOS User Guide
*Complete Operating Manual for Store Owners and Cashiers*

---

## Table of Contents
1. [Overview & System Requirements](#1-overview--system-requirements)
2. [Installation & Setup](#2-installation--setup)
3. [First-Run Setup Wizard](#3-first-run-setup-wizard)
4. [Daily POS Operations](#4-daily-pos-operations)
   - [Starting the Day](#starting-the-day)
   - [Making a Sale](#making-a-sale)
   - [Barcode Scanning & Quick Search](#barcode-scanning--quick-search)
   - [Payment Methods (Cash, Card, Split)](#payment-methods)
   - [Printing & Receipts](#printing--receipts)
   - [Sales Returns & Refunds](#sales-returns--refunds)
5. [Inventory & Purchasing](#5-inventory--purchasing)
   - [Products, Categories & Units](#products-categories--units)
   - [Recording Purchase Invoices](#recording-purchase-invoices)
   - [Stock Adjustments & Low Stock Warnings](#stock-adjustments--low-stock-warnings)
6. [Expenses & Supplier Management](#6-expenses--supplier-management)
7. [Reports & Business Analytics](#7-reports--business-analytics)
8. [Data Backup, Google Drive & Safety](#8-data-backup-google-drive--safety)
   - [Automatic Local Backups](#automatic-local-backups)
   - [Secondary Backup Drive (USB Drive)](#secondary-backup-drive)
   - [Encrypted Backups](#encrypted-backups)
   - [Cloud Sync with Google Drive](#cloud-sync-with-google-drive)
   - [Restoring from a Backup](#restoring-from-a-backup)
   - [Moving ShopPOS to a New PC](#moving-shoppos-to-a-new-pc)
9. [Settings, Maintenance & Diagnostics](#9-settings-maintenance--diagnostics)
   - [Creating a Support Bundle](#creating-a-support-bundle)
   - [Checking for Updates](#checking-for-updates)
10. [Troubleshooting & Frequently Asked Questions](#10-troubleshooting--faq)

---

## 1. Overview & System Requirements

ShopPOS is an offline-first, high-performance desktop point of sale application designed for single-workstation retail stores. All data is kept securely on your local computer, allowing operations to continue normally without an internet connection.

### Hardware & Software Requirements
- **Operating System**: Windows 10 (64-bit) or Windows 11 (64-bit).
- **Processor**: Intel Core i3 (6th Gen+) or AMD Ryzen 3 or equivalent.
- **Memory (RAM)**: 4 GB minimum (8 GB recommended).
- **Free Disk Space**: At least 1 GB for application and database growth.
- **Display**: Minimum screen resolution 1280 x 720 (1920 x 1080 recommended).
- **Peripherals (Optional)**:
  - Standard USB Barcode Scanner (HID keyboard emulation).
  - Thermal Receipt Printer (58mm or 80mm ESC/POS or Windows Driver).
  - ESC/POS Cash Drawer connected via receipt printer RJ11/RJ12 port.

---

## 2. Installation & Setup

1. Double-click the installer: `ShopPOS-Setup-v1.0.0.exe`.
2. Windows User Account Control (UAC) will prompt for administrator permissions. Click **Yes**.
3. Choose standard installation options:
   - Create a desktop shortcut.
   - Start ShopPOS automatically when Windows boots (optional).
4. Click **Install**. The setup installs all required components to `C:\Program Files\ShopPOS`.
5. Check **Launch ShopPOS** and click **Finish**.

> [!NOTE]
> ShopPOS keeps all your business data, database files, and backups in `C:\ShopData` (or your chosen data directory). Reinstalling or updating ShopPOS will never overwrite or erase your shop data.

---

## 3. First-Run Setup Wizard

When launching ShopPOS on a new computer without an existing database, the **First-Run Setup Wizard** opens automatically. Nothing is saved or modified until you click **Finish** on the final step.

### Step 1: Welcome
Choose one of two options:
- **Set up a new shop**: Start fresh for a new business.
- **Restore from a backup**: If you have an existing backup file (from a USB flash drive, external disk, or Google Drive), you can restore it directly. Once restored, the wizard finishes and takes you to your store.

### Step 2: Shop Profile
Enter your store details. These details appear on receipt headers and report printouts:
- **Shop Name** *(Required)*: The official name of your shop.
- **Address, Phone, Tax Number / NTN**: Optional details for receipts.

### Step 3: Currency & Taxes
- **Currency Symbol**: Defaults to `Rs`.
- **Decimals**: Standard decimal precision (default `2`).
- **Default Tax %**: Set your local sales tax percentage (e.g., `0%` or `17%`).
- **Prices Include Tax**: Check if your displayed shelf prices already include tax.
- **Cash Rounding**: Set rounding step for cash change calculation.

### Step 4: Receipt Printer
Configure your receipt printer or skip this step to configure later in Settings:
- Choose output mode: **Thermal ESC/POS**, **Windows Driver**, or **PDF Preview**.
- Select the installed printer and paper width (**80mm** or **58mm**).
- Click **Print Test Slip** to verify communication with your printer.

### Step 5: Data Folder Location
- By default, ShopPOS stores data in `C:\ShopData`.
- You may select a different folder or hard drive (e.g., `D:\ShopData`).
- The wizard automatically verifies that the directory is writable and has at least 500 MB of free storage.

### Step 6: Backup & Google Drive Protection
- **Enable Daily Automated Backups**: Backs up your database every evening.
- **Secondary Folder**: Designate a connected USB drive or external hard drive for a second automatic copy.
- **Optional Password Encryption**: Secure backup files using AES-256 encryption.
- **Google Drive**: Optionally sign in to Google Drive to keep cloud backups synced automatically.

### Step 7: Starting Data & Completion
- **Start Empty**: Creates clean database tables ready for your products.
- **Load Demo Data**: Populates sample categories, products, and suppliers so you can test all features right away.
- Click **Finish Setup** to create the database and open the POS Dashboard.

---

## 4. Daily POS Operations

### Starting the Day
1. Open ShopPOS from your desktop shortcut or Start Menu.
2. The Dashboard displays daily summary figures: Today's Sales, Transactions, Low Stock Alerts, and recent receipts.
3. Click **Point of Sale** (or press `F1`) on the sidebar to open the checkout screen.

### Making a Sale
1. Focus is automatically placed in the barcode and search box.
2. **Scan a barcode** or type the product name/SKU and press `Enter` to add it to the cart.
3. To change quantity: click the quantity field or use `+` / `-` buttons.
4. You may select **Retail** or **Wholesale** price level if applicable.
5. Apply line-item discounts or overall cart discount if authorized.

### Payment Methods
1. Click **Checkout (F12)** or press `F12`.
2. Choose payment method:
   - **Cash**: Enter the amount received. The exact change to return is calculated automatically.
   - **Card**: Enter the authorization / approval code if needed.
   - **Split Payment**: Split the transaction between cash and card.
3. Click **Complete Sale**. The cash drawer opens and the receipt prints automatically.

### Sales Returns & Refunds
1. Open **Sales** from the sidebar.
2. Locate the invoice by receipt number or date.
3. Click **Return / Refund**.
4. Select the items and quantities returned, choose refund payment method (Cash or Card), and confirm.
5. Stock inventory levels are automatically credited back to your store.

---

## 5. Inventory & Purchasing

### Products, Categories & Units
- Open **Inventory > Products** to add new items.
- Set Cost Price, Retail Selling Price, and Wholesale Price.
- Assign SKU and Barcode (EAN-13, Code 128, etc.).
- Set Reorder Level: when stock drops below this threshold, ShopPOS highlights the item on the dashboard.

### Recording Purchase Invoices
1. Navigate to **Purchases > New Purchase**.
2. Select the Supplier and enter the supplier's invoice number.
3. Add purchased products, quantities, and cost prices.
4. Record amount paid to the supplier (Full, Partial, or Credit).
5. Completing the purchase updates your stock inventory instantly and creates purchase audit trails.

---

## 6. Expenses & Supplier Management

- **Operating Expenses**: Track daily shop overheads such as Electricity, Rent, Staff Wages, Tea & Refreshments, and Transport.
- Click **Expenses > Add Expense**, pick a category, amount, payment method, and optional receipt reference.
- **Supplier Balances**: Review outstanding supplier payables under **Suppliers** and record ledger payments.

---

## 7. Reports & Business Analytics

ShopPOS provides instant financial insights with date filters (Today, Yesterday, This Week, This Month, Custom):
- **Sales Summary**: Total revenue, cost of goods sold (COGS), gross profit, transaction count, average ticket size.
- **Profit & Loss**: Gross profit minus registered operating expenses equals Net Profit.
- **Inventory Valuation**: Current total cost value and retail asset value of inventory on hand.
- **Low Stock Report**: List of items needing reorder.
- **PDF Export**: Print or export any report to an archival PDF with your store header.

---

## 8. Data Backup, Google Drive & Safety

Your database contains all your sales, inventory, and financial history. ShopPOS is built with multi-layered safety.

### Automatic Local Backups
- ShopPOS creates a full database snapshot daily at the scheduled time (default 10:00 PM) and whenever the application closes.
- Backups are non-blocking: they use the SQLite online backup engine, so cashiers can continue ringing up sales without interruption or lockups.
- Verified integrity checks (`PRAGMA integrity_check` and foreign key verification) ensure no backup file is corrupt.

### Secondary Backup Drive (USB Drive)
- In **Settings > Backups**, set a **Second Backup Folder** pointing to a USB flash drive (e.g., `E:\ShopPOS_Backups`).
- Every time a backup is created, an identical copy is saved to the USB drive. Keep this USB drive safe or take it home at the end of the day.

### Encrypted Backups
- In **Settings > Backups**, enable **Encrypt Backups**.
- Choose a strong password. All backup files will be encrypted using standard AES-256 before writing to disk.
- *Caution*: There is no backdoor. Keep your password written down in a secure place.

### Cloud Sync with Google Drive
- Connect your Google Drive account in **Settings > Backups**.
- ShopPOS automatically uploads new backups to a private folder named `ShopPOS Backups` on your Google Drive in the background whenever an internet connection is available.
- If internet is unavailable, backups queue up safely and upload automatically when the connection is restored.
- ShopPOS only accesses the files it creates (`drive.file` scope). It cannot access your other personal Google Drive documents.

### Restoring from a Backup
1. Go to **Settings > Backups**.
2. Select any local backup from the list or click **Restore from External File**.
3. A pre-restore safety snapshot of your current database is created automatically.
4. Confirm the restore. The database is restored, verified, and refreshed immediately.

### Moving ShopPOS to a New PC
1. On the old PC, click **Settings > Backups > Backup Now**.
2. Copy the latest `.bak` file from `C:\ShopData\Backups` onto a USB drive.
3. Install ShopPOS on the new PC.
4. On the **First-Run Setup Wizard**, choose **Restore from a backup**, select your `.bak` file on the USB drive, and click Restore.
5. All your data, inventory, sales, and settings will appear on the new PC instantly.

---

## 9. Settings, Maintenance & Diagnostics

### Creating a Support Bundle
If you ever need technical assistance, you can generate a diagnostics support bundle:
1. Open **Settings > About**.
2. Click **Create Support File (Diagnostics)**.
3. Choose a destination folder to save `ShopPOS_Support_<Date>.zip`.
4. This file contains:
   - Application version, Windows OS version, .NET version.
   - Database table row counts and schema version.
   - The last 7 days of application logs.
   - **Privacy Guarantee**: The support file NEVER includes your database, customer data, sales records, passwords, or cloud credentials.

### Checking for Updates
1. Open **Settings > About**.
2. View your current version and build date.
3. Click **Check for Updates** to visit the official release portal.

---

## 10. Troubleshooting & FAQ

### Q: What if the power cuts off during a sale?
**A:** ShopPOS uses SQLite Write-Ahead Logging (WAL) with strict transactional boundaries. If the computer loses power, unfinished transactions rollback cleanly. No database corruption occurs.

### Q: "Disk space is low" warning at startup.
**A:** ShopPOS requires at least 500 MB of free disk space on the drive hosting your data folder. Free up space on the drive to proceed safely.

### Q: "Database is locked by another process".
**A:** Ensure another instance of ShopPOS isn't running in Task Manager. Restarting the computer clears any stale operating system file locks.

### Q: The barcode scanner is not inputting data.
**A:** Most standard barcode scanners operate in USB HID Keyboard mode. Verify that scanning a barcode in Notepad outputs the number followed by an Enter key.

### Q: How do I change the receipt paper size?
**A:** Open **Settings > Receipt Printer** and change the paper width setting between `58mm` and `80mm`.


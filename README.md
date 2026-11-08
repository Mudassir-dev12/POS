# ShopPOS - Single-User Offline-First Windows POS & Retail ERP

A modern, offline-first Windows desktop Point of Sale (POS), Master Data Management, Inventory, Purchases, and Receipt Printing system built for single-shop retailers and owner-operated counters running on Windows 10/11.

The application is designed specifically for **SINGLE USER** operation on **ONE computer** (Owner only: no login screen, no roles/RBAC, no shift management, no credit customer ledger). All counter sales are walk-in transactions paid in full.

---

## 1. Solution Structure

The solution `ShopPOS.sln` is organized into a clean, decoupled 4-layer architecture:

```
C:\Projects\ShopPOS\
├── ShopPOS.Core/          # Class Library: Domain Entities, Enums, Interfaces, Domain Services, DTOs, Validators
│   ├── Common/            # BaseEntity (Id, CreatedAt, UpdatedAt, CreatedByUserId, IsDeleted)
│   ├── DTOs/              # ProductDtos, MasterDataDtos, PurchaseDtos, InventoryDtos, SalesDtos, ReceiptDocument
│   ├── Entities/          # User, ShopSetting, Category, Brand, Unit, Product, ProductBarcode,
│   │                      # ProductPriceHistory, PriceLevel, ProductPrice, Supplier, StockMovement,
│   │                      # Purchase, PurchaseItem, SupplierPayment, PurchaseReturn, PurchaseReturnItem,
│   │                      # StockAdjustment, Sale, SaleItem, SalePayment, SaleReturn, SaleReturnItem, AuditLog
│   ├── Enums/             # PaymentMethod, StockMovementType, StockStatus, PurchaseStatus, SaleStatus, SaleReturnStatus
│   ├── Interfaces/        # ICurrentUserProvider, IShopSettingService, IAuditLogService, IProductService,
│   │                      # ICategoryService, IBrandService, IUnitService, ISupplierService, IPriceLevelService,
│   │                      # ISaleService, ISaleReturnService, ISaleCalculator, IReceiptPrinterService, etc.
│   ├── Models/            # Result, Result<T>, PagedResult<T>, DatabaseOptions, LoggingOptions
│   ├── Services/          # CurrentUserProvider, SaleService, SaleReturnService, SaleCalculator, MasterDataServices, etc.
│   ├── Validators/        # FluentValidation for Products, Purchases, Sales, SaleReturns, StockAdjustments
│   └── CoreServiceRegistration.cs
│
├── ShopPOS.Data/          # Class Library: EF Core SQLite, Configurations, Repositories, Migrations
│   ├── Configurations/    # EntityTypeConfigurations (Money/Quantity Converters, Unique Indexes, Relations)
│   ├── Converters/        # MoneyConverter (minor units x100), QuantityConverter (thousandths x1000)
│   ├── Interceptors/      # SqlitePragmaInterceptor (WAL mode & Foreign Keys ON on connection open)
│   ├── Migrations/        # EF Core Migrations (InitialCreate -> Remove_Shifts_And_Customers)
│   ├── Repositories/      # ProductRepository, SaleRepository, SaleReturnRepository, PurchaseRepository, etc.
│   ├── Seeding/           # IDataSeeder, DataSeeder (Units, Fixed Owner User, Category, ShopSettings, PriceLevels)
│   ├── Services/          # DatabaseBackupHelper (safe pre-migration backups), CsvProductService, NumberSequenceService
│   ├── AppDbContext.cs    # EF Core DbContext with global soft-delete query filter
│   ├── AppDbContextFactory.cs # Design-time factory for `dotnet ef` CLI
│   └── DataServiceRegistration.cs
│
├── ShopPOS.UI/            # WPF (.NET 10) Application: Views, ViewModels, DI Host, Navigation, Dialogs
│   ├── Helpers/           # ValueConverters (Currency, Status, LowStock, NonZeroVisibility)
│   ├── Services/          # INavigationService, NavigationService, IDialogService, DialogService, ReceiptPrinterService
│   ├── ViewModels/        # PosSaleViewModel, SalesViewModel, SaleReturnsViewModel, DashboardViewModel,
│   │                      # ProductsViewModel, PurchasesViewModel, StockOverviewViewModel, SettingsViewModel, etc.
│   ├── Views/             # PosSaleView, SalesView, SaleReturnsView, DashboardView, ProductsView,
│   │                      # PurchasesView, StockOverviewView, SettingsView, Dialogs
│   ├── appsettings.json   # Configurable database path and Serilog file destination
│   ├── App.xaml / App.xaml.cs # Host Builder, Serilog file sink, Pre-Migration Backup, Direct Dashboard Startup
│   └── MainWindow.xaml / MainWindow.xaml.cs # Responsive Main Shell with Sidebar Navigation
│
├── ShopPOS.Tests/         # xUnit Test Project
│   ├── StructuralRemovalsTests.cs             # Migration integrity, backup creation, drawer breakdown, full payment
│   ├── DatabaseMigrationAndSeedDatabaseTests.cs # SQLite migrations, seed data, converters, soft delete
│   ├── MasterDataServiceTests.cs              # SKU auto-generation, barcode uniqueness, opening stock, price histories
│   ├── PurchaseModuleTests.cs                 # Purchases, supplier payments, returns, numbering, concurrency
│   ├── InventoryModuleTests.cs                # Stock adjustments, status boundaries, timestamps, valuation, reorder
│   ├── SalesModuleTests.cs                    # POS sales, walk-in full payments, change, returns, voiding rules
│   ├── ReceiptPrintingTests.cs                # Receipt document building, QuestPDF layout, ESC/POS commands, drawer pulse
│   ├── UiSmokeTests.cs                        # XAML load, layout, and rendering smoke tests for all views & dialogs
│   └── ViewModelDecouplingAndDialogTests.cs   # Architecture tests verifying zero Window/Owner coupling in ViewModels
│
├── ShopPOS.sln            # Standard Visual Studio Solution file
└── README.md              # Project documentation and developer guide
```

---

## 2. Core Architecture & Structural Rules

- **Single-User Architecture**: Strictly single-user and owner-operated. No login window, no user switching, no roles/RBAC. `ICurrentUserProvider` provides the local owner context (`UserId = 1`, `Username = "owner"`) for audit trail and foreign key integrity.
- **Walk-in Counter Sales with Full Payment**: All sales are counter sales paid in full at checkout. Credit sales, customer ledgers, credit limits, and customer balances are completely eliminated. `PaidAmount >= GrandTotal` is enforced at the database and service level (`DueAmount = 0`). Overpayment is permitted only on Cash transactions, and excess cash is returned as change.
- **Safe Pre-Migration Database Backup**: Before applying any destructive migration that drops tables or columns, `DatabaseBackupHelper.CreatePreMigrationBackup()` automatically creates a complete timestamped copy of `shop.db` at:
  ```
  C:\ShopData\Backups\pre-migration-<MigrationName>-<yyyyMMdd-HHmmss>.db
  ```
  If backup directory creation or copying fails, the migration is aborted to protect store data.
- **Drawer Reconciliation Breakdown**: End-of-day drawer reconciliation does not use shift records. Instead, the Sales List screen features a live, filtered footer breakdown calculating:
  - **Total Cash**: `Sum(Cash Tendered) - Sum(Change Returned)`
  - **Total Card**: `Sum(Card Payments)`
  - **Total Bank**: `Sum(Bank Transfers)`
  - **Total Mobile Wallet**: `Sum(Mobile Wallet Payments)`
  - **Total Other**: `Sum(Other Payments)`
- **Sale Cancellation Rules**: Completed sales can only be cancelled/voided on the day they were created (`SaleDate.Date == DateTime.Today`) and provided no posted returns exist for that sale. Past sales or sales with returns must be handled via formal Sale Returns to maintain ledger audit integrity.
- **Read-Only Historical Due**: Any `DueAmount` on sales created prior to structural removal is preserved as read-only historical data (`HasOldDue` flag with `"Unpaid (old)"` badge), ensuring historical audit integrity without allowing new credit sales.
- **Dynamic Price Levels**: The POS screen includes a Price Level dropdown (defaulting to "Retail" and remembering the selection until sale completion). Selecting a tier dynamically updates cart line unit prices according to the product's price tiers, falling back gracefully to retail `SalePrice` if no tier price is defined.
- **100% Offline-First**: Zero cloud or internet dependencies required at runtime.
- **SQLite WAL Mode & Foreign Keys**: Automatically executes `PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;` on every connection open via `SqlitePragmaInterceptor`.
- **Accurate Money Storage**: Monetary fields use `decimal` in C# and are stored in SQLite as 64-bit integer minor units (`value * 100`) via `MoneyConverter`, preventing floating-point rounding errors.
- **Weight-Based Fractional Quantities**: Quantities use `decimal` in C# and are stored in SQLite as integer thousandths (`value * 1000`) via `QuantityConverter` to natively support items sold by weight (e.g. 1.250 kg).
- **Single StockMovements Ledger**: Stock levels are never directly edited; they derive strictly from the immutable `StockMovements` ledger through `IStockLedgerQuery`.

---

## 3. POS & Selling Workflow (Phases 5A, 5B, 5C)

### 1. Main Selling Screen (`PosSaleView.xaml`)
- **Fast Barcode / SKU Scanning**: Auto-focused barcode input supporting direct USB barcode scans (Enter key terminates) and quick-multiplier syntax (e.g., `3*BARCODE` adds 3 units).
- **Live Search & Autocomplete**: Typing 2+ letters opens a debounced popup showing matching items with current stock badge, sale price, and category. Pressing Up/Down and Enter selects directly into the cart.
- **Line Editing**:
  - Quantity Increment (`+` button) / Decrement (`-` button).
  - Direct Quantity and Price edits respecting `AllowDecimalQuantity` and `AllowSellingBelowMinPrice`.
  - Line Discount dialog (**F7**): Percentage or fixed amount with instant preview.
  - Line deletion with cart recalculation.
- **Stock Warnings**: Amber badge for Low Stock, Red badge for Out of Stock or Negative Stock.
- **Hold & Recall Bills**:
  - **Hold (F4)**: Saves current cart lines with a timestamped hold reference (e.g., `HOLD-0001`) and clears the screen without affecting inventory or payments.
  - **Recall (F5)**: Opens the Held Bills dialog displaying all pending carts with item counts, elapsed time, and total. Selecting a bill loads it back into the POS cart and deletes the held record.
- **Bill-Level Discount (F6)**: Apply percentage or flat currency discount across the entire basket.
- **Price Level Selector**: Switch between Retail, Wholesale, VIP, etc. Cart prices update automatically.

### 2. POS Keyboard Shortcuts
| Key | Action | Description |
|:---:|:---|:---|
| **F1** | Help | Opens POS shortcut quick reference dialog |
| **F2** | Product Search | Opens modal product search dialog |
| **F4** | Hold Bill | Holds current cart without stock effect |
| **F5** | Held Bills | Opens held bills dialog to recall or discard |
| **F6** | Bill Discount | Prompts for bill-level percentage or flat discount |
| **F7** | Line Discount | Prompts for line-level discount on selected cart item |
| **F8** | Discard Sale | Prompts confirmation to clear cart |
| **F9** | Open Drawer | Manually fires cash drawer kick pulse |
| **F12** | Checkout | Opens payment dialog |
| **Esc** | Clear / Cancel | Clears search box or prompts to handle non-empty cart |

### 3. Payment & Checkout Dialog (`SalePaymentDialog.xaml`)
- **Payment Methods Supported**: Cash, Card, Bank Transfer, Mobile Wallet, Other.
- **Split Tender**: Combine multiple payment methods on a single ticket (e.g., Rs 2,000 Cash + Rs 3,000 Card).
- **Quick Cash Tender Buttons**: Preset denominations (Rs 100, Rs 500, Rs 1,000, Rs 5,000, Exact) for rapid cashier tender entry.
- **Change Calculation**: Real-time display of Change Due when cash tendered exceeds ticket total.
- **Full Payment Enforcement**: Checkout button is disabled and warns if total payments are less than the grand total.

### 4. Sale Returns (`SaleReturnsView.xaml` & `SaleReturnDialog.xaml`)
- **With Receipt**: Enter invoice number or select sale. Lines, sold quantities, previous returns, and original prices are loaded automatically.
- **Without Receipt**: Enabled via `AllowReturnWithoutReceipt` setting. Returns item using current master data `SalePrice`.
- **Restock vs. Damaged**: Line toggle specifies whether returned items return to stock (`ReturnToStock = true` creates `StockMovementType.Return` movement) or write off as damaged (`ReturnToStock = false` creates no stock movement).
- **Return Policy Window**: Configurable `SaleReturnWindowDays` setting enforces maximum return eligibility window from sale date.
- **Refund Methods**: Cash, Card, Bank, Mobile Wallet, or Other.

---

## 4. Hardware & Receipt Printing (Phase 5C)

### 1. Printing Pipeline
- **Printer-Independent Document Model**: `ReceiptDocument` represents the complete ticket (headers, shop info, tax number, lines, discounts, tax, round-off, payments breakdown, change, cashier "Owner", footer, barcode/QR).
- **Three Supported Output Modes**:
  1. **ESC/POS Raw Thermal Printing**: High-speed binary commands sent directly to thermal receipt printers via `winspool.drv` Raw mode. Supports 80mm (48 columns) and 58mm (32 columns) paper rolls with hardware font bolding, alignment, and paper cut (`GS V 1`).
  2. **Windows Driver Printing**: High-quality formatted printing through any installed Windows printer driver using WPF `PrintDocument`.
  3. **PDF Preview / Document Generation**: Cross-platform pixel-perfect PDF rendering powered by **QuestPDF**. Displays in an interactive preview window with zoom, save, and print controls.
- **Cash Drawer Solenoid Kick**: Dual-pulse RJ11 kick pulse (`\x1B\x70\x00\x19\xFA` and `\x1B\x70\x01\x19\xFA`) triggers automatically on cash sales if `OpenCashDrawerOnCashSale = true` and manually via **F9**.
- **Reprint Tracking & Duplicate Watermark**: Any receipt reprinted from the Sales List or Detail dialog automatically displays `*** DUPLICATE COPY ***` with reprint number and timestamp, logged to `AuditLogs`.

---

## 5. Master Data & Inventory Modules (Phases 2 - 4)

- **Products**: SKU, Barcode, Multi-Barcodes, Categories, Brands, Units, Price Tiers, Images, Reorder Levels.
- **Stock Overview**: Real-time calculated stock ledger status (OK, Low Stock, Out of Stock, Negative, Not Tracked).
- **Stock Movements**: Full audit log of all inventory changes (`Opening`, `Purchase`, `PurchaseReturn`, `Sale`, `SaleReturn`, `AdjustmentIn`, `AdjustmentOut`).
- **Stock Adjustments**: Manual inventory adjustments with mandatory reason codes and user audit tracking.
- **Stock Valuation**: Real-time FIFO and Average Cost inventory valuation.
- **Purchases & Supplier Ledger**: Purchase entry, goods received, supplier payments, debit/credit ledger, purchase returns.

---

## 6. How to Build, Test & Run

### Prerequisites
- Windows 10 or 11
- .NET 10 SDK (`dotnet --version` >= 10.0.x)

### 1. Build the Solution
```powershell
dotnet build ShopPOS.sln
```
*Expected output: `Build succeeded. 0 Warning(s), 0 Error(s)`.*

### 2. Run Fast Test Suite (Under 60 Seconds)
```powershell
dotnet test ShopPOS.sln --filter "Category!=Slow"
```
*Runs all 182 fast unit, integration, UI smoke, migration, and parallel tests in ~36 seconds.*

### 3. Run Full Test Suite (Including Heavy Performance Benchmarks)
```powershell
dotnet test ShopPOS.sln
```
*Runs all 190 tests, including 20,000 product stock overview and 300,000 movement history performance benchmarks.*

### 4. Verify Zero Test References to Live Database
```powershell
Get-ChildItem -Path ShopPOS.Tests -Recurse -Filter *.cs | Select-String "ShopData"
```
*Prints nothing. All tests strictly use temporary SQLite databases.*

### 5. Launch the Application
```powershell
dotnet run --project ShopPOS.UI
```

### 6. Production Release Packaging & Installer
```powershell
powershell -ExecutionPolicy Bypass -File scripts\build-release.ps1
```
- Produces `dist\ShopPOS-Setup-v1.0.0.exe` (Inno Setup 64-bit installer with bundled VC++ redistributable) and optional `dist\ShopPOS-Portable.exe`.
- Self-contained, zero-dependency win-x64 build (runs on clean Windows 10/11 machines with no .NET or VC++ installed).
- Non-admin runtime execution: writes to `C:\ShopData` by default; automatically falls back to `%LocalAppData%\ShopPOS\Data` if `C:\` is not writable.

### 7. Diagnostic Self-Check Probe
```powershell
dotnet run --project ShopPOS.UI -- --self-check
```
- Performs a pre-flight probe of architecture (64-bit), .NET runtime, SQLite native library (`e_sqlite3.dll`), QuestPDF/Skia graphics, writable data folder, print spooler, fonts, database schema, and in-memory streaming backup.
- Writes full report to `C:\ShopData\Logs\self-check-report.txt` and exits with code 0 (PASS) or 1 (FAIL).

---

## 7. Database & File Locations

- **Main SQLite Database**: `C:\ShopData\shop.db`
- **Pre-Migration Backups**: `C:\ShopData\Backups\pre-migration-<Name>-<Timestamp>.db`
- **Product Images**: `C:\ShopData\Images\`
- **Serilog Application Logs**: `C:\ShopData\Logs\app-.log`

---

## 8. Currency, Fast POS Search & Bulk Operations

### 1. PKR Currency & Invariant Date/Time Formatting
- **Root-level Formatting**: Powered by `IMoneyFormatter` (`MoneyFormatter.Instance`) and WPF converters (`MoneyConverter`, `MoneyNoSymbolConverter`, `DateDisplayConverter`, `TimeDisplayConverter`, `DateTimeDisplayConverter`).
- **Standardized Currency Display**: Defaults to `CurrencySymbol = "Rs"` and `CurrencyDecimals = 2` (e.g., `Rs 8,296.10`). Setting allows 0 to 3 decimal places.
- **Invariant Culture**: Always uses `CultureInfo.InvariantCulture` with explicit comma thousands separators and period decimal separator.
- **UI Dates and Times**: Displayed as `dd MMM yyyy` (e.g. `02 Nov 2026`) and `hh:mm tt` 12-hour format (e.g. `02:30 PM`).

### 2. POS High-Speed Product Search
- **Multi-Word Anywhere Matching**: Typing "cola 500" instantly matches "Coca Cola 500ml", "Coca-Cola Zero 500ml", etc.
- **Debounced Execution**: 150 ms debounce on typing prevents query flooding while delivering sub-50 ms search response over 20,000+ products via indexed `SearchText` column.
- **4-Tier Relevance Ranking**:
  1. Exact SKU / Barcode match (rank 1)
  2. Prefix / Starts-with match (rank 2)
  3. Word boundary / Contains match (rank 3)
  4. Multi-word full text match (rank 4)
- **Active Products Only**: Filtered to non-deleted, active products with stock badges and price level calculations.
- **Quantity Multiplier Prefix**: Typing `5*cola` searches for "cola" and pre-populates quantity as 5 on addition.
- **Full Keyboard Navigation**: Arrow keys navigate suggestions, Enter adds to cart, Escape closes popup.

### 3. Inventory Status Boundaries
- **Out of stock**: Strictly `stock <= 0` (Red badge and label "Out of stock").
- **Low stock**: Strictly `stock > 0 && stock <= ReorderLevel` (Amber badge and label "Low stock").
- **Negative stock**: Highlighted with distinct negative label.
- **OK stock**: Green badge for `stock > ReorderLevel`.
- **Not tracked**: Grey badge when `ReorderLevel` is not set or inventory tracking is disabled.
- **Filter Separation**: Status filters provide distinct options for "Low stock" and "Out of stock". The Dashboard cards count each separately.

### 4. Bulk Selection & Operations in Every Module
- **Modules Covered**: Products, Categories, Brands, Units, Suppliers, Purchases, Stock Adjustments, Sales, Supplier Payments, Purchase Returns, Sale Returns.
- **Checkbox & Multi-Select**: Select all on current page via header checkbox or `Ctrl+A`.
- **Cross-Page Selection**: Blue banner provides single-click "Select all N results matching this filter" spanning all pages.
- **Keyboard Shortcuts**: `Delete` key triggers Delete/Cancel; `Ctrl+A` selects all.
- **Module Safety Rules**:
  - **Master Data**: Unreferenced items are soft-deleted. Referenced items cannot be deleted; the user is prompted to deactivate them in bulk.
  - **Purchases & Adjustments**: Drafts can be deleted; posted records can be cancelled.
  - **Sales**: Held sales can be deleted; completed sales created today without returns can be cancelled.
  - **Payments & Returns**: Cancel only with mandatory reason.
- **Typed Confirmation Safeguard**: Operating on more than 50 items or all filtered results requires typing `DELETE` or `CANCEL` in `BulkConfirmDialog`.
- **Filtered Unique Indexes**: Soft-deleted entities release their SKU/Barcode immediately via SQLite filtered unique indexes (`WHERE IsDeleted = 0`), allowing instant re-use.

---

## 9. Modern Design System, Light/Dark Theming & Hover Bug Resolution

### 1. Unified Visual Design System
- **Color Palette & Theme Dictionaries**:
  - Centralized semantic color tokens declared in `ShopPOS.UI/Styles/ThemeLight.xaml` and `ShopPOS.UI/Styles/ThemeDark.xaml`.
  - Primary accents: Deep Teal (`#1E5E56` Light / `#2DD4BF` Dark), Sage/Olive backgrounds and subtle table borders.
  - Semantic Status Brushes: Green for OK/Active/Paid, Amber for Low stock/Parked, Red for Out of stock/Negative/Cancelled, Grey for Not tracked.
  - Zero hardcoded hex values in UI XAML: Enforced via `HexColorScanningTests.cs`. All colors resolve dynamically through `{DynamicResource ...}` tokens.
  - WCAG AAA / AA Contrast Compliance: All text, button, badge, and card combinations pass automated contrast ratio tests (`ThemeContrastTests.cs`).

### 2. Root-Level Hover Bug Fix
- **Defect Resolved**: Previously, hovering buttons caused both background and text color to turn white, making button labels invisible.
- **Root-Level Architectural Fix**:
  - Rebuilt all button templates in `ShopPOS.UI/Styles/Controls.xaml` (`PrimaryButtonStyle`, `SecondaryButtonStyle`, `DangerButtonStyle`, `DangerOutlineButtonStyle`, `GhostButtonStyle`, `SuccessButtonStyle`, `PaginationButtonStyle`, and navigation items).
  - Every style defines explicit, synchronized state pairs for `Normal`, `Hover`, `Pressed`, `Focused`, and `Disabled` setting **BOTH background AND foreground together**.
  - No style or template trigger ever overrides background without explicitly specifying foreground.

### 3. Light and Dark Theme Support
- **Runtime Theme Switching**: `IThemeService` (`ThemeService.cs`) dynamically swaps active theme dictionaries without requiring application restart.
- **Persistence**: Theme preference is automatically persisted to local application storage.
- **Settings Picker**: The Settings screen provides a single-click Theme picker (Light / Dark).

### 4. Typography, DataGrids & High-DPI Scaling
- **Typography Hierarchy**: Defined in `ShopPOS.UI/Styles/Typography.xaml` using `Segoe UI Variable, Segoe UI, sans-serif`. Includes `PageTitleStyle`, `SectionHeaderStyle`, `FormLabelStyle`, `KpiValueStyle`, and `SubtitleStyle`.
- **Tabular Monospace Numerics**: `MonospaceNumericCell` ensures clean vertical decimal alignment for amounts, quantities, and totals across DataGrids.
- **Responsive Layout**: Designed for a minimum window size of 1366x768, scaling smoothly across 125% and 150% Windows DPI scaling factors.

### 5. Automated Quality & Scanning Tests
- `HexColorScanningTests`: Scans all `*.xaml` views and dialogs to guarantee ZERO hardcoded hex codes outside theme files.
- `ThemeContrastTests`: Mathematically verifies contrast ratios of all theme colors against backgrounds.
- `UiSmokeTests`: Programmatically constructs, binds, renders, and closes all 20+ Views, Windows, and Dialogs on an STA thread in **both Light and Dark themes**.
- `CurrencyFormatScanningTests`: Enforces invariant `IMoneyFormatter` and eliminates raw `:C` format strings.
- `EmojiScanningTests`: Ensures clean professional typography with zero emojis or pictographs.
- `ViewModelDecouplingAndDialogTests`: Verifies strict MVVM decoupling (no `System.Windows`, `.Owner =`, or `Application.Current.MainWindow` in ViewModels).

---

## 10. Barcode Management & Database Integration

### 1. Barcode Generation & Validation (`IBarcodeService` / `BarcodeService`)
- **Supported Formats**: Full standards-compliant support for:
  - **GS1 EAN-13**: Mod-10 check digit calculation with 1/3 alternating weights, Left/Odd (L), Left/Even (G), and Right (R) parity encoding, and quiet zone enforcement.
  - **UPC-A**: 12-digit format with standard Mod-10 check digit calculation with 3/1 weights.
  - **Code 128**: High-density alphanumeric format for custom store SKUs and item codes.
- **Collision-Free Generation**: `GenerateUniqueBarcodeAsync()` queries the live database (both primary barcodes and `ProductBarcodes` aliases) to guarantee unique assignments.
- **Manual Entry & Validation**: UI dialogs allow manual barcode entry and editing with instantaneous checksum and syntax verification.
- **Vector Barcode Rendering**: `GetBarcodePattern()` generates exact binary bar/space module sequences for scalable vector rendering in UI and PDF documents.

### 2. Barcode Label Printing (`BarcodeLabelRequest` / QuestPDF)
- **Multi-Layout Templates**:
  - `A4Sheet30Up`: Standard 30-up A4 label sheet (3 columns x 10 rows, 70mm x 29.7mm per label).
  - `Thermal50x30`: Direct thermal continuous roll label (50mm width x 30mm height) with product name, formatted price, and vector barcode.
  - `Thermal40x25`: Compact thermal roll label (40mm width x 25mm height) for small goods and jewelry.
- **Direct Label Printing**: Available directly from the Products catalog toolbar ("Print labels" button) and within the Product editing dialog ("Print Label" button).

### 3. USB Barcode Scanner Integration (Keyboard Wedge Mode)
- **Universal Scanner Compatibility**: Standard USB barcode scanners operating in keyboard-wedge mode (sending keystrokes terminated with Enter) work seamlessly without dedicated drivers.
- **Window-Wide Focus Redirection**: Scanning anywhere in the POS sale screen automatically focuses and routes input to the barcode input box.
- **POS / Sales Workflow**:
  - **Auto-Add to Cart**: Scanning a valid barcode immediately looks up the product and adds it to the active cart.
  - **Repeat Scan Quantity Increment**: Scanning the same barcode repeatedly automatically increments that line's quantity.
  - **Multiplier Prefix Parsing**: Scanners or cashiers can scan with a multiplier prefix (e.g., `3*BARCODE` or `5*SKU`) to add 3 or 5 units in a single scan.
  - **Audio & Visual Alerts**: Scanning an unknown or unassigned barcode immediately shows a clear error message and triggers an audible system alert beep.
- **Receiving & Stock-In (`PurchaseEntryWindow.xaml`)**:
  - Top barcode scan bar allows rapid receiving of supplier shipments; scanning auto-adds or increments line items.
- **Inventory Adjustments (`StockAdjustmentDialog.xaml`)**:
  - Dedicated scan input box enables quick physical count corrections; scanning increments the physical count line item.

### 4. Database Cleanup & Fresh Retail Dataset
- **Database Path**: `C:\ShopData\shop.db` with automated pre-cleanup backup created in `C:\ShopData\Backups\pre-cleanup-<timestamp>.db`.
- **Legacy Cleanup**: Outdated legacy shift, customer, and login audit logs and demo test data safely eliminated; foreign keys and sequences maintained.
- **25 Fresh & Connected Retail Products**:
  - Spanning 5 core retail categories (Beverages, Snacks, Dairy & Bakery, Personal Care, Household & Cleaning) and 10 popular brands (Coca-Cola, PepsiCo, Lipton, Nestlé, Unilever, etc.).
  - Realistic cost and sale prices in PKR (`Rs`), retail and wholesale tiers, opening stock balances, and standard GS1 EAN-13 barcodes.
  - Initial purchase order receiving (`PUR-000001`) with bank payment settlement (`SPAY-000001`) and counter sale (`INV-000001`) maintaining complete stock movement ledger synchrony.

---

## 11. Phase 6B: Report Center & Real-Time Dashboard

### 1. Unified Report Center (`ReportsView.xaml` / `ReportService.cs`)
The Reports workspace provides a unified master-detail layout accessible from the sidebar. The left pane lists the 10 domain reports; the right pane displays the active report with dynamic date filtering presets, summary strips, paged tabular data, drill-down capabilities, and export options.

#### Mathematical Definitions & Reconciliation Invariants
All report maths lives strictly in Core services and SQL projections (`AsNoTracking`, grouped queries, sum/count aggregations; never loading entire entity sets into memory).
- **Completed Sales Only**: Cancelled sales, held bills, cancelled returns, cancelled purchases, and cancelled expenses never count toward revenue or totals.
- **Returns Subtraction**: Returns subtract in the period of the **Return Date**, at original sold price, discount share, tax, and cost snapshot.
- **Net Sales**:
  $$\text{Net Sales} = (\text{GrandTotal} - \text{TaxAmount} - \text{RoundOff}) - \text{Returns SubTotal}$$
  Equivalently: line total after discounts, excluding tax, minus returns.
- **Tax Collected**:
  $$\text{Tax Collected} = \text{Tax on Sales} - \text{Tax on Returns}$$
- **Cost of Goods Sold (COGS)**:
  $$\text{COGS} = \sum (\text{SaleItem.Quantity} \times \text{CostPriceSnapshot}) - \sum (\text{SaleReturnItem.Quantity} \times \text{CostPriceSnapshot}) \quad [\text{Restocked Only}]$$
  Damaged returns not returned to stock do not reverse cost.
- **Gross Profit**:
  $$\text{Gross Profit} = \text{Net Sales} - \text{COGS}$$
- **Net Profit**:
  $$\text{Net Profit} = \text{Gross Profit} - \text{Operating Expenses} - \text{Stock Write-Offs}$$
  *Operating Expenses* excludes cancelled expenses. *Stock Write-offs* includes negative stock adjustments with reasons `Damaged`, `Expired`, or `Lost` valued at unit cost. Positive `Found` adjustments are shown separately as informational valuation and are not added to profit.
- **Payment Totals & Cash Flow**:
  $$\text{Net Cash Flow} = \text{Sales Collected} - \text{Refunds Paid} - \text{Expenses Paid} - \text{Supplier Payments Paid}$$
  Applied change is excluded from sales collected.
- **Date Ranges (Local Time)**:
  From `00:00:00.000` of the start date to `23:59:59.999` of the end date. Standard presets: `Today`, `Yesterday`, `This week` (Monday start), `This month`, `Last month`, `This year`, and `Custom`.

### 2. Available Domain Reports
1. **Daily Sales Summary**: Per-day breakdown of sales count, gross sales, discounts, tax, returns, net sales, average sale value, and payment method breakdown (Cash, Card, Bank, Mobile Wallet, Other).
2. **Sales by Product**: Breakdown of quantity sold, net revenue, cost, profit, and margin percentage. Supports dynamic grouping by Product, Category, Brand, Hour of Day (00:00 - 23:59), and Payment Method, with Top N filtering and multi-column sorting.
3. **Profit & Loss Statement**: Formal P&L lines (Gross sales, Discounts, Returns, Net sales, COGS, Gross profit, Expenses by category, Stock write-offs, Net profit, and Found stock valuation) with comparison against the immediately preceding period of equal length.
4. **Purchases & Supplier Payables**: Net purchases by supplier, by product, and by day, alongside Supplier Payables with aging buckets (Current 0-30 days, 31-60 days, 61-90 days, >90 days).
5. **Tax Report**: Output tax by rate on sales minus returns, input tax by rate on purchases minus purchase returns, and net tax payable.
6. **Stock Reports**: Stock valuation by category, low stock, out of stock, stock movement summary for the period (Opening + Purchases In - Sales Out + Returns In +/- Adjustments = Closing, exactly reconciling with the stock ledger), and slow-moving / dead stock analysis.
7. **Expenses Report**: Active expenses grouped by category and by day, including percentage contribution to total operating expenses.
8. **Payment Methods Summary**: Money in vs money out across all payment channels (Sales collected, refunds paid, supplier payments, operating expenses).
9. **Audit Log Viewer**: Filtered and paged log viewer for system activities by date, entity type, and action.
10. **Barcode & Data Quality**: Exception reporting for products missing barcodes, duplicate barcodes, invalid check digits, zero cost prices, sale price below cost, and negative stock levels.

### 3. Export & Print Preview System
- **CSV Export**: Standard RFC 4180 format with UTF-8 BOM (`EF BB BF`), plain ISO dates (`yyyy-MM-dd`), plain decimal numbers (no currency symbols or thousands commas), and proper header rows.
- **PDF Export**: QuestPDF generation adhering to standard A4 page layout, store header, report title, period range, generation timestamp, page numbering (`Page X of Y`), and wrapping long names.
- **In-App Print Preview Dialog (`ReportPreviewDialog.xaml`)**: Visual page-by-page preview rendered directly from the QuestPDF layout using SkiaSharp rasterization, with zoom, page navigation, printer selection, and print execution.

### 4. Real Dashboard (`DashboardView.xaml` / `DashboardViewModel.cs`)
- **KPI Summary Strip**: Today's Net Sales (with percentage delta vs yesterday), Today's Net Profit, Sales Count Today, Active Held Bills Count, and Total Supplier Payables.
- **30-Day Net Sales Trend Chart**: Lightweight, vector-rendered daily net sales bar chart displaying local dates, amounts formatted in `Rs`, interactive tooltips, and seamless theme support.
- **Top 5 Selling Products Today**: Live table showing today's top products by quantity sold, net revenue, and margin.
- **Stock & Payables Operational Cards**: Quick-reference cards showing Low Stock items, Out of Stock items, and Supplier balances with direct navigation links.
- **Operational Daily Checklist**: Derived purely from live database state (first completed sale of the day, low stock review, automated database backup status).
- **Refresh Shortcut**: `F5` keybinding and refresh button for instant dashboard update.

### 5. EF Core Composite Indexes (`Phase6B_ReportIndexes`)
To guarantee sub-second dashboard load and under 2-second report queries even with hundreds of thousands of transactions, high-performance composite indexes are applied:
- `IX_SaleReturns_Status_ReturnDate`
- `IX_SalePayments_Method_PaidAt`
- `IX_SupplierPayments_IsCancelled_PaymentDate_Method`
- `IX_Expenses_IsCancelled_ExpenseDate_ExpenseCategoryId`
- `IX_StockAdjustments_Status_Reason_AdjustmentDate`
- `IX_Purchases_Status_PurchaseDate`
- `IX_AuditLogs_Timestamp_EntityName_Action`

---

## 12. Data Tools & Store Maintenance (Settings → Data)

Accessible from **Settings** under the **Data** section:
- **Automatic WAL-Safe Backups**: Every destructive action creates an automatic, timestamped backup file in `C:\ShopData\Backups\backup-<action>-<timestamp>.db` using SQLite online backup before executing any deletions. If the backup fails, the operation immediately aborts with no data touched.
- **Typed Confirmation Safeguard**: Each destructive operation requires typing the exact word `RESET` in capital letters in a confirmation dialog showing exact tables and row counts to be deleted and kept.
- **Tool A: Clear Transactions**:
  - Deletes all sales, sale items, payments, returns, purchases, purchase items, supplier payments, purchase returns, stock adjustments, stock movements, expenses, and audit logs.
  - Resets all document numbering sequences (`INV`, `HOLD`, `SRET`, `PUR`, `SPAY`, `PRET`, `ADJ`, `EXP`) back to 1.
  - Preserves products, categories, brands, units, suppliers, price levels, and settings. Product stock becomes 0.
- **Tool B: Clear Everything (New Shop)**:
  - Wipes all transactions plus products, product barcodes, price history, categories, brands, units, suppliers, and expense categories.
  - Re-seeds standard units (`pcs`, `kg`, `litre`, `box`, `dozen`), the default `General` category, `Retail` & `Wholesale` price tiers, and default expense categories.
  - Preserves store settings and the single local owner user row.
- **Tool C: Clear Audit Log Only**:
  - Clears all system audit logs while leaving transactions, inventory, products, and settings completely intact.
- **Tool D: Load Demo Data**:
  - Allowed only when transaction tables are empty (prompts the user to run Tool A if transactions exist).
  - Generates a realistic retail store through real services: 6 categories, 6 brands, 5 suppliers with phone numbers, ~60 products across categories with valid EAN-13 barcodes generated by `IBarcodeService` (using private in-store prefix `200..299`), 8 purchases over the last 30 days, supplier payments, 1 purchase return, 40 completed sales over the last 14 days, and 25 expenses.
  - Displays a sample table of 10 EAN-13 barcodes with SKU, name, price, and stock for immediate scanning and testing.


---

## 13. Phase 7A: Local Backup, Restore & Database Safety

ShopPOS includes an enterprise-grade, offline-first backup and disaster recovery engine designed specifically for single-computer retail environments.

### 1. Backup Archive Format & Structure
Backups are packaged as self-contained ZIP archives (or `.zip.enc` when encrypted):
- **Naming Convention**: `ShopPOS_<ShopNameSlug>_<yyyyMMdd-HHmmss>_<kind>.zip` (e.g. `ShopPOS_MyMart_20261104-120000_manual.zip`)
- **Archive Contents**:
  - `shop.db`: A complete, consistent SQLite database snapshot taken using the SQLite Online Backup API (`SqliteConnection.BackupDatabase`) while the application and WAL writer are actively running. Direct file copying while SQLite is running is strictly prohibited.
  - `manifest.json`: Metadata document containing application version, schema migration version, UTC creation timestamp, machine name, store name, SHA-256 database checksum, table row counts (`Products`, `Sales`, `Purchases`, etc.), and encryption state.
  - `Images/`: Complete product and catalog image hierarchy preserved with original folder structure.
- **Disk Space Pre-Flight Check**: Checks available free drive space before starting and requires at least 2x the live database size (minimum 10 MB) to prevent partial or corrupted archive writes.
- **Secondary Folder Mirroring**: When `SecondFolderPath` is configured (e.g., an external USB drive or secondary hard disk), the backup archive is automatically mirrored to the secondary folder. Failures to copy to the secondary path are logged as warnings and surfaced in the UI without aborting the primary backup.

### 2. AES-256-GCM Encryption
- **Cipher**: Authenticated AES-256 in Galois/Counter Mode (AES-256-GCM) with 128-bit authentication tag.
- **Key Derivation**: PBKDF2 with HMAC-SHA256 and **200,000 iterations**, utilizing a 16-byte cryptographically secure random salt generated per encryption.
- **File Format (`.zip.enc`)**:
  - `[Byte 0]`: Format version byte (`0x01`)
  - `[Bytes 1-16]`: 16-byte random PBKDF2 Salt
  - `[Bytes 17-28]`: 12-byte random AES-GCM Nonce / IV
  - `[Bytes 29-44]`: 16-byte GCM Authentication Tag
  - `[Bytes 45+]`: AES-256-GCM Ciphertext
- **Zero Password Storage**: The master password is never stored anywhere on disk or in the database. Only a PBKDF2 salt and SHA-256 verification hash (`EncryptionKeyCheck`) are persisted in `ShopSettings` to validate passwords entered by the user.
- **Clear Warning**: The UI explicitly displays *"If you lose this password, encrypted backups cannot be restored."* and enforces double-entry confirmation.

### 3. Grandfather-Father-Son (GFS) Retention Policy
Automatic retention runs after every scheduled backup to conserve disk space while preserving deep historical restore points:
- **Daily Retention**: Keeps backups for the **7 most recent distinct calendar days**.
- **Weekly Retention**: Keeps backups for the **4 most recent distinct calendar weeks**.
- **Monthly Retention**: Keeps backups for the **6 most recent distinct calendar months**.
- **Safety Exclusions (Never Deleted by Retention)**:
  - The **single newest backup** is never deleted under any circumstance.
  - **Manual backups** (`Kind = Manual`) are never purged automatically.
  - **Pre-Restore safety snapshots** younger than 30 days are preserved.
  - Backups with pending or in-progress cloud uploads (`DriveStatus == Pending` or `Uploading`) are protected.

### 4. Atomic Restore & Disaster Recovery
Accessible from the **Backup & Restore** screen via the **Restore** button or by opening an external `.zip` / `.zip.enc` file:
1. **Archive Validation**: Decrypts (if needed), extracts to temporary sandbox, validates manifest structure, verifies the SHA-256 checksum of `shop.db`, and runs `PRAGMA integrity_check` and `PRAGMA foreign_key_check`.
2. **Schema Version Check**:
   - If the archive was created by a **newer version** of ShopPOS, the restore is blocked with a clear instruction: *"This backup was created by a newer version of ShopPOS. Please update the application first."*
   - If the archive was created by an **older version**, the restore proceeds and automatically runs all pending EF Core migrations on the restored database.
3. **Pre-Restore Safety Snapshot**: Before replacing any live files, an automatic safety backup (`Kind = PreRestore`) is created. If this snapshot fails, the restore is aborted immediately to protect the current database.
4. **Atomic Replacement**: Live SQLite connections are closed and pools cleared. The current `shop.db` is moved to `shop.db.old`, the replacement database and catalog images are copied into place, and WAL/SHM auxiliary files are removed. If any failure occurs during copy, `shop.db.old` is rolled back immediately.

### 5. Automated Background Scheduler (`IBackupScheduler`)
- **Daily Schedule**: Runs daily at the configured `BackupTime` (default `22:00`). If missed (e.g. computer was turned off), an automatic catch-up backup runs 2 minutes after application startup if >24 hours have elapsed since the last successful backup.
- **Retry Mechanism**: If a scheduled backup fails, it automatically retries up to 3 times with a 30-minute delay before waiting for the next daily cycle.
- **Backup on Application Exit**: When `BackupOnAppClose` is enabled (default `true`), closing the application displays a graceful backup progress dialog with a **Skip** button and a strict **20-second timeout** to ensure the user is never held hostage by the software.

### 6. Database Safety & Integrity Guarantees
- **Startup Integrity Check**: Runs `PRAGMA quick_check;` on startup. If corruption is detected, an emergency dialog presents the user with options to restore from the latest healthy backup or repair the database.
- **Shutdown Checkpoint**: Executes `PRAGMA wal_checkpoint(TRUNCATE);` on application exit to flush all WAL pages back into the primary database file.
- **Single-Instance Mutex Guard**: Uses a system-wide named mutex (`Global\ShopPOS_SingleInstance_Mutex`) to prevent multiple instances of ShopPOS from opening simultaneously and corrupting SQLite files. If a second instance is launched, it brings the existing window to the front and cleanly exits.
- **Unclean Shutdown Detection**: Creates a `shop.lock` file in the data folder on startup and deletes it on clean shutdown. If `shop.lock` is found on startup, a warning is logged and a quick integrity check is executed.
- **Automatic Log Purge**: Serilog rolling log files in `C:\ShopData\Logs` older than 30 days are automatically deleted at application startup.

### 7. How to Move ShopPOS to Another PC
To transfer your store catalog, sales history, settings, and images to a new computer:
1. On the old PC, open **Backup & Restore** and click **Back Up Now** to create a fresh backup archive.
2. In the Backups list, click **Copy to...** (or navigate to `C:\ShopData\Backups`) and copy the generated `.zip` file to a USB flash drive.
3. Install and open ShopPOS on the new PC.
4. Go to **Backup & Restore**, click **Restore from File...**, and select the `.zip` archive from your USB flash drive.
5. Review the summary of products, sales, and purchases in the confirmation dialog, type `RESTORE` in all caps, and click **Restore Database**.
6. The application will restore the database, extract all product images, run any necessary migrations, and restart with your complete store data intact.


## 14. Phase 7B: Google Drive Cloud Backup Integration

ShopPOS includes background cloud backup synchronization to Google Drive. The application remains 100% usable offline; Google Drive is an optional offsite replica.

### 1. Privacy, Security & Data Protection
- **Least-Privilege Authorization**: Requests solely the `https://www.googleapis.com/auth/drive.file` scope. ShopPOS can **only** read and write files and folders that the application itself created. It cannot access, browse, or read any existing personal files or folders in your Google Drive.
- **Client Secrets**: Following Google OAuth 2.0 specifications for Desktop and Installed Applications, client credentials cannot be kept truly secret on client machines. ShopPOS loads client credentials from external configuration files or environment variables without embedding them in binary source code.
- **DPAPI Credential Encryption**: The OAuth 2.0 refresh token is stored encrypted on disk using the Windows Data Protection API (DPAPI) with `DataProtectionScope.CurrentUser` at `C:\ShopData\google_token.dat`. The token is never stored in the database, never written to log files, and never bundled inside backup zip archives.
- **Disconnection & Revocation**: Clicking **Disconnect** in Settings deletes the local encrypted token file and revokes authorization with Google's servers.

### 2. Google Cloud Console Setup Guide
To enable Google Drive integration for your own Google Cloud organization:
1. Navigate to the [Google Cloud Console](https://console.cloud.google.com/) and create a new project (e.g. `ShopPOS`).
2. Go to **APIs & Services > Library**, search for **Google Drive API**, and click **Enable**.
3. Go to **APIs & Services > OAuth consent screen**:
   - Choose **External** user type and click **Create**.
   - Fill in App name (e.g. `ShopPOS`), User support email, and Developer contact information.
   - On the **Scopes** page, click **Add or Remove Scopes**, select `../auth/drive.file`, and save.
   - Under **Test users**, add the Google email account(s) you wish to authorize.
4. Go to **APIs & Services > Credentials**:
   - Click **Create Credentials > OAuth client ID**.
   - Select **Desktop app** as the Application type.
   - Enter a name (e.g. `ShopPOS Desktop`) and click **Create**.
   - Copy the generated **Client ID** and **Client Secret**.

### 3. Client Configuration
Provide credentials to ShopPOS using either a JSON configuration file or environment variables:

#### Option A: Configuration File (`googledrive.client.json`)
Place `googledrive.client.json` next to `ShopPOS.UI.exe`, in `C:\ShopData`, or in the application working directory.

**Standard Flat Format**:
```json
{
  "client_id": "YOUR_CLIENT_ID.apps.googleusercontent.com",
  "client_secret": "GOCSPX-YOUR_CLIENT_SECRET"
}
```

**Google Cloud Downloaded Format**:
You may also rename the raw JSON file downloaded directly from the Google Cloud Console to `googledrive.client.json`:
```json
{
  "installed": {
    "client_id": "YOUR_CLIENT_ID.apps.googleusercontent.com",
    "client_secret": "GOCSPX-YOUR_CLIENT_SECRET",
    "auth_uri": "https://accounts.google.com/o/oauth2/auth",
    "token_uri": "https://oauth2.googleapis.com/token"
  }
}
```

#### Option B: Environment Variables
Alternatively, configure system or user environment variables:
- `SHOPPOS_GDRIVE_CLIENT_ID`: Your Google OAuth client ID.
- `SHOPPOS_GDRIVE_CLIENT_SECRET`: Your Google OAuth client secret.

If neither configuration is present, the app gracefully displays *"Google Drive is not configured in this build"* without interrupting offline operations.

### 4. Background Sync Architecture
- **Interactive PKCE Authorization**: Launches the default system browser using the OAuth 2.0 Loopback IP Receiver on a dynamically assigned free local port with a 3-minute timeout and Cancel capability.
- **Drive Folder Hierarchy**: Automatically creates a root folder named `ShopPOS Backups` on Google Drive and organizes archives under a subfolder named after the store (e.g. `ShopPOS Backups/MyMart/`).
- **Resumable 5 MB Chunked Uploads**: Archives are uploaded using Google Drive Resumable Upload sessions in 5 MB chunks. Upload session URIs are persisted in the database so uploads interrupted by network loss resume from the last completed chunk.
- **Integrity Verification**: Following upload completion, the file's MD5 checksum and byte count returned by Google Drive are validated against local hashes. Files that fail verification are purged from Drive and queued for re-upload.
- **Exponential Backoff**: Transient network and HTTP 5xx errors back off automatically (1 min, 5 min, 15 min, 1 hr, 4 hr, up to 24 hr).
- **Network Awareness**: Subscribes to `System.Net.NetworkInformation.NetworkChange` events to trigger pending uploads immediately upon network reconnection.
- **Quota & Reconnect Handling**: When storage quota is exceeded (HTTP 403) or tokens are revoked (HTTP 401), prominent warning banners appear in the dashboard and backup center, safely pausing background attempts until resolved.
- **Cloud GFS Retention**: Mirrored Grandfather-Father-Son retention runs on Drive archives (7 daily, 4 weekly, 6 monthly). The newest cloud backup and manual backups are preserved.

### 5. Running the Live Integration Test
Automated CI tests use an in-memory `FakeDriveClient`. To run the live test against real Google Drive APIs:
```powershell
$env:SHOPPOS_GDRIVE_LIVE_TEST = "1"
$env:SHOPPOS_GDRIVE_CLIENT_ID = "your-client-id"
$env:SHOPPOS_GDRIVE_CLIENT_SECRET = "your-client-secret"
dotnet test ShopPOS.Tests\ShopPOS.Tests.csproj --filter "FullyQualifiedName~DriveLiveIntegrationTests"
```

---

## 15. Phase 7B-GitHub: Private GitHub Repository Backup (Release Assets Architecture)

ShopPOS supports automatic offsite cloud backups directly to a private GitHub repository using **GitHub Release Assets** via `uploads.github.com`. The repository receives zero git commits beyond an initial bare `README.md`.

### 1. Storage Architecture
- **Repository Isolation**: One private repository per store (e.g. `owner/my-shop-backups`).
- **Yearly Releases**: Backups are organized into yearly pre-releases tagged `backup-<yyyy>` (e.g. `backup-2026`), created automatically upon the first upload of that year.
- **Release Assets**:
  - **Daily Archives**: `ShopPOS_<slug>_<yyyyMMdd-HHmmss>.zip.enc` and companion `<assetName>.manifest.json`.
  - **Permanent Monthly Archives**: `ShopPOS_<slug>_<yyyy-MM>_monthly.zip.enc` (the first successful upload of each calendar month is also uploaded under this name).
  - **Permanent Year-End Archives**: `ShopPOS_<slug>_<yyyy>_YEAR-END.zip.enc` created automatically on the first upload of the next year or on demand. Contains the database snapshot plus encrypted CSV exports for that year's sales, purchases, expenses, and inventory.
  - **Companion Manifests**: Each asset has a `.manifest.json` asset storing schema version, application version, creation date, size, and SHA-256 hash (contains no sensitive business data).

### 2. Retention Policy
- For the active yearly release, ShopPOS automatically retains the **newest 30 daily assets**.
- All **monthly archives** and **year-end archives** are permanent and never deleted.
- Older daily assets and their manifests are pruned automatically via the GitHub Release Assets API.
- The single newest daily asset is never deleted under any circumstance.
- Non-matching assets are left untouched.

### 3. Security & Safety Gates
- **Windows DPAPI Storage**: Fine-grained Personal Access Tokens (PAT) are stored encrypted via Windows DPAPI (`DataProtectionScope.CurrentUser`) in `%LOCALAPPDATA%\ShopPOS\github_token.dat`. Tokens are never logged, never saved in the database, and never packaged in backups.
- **Mandatory Repository Privacy**: Uploads are strictly refused if the target repository is public (`GitHubRepoIsPublic = true`).
- **Mandatory Encryption**: Cloud backups require backup encryption to be enabled with AES-256-GCM.
- **Token Scope Warning**: Warns if a provided token has access to multiple repositories and advises a single-repo fine-grained token.
- **Size Limits**: Warns at 500 MB and refuses upload above 1.8 GiB (release asset ceiling).
- **Exclude Product Images**: Defaults to ON to keep backup archives compact.

### 4. Disaster Recovery & Migration
- **Setup Wizard & Backup Restore UI**: Browse and restore any daily, monthly, or year-end archive with progress and SHA-256 integrity verification.
- **Repository Replicator Tool**: Copy all releases and assets to a new GitHub repository or from a local folder of downloaded files, with automatic deduplication and zero modifications to source files.


---

## 7. Phase 7C: First-Run Setup, Hardening & Windows Installer

### 1. First-Run Setup Wizard
Shown exclusively on clean installations when no database exists in the data directory. The wizard features 7 guided steps and guarantees that no data is committed until the final Finish button is clicked:
1. **Welcome**: Choose between initializing a new shop or restoring an existing backup file (local USB or cloud Drive).
2. **Shop Profile**: Store name (required), physical address, phone, and tax registration number.
3. **Currency & Tax**: Currency symbol (default `Rs`), decimal places, default tax rate, price tax inclusion, cash rounding step.
4. **Receipt Printer**: ESC/POS, Windows Driver, or PDF preview, paper width (58mm / 80mm), with live test slip print.
5. **Data Storage Folder**: Configurable data folder (default `C:\ShopData`) stored in `appsettings.user.json`, verified for writability and minimum 500 MB free storage.
6. **Backup Protection**: Daily scheduled backup time, secondary USB folder path, AES-256 backup encryption, optional Google Drive cloud sync.
7. **Initial Store Catalog**: Start empty or pre-populate comprehensive demo retail master data.

### 2. Startup Pre-Flight Hardening & Diagnostics
- **Filesystem & Space Verification**: Pre-flight verification enforces read/write permissions and checks for >= 500 MB available free disk space on application startup.
- **Process Lock Detection**: Inspects SQLite database files using exclusive file share testing, preventing corruption from multiple instances accessing the same database.
- **Schema Compatibility Guard**: Compares compiled EF Core migrations against `__EFMigrationsHistory`. If a database was created by a newer version of the application, startup refuses to run with a clear instruction to update. If older, an automatic pre-migration backup is created before migrating.
- **Crash Safety Dialog**: Global unhandled exception handler captures WPF UI thread and background domain faults, displaying a friendly recovery dialog with "Copy Details" and "Open Log Folder" options.
- **Diagnostics Support Bundle**: Accessible from **Settings > About**, creates an encrypted/sanitized ZIP bundle (`ShopPOS_Support_<Date>.zip`) containing system info, OS/.NET/app versions, schema version, table counts, and 7 days of logs. Database files, tokens, and customer secrets are strictly omitted.
- **Performance Watchdog**: Database interceptor alerts in debug builds if any query on the UI thread takes longer than 100 ms.

### 3. CLI Automation Flags
- `--data-dir <path>`: Overrides data folder location without modifying persistent user settings.
- `--smoke-test`: Starts the application headlessly, validates filesystem and migrations, opens Dashboard view model, triggers a verified manual database backup, and exits with status 0 (or 1 on failure).

### 4. Windows Installer & Build Automation
- **Installer Definition**: `installer\ShopPOS.iss` compiles a 64-bit Inno Setup installer (`ShopPOS-Setup-v1.0.0.exe`) targeting Windows 10/11 x64, with single-instance mutex detection, desktop shortcuts, and an optional "Start with Windows" task.
- **Data Protection on Uninstall**: Uninstallation strictly preserves `C:\ShopData` and user backups by default, preventing catastrophic business data loss.
- **Build Release Script**:
  ```powershell
  powershell -ExecutionPolicy Bypass -File scripts\build-release.ps1
  ```
  Executes fast unit tests, publishes self-contained win-x64 binaries with ReadyToRun, compiles Inno Setup installer, and calculates SHA-256 hash.
- **Automated Installer Verification**:
  ```powershell
  powershell -ExecutionPolicy Bypass -File scripts\test-installer.ps1
  ```
  Silently installs into a temporary sandbox, verifies binary integrity, runs the application under `--smoke-test`, uninstalls silently, and verifies database retention.
- **User Documentation**:
  - [Complete User Guide](docs/USER-GUIDE.md)
  - [Production Release Checklist](docs/RELEASE-CHECKLIST.md)

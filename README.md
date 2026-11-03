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

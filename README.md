# ShopPOS - Offline-First Windows POS & Retail ERP

A modern, offline-first Windows desktop Point of Sale (POS), Master Data Management, Inventory, and Purchase system built for single-shop marts, retailers, and counters running on Windows 10/11.

---

## 1. Solution Structure

The solution `ShopPOS.sln` is organized into a clean, decoupled 4-layer architecture:

```
C:\Projects\ShopPOS\
├── ShopPOS.Core/          # Class Library: Domain Entities, Enums, Interfaces, Domain Services, DTOs, Validators
│   ├── Common/            # BaseEntity (Id, CreatedAt, UpdatedAt, CreatedByUserId, IsDeleted)
│   ├── DTOs/              # ProductDtos, MasterDataDtos, PurchaseDtos, InventoryDtos, CsvImportOptions
│   ├── Entities/          # Role, User, ShopSetting, Category, Brand, Unit, Product, ProductBarcode,
│   │                      # ProductPriceHistory, PriceLevel, ProductPrice, Supplier, Customer, StockMovement,
│   │                      # Purchase, PurchaseItem, SupplierPayment, PurchaseReturn, PurchaseReturnItem, StockAdjustment, AuditLog
│   ├── Enums/             # StockMovementType, StockStatus, CostingMethod, PurchaseStatus, PaymentStatus, StockAdjustmentReason
│   ├── Interfaces/        # ICurrentUserProvider, IAuthService, IPermissionService, IShopSettingService, IAuditLogService,
│   │                      # IProductService, ICategoryService, IBrandService, IUnitService, ISupplierService,
│   │                      # ICustomerService, IPriceLevelService, ICsvProductService, IStockAdjustmentService, etc.
│   ├── Models/            # Result, Result<T>, PagedResult<T>, DatabaseOptions, LoggingOptions
│   ├── Services/          # CurrentUserProvider, AuthService, PermissionService, ProductService, InventoryService, etc.
│   ├── Validators/        # FluentValidation for Products, Purchases, StockAdjustments, etc.
│   └── CoreServiceRegistration.cs
│
├── ShopPOS.Data/          # Class Library: EF Core SQLite, Configurations, Repositories, Migrations
│   ├── Configurations/    # EntityTypeConfigurations (Money/Quantity Converters, Unique Indexes, Relations)
│   ├── Converters/        # MoneyConverter (minor units x100), QuantityConverter (thousandths x1000)
│   ├── Interceptors/      # SqlitePragmaInterceptor (WAL mode & Foreign Keys on connection open)
│   ├── Migrations/        # EF Core Code-First Migrations (InitialCreate -> RemoveStockCount)
│   ├── Repositories/      # ProductRepository, InventoryRepository, PurchaseRepository, StockAdjustmentRepository, etc.
│   ├── Seeding/           # IDataSeeder, DataSeeder (Roles, Units, Fixed Owner User, Category, ShopSettings)
│   ├── Services/          # CsvProductService (streaming CSV import/export with UTF-8 BOM and batch transactions)
│   ├── AppDbContext.cs    # EF Core DbContext with global soft-delete query filter
│   ├── AppDbContextFactory.cs # Design-time factory for `dotnet ef` CLI
│   └── DataServiceRegistration.cs
│
├── ShopPOS.UI/            # WPF (.NET 10) Application: Views, ViewModels, DI Host, Navigation, Dialogs
│   ├── Helpers/           # ValueConverters (Currency, Status, LowStock, StockStatusDisplayConverter)
│   ├── Services/          # INavigationService, NavigationService, IDialogService, DialogService
│   ├── ViewModels/        # DashboardViewModel, ProductsViewModel, PurchasesViewModel, StockOverviewViewModel,
│   │                      # StockAdjustmentsViewModel, ReorderListViewModel, StockValuationViewModel, etc.
│   ├── Views/             # DashboardView, ProductsView, PurchasesView, StockOverviewView, StockAdjustmentsView,
│   │                      # ReorderListView, StockValuationView, SettingsView, Dialogs
│   ├── appsettings.json   # Configurable database path and Serilog file destination
│   ├── App.xaml / App.xaml.cs # Host Builder, Serilog file sink, Direct Dashboard Startup
│   └── MainWindow.xaml / MainWindow.xaml.cs # Responsive Main Shell with Sidebar Navigation
│
├── ShopPOS.Tests/         # xUnit Test Project
│   ├── AuthServiceTests.cs                     # Single-user Owner context and CurrentUserProvider
│   ├── DatabaseMigrationAndSeedDatabaseTests.cs # SQLite migrations, seed data, converters, soft delete
│   ├── MasterDataServicesTests.cs              # SKU auto-generation, barcode uniqueness, opening stock, price histories
│   ├── PurchaseModuleTests.cs                  # Purchases, payments, returns, numbering, concurrency, migration integrity
│   ├── InventoryModuleTests.cs                 # Stock adjustments, status boundaries, timestamps, valuation, reorder
│   ├── UiSmokeTests.cs                         # XAML load, layout, and rendering smoke tests for all views & dialogs
│   └── ViewModelDecouplingAndDialogTests.cs   # Architecture tests verifying zero Window/Owner coupling in ViewModels
│
├── ShopPOS.sln            # Standard Visual Studio Solution file
└── README.md              # Project documentation and developer guide
```

---

## 2. Product & Architecture Rules

- **100% Offline-First**: Zero cloud or internet dependencies required at runtime.
- **SQLite with WAL Mode**: Automatically executes `PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;` on every connection open via `SqlitePragmaInterceptor`.
- **Accurate Money Storage**: Monetary fields (`CostPrice`, `SalePrice`, `MinSalePrice`, `OpeningBalance`, `CreditLimit`, `UnitCost`) use `decimal` in C# and are stored in SQLite as 64-bit integer minor units (`value * 100`) via `MoneyConverter`, preventing floating-point rounding errors in SQL `SUM()` and comparisons.
- **Weight-Based Fractional Quantities**: Quantities (`Quantity`, `ReorderLevel`) use `decimal` in C# and are stored in SQLite as integer thousandths (`value * 1000`) via `QuantityConverter` to natively support items sold by weight (e.g. 1.250 kg).
- **Soft Delete Pattern**: Every entity inherits `BaseEntity` (`Id`, `CreatedAt`, `UpdatedAt`, `CreatedByUserId`, `IsDeleted`). All queries automatically filter out deleted records via EF Core Global Query Filters (`!e.IsDeleted`).
- **Inventory Ledger**: Stock levels are never directly edited; they derive from the immutable `StockMovements` ledger. Creating a product with opening stock atomically commits an `Opening` movement in the same transaction.
- **Audit Logging**: Every create, update, deactivate, and delete operation writes an `AuditLog` entry tracking user, timestamp, entity, and change details.
- **Price History Ledger**: Changing product cost or sale prices automatically generates a `ProductPriceHistory` record.

---

## 3. Master Data Management Screens (Phase 2)

All list views share a standardized generic CRUD base with server-side paging (50 items/page), 300 ms debounced search, active/inactive filtering, responsive layouts (1366x768 and up), and accessible keyboard shortcuts.

### 1. Products (`ProductsView.xaml`)
- **Columns**: SKU, Barcode, Name, Category, Brand, Unit, Cost Price, Sale Price, Tax %, Current Stock, Status.
- **Real-Time Stock Query**: Stock is aggregated dynamically in a single grouped SQL query from `StockMovements` (no N+1 query problem).
- **Visual Cues**: Rows with `Current Stock <= Reorder Level` are highlighted with an amber accent; inactive products appear greyed out.
- **Filter Dropdowns**: Category, Brand, Status (All / Active / Inactive), and "Low Stock Only" checkbox.
- **Product Dialog (5 Tabs)**:
  - *General*: Name, SKU, Category, Brand, Unit, Description.
  - *Pricing*: Cost Price, Sale Price, Min Sale Price, Tax %, and multi-tier Price Levels grid (Retail, Wholesale).
  - *Barcodes*: Primary barcode and additional barcodes (packs, singles, legacy codes). USB scanner enter-key safe.
  - *Stock*: Track Stock toggle, Reorder Level, and Opening Quantity (opening stock input shown only upon creation).
  - *Image*: Optional product image selection with auto-copying to `C:\ShopData\Images` and relative path storage.
- **Toolbar Actions**:
  - *Duplicate Product*: Copies name, category, brand, unit, pricing, and stock settings while leaving SKU and barcodes blank for safe new entries.
  - *Price History*: Opens the audit modal displaying historic cost and sale price changes, timestamps, and editing users.
  - *Export CSV*: Generates Excel-ready UTF-8 BOM CSV based on current filters.
  - *Import CSV*: Launches the 4-step wizard.

### 2. Categories (`CategoriesView.xaml`)
- Hierarchical **TreeView** representation supporting unlimited nesting.
- Actions: Add root category, Add child category, Edit/Rename category, Deactivate, Delete.
- **Cycle Prevention**: Prevents moving a category underneath itself or any of its descendants.
- **Referential Integrity**: Deleting a category with active products or child categories is blocked; the system prompts for deactivation instead.

### 3. Brands (`BrandsView.xaml`)
- Clean CRUD list with search and server-side pagination.
- Add and edit dialog with inline validation.
- Safe deletion check preventing removal of brands currently referenced by products.

### 4. Units of Measure (`UnitsView.xaml`)
- Unit management with `Name`, `ShortName` (e.g. `kg`, `pcs`), and `AllowDecimal` flag.
- Safe deletion check: Deleting units tied to existing products is blocked.

### 5. Suppliers (`SuppliersView.xaml`)
- Grid with search, pagination, and status filtering.
- Side detail panel displaying primary contact person, email, tax number, notes, and opening balance.
- Clear balance labeling: **"We owe them: $X.XX"** (signed amount).
- Opening balance is editable until transaction records exist.

### 6. Customers (`CustomersView.xaml`)
- Grid with search, pagination, and status filtering.
- Detail and edit dialog featuring Credit Limit, Default Price Level (Retail / Wholesale), Tax Number, and Notes.
- Clear balance labeling: **"They owe us: $X.XX"** (signed amount).

---

## 4. Keyboard Shortcuts

The entire Master Data suite supports fast keyboard-only operation:

| Shortcut | Scope | Action |
|---|---|---|
| `Ctrl + N` | Any List Screen | Add new record (opens dialog with focus on first input) |
| `Ctrl + S` | Any Modal Dialog | Save / Submit record |
| `Esc` | Any Modal Dialog | Cancel / Close dialog without saving |
| `Enter` or Double-Click | Grid DataRow | Edit selected record |
| `Del` | Grid DataRow | Delete or Deactivate selected record (with confirmation) |
| `F5` | Any List Screen | Refresh current grid data |
| `Enter` | Barcode Input Field | Adds or updates barcode without accidentally saving or closing the dialog (USB Scanner friendly) |

---

## 5. CSV Import & Export Specifications

### Export
- Formats all products respecting active search and category/brand filters.
- Encoded with **UTF-8 with BOM** (`0xEF, 0xBB, 0xBF`) to ensure instant compatibility when opened in Microsoft Excel.

### Import Wizard (4 Steps)
1. **File Selection**: Pick `.csv` file with progress indicator and downloadable sample template button.
2. **Preview & Mapping**: Instant preview of the first 20 rows with auto-detected columns.
3. **Validation Report**: Pre-import validation inspecting every row. Displays exact row numbers and friendly error messages for invalid formats.
4. **Execution & Confirmation**:
   - Choose collision strategy: **Skip Existing** or **Update Existing** (matched by SKU or Barcode).
   - Automatically provisions missing Categories, Brands, and Units (case-insensitive).
   - Commits records in atomic database transactions per batch of **500 rows**.
   - Asynchronous streaming architecture handles **10,000+ rows** smoothly with cancel capability and UI progress reporting.

### CSV Column Reference
| Column Header | Required | Example | Description |
|---|:---:|---|---|
| `SKU` | No | `PROD-0001` | Unique SKU. If empty, auto-generated sequentially (`P000001`). |
| `Barcode` | No | `8901234567890` | Primary barcode. Must be unique across all products and barcodes. |
| `Name` | **Yes** | `Organic Whole Milk 1L` | Product display name. |
| `Category` | No | `Dairy & Eggs` | Category name (auto-created if not found). |
| `Brand` | No | `Horizon` | Brand name (auto-created if not found). |
| `Unit` | No | `pcs` | Unit of measure name or short name (auto-created if not found). |
| `CostPrice` | No | `2.50` | Unit purchase cost (must be >= 0). Defaults to 0.00. |
| `SalePrice` | **Yes** | `3.99` | Unit selling price (must be >= 0). |
| `MinSalePrice` | No | `3.50` | Minimum allowable sale price (must not exceed SalePrice). |
| `TaxPercent` | No | `5.0` | Product tax percentage (0 to 100). Defaults to shop setting. |
| `Description` | No | `Pasteurized whole milk` | Product details / description. |
| `ReorderLevel` | No | `10` | Low-stock alert threshold. |
| `OpeningStock` | No | `25` | Initial stock (creates an `Opening` StockMovement). |
| `IsWeighable` | No | `false` | Set `true` if item is weighed at checkout. |
| `AllowDecimalQuantity` | No | `false` | Set `true` if fractional quantities (e.g. 1.250 kg) are permitted. |
| `IsActive` | No | `true` | Product active status (`true`/`false`). Defaults to `true`. |

---

## 6. Role-Based Navigation & Access Control

Access is strictly enforced at both the UI navigation layer and the Core Service layer:

| Module / Action | Cashier | Manager | Admin |
|---|:---:|:---:|:---:|
| **Products** (View / Search) | ✅ (Read-Only) | ✅ | ✅ |
| **Products** (Create / Edit / Delete / Import) | ❌ | ✅ | ✅ |
| **Categories / Brands / Units** | ✅ (Read-Only) | ✅ | ✅ |
| **Categories / Brands / Units** (Create / Edit / Delete) | ❌ | ✅ | ✅ |
| **Customers** (View / Search / Add / Edit) | ✅ | ✅ | ✅ |
| **Customers** (Delete / Deactivate) | ❌ | ✅ | ✅ |
| **Suppliers** (All Access) | ❌ | ✅ | ✅ |
| **Purchases / Payments / Returns** (Manage) | ❌ | ✅ | ✅ |
| **Settings & Users** | ❌ | ❌ | ✅ |

---

## 7. Purchase Module Architecture & Business Rules (Phase 3A)

Phase 3A delivers the domain model, business services, calculation engine, repository persistence, and tests for the Purchase module. (UI screens are deferred to Phase 3B).

### Purchase Order Workflow
1. **Draft**:
   - Initial editable state. Line items, quantities, costs, discounts, and freight can be freely modified or soft-deleted.
   - Drafts have **no effect** on inventory stock or supplier financial ledger.
   - Generates warnings if any line item unit cost deviates by > 30% from the product's current recorded cost price.
2. **Post** (All-or-Nothing Database Transaction):
   - Re-validates that supplier and products are active, quantities > 0, unit costs >= 0, and invoice date is not more than 1 day in the future.
   - Creates a `StockMovement` entry per line with `MovementType = Purchase`, positive quantity, and `UnitCost` set to the effective net unit cost (after line discount, before tax).
   - Recalculates product `CostPrice` based on the configured `CostingMethod` and records `ProductPriceHistory` attributed to the logged-in user.
   - Supports immediate "Pay Now" allocation: creates a linked `SupplierPayment` atomically in the same transaction.
   - Transitions status to `Posted`, recording `PostedAt` and `PostedByUserId`.
   - **Posted purchases are immutable** and cannot be edited or deleted.
3. **Cancel**:
   - Requires a mandatory cancellation reason.
   - **Preconditions**:
     - Blocked if any active (non-cancelled) payments are allocated to the purchase (all linked payments must be cancelled first).
     - Blocked if any active purchase returns are linked to the invoice.
     - Blocked if deducting the purchased stock would drive any product quantity below zero while `ShopSetting.AllowNegativeStock` is `false`.
   - Reverses inventory by creating offsetting `StockMovement` rows (`MovementType = PurchaseCancel`, negative quantity). Original movements are strictly preserved.
   - Transitions status to `Cancelled`, recording `CancelledAt`, `CancelledByUserId`, and `CancelReason`.

### Costing Methods & Valuation Formulas
Configured store-wide via `ShopSetting.CostingMethod`:
1. **Weighted Average (`WeightedAverage`, Default)**:
   - Evaluates current on-hand ledger stock before applying the purchase:
     $$\text{New Cost} = \frac{(\text{Current Stock} \times \text{Current Cost}) + (\text{Purchase Qty} \times \text{Net Unit Cost})}{\text{Current Stock} + \text{Purchase Qty}}$$
   - **Zero / Negative Stock Guard**: If current ledger stock is $\le 0$, the purchase's net unit cost becomes the new cost price directly, avoiding division-by-zero or distortion from negative stock.
2. **Last Cost (`LastCost`)**:
   - Updates the product cost price directly to the effective net unit cost of the latest posted purchase:
     $$\text{New Cost} = \text{Purchase Net Unit Cost}$$

### Transaction-Safe Numbering
Document numbers (`PUR-000001`, `SPAY-000001`, `PRET-000001`) are coordinated through the `NumberSequences` table via `NumberSequenceService`.
- Utilizes native SQLite atomic upsert with returning:
  ```sql
  INSERT INTO NumberSequences ([Key], [LastNumber]) VALUES (@key, 1)
  ON CONFLICT([Key]) DO UPDATE SET [LastNumber] = [LastNumber] + 1
  RETURNING [LastNumber];
  ```
- Protected by an asynchronous `SemaphoreSlim` lock, ensuring zero duplicate numbers even under concurrent, high-frequency asynchronous saves.

### Supplier Balances & Financial Ledger
Supplier balance (total outstanding debt owed by the business) is **never stored statically**; it is computed dynamically in SQL queries:
$$\text{Supplier Balance} = \text{Opening Balance} + \sum \text{Posted Purchases} - \sum \text{Posted Returns} - \sum \text{Active Payments}$$
- **Supplier Ledger**: Chronological audit statement reporting opening balance, all debits (purchases), credits (payments, returns), and running balance per transaction.

### Purchase Returns & Supplier Payments
- **Purchase Return**: Decreases stock via `StockMovementType.PurchaseReturn` (negative quantity). Return quantity cannot exceed the purchased quantity minus previously returned quantities on the invoice. Cancelling a return creates a reversing `PurchaseReturnCancel` movement.
- **Supplier Payment**: Immutable once recorded. Payment allocated to a specific purchase cannot exceed the outstanding balance (`GrandTotal - Returns - Active Payments`). Mistakes are corrected by cancelling the payment record.

### Current Limitations
- **No Retroactive Cost Recalculation**: Cancelling a posted purchase reverses the inventory quantities in the stock ledger, but historical `CostPrice` and `ProductPriceHistory` changes are not rolled back retroactively to avoid corrupting intermediate profit margins and past sales valuations. Historical purchase items retain their snapshotted unit costs and product names.

---

## 7. Purchase Management Screens (Phase 3B)

Phase 3B delivers full desktop UI screens for purchase operations, supplier accounting, debit notes, ledger statements, and configuration:

### 1. Purchases List (`PurchasesView.xaml`)
- **Columns**: Purchase Number, Date, Supplier, Supplier Invoice No, Grand Total, Paid Amount, Balance, Payment Status, Document Status.
- **Filters**: Date range (From / To), Supplier, Document Status (`Draft`, `Posted`, `Cancelled`), Payment Status (`Unpaid`, `PartiallyPaid`, `Paid`, `Overdue`).
- **Debounced Search**: Matches purchase number, supplier invoice number, or supplier name.
- **Paging**: Server-side pagination at 50 records per page.
- **Action Shortcuts**:
  - `Ctrl+N`: New Purchase
  - `Enter`: Open / View Purchase details
  - `F5`: Refresh list
- **Post & Cancel Actions**:
  - Direct Post button with confirmation.
  - Cancel button prompting for a mandatory cancellation reason and executing stock/ledger reversals.
  - PDF Export via QuestPDF.

### 2. Purchase Entry Window (`PurchaseEntryWindow.xaml`)
- **Full-Page Dialog**: Optimized for rapid invoice entry.
- **Header**: Searchable supplier selector, invoice number, purchase date, due date, notes.
- **Barcode & Fast Scan Bar**:
  - Enter key adds item or increments existing line quantity.
  - Fully compatible with USB barcode scanners.
  - `F2`: Opens searchable Product Catalog Search modal with real-time stock and prices.
- **Line Items Grid**:
  - Editable columns: Quantity, Unit Cost, Discount %, Tax % (auto-populated from product).
  - Computed Line Total and effective Net Unit Cost.
  - `Delete`: Removes selected line.
- **Sidebar Inspector**:
  - Displays selected product's current stock on hand and last recorded cost price.
  - Alerts user with warning badges if entered unit cost deviates > 30% from recorded cost.
- **Invoice Footer**:
  - Subtotal, Line Discount Total, Invoice Discount, Tax Amount, Freight, and Grand Total.
  - "Pay Now" immediate payment option with payment method dropdown.
- **Audit & Read-Only Protection**:
  - Posted and Cancelled purchases load strictly in read-only mode with audit stamps (`PostedByName`, `CancelledByName`, `CancelReason`).
  - `Ctrl+S`: Save Draft.
  - `Ctrl+Enter`: Post Purchase.
  - `Esc`: Close window (prompts confirmation if unsaved changes exist).

### 3. Supplier Payments (`SupplierPaymentsView.xaml` & `SupplierPaymentDialog.xaml`)
- **Payment List**: Historical disbursements with reference codes, payment methods, and cancellation statuses.
- **Record Payment Modal**:
  - Supplier picker automatically loads open invoices with outstanding balances.
  - Selecting an invoice auto-fills the payment amount to the outstanding balance.
  - Supports Cash, Card, Bank Transfer, Cheque, and Other.
- **Cancellation**: Cancel payment button requires a reason, marks payment cancelled, and restores invoice balance.

### 4. Purchase Returns (`PurchaseReturnsView.xaml` & `PurchaseReturnDialog.xaml`)
- **Returns List**: Debit notes with supplier, invoice reference, total credit, and status badge.
- **Create Return Modal**:
  - Select supplier and posted purchase invoice to load invoice line items.
  - Return quantities are strictly capped at returnable quantities (`Purchased - AlreadyReturned`).
- **Cancellation**: Cancel return button prompts for reason, reverses stock deduction, and restores vendor payable balance.

### 5. Supplier Ledger (`SupplierLedgerView.xaml`)
- **Statement Report**:
  - Filters by supplier and date range.
  - KPI Stat Cards: Opening Balance, Total Invoiced (Debits), Total Paid/Credited (Credits), Closing Balance.
  - Detailed chronological ledger grid with Running Balance per transaction.
  - One-click export to PDF report and CSV spreadsheet.

### 6. Updated Suppliers Screen (`SuppliersView.xaml`)
- Real-time payable Balance column in supplier data grid.
- "View Ledger" button navigates directly to the Supplier Ledger for the selected vendor.

### 7. Settings Screen (`SettingsView.xaml`)
- General store details: Store name, address, phone, tax ID, currency symbol, receipt footer, paper width (58/80 mm), default tax %.
- **Admin-Only Controls**:
  - Inventory Costing Method (`WeightedAverage` vs `LastCost`) with inline educational guides.
  - `AllowNegativeStock` toggle with warning context.

### 8. Decoupled Dialog Architecture & Central Dialog Service
- All UI dialogs are opened exclusively through `IDialogService`.
- Zero direct references to `System.Windows`, `Window`, or `Application.Current.MainWindow` inside ViewModels.

---

## 8. Inventory & Single-User Operational Model

### 1. Inventory Workflow
The inventory module provides end-to-end control over the retail store's physical assets:
1. **Stock Overview (`StockOverviewView.xaml`)**: Central cockpit displaying live inventory across all products with real-time stock balances calculated directly from the immutable `StockMovements` ledger. Supports distinct status filtering: All, OK, Low stock, Out of stock, Negative, and Not tracked.
2. **Stock Adjustments (`StockAdjustmentsView.xaml`)**: Manual corrections for damages, shrinkages, expired goods, or count reconciliations with an atomic draft/post workflow.
3. **Reorder & Replenishment (`ReorderListView.xaml`)**: Restock recommendations for items at or below reorder levels (including both Low Stock and Out of Stock items with exact status indicators), with one-click purchase order generation.
4. **Category Stock Valuation (`StockValuationView.xaml`)**: Margin and valuation analytics reporting total units, cost asset value, retail value, and potential profit across product categories.
5. **Stock Movement History (`StockMovementHistoryView.xaml`)**: Audit log tracking every change in stock quantity with running balance calculation, ordered chronologically with full local timestamps (`dd MMM yyyy HH:mm`).

### 2. Stock Status Rules
Stock status is evaluated dynamically from the ledger:
| Status | Condition | Description | UI Indicator |
|---|---|---|---|
| **Negative** | $\text{CurrentStock} < 0$ | Stock level is below zero (anomaly requiring investigation). | Red badge / Label "Negative" |
| **OutOfStock** | $\text{CurrentStock} = 0$ | Inventory is completely depleted. | Red badge / Label "Out of stock" |
| **Low** | $0 < \text{CurrentStock} \le \text{ReorderLevel}$ | Stock has reached or fallen below minimum shelf threshold. | Amber badge / Label "Low stock" |
| **OK** | $\text{CurrentStock} > \text{ReorderLevel}$ | Healthy inventory level above reorder point. | Green badge / Label "OK" |
| **Not Tracked** | $\text{!TrackStock}$ | Product inventory is not tracked. | Grey badge / Label "Not tracked" |

*Note: The "Low stock only" filter strictly excludes out-of-stock items ($stock \le 0$). The executive dashboard displays separate Low Stock and Out of Stock metric cards that navigate to Stock Overview with the matching filter pre-selected.*

### 3. Stock Adjustment & Multi-Product Selection
- **Multi-Product Search Dialog (F2)**: The product search popup features multi-selection with checkboxes, a "Select All" header toggle, and an "Add Selected" action. Products already present in the adjustment document are flagged as `In document` and disabled from duplicate addition.
- **Independent Line Selection & Keyboard Navigation**: Every item row in the adjustment grid is independently selectable. Pressing Tab or Enter inside a line's `New Quantity` box automatically advances focus and selects the text of the next row's `New Quantity` box.
- **Bulk Row Deletion**: The grid supports single-line removal buttons as well as a bulk `Remove Selected Lines` action.
- **Atomic Ledger Difference**: When posted, the system re-reads the ledger inside the database transaction and writes `StockMovement` records only for non-zero differences (`NewQuantity - CurrentLedgerStock`). Lines with unchanged quantities produce zero movements.

### 4. Movement Timestamps & Legacy Rows
- All stock movements, purchases, supplier payments, returns, and adjustments capture the full date and local time of day (`DateTime.Now`).
- Movement history lists and CSV exports display timestamps in `dd MMM yyyy HH:mm` format, ordered by `MovementDate`, then `Id`.
- Legacy rows stored at midnight (00:00:00) automatically fall back to the record's `CreatedAt` local timestamp for display without rewriting historical rows.

### 5. Single-User Architecture (No Login, No Roles)
- **Direct Startup**: The application launches straight into the Dashboard with zero login prompts or password dialogs.
- **Owner Context**: A fixed local user (`Id = 1`, FullName "Owner") is provided via `ICurrentUserProvider`. All audit columns (`CreatedByUserId`, `PostedByUserId`, `AuditLog.UserId`) reference this user seamlessly.
- **Foreign Key Integrity**: The `Users` and `Roles` tables remain intact in the database schema to preserve referential integrity with historical records.
- **Unrestricted Access**: All menu items, purchase operations, inventory adjustments, and system settings are fully accessible to the Owner without role restrictions.

### 6. Architectural Scope & Limitations
- **Single-Store Offline Desktop**: Built specifically for single-store marts, grocery counters, and retail shops running standalone on Windows 10/11 with zero external cloud or internet dependencies.
- **Single Central Inventory Ledger**: Does not support multi-warehouse bin-location routing or inter-branch stock transfers. All stock events flow through the central `StockMovements` ledger.
- **Sequential Document Numbering**: Document numbers (`ADJ-XXXXXX`, `PUR-XXXXXX`, `RET-XXXXXX`, `PAY-XXXXXX`) are generated using SQLite transactions with optimistic concurrency in WAL mode, guaranteeing gapless, unique sequences without duplicate numbers under concurrent operations.
- **No Direct Stock Manipulation**: Product stock cannot be altered via direct edits to `Product` records. All inventory alterations occur exclusively through transactional ledger entries.

### 7. Executive Dashboard (`DashboardView.xaml`)
- **High-Level Metrics**: Total active products, Low stock count, Out of stock count, Total stock value at cost, Total outstanding supplier payables, and Total purchases this month.
- **Interactive Drilldown**: Clicking on the Low Stock or Out of Stock cards navigates directly to Stock Overview with the respective filter applied.

---

## 9. How to Add EF Core Migrations

When domain entities or configurations change:

### Add a New Migration
```powershell
dotnet ef migrations add Phase4_Inventory --project ShopPOS.Data --startup-project ShopPOS.Data
```

### Apply Migrations to SQLite Database
```powershell
dotnet ef database update --project ShopPOS.Data --startup-project ShopPOS.Data
```
*(Note: Migrations and initial reference data seeding are also applied automatically at application startup by `App.xaml.cs`).*

---

## 10. How to Build, Test & Run

### Prerequisites
- Windows 10 or 11
- .NET 10 SDK (`dotnet --version` >= 10.0.x)

### 1. Build the Solution
```powershell
cd C:\Projects\ShopPOS
dotnet build ShopPOS.sln
```

### 2. Run All Unit & Integration Tests
```powershell
dotnet test ShopPOS.sln --filter "Category!=Slow"
```

### 3. Launch the Application
```powershell
dotnet run --project ShopPOS.UI
```

---

## 11. Database Verification & Location

The SQLite database file is stored at:
```
C:\ShopData\shop.db
```
Product images are stored at:
```
C:\ShopData\Images\
```
Log files are stored at:
```
C:\ShopData\Logs\app-.log
```
The database can be inspected with [DB Browser for SQLite](https://sqlitebrowser.org/) to verify all tables, WAL mode, foreign key enforcement, unique constraints, and seeded records.

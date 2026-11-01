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

## 8. Sales Module Backend (Phase 5A)

The sales module backend provides a high-performance, transactionally secure sales, billing, shift management, and customer returns engine tailored for single-store retail counters.

### 1. Sales & Checkout Workflow
- **Product Lookup (`IProductLookupService`)**:
  - Direct scanning of primary barcodes or additional package barcodes.
  - SKU code search fallback.
  - Quick quantity prefix expressions (e.g., `5*BARCODE` parses as quantity `5` of barcode).
  - Price level and customer tier price resolution (`PriceLevelId`).
- **Calculation Engine (`ISaleCalculator` / `SaleCalculator`)**:
  - Operates using midpoint away from zero rounding for currency minor units ($0.01).
  - **Line Discounts**: Specified as percentage (`DiscountPercent`) or fixed amount (`DiscountAmount`).
  - **Bill-Level Discounts**: Proportional allocation across bill lines using integer minor unit distribution with remainder absorption into the largest line total to guarantee zero rounding divergence ($Total Lines = Bill Total$).
  - **Tax Handling**: Supports both tax-exclusive pricing ($\text{LineTotal} \times (1 + \text{Tax\%})$) and tax-inclusive pricing ($\text{Tax} = \text{Amount} - [\text{Amount} / (1 + \text{Tax\%})]$) according to `PricesIncludeTax` store setting.
  - **Cash Rounding**: Supports store-configurable cash rounding steps (e.g., `0.00`, `0.05`, `0.10`, `0.25`, `0.50`, `1.00`), recording the exact difference in `RoundOff`.
- **Atomic Transaction Commit (`ISaleService.CompleteSaleAsync`)**:
  - Re-reads product details, tax rates, and minimum allowed prices.
  - Re-reads inventory stock from the `StockMovements` ledger *inside the active database transaction* to eliminate race conditions.
  - Blocks sales that would cause negative stock if `AllowNegativeStock` is disabled.
  - Non-tracked products bypass inventory deductions and negative stock blocks.
  - Generates immutable sequential sale numbers (`INV-000001`) via atomic sequence generator.
  - Records line snapshots (`ProductName`, `Sku`, `Barcode`, `UnitPrice`, `CostPriceSnapshot`).
  - Writes `StockMovement` ledger entries with movement type `Sale` and links `ReferenceId` to the sale.
  - Generates comprehensive `AuditLog` records.

### 2. Held Bills (Parked Sales)
- **Parking (`SaveHeldBillAsync`)**: Cart state is saved with status `Held` and sequential hold number (`HOLD-000001`). Held bills do not create stock movements or affect customer balances.
- **Listing & Resuming**: Active held bills are retrieved with `GetHeldBillsAsync()`. When completing a sale originating from a held bill, the held record is cleanly consumed.
- **Cancellation**: Discarding a held bill soft-deletes the record without affecting inventory.

### 3. Credit Sales & Customer Balances
- **Credit Policy Validation**: Credit sales (where $\text{PaidAmount} < \text{GrandTotal}$) require an active customer and require `AllowCreditSales = true`.
- **Credit Limit Verification**: Customer outstanding balance plus new unpaid amount cannot exceed `Customer.CreditLimit` (when limit > 0).
- **Dynamic Ledger Balance Query (`ICustomerBalanceQuery`)**: Customer balances are computed in real time:
  $$\text{CurrentBalance} = \text{OpeningBalance} + \sum \text{Sale.DueAmount} - \sum \text{StoreCreditRefunds}$$

### 4. Split Tender & Overpayment Rules
- **Multiple Payment Tenders**: Sales support split tenders across `Cash`, `Bank`, `Cheque`, `Card`, `MobileWallet`, and `StoreCredit`.
- **Overpayment & Change**:
  - Overpayment ($\text{Tendered} > \text{GrandTotal}$) is strictly permitted only when payment method is `Cash`.
  - Non-cash payment methods reject overpayment.
  - The resulting change is recorded in `ChangeAmount`, while `PaidAmount` reflects the net revenue retained.

### 5. Shifts & Expected Cash Management (`IShiftService`)
- **Single Open Shift Rule**: Only one active shift is permitted at any given time. This rule is enforced both at the domain service layer and at the database schema level via a SQLite filtered unique index:
  ```sql
  CREATE UNIQUE INDEX IX_Shifts_Status ON Shifts (Status) WHERE [Status] = 1 AND [IsDeleted] = 0;
  ```
- **Sequential Shift Numbering**: Shifts receive formatted sequential identifiers (`SHF-000001`).
- **Mid-Shift Cash Movements**: Cashiers can log `CashIn` (float top-ups) or `CashOut` (petty expenses) against the active shift with mandatory reasons.
- **Expected Cash Formula**:
  $$\text{ExpectedCash} = \text{OpeningCash} + \text{NetCashSales} - \text{CashRefunds} + \text{CashIn} - \text{CashOut}$$
- **Shift Reconciliation & Closure**:
  - The cashier enters `CountedCash`.
  - System computes $\text{Difference} = \text{CountedCash} - \text{ExpectedCash}$ (overage or shortage).
  - Shift status transitions to `Closed`, locking the shift from further modifications.

### 6. Sales Returns & Refunds (`ISaleReturnService`)
- **Return Identifiers**: Returns receive sequential identifiers (`SRET-000001`).
- **Returnable Line Validation**: Quantities returned cannot exceed $\text{OriginalSoldQuantity} - \sum \text{PreviouslyReturnedQuantities}$.
- **Return Window Enforcement**: Returns are blocked if the sale date exceeds `SaleReturnWindowDays` (e.g. 30 days).
- **Non-Receipt Returns**: Returns without an invoice require `AllowReturnWithoutReceipt = true`.
- **Restocking vs Damaged Goods**:
  - `ReturnToStock = true`: Generates a `StockMovementType.SaleReturn` ledger entry restoring product stock.
  - `ReturnToStock = false` (damaged/expired items): No stock is added back to active inventory.
- **Refund Methods**: Supports refunding via original payment method, `Cash`, or issuing `StoreCredit` credited to the customer account.
- **Return Cancellation**: If a return is cancelled, the restocked inventory is safely re-deducted, asserting that stock does not go negative if negative stock is disallowed.

### 7. Store Settings Configured for Sales
- `RequireOpenShiftForSales` (Default: `false`) - Enforces opening a shift before completing sales.
- `AllowCreditSales` (Default: `true`) - Allows sales with outstanding balance to customers.
- `AllowSellingBelowMinPrice` (Default: `false`) - Prevents cashiers from discounting below minimum selling price.
- `PricesIncludeTax` (Default: `true`) - Toggles between gross (tax inclusive) and net pricing modes.
- `CashRoundingStep` (Default: `0.00`) - Step for rounding cash totals (0.00, 0.05, 0.10, 0.25, 0.50, 1.00).
- `AllowReturnWithoutReceipt` (Default: `false`) - Requires invoice reference for returns when disabled.
- `SaleReturnWindowDays` (Default: `30`) - Days after purchase allowed for product returns.
- `AutoPrintReceipt`, `ReceiptPrinterName`, `OpenCashDrawerOnCashSale`, `ReceiptCopies` - Receipt hardware options.

### 8. POS & Sales Screens (Phase 5B)

The POS frontend provides an ultra-fast, keyboard-driven checkout interface optimized for high-throughput retail counters at screen resolutions of 1366x768 and higher. All window transitions and popups are strictly decoupled through `IDialogService`.

#### 1. POS Selling Screen (`PosSaleView.xaml` / `PosSaleViewModel.cs`)
- **Barcode & Multiplier Scanning**:
  - Top barcode input keeps focus after every operation.
  - Multiplier prefix support (`"5*123456"`) adds 5 units of product in a single scan.
  - Live 300 ms debounced product suggestions appear when typing product names.
  - `F2` opens the multi-select Product Search dialog with live stock counts and categories.
- **Cart Management**:
  - Quantity `+` and `-` buttons, direct quantity edits, and line removal.
  - Stock badges: Out of stock (red), Low stock (amber), Negative (dark red), OK (green), Not tracked (grey).
  - Minimum selling price validation blocks discounted pricing below cost/minimum allowed price.
  - Line-level (`F6`) and Bill-level (`F7`) discount dialogs with percent or fixed minor unit values.
  - Cart totals (Subtotal, Tax, Bill Discount, Round-off, Grand Total) are derived strictly from `ISaleCalculator`.
- **Customer Selection (`F3`)**:
  - Quick walk-in default or select an existing customer.
  - Displays real-time outstanding customer balance and credit limit.
- **Park & Resume Bills (`F4` / `F5`)**:
  - Park cart to held bills (`F4`) to serve another customer without losing cart items.
  - Held bills dialog (`F5`) lists parked carts with line counts, customer names, and resume/delete options.
  - Closing or navigating away from a non-empty cart displays an exit prompt asking to **Hold**, **Discard**, or **Keep**.
- **Checkout & Payment Dialog (`F12` / `Ctrl+Enter`)**:
  - Split tender payment across `Cash`, `Card`, `Bank`, and `MobileWallet`.
  - Quick cash note buttons automatically rounded up to common currency steps (e.g., $10, $20, $50, $100).
  - Accurate change calculation for cash overpayments; rejects non-cash over-tender.
  - Enforces customer selection and credit limits for credit sales (sales with an outstanding balance).
- **Post-Sale Overlay**:
  - Displays change due, total paid, and sale number.
  - One-click receipt reprint and PDF receipt preview via QuestPDF.
  - Auto-print receipt upon transaction completion when enabled in settings.
- **Shift Enforcement**:
  - When `RequireOpenShiftForSales = true`, completing a sale or scanning an item without an open shift prompts the cashier to open a shift with initial drawer float cash.

#### 2. Keyboard Shortcut Matrix
| Shortcut | Action | Description |
|:---|:---|:---|
| **F1** | Help | Opens POS shortcut cheat-sheet dialog |
| **F2** | Product Search | Multi-select product search popup |
| **F3** | Customer | Select customer / view customer credit balance |
| **F4** | Hold Bill | Park current cart to held bills |
| **F5** | Resume Bill | View and resume parked held bills |
| **F6** | Line Discount | Apply percentage or fixed discount to selected line |
| **F7** | Bill Discount | Apply invoice-level discount |
| **F8** | Cash In/Out | Log float cash in or petty expense cash out |
| **F9** | Returns | Navigate to Sale Returns screen |
| **F10** | Drawer | Trigger cash drawer kick |
| **F12** / **Ctrl+Enter** | Checkout | Open payment dialog |
| **Ctrl+D** / **Esc** | Clear Cart | Prompt to Hold, Discard, or Keep non-empty cart |
| **Delete** | Delete Line | Remove selected cart line item |
| **+** / **-** | Qty +/- | Increment or decrement selected cart line quantity |

#### 3. Sales List Screen (`SalesView.xaml` / `SalesViewModel.cs`)
- **Paging & Filtering**: 50 items/page, date range filter, customer filter, sale status, "With Due Only" toggle, and text search.
- **Summary Totals**: Running summary of filtered sales count, gross revenue, collected payments, and total due amount.
- **Actions**:
  - *View Details*: Opens sale invoice modal with complete line item breakdown, tax details, and payment tenders.
  - *Reprint Receipt*: Generates 80mm thermal receipt PDF via QuestPDF.
  - *Cancel Sale*: Cancels sale with mandatory reason prompt, reversing stock movements in the ledger.
  - *Process Return*: Quick navigation to Sale Return dialog with pre-loaded invoice lines.
  - *Export*: Export filtered list to UTF-8 BOM CSV or printable PDF report.

#### 4. Sale Returns Screen & Dialog (`SaleReturnsView.xaml` / `SaleReturnDialog.xaml`)
- **Invoice Search**: Search sale by invoice number to load returnable line items.
- **Quantity Limits**: Return quantity is strictly clamped to $\text{SoldQty} - \sum \text{PreviouslyReturnedQty}$.
- **Restock vs Write-off**: Cashier specifies per item whether returned item should be returned to inventory (`ReturnToStock = true`) or written off as damaged/expired goods.
- **Refund Method**: Cash, Card, Bank, or Store Credit added to customer balance.

#### 5. Shifts Screen & Dialogs (`ShiftsView.xaml` / `ShiftsViewModel.cs`)
- **Active Shift Card**: Real-time display of shift number, open time, opening float, cash sales, non-cash sales, returns, cash in/out, and expected cash drawer total.
- **Mid-Shift Cash Movements (`CashMovementDialog.xaml`)**: Log float additions (`CashIn`) or petty cash withdrawals (`CashOut`) with mandatory justification notes.
- **Shift Reconciliation & Closure (`CloseShiftDialog.xaml`)**:
  - Cashier enters actual counted cash.
  - System computes difference ($\text{Discrepancy} = \text{Counted} - \text{Expected}$) and badges overage (blue) or shortage (red).
- **Shift Reports**:
  - *X-Report*: Mid-shift interim audit report PDF without closing the register.
  - *Z-Report*: Official end-of-shift closure report PDF summarizing financial breakdown, payment methods, and cash discrepancy.
- **Shift History**: Past shifts table with status, opening cash, closing cash, discrepancy, and historical Z-Report reprint.

#### 6. Customer Ledger & Settings Integration
- **Customer Screen (`CustomersView.xaml`)**: Includes real-time "Balance" column computed from ledger; "Sales History" button filters sales list for selected customer.
- **Settings Screen (`SettingsView.xaml`)**: Configures POS policies: `RequireOpenShiftForSales`, `AllowCreditSales`, `AllowSellingBelowMinPrice`, `PricesIncludeTax`, `CashRoundingStep`, `SaleReturnWindowDays`, `AllowReturnWithoutReceipt`, `AutoPrintReceipt`, and `ReceiptPrinterName`.
- **Dashboard Screen (`DashboardView.xaml`)**: Adds live POS metric cards: Today's Sales Count & Total, Today's Profit, Drawer Expected Cash, Held Bills Count, and Customers Owing Total, with a 1-click "Start POS Sale" button.

---

## 9. Receipt Printing, Reprint & Cash Drawer Hardware (Phase 5C)

ShopPOS features a production-grade, 3-tier offline printing subsystem designed for reliability, speed, and zero physical dependency blockers.

```
                    ┌────────────────────────┐
                    │    ReceiptDocument     │
                    │   (Printer-Agnostic)   │
                    └───────────┬────────────┘
                                │
               ┌────────────────┼────────────────┐
               ▼                ▼                ▼
       ┌───────────────┐ ┌───────────────┐ ┌───────────────┐
       │  PdfPreview   │ │ WindowsDriver │ │   EscPosRaw   │
       │   (QuestPDF)  │ │ (GDI Spooler) │ │ (winspool.drv)│
       └───────┬───────┘ └───────┬───────┘ └───────┬───────┘
               │                 │                 │
       ┌───────▼───────┐         ▼                 ▼
       │ ReceiptPreview│    Windows Print    Direct ESC/POS
       │ Dialog (WPF)  │    Queue Spooler    Thermal Printer
       └───────────────┘                     & RJ11 Drawer
```

### 1. Architectural Layers

1. **`ReceiptDocument` Model (`ShopPOS.Core/Models/ReceiptDocument.cs`)**:
   - Clean, printer-independent data structure built via `IReceiptBuilder`.
   - Populated from completed Sales, Sale Returns, Shift Z-Reports, or hardware Diagnostic Test Slips.
   - Includes: Shop branding & tax registration, invoice header, word-wrapped item lines, discounts, tax breakdowns, cash rounding, multi-tender payments, change, customer due balance, footer notice, reprint watermark banner (`*** DUPLICATE COPY ***`), and Code 128 barcode value.
   - Native width layout handling: 58mm (32 chars) and 80mm (48 chars).

2. **Renderers & Dispatching (`ShopPOS.Data/Services/ReceiptPrinterService.cs`)**:
   - **`PdfPreview` (Guaranteed Fallback)**:
     - Uses QuestPDF to render high-DPI vector documents.
     - Supports 58mm roll, 80mm roll, and full corporate A4 tax invoices.
     - Displays directly inside `ReceiptPreviewDialog.xaml` as rendered page images, with 1-click system printing and "Save PDF" file export.
     - Runs without any physical printer installed or connected.
   - **`WindowsDriver`**:
     - Prints directly through any standard Windows printer driver using `System.Drawing.Printing.PrintDocument`.
     - Supports network shared printers, USB receipt printers with Windows drivers, and virtual PDF printers.
   - **`EscPosRaw`**:
     - Pure ESC/POS thermal command generator (`EscPosGenerator.cs`) bypasses driver rendering for instant printing speeds.
     - Sends raw byte buffers directly to the Windows Spooler queue via Win32 `winspool.drv` P/Invoke (`RawPrinterHelper.cs`).
     - Supported hardware commands: Initialization (`ESC @`), text alignment (`ESC a n`), bold (`ESC E n`), Code 128 barcode generation (`GS k 73`), paper cut (`GS V 66 0`), and cash drawer kick pulses (`ESC p 0/1`).
     - Automated currency symbol normalization (e.g. converting non-CP437 symbols like `Rs.` or `$` safely for standard thermal codepages).

### 2. Cash Drawer Integration
- **Universal Dual-Pin RJ11 Kick Pulse**: Thermal printers use either RJ11 Pin 2 (`\x1B\x70\x00\x19\xFA`) or Pin 5 (`\x1B\x70\x01\x19\xFA`) to fire the cash drawer solenoid. `EscPosGenerator.GenerateDrawerPulse()` sends both pulses in sequence to guarantee compatibility across Epson, Star, Citizen, Munbyn, Bixolon, Xprinter, POSBANK, and generic ESC/POS printers.
- **Automated Trigger**: The drawer is triggered automatically upon completing a sale if `OpenCashDrawerOnCashSale = true` and the sale includes cash tender.
- **Manual Control**: Accessible anytime via the **F10** hotkey in POS, the dedicated "Open Drawer" button in the Settings screen, and diagnostic controls in the Printer Settings Dialog.

### 3. Reprint Tracking & Audit Trail
- Receipts can be reprinted from:
  - POS Checkout Success Panel (**Reprint (Duplicate)** button)
  - Sales List screen (**Print Receipt** action)
  - Sale Detail dialog (**Reprint Receipt** and **Print A4 Invoice** buttons)
- Every reprint automatically enables `IsDuplicateCopy = true`, adding a prominent bold banner:
  ```
  *** DUPLICATE COPY ***
  Reprint #1 on 2026-11-01 10:15
  ```
- All reprints write an entry to `AuditLog` capturing `UserId`, action `"ReprintReceipt"`, entity `"Sale"`, sale ID/number, and timestamp.

### 4. Non-Blocking Resilience & Safety
- **Sales Never Roll Back on Print Failure**: Printing runs off the WPF UI thread via `Task.Run()`. Any spooler error, disconnected cable, paper jam, or missing printer displays a clear non-blocking notice (`IDialogService.ShowMessage`) informing the cashier that the sale was saved successfully and can be reprinted anytime.
- **Safe Spooler Enumeration**: Printer discovery safely catches cases where the Windows Print Spooler service (`spoolsv.exe`) is stopped or unavailable, returning friendly guidance rather than throwing unhandled exceptions.

### 5. Manual Hardware Verification Checklist

When deploying ShopPOS on a new physical counter, run this verification checklist:

| Step | Test Item | Verification Procedure | Expected Outcome |
|:---:|:---|:---|:---|
| 1 | **Printer Discovery** | Open Settings -> Hardware & Printing -> View Printer dropdown | All installed Windows printers are visible in the dropdown. If Spooler is stopped, a warning banner appears. |
| 2 | **Paper Width Test (80mm)** | Select 80mm, set mode to `PdfPreview`, click **Test Print** | Preview dialog opens showing 48-column test receipt with aligned totals and Code 128 barcode. |
| 3 | **Paper Width Test (58mm)** | Select 58mm, click **Test Print** | Preview dialog opens showing 32-column test receipt with correct compact word wrapping. |
| 4 | **ESC/POS Raw Thermal Test** | Select installed USB/Network thermal printer, set mode to `EscPosRaw`, click **Test Print** | Printer immediately feeds slip, prints formatted text, bold headers, barcode, and performs a clean partial/full paper cut. |
| 5 | **Cash Drawer Kick Test** | Connect cash drawer via RJ11 cable to printer receipt port; click **Open Drawer** (or press **F10** on POS screen) | Drawer solenoid fires instantly and drawer pops open. |
| 6 | **Auto-Print on Cash Sale** | Enable `AutoPrintReceipt` and `OpenCashDrawerOnCashSale`. Complete a cash sale in POS | Receipt prints automatically and drawer pops open without any blocking dialogs or UI delays. |
| 7 | **Drawer Suppression on Card/Credit Sale** | Complete a 100% Card or 100% Credit sale in POS | Receipt prints normally, but cash drawer does NOT kick open. |
| 8 | **Printer Disconnect Resilience** | Unplug thermal printer USB cable; complete a sale in POS | Sale completes instantly and successfully; non-blocking notice appears ("Receipt could not be printed: ... You can reprint anytime from the Sales list"). |
| 9 | **Reprint Duplicate Watermark** | Reconnect printer; go to Sales List or POS Success Panel and click **Reprint (Duplicate)** | Receipt prints with `*** DUPLICATE COPY ***` banner; entry is written to `AuditLogs` table in `shop.db`. |
| 10 | **A4 Tax Invoice Export** | Open any sale in Sales List -> View Details -> **Print A4 Invoice** | Full-page corporate invoice opens in PDF preview with customer tax details, item breakdown, and print/save buttons. |

---

## 10. Architectural Limitations
- **Single-User Workstation**: Single workstation design (`ICurrentUserProvider` returns local `UserId = 1`).
- **Customer Receive Payments & Sales Reports**: Customer payment reconciliation and advanced sales reports are part of Phase 6.

---

## 11. How to Add EF Core Migrations

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

## 12. How to Build, Test & Run

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

## 13. Database Verification & Location

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

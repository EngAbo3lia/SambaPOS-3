# SambaPOS 3 — Port Plan: Features & UI from POS Lite

> Status: Plan (build mode ready)
> Date: Aug 2026
> Base project: `D:\Aboalia\POS` (SambaPOS 3, v3.0.35 BETA — .NET Framework / WPF / Prism-MEF / EF + SQL Server CE)
> Source of ported features: `D:\Aboalia\SAP Business One - Point Of Sale - Lite` (React 18 + Express 5 + SQLite)
> Companion doc: `../SAP Business One - Point Of Sale - Lite/docs/SambaPOS-vs-POS-Lite-Feature-Gap-Analysis.md`

---

## 0. Architecture Conventions (apply to every feature)

| Rule | Detail |
|------|--------|
| Module | One Prism/MEF module per feature in `Samba.Modules.*`; register screens via `AddDashboardCommand(...)` in module's `[ModuleInit]` |
| Service | Interface in `Samba.Services` (MEF `[Export]`), implementation in `Samba.Services/Implementations` |
| Domain | Entities in `Samba.Domain/Models`; persistence via `Samba.Persistance` DAO (interface + implementation + `CachedDao`/`Dao` registration) |
| DB | FluentMigrator migration per schema change in `Samba.Persistance.DBMigration` (next numbered file, bump `CurrentDbVersion`); SQL CE + SQL Server compatible |
| UI | WPF; views in module `Views/`, viewmodels in module `ViewModels`; new window/dialog via `InteractionService` or widget pattern |
| Localization | Add strings to all 21 language resources in `Samba.Localization` (fallback: en) |
| Tests | Unit tests in `Samba.Domain.Tests` / `Samba.Services.Tests` / `Samba.Modules.*.Tests`; verify via `SambaPos.sln` test run |
| Conventions | Functions ≤ 20 lines, KISS, DRY (extract on 3rd occurrence), no comments unless required |

---

## Part A — Tier 1 Features (low/medium effort)

### A1. Refund Flow (payment reversal)

**Current state:** No refund concept. `ITicketService.CancelSelectedOrders` voids orders. EndDayReport detects "returns" as tickets with `TotalAmount < 0`. No dedicated refund payment type or UI.

**Goal:** Full refund flow: select paid order(s) → refund payment (cash/card/credit) → payment reversal transaction → report line.

**Implementation steps:**

1. **Domain** (`Samba.Domain`):
   - Add `RefundPayment` concept: extend `Payment` with `IsRefund` flag (nullable bit, migration).
   - Add `PaymentType.Refund` seed? No — model as new `PaymentType` with `AccountTransactionType` mapped to a "Refund" transaction type (reverse debit/credit).
2. **Service** (`Samba.Presentation.Services`):
   - `ITicketService`: add `RefundTicket(Ticket, Order[], PaymentType, Account, decimal amount, string reason)` and `CanRefundOrders(Ticket, Order[])`.
   - `TicketService` implementation: validate orders belong to ticket + are paid (`PaidItems`), reverse the sale `AccountTransaction`, apply refund payment, log via `AddLog(user, "Refund", reason)`, update `RemainingAmount`.
3. **Payment** (`Samba.Modules.PaymentModule`):
   - `PaymentEditorViewModel`: add refund mode — select paid items (reuse `OrderSelector`), pick refund payment type, tendered = refund amount, optional reason field.
   - New "Refund" button on paid-ticket view, gated by permission `RefundTickets`.
   - Number pad: `BalanceMode` variant for refunds (return full paid amount or partial).
4. **Reports** (`Samba.Modules.BasicReports`):
   - `ReportContext`: extend EndOfDayReport "Returns" table — show refund payments by type (separate from voided negative tickets).
5. **Automation:** new action processor `RefundTicket` (optional, pattern exists in `ActionProcessors`).
6. **Tests:** `Samba.Modules.PaymentModule.Tests` — refund of partial qty, full order, credit-limit re-credit, double-refund prevention (idempotency via ticket log check).

**Effort:** Medium (3-4 days). **Files:** `TicketService.cs`, `PaymentEditorViewModel`, `ReportContext.cs`, migration, `PermissionNames`.

---

### A2. Barcode Login

**Current state:** PIN-only. `User.PinCode`, `UserService.CheckPinCodeStatus`, `UserDao.GetUserByPinCode`, `LoginPadControl.xaml.cs` numeric pad, `ApiServer/LoginController`.

**Goal:** Scan barcode to log in (cashier badge), PIN still supported.

**Implementation steps:**

1. `Samba.Domain/Models/Users/User.cs`: add `Barcode` string property (unique index, migration).
2. `Samba.Persistance`: `IUserDao.GetUserByBarcode(string)` + implementation.
3. `Samba.Services/Implementations/UserModule/UserService.cs`: `LoginUserByBarcode(string)` mirroring PIN flow (lockout/suspend logic shared).
4. `Samba.Modules.LoginModule/LoginPadControl.xaml.cs`: barcode branch — keyboard-wedge text input (length > 4, non-digit or F1-scan suffix) → `LoginUserByBarcode`; add small "Scan barcode" hint UI.
5. `Samba.ApiServer/Controllers/LoginController.cs`: accept barcode in token endpoint.
6. `Samba.Modules.UserModule/UserView.xaml`: barcode field in user editor.
7. Tests: `Samba.Services.Tests` — barcode lookup, invalid barcode, suspended user.

**Effort:** Small (1 day). **Files:** `User.cs`, `UserDao.cs`, `UserService.cs`, `LoginPadControl.xaml(.cs)`, `LoginController.cs`, `UserView.xaml`.

---

### A3. New Reports: Low Stock, Stock Turnover, Profit & Margin, VAT by Rate

**Current state:** Report pattern is cheap — extend `ReportViewModelBase`, override `CreateFilterGroups()`, `GetReport()` (builds `SimpleReport`), `GetHeader()`; register with one line in `ReportContext.GetReports()` (`ReportContext.cs:40-54`). `InventoryReportViewModel` is ~79 lines.

**Implementation steps (per report, files under `Samba.Modules.BasicReports/Reports/`):**

1. **LowStockReportViewModel** (`Reports/InventoryReports/`): rows = `InventoryItem` with `GetInventory(item, warehouse) <= MinStock`, group by warehouse; filter: warehouse, min-stock-only toggle.
2. **StockTurnoverReportViewModel**: period filter (from/to), movement counts per item via `InventoryTransaction` aggregation (`IInventoryDao`), turnover = issued qty / avg stock.
3. **ProfitMarginReportViewModel** (`Reports/MenuItemReports/`): per menu item: revenue (sold qty × price), cost from `Recipe` (`GetRequiredRecipesForSales` + `RecipeItem.FixedCost`), margin + %; group by department/group code.
4. **VatByRateReportViewModel**: aggregate `TaxValue` per `TaxTemplate` (per-ticket tax lines already stored in ticket JSON), period filter, totals.
5. Register all 4 in `ReportContext.GetReports()` (one line each).
6. Localize report titles/columns in 21 resources.
7. Tests: `Samba.Services.Tests` — cost aggregation logic; manual verify each report renders + prints to XPS.

**Effort:** Small (2-3 days for 4 reports). **Files:** 4 new ViewModels + `ReportContext.cs`.

---

### A4. Receipt Reprint with Counter/Log

**Current state:** `PrintTicket(ticket, printJob, orderSelector, highPriority)` exists; no reprint tracking.

**Implementation steps:**

1. `Ticket`/`TicketLogValue`: log reprints via `AddLog(userName, "Receipt", "Reprint #N")`; track count in a new `ReprintCount` field on Ticket (migration, nullable int).
2. `Samba.Modules.TicketModule`: "Reprint Receipt" button on closed/paid ticket view + TicketExplorer context menu (permission `ReprintReceipt`), re-runs last `PrintJob` for the ticket.
3. Reports: EndOfDayReport optional "Reprints" column (per user).
4. Tests: reprint increments counter, permission enforced.

**Effort:** Small (1 day). **Files:** Ticket model, TicketModule view, `PermissionNames`, EndOfDayReport.

---

### A5. Customer Order History at POS

**Current state:** Widget framework exists (`Widget`, `WidgetViewModel`, TicketExplorer/TicketLister widgets in TicketModule); `TicketExplorer` already searches by entity.

**Implementation steps:**

1. New widget `CustomerHistoryWidget` in `Samba.Modules.TicketModule/Widgets/`: input = entity (auto-fills from ticket's `TicketEntity`), shows last N tickets (number, date, total, status) via `ITicketDao.GetTicketsByEntity(entityId, limit)`.
2. Click a row → opens ticket in read-only mode (`DisplayOldTickets` permission) or prints receipt.
3. Place widget on TicketView side panel or EntityDashboardView.
4. Tests: widget data binding with empty entity (empty state), permission gate.

**Effort:** Small-medium (1-2 days). **Files:** new widget + `TicketExplorer` refactor reuse.

---

### A6. Quick Product Creation from POS

**Current state:** Product editing only via Management dashboard (`Samba.Modules.MenuModule/MenuItemViewModel.cs` + `MenuItemView.xaml.cs`). PosModule only selects existing items.

**Implementation steps:**

1. New dialog `QuickProductDialog` in `Samba.Modules.PosModule/`: fields = name, group code, barcode (auto-scan), price (default portion), tax template; creates `MenuItem` + `MenuItemPortion` + price via `IMenuService`/`IMenuDao`.
2. Trigger: "New Item" button on MenuItemSelector + automation action processor `QuickCreateMenuItem`.
3. Permission `CreateMenuItem` gates the button; audit log entry.
4. Duplicate-barcode check with inline validation error.
5. Tests: dialog creation flow, duplicate barcode rejection.

**Effort:** Small (1 day). **Files:** `QuickProductDialog.xaml(.cs)`, PosModule view, `PermissionNames`.

---

### A7. Customer Creation at Checkout

**Current state:** `IEntityService.CreateEntity` exists; entity screens exist. No quick-create at POS payment/ticket stage.

**Implementation steps:**

1. New dialog `QuickEntityDialog` (customer/table/room): name, primary field (phone), optional account/warehouse defaults from `EntityType`.
2. Reuse `EntityCustomField` definitions — render fields dynamically (string/number/date/query types).
3. Trigger: "New Customer" button on `TicketEntityList` + payment screen when no entity assigned; after creation, attach to ticket via `UpdateEntity`.
4. Duplicate detection by primary field (phone) with "open existing?" prompt.
5. Tests: creation + attachment flow, field validation.

**Effort:** Small (1-2 days). **Files:** `QuickEntityDialog`, `TicketEntityList` view, `EntityService`.

---

### A8. WhatsApp Receipt Delivery

**Current state:** `SendEmail` action + `IEmailService` exist. No WhatsApp/URL-message action.

**Implementation steps:**

1. New automation action processor `SendWhatsAppMessage` in `Samba.Modules.AutomationModule/ActionProcessors/`: parameters = phone (from entity field or literal), message template (supports `[=expression]` + `{:setting}` + ValueChangers), builds `https://wa.me/{digits}?text=...`, opens via `Process.Start` (desktop) — no WhatsApp Cloud API in SambaPOS (offline desktop).
2. Optional phone normalization (strip +/spaces, country prefix setting).
3. Default automation rule: `TicketClosed` → SendWhatsAppMessage with receipt summary (order lines, totals, payment).
4. Tests: URL building, template replacement, missing-phone fallback (rule constraint `IsNotNull`).

**Effort:** Small (1 day). **Files:** new ActionProcessor, `RuleEventNames` reuse, settings.

---

### A9. Shift Management (Declared Cash, Counted, Variance)

**Current state:** `WorkPeriod` = start/end dates + description only (`Samba.Domain/Models/Settings/WorkPeriod.cs`). No float/cash-drawer concept. Lite has openShift/closeShift with declared + counted cash and variance.

**Implementation steps:**

1. **Domain:** extend `WorkPeriod` with `OpeningCash`, `ClosingCash`, `ExpectedCash` (nullable decimals), `ClosingUser`, `ClosingNotes` (migration).
2. **Accounts:** seed `AccountTransactionType` "Float (Opening Cash)" + "Float (Closing Cash)" document types in `Samba.Modules.AccountModule` (default accounts per terminal).
3. **Service:** `IWorkPeriodService.StartWorkPeriod(terminal, openingCash)` creates float transaction + sets opening amount; `StopWorkPeriod(...)` records closing cash + counted variance (`ExpectedCash = OpeningCash + sales − payouts`).
4. **UI:** `Samba.Modules.WorkperiodModule` — extend WorkPeriodsView: open dialog (declare opening cash), close dialog (counted cash entry, shows expected vs variance), work period list with variance column.
5. **Reports:** EndOfDayReport header gains opening/closing/expected/variance block.
6. **Tests:** variance math, float transactions posting, re-open guard (work period already open).

**Effort:** Medium (3 days). **Files:** `WorkPeriod.cs`, `IWorkPeriodService`, WorkperiodModule views, AccountModule seeds, EndOfDayReport.

---

### A10. Price Levels per Customer (Entity-Scoped Pricing)

**Current state:** `MenuItemPrice` = `{MenuItemPortionId, PriceTag, Price}`; selection in `Order.UpdatePortion` (Order.cs:152-163) matches `PriceTag` only. No entity-scoped price lists. Lite has customer-specific price levels.

**Implementation steps:**

1. **Domain:** add `PriceList` entity (`Name`, `IsDefault`) + `MenuItemPriceList` (or extend `MenuItemPrice` with `PriceListId`, migration, nullable = default price tag pricing).
2. **Service:** `IPriceListService`: CRUD price lists + bulk assign prices per list (`GetTags`-style iteration, reuse `PriceListService` bulk logic).
3. **Resolution:** `Order.UpdatePortion` — resolve price: ticket entity's `PriceListId` (via `TicketEntity` custom data or entity field) → list price → fallback price-tag price.
4. **UI:** `Samba.Modules.MenuModule` — price list editor screen (dashboard command "Price Lists"); assign price list to customer in EntityModule (dropdown field on `EntityCustomField`-like setting or dedicated field).
5. **Reports:** optional price-list column in ItemSalesReport.
6. **Tests:** resolution precedence (list > tag > base), fallback when item missing from list, entity switch mid-ticket recalculates.

**Effort:** Medium (3-4 days). **Files:** `MenuItemPrice.cs`, `PriceListService`, `Order.cs`, MenuModule screen, EntityModule field.

---

### A11. Multi-Warehouse Transfer Workflow UI (approve/reject/receive)

**Current state:** `InventoryTransactionType` + `InventoryTransactionDocument` support source→target warehouses; no status workflow or Lite-style screens.

**Implementation steps:**

1. **Domain:** add `Status` (Draft/Pending/Approved/Rejected/Received) + `DamagedQuantity` to `InventoryTransactionDocument`/`InventoryTransaction` (migration).
2. **Service:** `IInventoryService.CreateTransfer(...)`, `ApproveTransfer`, `RejectTransfer`, `ReceiveTransfer` — status transitions + stock effect only on receive (idempotent).
3. **UI:** new `Samba.Modules.InventoryModule` screen `TransferView`: create (source/target warehouse, items, qty), list with status filters, approve/reject/receive buttons per permission (`ApproveTransfers`, `ReceiveTransfers`).
4. **Print:** transfer document print via existing `PrintObject`/`ReportPrinter`.
5. **Tests:** status machine, double-receive prevention, stock counts after receive.

**Effort:** Medium (2-3 days). **Files:** Inventory domain, `IInventoryService`, new InventoryModule screen, `PermissionNames`.

---

## Part B — Tier 2 Features (medium effort, bigger builds)

### B1. Bundles (Kits) with Stock-Check Dialog + Substitute Picker

**Current state:** No bundle concept (recipes consume stock, but no "sell multi-item kit with one barcode"). Lite has full bundle CRUD + POS stock-check dialog + substitute picker.

**Implementation steps:**

1. **Domain:** `Bundle` (`Name`, `Barcode`, `Image`, `Price`, `IsActive`) + `BundleItem` (`BundleId`, `MenuItemId` or InventoryItemId, `Quantity`, `IsOptional`, `SubstituteGroupId`).
2. **Barcode:** `IMenuService.FindMenuItemByBarcode` → also `FindBundleByBarcode`; POS barcode entry resolves bundles first.
3. **Service:** `IBundleService` (CRUD, `GetBundleStock(bundleId, warehouseId)` = min over components, `ResolveSubstitutes(...)`).
4. **POS:** `MenuItemSelector` shows bundles with stock badge; selecting opens **BundleStockDialog** (component qty vs stock, warning on shortage) with **SubstitutePicker** (alternative item per substitute group).
5. **Ticket:** adding bundle expands to component orders (tagged/grouped so they don't appear as separate lines) — implemented via new `Order.Tag` + a "Bundle" group code; kitchen print shows components, receipt shows bundle line + components.
6. **UI:** `Samba.Modules.MenuModule` — `BundleView` screen (CRUD, components grid, substitute groups, image).
7. **Tests:** stock-check math (min over components), substitute resolution, expansion onto ticket, barcode collision handling.

**Effort:** Large (1-1.5 weeks). **Files:** new BundleModule or MenuModule extension, POS dialogs, migrations.

---

### B2. Sales Employees & Commission Targets

**Current state:** Zero commission code. Lite has employees, targets per period, commission rate, `CommissionService.targetSummary`.

**Implementation steps:**

1. **Domain:** `SalesTarget` (`UserId`, `PeriodStart`, `PeriodEnd`, `TargetAmount`, `CommissionRate`), `Employee` flags on `User` (nullable `CommissionRate`), migration.
2. **Service:** `ICommissionService` — `CalculateCommission(userId, period)` from paid tickets (sale `AccountTransactionType` only), `GetTargetSummary` (achieved vs target %, commission earned).
3. **Assignment:** POS — select sales employee per ticket (combo on TicketView, stored on `Ticket` new `SalesEmployeeId`).
4. **UI:** `Samba.Modules.UserModule` — targets editor screen; `Samba.Modules.BasicReports` — **CommissionReportViewModel** (pattern A3).
5. **Automation:** optional `CalculateCommission` action on WorkPeriodEnd.
6. **Tests:** commission math with refunds excluded, period boundaries.

**Effort:** Medium (3-4 days). **Files:** new `ICommissionService`, `Ticket.cs`, UserModule screen, report, migration.

---

### B3. Quotations with PDF Export

**Current state:** No quote concept. Lite has quotations with status workflow + pdfkit PDF.

**Implementation steps:**

1. **Domain:** `Quotation` (`Number` via `Numerator`, `EntityId`, `Status` Draft/Issued/Converted/Expired, `ValidUntil`, lines as `QuotationItem`: menu item + qty + price + tax, notes, discount).
2. **Service:** `IQuotationService` CRUD + `ConvertToTicket(quotation)` → creates ticket with same lines (auto-fills ticket).
3. **UI:** `Samba.Modules.TicketModule` or new `QuotationModule` — quotation editor (line grid, entity picker, totals), list with status filters.
4. **PDF:** reuse `ReportPrinter` → XPS + save; or port Lite's pdfkit approach via new `ExportQuotationToPdf` (needs PDF lib — use existing report/print pipeline first, PDF as optional enhancement).
5. **Tests:** convert-to-ticket fidelity (prices, taxes), numerator numbering, expiry workflow.

**Effort:** Medium (4-5 days). **Files:** new QuotationModule, `IQuotationService`, migration, report print.

---

### B4. Purchase Approve/Receive Workflow

**Current state:** Inventory transaction documents exist; no purchase-order status flow. Lite has purchase invoices with complete/void + stock transfers workflow.

**Implementation steps:**

1. **Domain:** `InventoryTransactionDocument` gains `Status` (Draft/Pending/Completed/Voided) + `ExpectedDate` (migration) — reuse A11 status pattern.
2. **Service:** `IInventoryService.CreatePurchaseDocument`, `ApprovePurchase`, `CompletePurchase` (posts stock + creates account transaction to supplier account), `VoidPurchase`.
3. **UI:** extend InventoryModule transaction views with status column + action buttons per permission.
4. **Reports:** PurchasesReport (by supplier, period, status) — pattern A3.
5. **Tests:** stock posted only on completion, double-complete guard, supplier account posting.

**Effort:** Medium (3 days). **Files:** Inventory domain, `IInventoryService`, InventoryModule views, report.

---

### B5. Data Management Hub (Import/Export/Backup/Rollback)

**Current state:** CSV export via `CsvBuilder`; no import/backup/rollback. Lite has full pipeline (upload → validate → preview → execute → rollback, template downloads, backups).

**Implementation steps:**

1. **Service:** `IDataManagementService` — export (menu items, entities, price lists, inventory, taxes to CSV/XLS via existing builders), import (validate headers/types → preview diff → execute within transaction → rollback on error), backup (copy `.sdf` + settings file with timestamp, keep-last-N setting).
2. **UI:** new Management dashboard screen `DataManagementView` — tabs: Export (with filters), Import (file → preview grid → confirm), Backup (list, restore, delete).
3. **Validation:** row-level errors reported inline, zero partial commits on failure.
4. **Tests:** round-trip export→import identity, rollback on bad row, backup restore.

**Effort:** Large (5-7 days). **Files:** new `IDataManagementService`, DataManagementView, `CsvBuilder` extension.

---

### B6. E-Invoicing (ZATCA / ETA)

**Current state:** None. Lite: ZATCA spec-only, ETA schema-only (also not built). SambaPOS tax foundation = `TaxTemplate` + per-ticket tax lines.

**Implementation steps (phased, region configurable):**

1. **Phase 1 — Data readiness:** ensure all ticket lines carry tax template + entity tax number (add `TaxNumber` on `Entity` custom data); VAT-by-rate report (A3) as prerequisite.
2. **Phase 2 — Invoice numbering:** configurable e-invoice number format per work period via `Numerator`.
3. **Phase 3 — Export:** e-invoice XML/JSON export action per ticket (ZATCA phase-1 format / ETA), QR code on receipt (ESC/POS `PrintQrCode` already exists) containing invoice hash + seller info.
4. **Phase 4 — Signing/submission:** ZATCA phase-2 (CSR, e-invoicing API) — only when a customer requires it; big scope, external API integration.
5. **UI:** settings screen for seller credentials (CRN/VAT number), per-terminal toggle.

**Effort:** Large (1-2 weeks per region phase 1-3; phase 2/4 external). **Files:** export service, settings, printer template additions.

---

## Part C — UI Restyle (Design Language from POS Lite)

> **Status (Aug 2026):** C1 theme system ✅ Done · C7 toasts ✅ Done · C2 dashboard ⏳ Next · C10 states ⏳ Next · C3-C6, C8-C9 pending.

### C1. Theme / Design Tokens

**Goal:** Lite's clean look — cards, sidebar, badges, high-contrast hierarchy, light/dark palette, without breaking the 22-module shell.

**Implementation steps:**

1. New `Samba.Presentation/Resources/Themes/PosLite.xaml` `ResourceDictionary`: colors (primary amber `#C2800A` accent from Lite, neutrals), corner radii, control templates for `Button`, `TextBox`, `ComboBox`, `DataGrid`, `TabControl`, `ToggleButton`, scrollbars.
2. MahApps-style flat buttons + pill badges (`Badge` attached property: count/status).
3. Fonts: keep Segoe UI; bump base size, define display/body/text styles.
4. Merge dictionary in `Shell.xaml`/`App.xaml`; dark/light toggle setting (`ProgramSettingValue` `UITheme`).
5. Verify: portrait/landscape POS layouts unaffected; keyboard-navigable.

**Effort:** Medium (3-5 days). **Files:** new theme dictionary + Shell.xaml, App.xaml.

### C2. KPI Dashboard Home Screen

**Goal:** Lite's dashboard summary (today's sales by terminal/cashier, orders count, low-stock alerts, quick links).

**Implementation steps:**

1. New widget `DashboardWidget` in `Samba.Modules.BasicReports` (or NavigationModule): KPI cards — today's gross sales, ticket count, avg ticket, payment breakdown, low-stock count (drill to report), open tickets.
2. Data from existing services (`IAccountService.GetAccountTransactionSummary`, `ITicketDao`, `IInventoryService`), work-period scoped.
3. Grid layout widget placed on the default navigation landing view (new `HomeView` before Management dashboard).
4. Permission-gated per role (viewers see it, cashiers get POS).
5. Tests: data aggregation with empty day (empty state), period rollover.

**Effort:** Medium (2-3 days). **Files:** new widget + HomeView + navigation registration.

### C3. Payment Modal UX (method grid, mixed payment, change)

**Goal:** Port Lite's `PaymentModal` interaction — big method buttons, mixed payment, instant change calc — as a restyled PaymentScreen.

**Implementation steps:**

1. Restyle `PaymentButtons`, `NumberPad`, `TenderedValue`, `ReturningAmount` per C1 theme.
2. Add mixed-payment flow: select method → enter amount → add another method until covered; totals row; "Complete" enabled when covered (SambaPOS already supports multiple payments per ticket — surface it in UI).
3. Change template selector visible on same screen (A1 refund mode reuses this).
4. Tests: mixed payment sum enforcement, change math.

**Effort:** Medium (2-3 days). **Files:** PaymentModule views + PaymentEditorViewModel.

### C4. Customer Modal at Checkout (search/create/history in one dialog)

**Goal:** Lite's CustomerModal: search by name/phone, quick create, order history panel.

**Implementation steps:**

1. New `CustomerDialog` in `Samba.Modules.PosModule`/EntityModule: search box (live, via `IEntityService.SearchEntities`), results grid, "New" (A7 QuickEntityDialog), history list (A5 data source), OK → `UpdateEntity` on ticket.
2. Used from TicketView entity button and payment screen.
3. Tests: search debounce, empty state, attach-to-ticket.

**Effort:** Medium (2 days). **Files:** CustomerDialog, TicketEntityList integration.

### C5. Bundle Stock-Check + Substitute Picker Dialogs

Direct WPF translation of Lite's `BundleStockDialog` + `SubstitutePicker` (depends on B1). Rendered as part of B1.

### C6. System Health Page

**Goal:** Lite's System Health: printer status, DB size/health, disk space, messaging server status, cache stats.

**Implementation steps:**

1. New Management dashboard screen `SystemHealthView`: cards — DB path/size (SQL CE file), disk free, printer reachability (`IAsyncPrinterService` ping or last-error), MessagingServer client status, cache item count (`ICacheService`), recent errors (`ILogService`).
2. Refresh button + auto-refresh timer (30s); warning badges when unhealthy.
3. Tests: status aggregation with missing printers (error state).

**Effort:** Small-medium (2 days). **Files:** SystemHealthView + status service.

### C7. Toast Notifications (vs popups)

**Goal:** Replace intrusive modal popups with Lite-style toasts for non-blocking events (payment OK, ticket closed, low stock).

**Implementation steps:**

1. New `ToastService` in `Samba.Presentation.Common` (MEF exported, `IToastService.Show(message, type, duration)`); `ToastHostControl` overlay on Shell (top-right stack, slide/fade animation).
2. Wire existing `INotificationService` call sites gradually (start: POS payment, automation `ShowMessage`), keep `DisplayPopup` for blocking confirmations.
3. Tests: queue behavior, timeout dismissal, click-to-dismiss.

**Effort:** Small (1-2 days). **Files:** ToastService, ToastHostControl, Shell.xaml.

### C8. SetupWizard (first-run)

**Goal:** Lite's SetupWizard replaces raw demo-data seeding with guided setup.

**Implementation steps:**

1. `Samba.Modules.ManagementModule` new `SetupWizardWindow`: steps — language/currency → company info (name, VAT no) → default department/terminal → payment types → admin user → (optional) demo data toggle.
2. Runs when no terminal/department exists (existing `DataCreationService` called only if user opts in).
3. Tests: wizard completes → system usable; skip path works.

**Effort:** Medium (3 days). **Files:** SetupWizardWindow, ManagementModule init hook.

### C9. Tabbed Reports Interface

**Goal:** Lite's `Reports.jsx` tabs (Products, Payments, Cashiers, Shifts, Low stock, Stock value, Turnover, Customers, VAT, Orders, Discounts, Categories).

**Implementation steps:**

1. Restyle `ReportView` with left rail of grouped report links (Lite tab grouping) + content area; keep `ReportViewModelBase` unchanged.
2. Grouping: Sales (ItemSales, Z-report, Profit Margin), Inventory (Inventory, Low Stock, Turnover, Cost), Accounts (Internal, AR, AP), VAT.
3. Tests: navigation, permission gating (reports permission).

**Effort:** Small (1-2 days). **Files:** ReportView.xaml + navigation VM.

### C10. Loading/Empty/Error States

**Goal:** Standard states on all lists (Lite pattern): skeleton/loading indicator, empty message with CTA, error with retry.

**Implementation steps:**

1. `Samba.Presentation.Common`: `LoadingState` attached behavior + `EmptyStateControl` (icon, title, action button) + `ErrorStateControl` (message, retry button).
2. Apply to: inventory lists, ticket explorer, account screen, quotations, transfers, data management.
3. Tests: each state renders correctly per VM state enum.

**Effort:** Medium (2-3 days). **Files:** new controls + view integration.

---

## Part D — Roadmap & Sequencing

| Phase | Items | Est. |
|-------|-------|------|
| **P1 — Quick wins** | A2 barcode login, A3 reports (4), A4 reprint, A6 quick product, A7 quick customer, A8 WhatsApp, C6 health, C7 toasts | 1.5 weeks |
| **P2 — Money & shifts** | A1 refunds, A9 shifts, C3 payment UX | 1.5 weeks |
| **P3 — Pricing & workflow** | A10 price levels, A11 transfers, B4 purchases, A5 customer history, C4 customer modal | 2 weeks |
| **P4 — UI restyle** | C1 theme, C2 dashboard, C9 tabbed reports, C10 states | 2 weeks |
| **P5 — Big builds** | B1 bundles, B2 commissions, B3 quotations, B5 data hub, C8 wizard | 3-4 weeks |
| **P6 — Regional** | B6 e-invoicing (optional, only on customer demand) | 1-2 weeks |

**Total:** ~3.5 months solo full-time; parallelizable by module boundaries.

## Part E — Verification Protocol (every feature)

1. Build `SambaPos.sln` (Debug, x86) — zero warnings introduced.
2. Run test projects: `Samba.Domain.Tests`, `Samba.Services.Tests`, `Samba.Presentation.Tests`, `Samba.Modules.PaymentModule.Tests`.
3. Run app with demo data (`DataCreationService`), exercise: happy path, permission-denied path, empty-data path, error path.
4. Verify each new screen: loading state, empty state, error state, validation errors inline.
5. Print-path features verified against ESC/POS demo printer (Demo printer type).
6. Localization: new strings present in en; missing keys fall back cleanly.
7. Update this doc: mark item ✅ Done with date + notes.
# PHASE 2 REPORT — COMMERCIAL & INVOICE MANAGEMENT

> **Status:** Completed
> **Scope:** Commercial & Invoice Management, integrated with the Phase-1 Accounting Core.

## 0. Reference Documents & Assumptions

The task references `docs/financial/MASTER_GAP_MATRIX.md`, `docs/financial/IMPLEMENTATION_ROADMAP.md`, and `docs/financial/PHASE_0_REPORT.md`. As in Phase 1, these planning documents **do not exist** anywhere in the repository. The audit and implementation were therefore performed against the actual current implementation (see *What Was Inspected*) rather than against missing planning documents.

**Assumption made (documented):** Because the existing Phase-1 `Invoice` entity already lives in `FinanceDbContext` and the shared accounting engine (`FinanceLedgerService.PostInvoiceAsync`) is wired to it, the new Commercial concepts were added to the **same `FinanceDbContext`**. Module separation is therefore achieved at the **code/UI layer** (a dedicated `Modules/Commercial/` module, a `CommercialController`, and its own views), while keeping documents transactionally consistent with the accounting engine — this honours the task's "use the existing accounting engine" and "no duplicate accounting logic" constraints, and avoids a second financial database. No existing entity was duplicated.

---

## 1. Module Separation (§1)

Created a dedicated **Commercial** module under `Modules/Commercial/`:

```
Modules/Commercial
├── Domain/            CommercialParty, InvoiceReturn(+Item), CommercialQuotation(+Line+Status), SettlementTransaction, InvoiceAttachment(+Category)
├── Application/       CommercialService, CommercialViewModels (payment status, index/detail/row VMs)
└── Infrastructure/    Persistence/Configurations (EF mapping for all new entities)
Controllers/CommercialController.cs
Views/Commercial/      InvoiceGrid, Details, ReturnsGrid, CreateReturn, Quotations, QuotationEditor, Parties, PartyEditor
```

The Commercial screens (Sales, Purchases, Returns, Quotations, Parties) are **separated from the generic accounting UI** (LedgerOperations / Trial Balance / Voucher screens), while remaining integrated with the accounting engine through `FinanceLedgerService`.

---

## 2. Sales (§2) & 3. Purchases (§3) — Master Grids

New master grids at `/Commercial/Sales` and `/Commercial/Purchases`:

- `+ New Sales Invoice` / `+ New Purchase Invoice` (routes to the existing non-duplicated invoice editor)
- Search, year filter, party filter, and payment-status filter
- Columns: Invoice Number, Party (Customer/Supplier), Date, Due Date, Total, VAT, Paid Amount, Outstanding Amount, Status, Payment Status, actions
- **Double-click on a row opens the invoice in a new Rhyno tab** (`window.open(..., '_blank')` on `Commercial/Details?_tab=true`; the tab content layout is selected via `_ViewStart.cshtml` → `_TabLayout`)
- Summary footer: Total Payable / Total Paid / Total Outstanding

> The existing `Invoice` entity and its editor (`Financial.CreateInvoice` / `EditInvoice`) were **reused**, not duplicated, to keep one invoice lifecycle. The Commercial grids expose the Phase-2 fields (Due Date, payment status, outstanding) the generic grid lacks.

---

## 4. Sales Invoice & 5. Purchase Invoice

Supported via the existing, non-duplicated invoice editor (Customer/Supplier, Invoice Number, Invoice Date, Due Date, lines: product/service, quantity, unit price, discount, tax, VAT, total, notes, attachments).

Attachments are now first-class financial attachments (`InvoiceAttachment`): invoice scan, contract, receipt, supporting document — with upload, list, download and delete on the invoice **Details** screen (§10).

---

## 6. Returns (§6)

Implemented `SalesReturns` and `PurchaseReturns`:

- **Create return** against a source invoice; invoice lines are loaded automatically and reduced (quantity / unit price / VAT / totals).
- **Finalize return** posts a **balanced reversal-style accounting voucher** through the shared engine (`FinanceLedgerService.PostReturnAsync`):
  - Sales return: reduces Accounts Receivable (credit) and reverses Sales Revenue + Output VAT.
  - Purchase return: reduces Accounts Payable (debit) and reverses Purchase Expense + Input VAT.
- Returns correctly affect:
  - **Invoice balance** — approved returns reduce the outstanding amount.
  - **VAT** — return line VAT reverses the original VAT.
  - **Accounting** — via the shared engine (no second engine).
  - **Customer/Supplier balance** — reduced via the receivable/payable reversal against the party floating detail.
- Inventory: the return posts stock-affecting accounting; physical stock movement is delegated to the existing Inventory module (return documents carry `ProductId`), consistent with the Phase-1 separation.

---

## 7. Quotations (§7)

Implemented **Sales Quotation** and **Purchase Quotation**:

- `CommercialQuotation` + `CommercialQuotationLine` with lifecycle (`Draft → Sent → Accepted / Rejected / Converted / Expired / Cancelled`).
- **Conversion: Quotation → Invoice** (`ConvertQuotationToInvoiceAsync`) creates a real `Invoice` from the quotation lines and marks the quotation `Converted` with `ConvertedInvoiceId`.
- **No duplication of customer or product data** — quotations reference the unified `CommercialParty` and reuse existing products; conversion copies line data into a real invoice without creating new master records.

---

## 8. Accounting Integration (§8)

Commercial documents post to accounting exclusively through the **Phase-1 accounting engine** (`FinanceLedgerService`). The engine was **extended** with two posting methods so all voucher logic stays in one place:

| Document | Debit | Credit |
|----------|-------|--------|
| Sales Invoice (`PostInvoiceAsync`, existing) | Accounts Receivable (party) | Sales Revenue, VAT Payable |
| Purchase Invoice (`PostInvoiceAsync`, existing) | Purchase Expense, VAT Receivable | Accounts Payable (party) |
| Sales Return (`PostReturnAsync`, new) | Sales Revenue, VAT Payable | Accounts Receivable (party) |
| Purchase Return (`PostReturnAsync`, new) | Accounts Payable (party) | Purchase Expense, VAT Receivable |
| Sales Settlement / Receipt (`PostSettlementAsync`, new) | Cash on Hand | Accounts Receivable (party) |
| Purchase Settlement / Payment (`PostSettlementAsync`, new) | Accounts Payable (party) | Cash on Hand |

- Added `FinanceAccountKeys.CashOnHand = "CASH_ON_HAND"` for settlement vouchers.
- Every invoking path calls `SetTotalsAndValidate` (Total Debit = Total Credit) and posts as `Permanent/Posted` (immutable).
- **No second accounting engine** is implemented inside Commercial — it only orchestrates documents and delegates posting.

---

## 9. Payment Status (§9)

Full payment/status support:

- `Unpaid`, `PartiallyPaid`, `FullyPaid`, `Overpaid`, `Cancelled`.
- **Outstanding amount is calculated from actual settlement transactions** (`SettlementTransaction`) plus approved returns — never a stored denormalized figure.
  - `Outstanding = GrandTotal − (approved returns) − (approved settlements)`.
- Settlements are real cash/bank transactions (`SettlementTransaction`) against the invoice; when `postNow` is set, a treasury voucher is posted and the settlement is marked `Approved`.
- The status is derived (`CommercialPaymentStatus.Resolve`) and surfaced in both the master grid and the invoice Details screen.

---

## 10. Attachments (§10)

`InvoiceAttachment` supports financial attachments:

- Categories: **Invoice scan, Contract, Receipt, Supporting document**.
- Upload (files stored under `wwwroot/uploads/commercial/invoices/`, metadata in DB), list, download, and delete.
- Surfaces on the invoice **Details** screen alongside settlements and the accounting trace.

---

## 11. UI (§11)

- **RTL** on all Commercial screens.
- **Compact filters** (search, year, party, payment status).
- **Jalali** (Shamsi) dates throughout (`DateShamsi`); `ToShamsi` helper for the Due Date column.
- **Numeric formatting** (Persian/`N0` grouping) on monetary columns.
- **Professional grids** matching the existing Rahkaran-inspired design system (dense tables, status badges, totals footer).
- **Double-click tab navigation** — row click opens `Details` in a new Rhyno tab.
- **Standard toolbar**: Refresh / Print / Excel / Row-Count are provided by the shared shell and the existing grid patterns; the Commercial grids expose New, Search, Filters, and per-row actions consistent with the application-wide toolbar conventions.

---

## 12. Testing (§12)

Added `OfficeAutomation.Tests/CommercialModuleTests.cs` (5 tests), all passing:

1. `PaymentStatus_IsUnpaid_Partial_Full_FromSettlements` — status derivation from settlements (incl. over/cancelled).
2. `CreateReturn_UpdateBalance_AndFinalizePostsBalancedVoucher` — the shared engine posts a **balanced** reversal voucher and marks the return approved.
3. `RegisterSettlement_PostsTreasuryVoucher_AndPaymentStatusBecomesFullyPaid` — settlement posts a **balanced** treasury voucher and drives payment status to Fully Paid.
4. `Quotation_ConvertsToInvoice_AndMarksConverted` — Quotation→Invoice conversion, no party duplication.
5. `SaveParty_CreatesAndUpdatesUnifiedParty` — unified party CRUD.

**Full test suite:** 115 passed / 1 failed. The single failure is the **pre-existing, unrelated** `DomainServiceTests.DataIsolationService_ReturnsEmptyQueryWhenNoProfile`, which exercises `Services/Security/DataIsolationService.cs` (tenant/identity isolation) — a file not touched by Phase 2 (and already documented as a known failing test in Phase 1). All Commercial and accounting tests pass.

**Coverage of required scenarios:** invoice creation (existing flow, reused), editing a draft, posting (PostInvoice), accounting integration (vouchers), VAT (return-line VAT reversal), return, partial payment (Partial settlement → PartiallyPaid), full payment (→ FullyPaid), cancellation (reversal voucher + `Canceled` status).

---

## 13. Database Changes

Migration `Migrations/FinanceDb/20260814164546_AddCommercialModule.cs`:

| Change | Type |
|--------|------|
| `Invoices.DueDate` (datetime2, null) | Column |
| `Invoices.CommercialPartyId` (int, null) + FK + index | Column + FK + index |
| `CommercialParties` | Table (unified party master) |
| `InvoiceReturns` + `InvoiceReturnItems` | Tables (sales/purchase returns) |
| `CommercialQuotations` + `CommercialQuotationLines` | Tables (sales/purchase quotations) |
| `SettlementTransactions` | Table (real settlement transactions) |
| `InvoiceAttachments` | Table (financial attachments) |

---

## Definition of Done Checklist

- [x] **Module separation** — Commercial module (code/views/controller) separate from generic accounting UI.
- [x] **Sales & Purchases master grids** with all required columns + filters + totals.
- [x] **New invoice** links reuse the existing (non-duplicated) editor.
- [x] **Double-click opens invoice in a new Rhyno tab**.
- [x] **Returns** (sales + purchase) affect invoice balance, VAT, accounting and party balance.
- [x] **Quotations** (sales + purchase) with Quotation → Invoice conversion, no data duplication.
- [x] **Accounting integration** via the Phase-1 engine only — no second accounting engine.
- [x] **Payment status** (Unpaid/Partially/Fully/Overpaid/Cancelled) derived from **actual settlement transactions**.
- [x] **Outstanding amount** computed from settlements + returns (never stored).
- [x] **Financial attachments** (scan, contract, receipt, supporting doc).
- [x] **UI** — RTL, compact filters, Jalali dates, numeric formatting, professional grids, tab navigation.
- [x] **Tests** — 5 new Commercial tests pass; full suite 115 passed (1 pre-existing unrelated failure).
- [x] **Build succeeds** — `dotnet build OfficeAutomation.csproj -c Debug` → 0 errors.
- [x] **Traceability** — Invoice → Accounting Voucher → Ledger → Party Balance is exposed on the Details screen (with the voucher number and a Ledger link).
- [x] **No duplicate accounting logic** — all voucher construction lives in `FinanceLedgerService`.
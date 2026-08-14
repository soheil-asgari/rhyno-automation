# Financial Module Analysis & Enterprise Overhaul Blueprint

**Document Type:** Enterprise Architecture Analysis  
**Prepared by:** CFO & Enterprise Financial Systems Architect  
**Date:** August 14, 2026  
**Target Standards:** NetSuite Finance, SAP FICO, Microsoft Dynamics 365 Finance, Odoo Accounting

---

## Table of Contents

1. [Executive Audit of Current Financial Module](#1-executive-audit-of-current-financial-module)
2. [Comprehensive Gap Analysis vs World-Class ERP Standards](#2-comprehensive-gap-analysis-vs-world-class-erp-standards)
3. [Detailed Module Breakdown](#3-detailed-module-breakdown)
4. [Proposed C# Entity Models & Database Schema Additions](#4-proposed-c-entity-models--database-schema-additions)
5. [Recommended Financial Workflow & UI Layout Architecture](#5-recommended-financial-workflow--ui-layout-architecture)
6. [Phased Implementation Roadmap](#6-phased-implementation-roadmap)

---

## 1. Executive Audit of Current Financial Module

### 1.1 Current Architecture Overview

The existing financial module is built on ASP.NET Core with EF Core, utilizing a modular DbContext architecture (`FinanceDbContext`). The system implements a Persian/Shamsi calendar-based fiscal year management.

#### Current Data Model (As-Is)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           FINANCE MODULE SCHEMA                              │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ACCOUNT HIERARCHY (4-Level COA)                                            │
│  ┌──────────────┐   ┌────────────────┐   ┌─────────────────────┐           │
│  │ AccountGroup │──▶│ GeneralAccount │──▶│ SubsidiaryAccount   │           │
│  │ (Code, Name) │   │ (Code, Name)   │   │ (Code, Name,        │           │
│  │              │   │                │   │  SystemKey, Nature) │           │
│  └──────────────┘   └────────────────┘   └──────────┬──────────┘           │
│                                                      │                       │
│                              ┌───────────────────────┼───────────────┐      │
│                              ▼                       ▼               ▼      │
│                    ┌─────────────────┐   ┌──────────────────┐  ┌─────────┐ │
│                    │ DetailedAccount │   │ FloatingDetail   │  │ Voucher │ │
│                    │ (Party Sub-Led) │   │ Account          │  │ Line    │ │
│                    └─────────────────┘   └──────────────────┘  └─────────┘ │
│                                                                              │
│  TRANSACTION LAYER                                                           │
│  ┌────────────────┐   ┌────────────────┐   ┌────────────────────┐          │
│  │ VoucherHeader  │──▶│ VoucherLine    │──▶│ SubsidiaryAccount  │          │
│  │ (Date, Status, │   │ (Debit, Credit,│   │ JournalType        │          │
│  │  DocNumber)    │   │  Currency, FX) │   │ FiscalPeriod       │          │
│  └────────────────┘   └────────────────┘   └────────────────────┘          │
│                                                                              │
│  SUPPORTING ENTITIES                                                         │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐    │
│  │ FiscalYear   │  │ FiscalPeriod │  │ Currency     │  │ ExchangeRate │    │
│  └──────────────┘  └──────────────┘  └──────────────┘  └──────────────┘    │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐                      │
│  │ Invoice      │  │ InvoiceItem  │  │ PayrollList  │                      │
│  │ (Sale/Purch) │  │              │  │ PayrollItem  │                      │
│  └──────────────┘  └──────────────┘  └──────────────┘                      │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 1.2 Current Capabilities Assessment

| Domain | Current State | Maturity Level |
|--------|---------------|----------------|
| **Chart of Accounts** | 4-level hierarchy with account nature | ★★★☆☆ (3/5) |
| **Journal Entries** | Manual vouchers with double-entry validation | ★★★★☆ (4/5) |
| **Invoice Management** | Sales/Purchase invoices with approval workflow | ★★★☆☆ (3/5) |
| **Tax Handling** | 10% VAT, seasonal reports (Art. 169) | ★★★☆☆ (3/5) |
| **Multi-Currency** | Currency master, exchange rates, FX revaluation | ★★★★☆ (4/5) |
| **Period Management** | Fiscal year/period with lock status | ★★★★☆ (4/5) |
| **Payroll** | Basic salary calculation | ★★☆☆☆ (2/5) |
| **Financial Reporting** | Trial Balance, Dashboard | ★★☆☆☆ (2/5) |
| **Audit Trail** | Voucher immutability after posting | ★★★☆☆ (3/5) |

### 1.3 Strengths of Current Implementation

1. **Solid Foundation**: Clean modular architecture with `FinanceDbContext`
2. **Double-Entry Validation**: Enforced at `VoucherHeader` level
3. **Voucher Lifecycle**: Draft → Reviewed → Approved → Permanent workflow
4. **Period Controls**: Open/SoftLocked/HardLocked status for fiscal periods
5. **Multi-Currency Support**: Foreign currency voucher lines with FX revaluation
6. **Persian Calendar Integration**: Proper Shamsi date handling
7. **Segregation of Duties**: SoD validation on invoice approval
8. **Immutable Posted Vouchers**: Posted vouchers cannot be modified

### 1.4 Critical Weaknesses

1. **No Financial Statements**: Missing Balance Sheet, P&L, Cash Flow
2. **No Fixed Assets**: No asset register or depreciation
3. **No Budgeting**: No budget creation, tracking, or variance analysis
4. **No Bank Reconciliation**: No bank statement import or matching
5. **No Aging Reports**: No AR/AP aging analysis
6. **No Payment Processing**: No payment gateway or check management
7. **No Cost Centers**: Cost center field exists but not utilized
8. **Limited Reporting**: Only Trial Balance available
9. **No Intercompany**: No multi-entity support
10. **No Audit Log Integration**: Financial changes not tracked in audit log

---

## 2. Comprehensive Gap Analysis vs World-Class ERP Standards

### 2.1 Gap Analysis Matrix

| Feature Area | NetSuite | SAP FICO | Dynamics 365 | Odoo | Current System | Gap Priority |
|--------------|----------|----------|--------------|------|----------------|--------------|
| **GENERAL LEDGER** |
| Multi-level COA | ✓ | ✓ | ✓ | ✓ | ✓ (4-level) | LOW |
| Journal Entry Templates | ✓ | ✓ | ✓ | ✓ | ✗ | HIGH |
| Recurring Journals | ✓ | ✓ | ✓ | ✓ | ✗ | HIGH |
| Reverse Journals | ✓ | ✓ | ✓ | ✓ | ✓ | LOW |
| Allocation Journals | ✓ | ✓ | ✓ | Partial | ✗ | MEDIUM |
| **ACCOUNTS RECEIVABLE** |
| Customer Invoices | ✓ | ✓ | ✓ | ✓ | ✓ | LOW |
| Credit Notes | ✓ | ✓ | ✓ | ✓ | ✗ | CRITICAL |
| Debit Notes | ✓ | ✓ | ✓ | ✓ | ✗ | HIGH |
| Payment Receipts | ✓ | ✓ | ✓ | ✓ | ✗ | CRITICAL |
| Aging Reports | ✓ | ✓ | ✓ | ✓ | ✗ | CRITICAL |
| Dunning Letters | ✓ | ✓ | ✓ | Partial | ✗ | MEDIUM |
| **ACCOUNTS PAYABLE** |
| Vendor Bills | ✓ | ✓ | ✓ | ✓ | ✓ (Purchase Inv) | LOW |
| Bill Payment | ✓ | ✓ | ✓ | ✓ | ✗ | CRITICAL |
| 1099/Tax Reporting | ✓ | ✓ | ✓ | Partial | ✓ (Seasonal) | LOW |
| Payment Terms | ✓ | ✓ | ✓ | ✓ | ✗ | HIGH |
| **CASH & BANK** |
| Bank Accounts | ✓ | ✓ | ✓ | ✓ | ✗ | CRITICAL |
| Bank Reconciliation | ✓ | ✓ | ✓ | ✓ | ✗ | CRITICAL |
| Bank Feeds Import | ✓ | ✓ | ✓ | Partial | ✗ | HIGH |
| Petty Cash | ✓ | ✓ | ✓ | ✓ | ✗ | MEDIUM |
| Check Management | ✓ | ✓ | ✓ | ✓ | ✗ | HIGH |
| **FIXED ASSETS** |
| Asset Register | ✓ | ✓ | ✓ | ✓ | ✗ | CRITICAL |
| Depreciation Methods | ✓ (SL, DB) | ✓ (Multiple) | ✓ (Multiple) | ✓ (SL, DB) | ✗ | CRITICAL |
| Asset Disposal | ✓ | ✓ | ✓ | ✓ | ✗ | HIGH |
| Asset Transfer | ✓ | ✓ | ✓ | Partial | ✗ | MEDIUM |
| **BUDGETING** |
| Budget Creation | ✓ | ✓ | ✓ | ✓ | ✗ | CRITICAL |
| Budget vs Actual | ✓ | ✓ | ✓ | ✓ | ✗ | CRITICAL |
| Budget Approval | ✓ | ✓ | ✓ | Partial | ✗ | HIGH |
| **REPORTING** |
| Balance Sheet | ✓ | ✓ | ✓ | ✓ | ✗ | CRITICAL |
| Income Statement | ✓ | ✓ | ✓ | ✓ | ✗ | CRITICAL |
| Cash Flow Statement | ✓ | ✓ | ✓ | ✓ | ✗ | CRITICAL |
| Trial Balance | ✓ | ✓ | ✓ | ✓ | ✓ | LOW |
| General Ledger Detail | ✓ | ✓ | ✓ | ✓ | Partial | HIGH |
| **TAX MANAGEMENT** |
| Multi-rate VAT | ✓ | ✓ | ✓ | ✓ | ✗ (10% only) | HIGH |
| Tax Exemptions | ✓ | ✓ | ✓ | ✓ | Partial | MEDIUM |
| Tax Filing Export | ✓ | ✓ | ✓ | Partial | Partial | MEDIUM |
| **MULTI-ENTITY** |
| Intercompany | ✓ | ✓ | ✓ | Partial | ✗ | MEDIUM |
| Consolidation | ✓ | ✓ | ✓ | ✗ | ✗ | LOW |

### 2.2 Priority Classification

#### CRITICAL GAPS (Must-Have for Enterprise)
1. Financial Statements (Balance Sheet, P&L, Cash Flow)
2. Accounts Receivable/Payable Aging Reports
3. Payment Processing (Receipts & Payments)
4. Credit/Debit Notes
5. Bank Account Management & Reconciliation
6. Fixed Asset Register with Depreciation
7. Budgeting & Variance Analysis

#### HIGH PRIORITY GAPS
1. Journal Entry Templates & Recurring Journals
2. Check Management
3. Payment Terms Management
4. General Ledger Detail Report
5. Multi-rate Tax Support
6. Budget Approval Workflow

#### MEDIUM PRIORITY GAPS
1. Allocation Journals
2. Dunning Letters
3. Petty Cash Management
4. Asset Transfer
5. Tax Filing Export

---

## 3. Detailed Module Breakdown

### 3.1 General Ledger (GL) & Chart of Accounts (COA)

#### Current State
```csharp
// Existing 4-Level Hierarchy
AccountGroup (Code, Name, Nature)
    └── GeneralAccount (Code, Name, Nature)
            └── SubsidiaryAccount (Code, Name, SystemKey, IsTemporary)
                    ├── DetailedAccount (Code, Name, PartyType, ExternalReference)
                    └── FloatingDetailAccount (Code, Name, Type)
```

#### Gap Analysis
| Requirement | Status | Notes |
|-------------|--------|-------|
| Multi-level tree structure | ✓ Implemented | 4 levels supported |
| Account nature (Debit/Credit) | ✓ Implemented | `AccountNature` enum |
| Account status (Active/Inactive) | ✓ Implemented | `IsActive` flag |
| Temporary account flag | ✓ Implemented | `IsTemporary` for P&L accounts |
| System keys for auto-posting | ✓ Implemented | `FinanceAccountKeys` constants |
| Journal entry templates | ✗ Missing | No template system |
| Recurring journals | ✗ Missing | No recurring entry setup |
| Budget integration | ✗ Missing | No budget dimension |
| Department/Project dimension | ✗ Missing | CostCenterId exists but unused |

#### Recommended Enhancements
1. Add Journal Entry Templates for common transactions
2. Implement Recurring Journal functionality
3. Activate Cost Center dimension
4. Add Project/Department dimension
5. Implement Account Aliases for flexible reporting

### 3.2 Accounts Receivable (AR)

#### Current State
- Customer invoices via `Invoice` entity (InvoiceType = "Sale")
- Employer master data via `Employer` entity
- Basic approval workflow
- No payment receipt processing
- No aging analysis
- No credit note capability

#### Gap Analysis
| Requirement | Status | Impact |
|-------------|--------|--------|
| Customer master | ✓ Partial | Uses Employer entity |
| Invoice creation | ✓ Implemented | Sales invoices |
| Invoice approval | ✓ Implemented | Workflow status |
| Payment receipts | ✗ Missing | Cannot record customer payments |
| Credit notes | ✗ Missing | Cannot issue credit memos |
| Debit notes | ✗ Missing | Cannot issue debit memos |
| Aging reports | ✗ Missing | No AR aging analysis |
| Dunning | ✗ Missing | No collection letters |
| Payment terms | ✗ Missing | No due date calculation |
| Write-offs | ✗ Missing | Cannot write off bad debts |

#### Recommended Data Model
```csharp
// New entities needed
public class Customer { /* Dedicated customer entity */ }
public class PaymentReceipt { /* Customer payment records */ }
public class CreditNote { /* Credit memo records */ }
public class PaymentTerm { /* Payment term definitions */ }
public class ArAgingBucket { /* Aging configuration */ }
```

### 3.3 Accounts Payable (AP)

#### Current State
- Purchase invoices via `Invoice` entity (InvoiceType = "Purchase")
- Vendor master via `Vendor` entity
- Warehouse receipt linkage
- No payment processing
- No aging analysis

#### Gap Analysis
| Requirement | Status | Impact |
|-------------|--------|--------|
| Vendor master | ✓ Implemented | Vendor entity exists |
| Bill creation | ✓ Implemented | Purchase invoices |
| Bill approval | ✓ Implemented | Workflow status |
| Payment processing | ✗ Missing | Cannot record vendor payments |
| Credit notes | ✗ Missing | Cannot record vendor credits |
| Aging reports | ✗ Missing | No AP aging analysis |
| 1099 reporting | ✓ Partial | Seasonal tax report |
| Payment terms | ✗ Missing | No due date calculation |
| Check printing | ✗ Missing | No check management |

### 3.4 Cash & Bank Management

#### Current State
- **NO bank account management**
- **NO bank reconciliation**
- **NO check management**
- **NO petty cash**

This is a completely missing module.

#### Required Entities
```csharp
public class BankAccount {
    public int Id { get; set; }
    public string AccountNumber { get; set; }
    public string BankName { get; set; }
    public string BranchName { get; set; }
    public string BranchCode { get; set; }
    public string AccountType { get; set; } // Checking, Savings, etc.
    public string CurrencyCode { get; set; }
    public decimal CurrentBalance { get; set; }
    public decimal BookBalance { get; set; }
    public bool IsActive { get; set; }
    public int? GlAccountId { get; set; } // Linked GL control account
}

public class BankStatement {
    public int Id { get; set; }
    public int BankAccountId { get; set; }
    public DateTime StatementDate { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public List<BankStatementLine> Lines { get; set; }
}

public class BankReconciliation {
    public int Id { get; set; }
    public int BankAccountId { get; set; }
    public DateTime ReconciliationDate { get; set; }
    public decimal BookBalance { get; set; }
    public decimal BankBalance { get; set; }
    public decimal Difference { get; set; }
    public string Status { get; set; } // Draft, Reconciled
    public List<ReconciliationMatch> Matches { get; set; }
}

public class Check {
    public int Id { get; set; }
    public int BankAccountId { get; set; }
    public string CheckNumber { get; set; }
    public DateTime IssueDate { get; set; }
    public string PayeeName { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } // Issued, Cleared, Void, Stopped
    public int? PaymentId { get; set; }
}
```

### 3.5 Fixed Assets & Depreciation

#### Current State
- **NO fixed asset management**
- **NO depreciation calculation**
- **NO asset register**

This is a completely missing module critical for enterprise finance.

#### Required Data Model
```csharp
public class AssetCategory {
    public int Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string DepreciationMethod { get; set; } // SL, DB, SYD, etc.
    public int UsefulLifeMonths { get; set; }
    public decimal SalvageValuePercent { get; set; }
    public int AssetAccountId { get; set; } // GL account
    public int DepreciationAccountId { get; set; } // Expense account
    public int AccumulatedDepreciationAccountId { get; set; } // Contra account
}

public class FixedAsset {
    public int Id { get; set; }
    public string AssetCode { get; set; }
    public string AssetName { get; set; }
    public string Description { get; set; }
    public string SerialNumber { get; set; }
    public int CategoryId { get; set; }
    public int LocationId { get; set; }
    public int DepartmentId { get; set; }
    public int CustodianId { get; set; }
    
    // Financial
    public decimal AcquisitionCost { get; set; }
    public decimal SalvageValue { get; set; }
    public decimal DepreciableBasis { get; set; }
    public decimal AccumulatedDepreciation { get; set; }
    public decimal NetBookValue { get; set; }
    
    // Dates
    public DateTime AcquisitionDate { get; set; }
    public DateTime PlacedInServiceDate { get; set; }
    public DateTime? DisposalDate { get; set; }
    
    // Status
    public string Status { get; set; } // Active, Fully Depreciated, Disposed, Retired
    
    // GL Links
    public int AssetAccountId { get; set; }
    public int AccumulatedDepAccountId { get; set; }
    public int DepreciationExpenseAccountId { get; set; }
}

public class DepreciationRun {
    public int Id { get; set; }
    public DateTime RunDate { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string Status { get; set; } // Draft, Posted
    public decimal TotalDepreciation { get; set; }
    public int VoucherHeaderId { get; set; } // Generated journal entry
    public List<DepreciationEntry> Entries { get; set; }
}

public class DepreciationEntry {
    public int Id { get; set; }
    public int DepreciationRunId { get; set; }
    public int FixedAssetId { get; set; }
    public decimal DepreciationAmount { get; set; }
    public decimal AccumulatedDepreciation { get; set; }
    public decimal NetBookValue { get; set; }
}
```

#### Depreciation Methods to Support
1. **Straight-Line (SL)**: `(Cost - Salvage) / Useful Life`
2. **Declining Balance (DB)**: `Book Value × (2 / Useful Life)`
3. **Sum-of-Years-Digits (SYD)**: Accelerated depreciation
4. **Units of Production**: Based on usage

### 3.6 Budgeting & Cost Centers

#### Current State
- **NO budgeting functionality**
- CostCenterId exists in VoucherLine but is unused
- No budget creation, tracking, or variance analysis

#### Required Data Model
```csharp
public class Budget {
    public int Id { get; set; }
    public string BudgetName { get; set; }
    public int FiscalYearId { get; set; }
    public string Status { get; set; } // Draft, Approved, Final
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string ApprovalStatus { get; set; }
    public string ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public List<BudgetLine> Lines { get; set; }
}

public class BudgetLine {
    public int Id { get; set; }
    public int BudgetId { get; set; }
    public int AccountId { get; set; }
    public int? CostCenterId { get; set; }
    public int? DepartmentId { get; set; }
    public int? ProjectId { get; set; }
    
    // Monthly amounts
    public decimal Period01 { get; set; }
    public decimal Period02 { get; set; }
    // ... through Period12
    
    public decimal AnnualTotal { get; set; }
}

public class BudgetVariance {
    // Computed view for reporting
    public int AccountId { get; set; }
    public int Period { get; set; }
    public decimal BudgetAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal Variance { get; set; }
    public decimal VariancePercent { get; set; }
}
```

### 3.7 Financial Reporting

#### Current State
| Report | Status |
|--------|--------|
| Trial Balance | ✓ Implemented |
| Dashboard | ✓ Implemented |
| Seasonal Tax Report | ✓ Implemented |
| Balance Sheet | ✗ Missing |
| Income Statement (P&L) | ✗ Missing |
| Cash Flow Statement | ✗ Missing |
| General Ledger Detail | ✗ Missing |
| Account Analysis | ✗ Missing |
| AR Aging | ✗ Missing |
| AP Aging | ✗ Missing |

#### Required Financial Statements

##### Balance Sheet (Statement of Financial Position)
```
ASSETS
├── Current Assets
│   ├── Cash & Cash Equivalents
│   ├── Accounts Receivable (net)
│   ├── Inventory
│   ├── Prepaid Expenses
│   └── Other Current Assets
├── Non-Current Assets
│   ├── Fixed Assets (net)
│   ├── Intangible Assets
│   └── Other Non-Current Assets
└── TOTAL ASSETS

LIABILITIES & EQUITY
├── Current Liabilities
│   ├── Accounts Payable
│   ├── Accrued Expenses
│   ├── Taxes Payable
│   └── Other Current Liabilities
├── Non-Current Liabilities
│   ├── Long-term Debt
│   └── Other Non-Current Liabilities
└── Equity
    ├── Share Capital
    ├── Retained Earnings
    └── Current Year Earnings
```

##### Income Statement (Profit & Loss)
```
REVENUE
├── Sales Revenue
├── Service Revenue
└── Other Revenue

COST OF GOODS SOLD
├── Opening Inventory
├── Purchases
├── Direct Costs
└── Closing Inventory

GROSS PROFIT

OPERATING EXPENSES
├── Salaries & Wages
├── Rent
├── Utilities
├── Depreciation
└── Other Operating Expenses

OPERATING PROFIT

OTHER INCOME/EXPENSES
├── Interest Income
├── Interest Expense
└── FX Gain/Loss

NET PROFIT BEFORE TAX

INCOME TAX

NET PROFIT
```

##### Cash Flow Statement
```
OPERATING ACTIVITIES
├── Net Profit
├── Adjustments for:
│   ├── Depreciation
│   ├── Changes in AR
│   ├── Changes in Inventory
│   ├── Changes in AP
│   └── Other Working Capital
└── Net Cash from Operations

INVESTING ACTIVITIES
├── Purchase of Fixed Assets
├── Sale of Fixed Assets
└── Net Cash from Investing

FINANCING ACTIVITIES
├── Capital Contributions
├── Loan Proceeds
├── Loan Repayments
└── Net Cash from Financing

NET CHANGE IN CASH
```

### 3.8 Tax Engine

#### Current State
- Single 10% VAT rate
- Seasonal transaction reporting (Article 169)
- VAT dashboard with reconciliation
- Tax analytics with AI assistant

#### Gaps
| Requirement | Status |
|-------------|--------|
| Multiple tax rates | ✗ Missing |
| Tax exemptions by category | Partial |
| Tax exemption certificates | ✗ Missing |
| Withholding tax | ✗ Missing |
| Tax payment tracking | ✗ Missing |
| Tax return generation | ✗ Missing |
| Multi-jurisdiction tax | ✗ Missing |

---

## 4. Proposed C# Entity Models & Database Schema Additions

### 4.1 New Entity Models

#### 4.1.1 Bank & Cash Management

```csharp
namespace OfficeAutomation.Modules.Finance.Domain;

/// <summary>
/// Bank account master data
/// </summary>
public sealed class BankAccount
{
    public int Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string BranchCode { get; set; } = string.Empty;
    public string AccountType { get; set; } = "Checking"; // Checking, Savings, Current
    public string CurrencyCode { get; set; } = "IRR";
    public string IbanNumber { get; set; } = string.Empty;
    public string SwiftCode { get; set; } = string.Empty;
    
    // GL Integration
    public int? ControlAccountId { get; set; }
    public SubsidiaryAccount? ControlAccount { get; set; }
    
    // Balances
    public decimal CurrentBalance { get; set; }
    public decimal BookBalance { get; set; }
    public decimal LastReconciledBalance { get; set; }
    public DateTime? LastReconciledDate { get; set; }
    
    // Status
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }
    
    // Audit
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string? CreatedBy { get; set; }
    
    // Navigation
    public List<BankStatement> Statements { get; set; } = [];
    public List<PaymentTransaction> Transactions { get; set; } = [];
}

/// <summary>
/// Bank statement imported from bank
/// </summary>
public sealed class BankStatement
{
    public int Id { get; set; }
    public int BankAccountId { get; set; }
    public BankAccount BankAccount { get; set; } = null!;
    
    public DateTime StatementDate { get; set; }
    public DateTime PeriodStartDate { get; set; }
    public DateTime PeriodEndDate { get; set; }
    
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public decimal TotalDebits { get; set; }
    public decimal TotalCredits { get; set; }
    
    public string? ImportFileName { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.Now;
    
    public List<BankStatementLine> Lines { get; set; } = [];
}

public sealed class BankStatementLine
{
    public int Id { get; set; }
    public int BankStatementId { get; set; }
    public BankStatement BankStatement { get; set; } = null!;
    
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public decimal Balance { get; set; }
    
    // Reconciliation
    public bool IsReconciled { get; set; }
    public int? MatchedTransactionId { get; set; }
    public PaymentTransaction? MatchedTransaction { get; set; }
}

/// <summary>
/// Bank reconciliation session
/// </summary>
public sealed class BankReconciliation
{
    public int Id { get; set; }
    public int BankAccountId { get; set; }
    public BankAccount BankAccount { get; set; } = null!;
    
    public DateTime ReconciliationDate { get; set; }
    public DateTime StatementEndDate { get; set; }
    
    public decimal StatementBalance { get; set; }
    public decimal BookBalance { get; set; }
    public decimal Difference { get; set; }
    
    public string Status { get; set; } = "Draft"; // Draft, InProgress, Reconciled
    public string? Notes { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string? CreatedBy { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletedBy { get; set; }
    
    public List<ReconciliationItem> Items { get; set; } = [];
}

public sealed class ReconciliationItem
{
    public int Id { get; set; }
    public int BankReconciliationId { get; set; }
    public BankReconciliation BankReconciliation { get; set; } = null!;
    
    public string ItemType { get; set; } = string.Empty; // OutstandingCheck, DepositInTransit, BankError, BookError
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Side { get; set; } = string.Empty; // Bank, Book
    
    public int? TransactionId { get; set; }
    public PaymentTransaction? Transaction { get; set; }
}
```

#### 4.1.2 Payment Processing

```csharp
namespace OfficeAutomation.Modules.Finance.Domain;

/// <summary>
/// Payment transaction (receipts and payments)
/// </summary>
public sealed class PaymentTransaction
{
    public int Id { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    
    public string TransactionType { get; set; } = string.Empty; // Receipt, Payment, Transfer
    public string PaymentMethod { get; set; } = string.Empty; // Cash, Check, BankTransfer, CreditCard
    
    // Amounts
    public decimal Amount { get; set; }
    public decimal? Discount { get; set; }
    public decimal NetAmount { get; set; }
    
    // Currency
    public Guid? CurrencyId { get; set; }
    public Currency? Currency { get; set; }
    public decimal ExchangeRate { get; set; } = 1m;
    public decimal? ForeignAmount { get; set; }
    
    // Party
    public string PartyType { get; set; } = string.Empty; // Customer, Vendor, Employee
    public int? PartyId { get; set; }
    public string PartyName { get; set; } = string.Empty;
    
    // Bank
    public int? BankAccountId { get; set; }
    public BankAccount? BankAccount { get; set; }
    
    // Check details
    public string? CheckNumber { get; set; }
    public DateTime? CheckDate { get; set; }
    public string? CheckBank { get; set; }
    public string? CheckStatus { get; set; } // Issued, Cleared, Bounced, Void
    
    // GL Integration
    public int VoucherHeaderId { get; set; }
    public VoucherHeader Voucher { get; set; } = null!;
    
    // Status
    public string Status { get; set; } = "Draft"; // Draft, Posted, Cleared, Void
    public string? Notes { get; set; }
    
    // Audit
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string? CreatedBy { get; set; }
    
    // Allocations
    public List<PaymentAllocation> Allocations { get; set; } = [];
}

/// <summary>
/// Allocation of payment to specific invoices
/// </summary>
public sealed class PaymentAllocation
{
    public int Id { get; set; }
    public int PaymentTransactionId { get; set; }
    public PaymentTransaction PaymentTransaction { get; set; } = null!;
    
    public string DocumentType { get; set; } = string.Empty; // Invoice, CreditNote, DebitNote
    public int DocumentId { get; set; }
    
    public decimal AllocatedAmount { get; set; }
    public decimal DiscountTaken { get; set; }
    public decimal WriteOffAmount { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
```

#### 4.1.3 Credit & Debit Notes

```csharp
namespace OfficeAutomation.Modules.Finance.Domain;

/// <summary>
/// Credit note (for both AR and AP)
/// </summary>
public sealed class CreditNote
{
    public int Id { get; set; }
    public string CreditNoteNumber { get; set; } = string.Empty;
    public DateTime CreditNoteDate { get; set; }
    
    public string NoteType { get; set; } = string.Empty; // Customer, Vendor
    public string Reason { get; set; } = string.Empty; // Return, Discount, Correction, Other
    
    // Original document reference
    public int? OriginalInvoiceId { get; set; }
    public Invoice? OriginalInvoice { get; set; }
    
    // Party
    public int? CustomerId { get; set; }
    public int? VendorId { get; set; }
    public string PartyName { get; set; } = string.Empty;
    public string? NationalId { get; set; }
    
    // Amounts
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    
    // GL Integration
    public int? VoucherHeaderId { get; set; }
    public VoucherHeader? Voucher { get; set; }
    
    // Status
    public string Status { get; set; } = "Draft"; // Draft, Approved, Applied, Void
    public string? Notes { get; set; }
    
    // Audit
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string? CreatedBy { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    
    // Items
    public List<CreditNoteLine> Lines { get; set; } = [];
}

public sealed class CreditNoteLine
{
    public int Id { get; set; }
    public int CreditNoteId { get; set; }
    public CreditNote CreditNote { get; set; } = null!;
    
    public string Description { get; set; } = string.Empty;
    public int? ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    
    public int DisplayOrder { get; set; }
}

/// <summary>
/// Debit note (additional charges)
/// </summary>
public sealed class DebitNote
{
    public int Id { get; set; }
    public string DebitNoteNumber { get; set; } = string.Empty;
    public DateTime DebitNoteDate { get; set; }
    
    public string NoteType { get; set; } = string.Empty; // Customer, Vendor
    public string Reason { get; set; } = string.Empty;
    
    // Party
    public int? CustomerId { get; set; }
    public int? VendorId { get; set; }
    public string PartyName { get; set; } = string.Empty;
    
    // Amounts
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    
    // GL Integration
    public int? VoucherHeaderId { get; set; }
    public VoucherHeader? Voucher { get; set; }
    
    // Status
    public string Status { get; set; } = "Draft";
    public string? Notes { get; set; }
    
    // Audit
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string? CreatedBy { get; set; }
    
    public List<DebitNoteLine> Lines { get; set; } = [];
}

public sealed class DebitNoteLine
{
    public int Id { get; set; }
    public int DebitNoteId { get; set; }
    public DebitNote DebitNote { get; set; } = null!;
    
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    
    public int DisplayOrder { get; set; }
}
```

#### 4.1.4 Fixed Assets

```csharp
namespace OfficeAutomation.Modules.Finance.Domain;

/// <summary>
/// Asset category with depreciation rules
/// </summary>
public sealed class AssetCategory
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    // Depreciation settings
    public string DepreciationMethod { get; set; } = "SL"; // SL, DB, SYD, Units
    public int UsefulLifeMonths { get; set; }
    public decimal SalvageValuePercent { get; set; }
    public decimal DepreciationRate { get; set; } // For DB method
    
    // GL Account mappings
    public int AssetAccountId { get; set; }
    public int AccumulatedDepreciationAccountId { get; set; }
    public int DepreciationExpenseAccountId { get; set; }
    public int? DisposalGainLossAccountId { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    // Navigation
    public SubsidiaryAccount AssetAccount { get; set; } = null!;
    public SubsidiaryAccount AccumulatedDepreciationAccount { get; set; } = null!;
    public SubsidiaryAccount DepreciationExpenseAccount { get; set; } = null!;
    
    public List<FixedAsset> Assets { get; set; } = [];
}

/// <summary>
/// Individual fixed asset
/// </summary>
public sealed class FixedAsset
{
    public int Id { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SerialNumber { get; set; }
    public string? Barcode { get; set; }
    
    // Classification
    public int CategoryId { get; set; }
    public AssetCategory Category { get; set; } = null!;
    public int? LocationId { get; set; }
    public int? DepartmentId { get; set; }
    public int? CustodianEmployeeId { get; set; }
    
    // Acquisition
    public DateTime AcquisitionDate { get; set; }
    public DateTime PlacedInServiceDate { get; set; }
    public string AcquisitionType { get; set; } = "Purchase"; // Purchase, Construction, Donation, Transfer
    
    // Financial
    public decimal AcquisitionCost { get; set; }
    public decimal SalvageValue { get; set; }
    public decimal DepreciableBasis { get; set; }
    public decimal AccumulatedDepreciation { get; set; }
    public decimal NetBookValue { get; set; }
    public decimal? ImpairmentAmount { get; set; }
    
    // Depreciation
    public string DepreciationMethod { get; set; } = "SL";
    public int UsefulLifeMonths { get; set; }
    public DateTime? LastDepreciationDate { get; set; }
    public DateTime? NextDepreciationDate { get; set; }
    public int DepreciationPeriodsRun { get; set; }
    
    // Disposal
    public DateTime? DisposalDate { get; set; }
    public string? DisposalType { get; set; } // Sale, Scrap, Donation, Transfer
    public decimal? DisposalProceeds { get; set; }
    public decimal? DisposalGainLoss { get; set; }
    
    // Status
    public string Status { get; set; } = "Active"; // Active, FullyDepreciated, Disposed, Retired, Impaired
    
    // GL Integration
    public int AssetAccountId { get; set; }
    public int AccumulatedDepAccountId { get; set; }
    public int DepreciationExpenseAccountId { get; set; }
    
    // Audit
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    
    // Navigation
    public List<AssetDepreciationEntry> DepreciationEntries { get; set; } = [];
    public List<AssetMovement> Movements { get; set; } = [];
}

/// <summary>
/// Depreciation calculation entry
/// </summary>
public sealed class AssetDepreciationEntry
{
    public int Id { get; set; }
    public int FixedAssetId { get; set; }
    public FixedAsset FixedAsset { get; set; } = null!;
    
    public int DepreciationRunId { get; set; }
    public DepreciationRun DepreciationRun { get; set; } = null!;
    
    public DateTime PeriodStartDate { get; set; }
    public DateTime PeriodEndDate { get; set; }
    
    public decimal BeginningBookValue { get; set; }
    public decimal DepreciationAmount { get; set; }
    public decimal AccumulatedDepreciation { get; set; }
    public decimal EndingBookValue { get; set; }
    
    public int? VoucherLineId { get; set; }
}

/// <summary>
/// Depreciation run (batch process)
/// </summary>
public sealed class DepreciationRun
{
    public int Id { get; set; }
    public string RunNumber { get; set; } = string.Empty;
    public DateTime RunDate { get; set; }
    
    public DateTime PeriodStartDate { get; set; }
    public DateTime PeriodEndDate { get; set; }
    
    public string Status { get; set; } = "Draft"; // Draft, Calculated, Posted
    public decimal TotalDepreciation { get; set; }
    public int AssetCount { get; set; }
    
    public int? VoucherHeaderId { get; set; }
    public VoucherHeader? Voucher { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string? CreatedBy { get; set; }
    public DateTime? PostedAt { get; set; }
    public string? PostedBy { get; set; }
    
    public List<AssetDepreciationEntry> Entries { get; set; } = [];
}

/// <summary>
/// Asset movement history
/// </summary>
public sealed class AssetMovement
{
    public int Id { get; set; }
    public int FixedAssetId { get; set; }
    public FixedAsset FixedAsset { get; set; } = null!;
    
    public DateTime MovementDate { get; set; }
    public string MovementType { get; set; } = string.Empty; // Transfer, Assignment, StatusChange
    
    public int? FromLocationId { get; set; }
    public int? ToLocationId { get; set; }
    public int? FromDepartmentId { get; set; }
    public int? ToDepartmentId { get; set; }
    public int? FromCustodianId { get; set; }
    public int? ToCustodianId { get; set; }
    public string? FromStatus { get; set; }
    public string? ToStatus { get; set; }
    
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string? CreatedBy { get; set; }
}
```

#### 4.1.5 Budgeting

```csharp
namespace OfficeAutomation.Modules.Finance.Domain;

/// <summary>
/// Budget master
/// </summary>
public sealed class Budget
{
    public int Id { get; set; }
    public string BudgetCode { get; set; } = string.Empty;
    public string BudgetName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    public int FiscalYearId { get; set; }
    public FiscalYear FiscalYear { get; set; } = null!;
    
    public string BudgetType { get; set; } = "Operating"; // Operating, Capital, Project, CashFlow
    public string Status { get; set; } = "Draft"; // Draft, Submitted, Approved, Final, Closed
    
    // Approval
    public string? SubmittedBy { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    
    // Totals
    public decimal TotalBudget { get; set; }
    public decimal TotalActual { get; set; }
    public decimal TotalVariance { get; set; }
    
    // Audit
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    
    // Navigation
    public List<BudgetLine> Lines { get; set; } = [];
}

/// <summary>
/// Budget line item
/// </summary>
public sealed class BudgetLine
{
    public int Id { get; set; }
    public int BudgetId { get; set; }
    public Budget Budget { get; set; } = null!;
    
    // Dimensions
    public int AccountId { get; set; }
    public SubsidiaryAccount Account { get; set; } = null!;
    public int? CostCenterId { get; set; }
    public int? DepartmentId { get; set; }
    public int? ProjectId { get; set; }
    
    // Monthly amounts (Persian calendar months 1-12)
    public decimal Period01 { get; set; }
    public decimal Period02 { get; set; }
    public decimal Period03 { get; set; }
    public decimal Period04 { get; set; }
    public decimal Period05 { get; set; }
    public decimal Period06 { get; set; }
    public decimal Period07 { get; set; }
    public decimal Period08 { get; set; }
    public decimal Period09 { get; set; }
    public decimal Period10 { get; set; }
    public decimal Period11 { get; set; }
    public decimal Period12 { get; set; }
    
    public decimal AnnualTotal { get; set; }
    
    // Notes
    public string? Notes { get; set; }
}

/// <summary>
/// Budget version for scenario planning
/// </summary>
public sealed class BudgetVersion
{
    public int Id { get; set; }
    public int BudgetId { get; set; }
    public Budget Budget { get; set; } = null!;
    
    public string VersionName { get; set; } = string.Empty; // Original, Revised, Forecast
    public string Description { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string? CreatedBy { get; set; }
    
    public List<BudgetLineVersion> Lines { get; set; } = [];
}

public sealed class BudgetLineVersion
{
    public int Id { get; set; }
    public int BudgetVersionId { get; set; }
    public BudgetVersion BudgetVersion { get; set; } = null!;
    public int BudgetLineId { get; set; }
    public BudgetLine BudgetLine { get; set; } = null!;
    
    public decimal Period01 { get; set; }
    public decimal Period02 { get; set; }
    // ... through Period12
    public decimal AnnualTotal { get; set; }
}
```

#### 4.1.6 Cost Centers & Dimensions

```csharp
namespace OfficeAutomation.Modules.Finance.Domain;

/// <summary>
/// Cost center for expense allocation
/// </summary>
public sealed class CostCenter
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    public int? ParentCostCenterId { get; set; }
    public CostCenter? ParentCostCenter { get; set; }
    
    public int? ManagerEmployeeId { get; set; }
    public int? DepartmentId { get; set; }
    
    public string CostType { get; set; } = "Operating"; // Operating, Capital, Project
    public string AllocationMethod { get; set; } = "Direct"; // Direct, StepDown, ActivityBased
    
    public bool IsActive { get; set; } = true;
    public bool IsPostingAllowed { get; set; } = true;
    
    // Budget link
    public int? BudgetId { get; set; }
    
    // Audit
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string? CreatedBy { get; set; }
    
    // Navigation
    public List<CostCenter> ChildCostCenters { get; set; } = [];
    public List<VoucherLine> VoucherLines { get; set; } = [];
}

/// <summary>
/// Project for project accounting
/// </summary>
public sealed class Project
{
    public int Id { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    public int? CustomerId { get; set; }
    public int? DepartmentId { get; set; }
    public int? ProjectManagerId { get; set; }
    
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    
    public decimal BudgetAmount { get; set; }
    public decimal ActualCost { get; set; }
    public decimal Revenue { get; set; }
    
    public string Status { get; set; } = "Planning"; // Planning, Active, OnHold, Completed, Cancelled
    public decimal CompletionPercent { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    // Audit
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string? CreatedBy { get; set; }
    
    // Navigation
    public List<VoucherLine> VoucherLines { get; set; } = [];
    public List<ProjectCost> Costs { get; set; } = [];
}

public sealed class ProjectCost
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    
    public int VoucherLineId { get; set; }
    public VoucherLine VoucherLine { get; set; } = null!;
    
    public string CostType { get; set; } = string.Empty; // Labor, Material, Overhead, Other
    public decimal Amount { get; set; }
    public DateTime CostDate { get; set; }
    public string? Description { get; set; }
}
```

### 4.2 Database Schema Extensions

#### New Tables Required

```sql
-- Bank & Cash Management
CREATE TABLE [finance].[BankAccounts] (
    [Id] INT PRIMARY KEY IDENTITY(1,1),
    [AccountNumber] NVARCHAR(50) NOT NULL,
    [BankName] NVARCHAR(100) NOT NULL,
    [BranchName] NVARCHAR(100),
    [BranchCode] NVARCHAR(20),
    [AccountType] NVARCHAR(20) DEFAULT 'Checking',
    [CurrencyCode] NVARCHAR(10) DEFAULT 'IRR',
    [IbanNumber] NVARCHAR(50),
    [SwiftCode] NVARCHAR(20),
    [ControlAccountId] INT,
    [CurrentBalance] DECIMAL(18,2) DEFAULT 0,
    [BookBalance] DECIMAL(18,2) DEFAULT 0,
    [LastReconciledBalance] DECIMAL(18,2) DEFAULT 0,
    [LastReconciledDate] DATETIME2,
    [IsActive] BIT DEFAULT 1,
    [IsDefault] BIT DEFAULT 0,
    [CreatedAt] DATETIME2 DEFAULT GETDATE(),
    [CreatedBy] NVARCHAR(450),
    FOREIGN KEY ([ControlAccountId]) REFERENCES [finance].[SubsidiaryAccounts]([Id])
);

CREATE TABLE [finance].[BankStatements] (
    [Id] INT PRIMARY KEY IDENTITY(1,1),
    [BankAccountId] INT NOT NULL,
    [StatementDate] DATETIME2 NOT NULL,
    [PeriodStartDate] DATETIME2 NOT NULL,
    [PeriodEndDate] DATETIME2 NOT NULL,
    [OpeningBalance] DECIMAL(18,2) NOT NULL,
    [ClosingBalance] DECIMAL(18,2) NOT NULL,
    [TotalDebits] DECIMAL(18,2) NOT NULL,
    [TotalCredits] DECIMAL(18,2) NOT NULL,
    [ImportFileName] NVARCHAR(200),
    [ImportedAt] DATETIME2 DEFAULT GETDATE(),
    FOREIGN KEY ([BankAccountId]) REFERENCES [finance].[BankAccounts]([Id])
);

CREATE TABLE [finance].[BankStatementLines] (
    [Id] INT PRIMARY KEY IDENTITY(1,1),
    [BankStatementId] INT NOT NULL,
    [TransactionDate] DATETIME2 NOT NULL,
    [Description] NVARCHAR(500) NOT NULL,
    [Reference] NVARCHAR(100),
    [DebitAmount] DECIMAL(18,2) NOT NULL,
    [CreditAmount] DECIMAL(18,2) NOT NULL,
    [Balance] DECIMAL(18,2) NOT NULL,
    [IsReconciled] BIT DEFAULT 0,
    [MatchedTransactionId] INT,
    FOREIGN KEY ([BankStatementId]) REFERENCES [finance].[BankStatements]([Id])
);

CREATE TABLE [finance].[BankReconciliations] (
    [Id] INT PRIMARY KEY IDENTITY(1,1),
    [BankAccountId] INT NOT NULL,
    [ReconciliationDate] DATETIME2 NOT NULL,
    [StatementEndDate] DATETIME2 NOT NULL,
    [StatementBalance] DECIMAL(18,2) NOT NULL,
    [BookBalance] DECIMAL(18,2) NOT NULL,
    [Difference] DECIMAL(18,2) NOT NULL,
    [Status] NVARCHAR(20) DEFAULT 'Draft',
    [Notes] NVARCHAR(1000),
    [CreatedAt] DATETIME2 DEFAULT GETDATE(),
    [CreatedBy] NVARCHAR(450),
    [CompletedAt] DATETIME2,
    [CompletedBy] NVARCHAR(450),
    FOREIGN KEY ([BankAccountId]) REFERENCES [finance].[BankAccounts]([Id])
);

-- Payment Processing
CREATE TABLE [finance].[PaymentTransactions] (
    [Id] INT PRIMARY KEY IDENTITY(1,1),
    [TransactionNumber] NVARCHAR(50) NOT NULL UNIQUE,
    [TransactionDate] DATETIME2 NOT NULL,
    [TransactionType] NVARCHAR(20) NOT NULL, -- Receipt, Payment, Transfer
    [PaymentMethod] NVARCHAR(20) NOT NULL, -- Cash, Check, BankTransfer, CreditCard
    [Amount] DECIMAL(18,2) NOT NULL,
    [Discount] DECIMAL(18,2),
    [NetAmount] DECIMAL(18,2) NOT NULL,
    [CurrencyId] UNIQUEIDENTIFIER,
    [ExchangeRate] DECIMAL(18,6) DEFAULT 1,
    [ForeignAmount] DECIMAL(18,2),
    [PartyType] NVARCHAR(20) NOT NULL, -- Customer, Vendor, Employee
    [PartyId] INT,
    [PartyName] NVARCHAR(150) NOT NULL,
    [BankAccountId] INT,
    [CheckNumber] NVARCHAR(50),
    [CheckDate] DATETIME2,
    [CheckBank] NVARCHAR(100),
    [CheckStatus] NVARCHAR(20),
    [VoucherHeaderId] INT NOT NULL,
    [Status] NVARCHAR(20) DEFAULT 'Draft',
    [Notes] NVARCHAR(1000),
    [CreatedAt] DATETIME2 DEFAULT GETDATE(),
    [CreatedBy] NVARCHAR(450),
    FOREIGN KEY ([VoucherHeaderId]) REFERENCES [finance].[VoucherHeaders]([Id]),
    FOREIGN KEY ([BankAccountId]) REFERENCES [finance].[BankAccounts]([Id])
);

CREATE TABLE [finance].[PaymentAllocations] (
    [Id] INT PRIMARY KEY IDENTITY(1,1),
    [PaymentTransactionId] INT NOT NULL,
    [DocumentType] NVARCHAR(20) NOT NULL, -- Invoice, CreditNote, DebitNote
    [DocumentId] INT NOT NULL,
    [AllocatedAmount] DECIMAL(18,2) NOT NULL,
    [DiscountTaken] DECIMAL(18,2) DEFAULT 0,
    [WriteOffAmount] DECIMAL(18,2) DEFAULT 0,
    [CreatedAt] DATETIME2 DEFAULT GETDATE(),
    FOREIGN KEY ([PaymentTransactionId]) REFERENCES [finance].[PaymentTransactions]([Id])
);

-- Credit & Debit Notes
CREATE TABLE [finance].[CreditNotes] (
    [Id] INT PRIMARY KEY IDENTITY(1,1),
    [CreditNoteNumber] NVARCHAR(50) NOT NULL UNIQUE,
    [CreditNoteDate] DATETIME2 NOT NULL,
    [NoteType] NVARCHAR(20) NOT NULL, -- Customer, Vendor
    [Reason] NVARCHAR(200) NOT NULL,
    [OriginalInvoiceId] INT,
    [CustomerId] INT,
    [VendorId] INT,
    [PartyName] NVARCHAR(150) NOT NULL,
    [NationalId] NVARCHAR(20),
    [SubTotal] DECIMAL(18,2) NOT NULL,
    [TaxAmount] DECIMAL(18,2) NOT NULL,
    [TotalAmount] DECIMAL(18,2) NOT NULL,
    [VoucherHeaderId] INT,
    [Status] NVARCHAR(20) DEFAULT 'Draft',
    [Notes] NVARCHAR(1000),
    [CreatedAt] DATETIME2 DEFAULT GETDATE(),
    [CreatedBy] NVARCHAR(450),
    [ApprovedBy] NVARCHAR(450),
    [ApprovedAt] DATETIME2
);

CREATE TABLE [finance].[CreditNoteLines] (
    [Id] INT PRIMARY KEY IDENTITY(1,1),
    [CreditNoteId] INT NOT NULL,
    [Description] NVARCHAR(300) NOT NULL,
    [ProductId] INT,
    [Quantity] DECIMAL(18,4) NOT NULL,
    [UnitPrice] DECIMAL(18,2) NOT NULL,
    [LineAmount] DECIMAL(18,2) NOT NULL,
    [TaxAmount] DECIMAL(18,2) NOT NULL,
    [LineTotal] DECIMAL(18,2) NOT NULL,
    [DisplayOrder] INT NOT NULL,
    FOREIGN KEY ([CreditNoteId]) REFERENCES [finance].[CreditNotes]([Id])
);

-- Fixed Assets
CREATE TABLE [finance].[AssetCategories] (
    [Id] INT PRIMARY KEY IDENTITY(1,1),
    [Code] NVARCHAR(20) NOT NULL UNIQUE,
    [Name] NVARCHAR(100) NOT NULL,
    [Description] NVARCHAR(500),
    [DepreciationMethod] NVARCHAR(10) DEFAULT 'SL',
    [UsefulLifeMonths] INT NOT NULL,
    [SalvageValuePercent] DECIMAL(5,2) DEFAULT 0,
    [DepreciationRate] DECIMAL(10,6),
    [AssetAccountId] INT NOT NULL,
    [AccumulatedDepreciationAccountId] INT NOT NULL,
    [DepreciationExpenseAccountId] INT NOT NULL,
    [DisposalGainLossAccountId] INT,
    [IsActive] BIT DEFAULT 1,
    FOREIGN KEY ([AssetAccountId]) REFERENCES [finance].[SubsidiaryAccounts]([Id]),
    FOREIGN KEY ([AccumulatedDepreciationAccountId]) REFERENCES [finance].[SubsidiaryAccounts]([Id]),
    FOREIGN KEY ([DepreciationExpenseAccountId]) REFERENCES [finance].[SubsidiaryAccounts]([Id])
);

CREATE TABLE [finance].[FixedAssets] (
    [Id] INT PRIMARY KEY IDENTITY(1,1),
    [AssetCode] NVARCHAR(30) NOT NULL UNIQUE,
    [AssetName] NVARCHAR(150) NOT NULL,
    [Description] NVARCHAR(500),
    [SerialNumber] NVARCHAR(50),
    [Barcode] NVARCHAR(50),
    [CategoryId] INT NOT NULL,
    [LocationId] INT,
    [DepartmentId] INT,
    [CustodianEmployeeId] INT,
    [AcquisitionDate] DATETIME2 NOT NULL,
    [PlacedInServiceDate] DATETIME2 NOT NULL,
    [AcquisitionType] NVARCHAR(20) DEFAULT 'Purchase',
    [AcquisitionCost] DECIMAL(18,2) NOT NULL,
    [SalvageValue] DECIMAL(18,2) DEFAULT 0,
    [DepreciableBasis] DECIMAL(18,2) NOT NULL,
    [AccumulatedDepreciation] DECIMAL(18,2) DEFAULT 0,
    [NetBookValue] DECIMAL(18,2) NOT NULL,
    [ImpairmentAmount] DECIMAL(18,2),
    [DepreciationMethod] NVARCHAR(10) DEFAULT 'SL',
    [UsefulLifeMonths] INT NOT NULL,
    [LastDepreciationDate] DATETIME2,
    [NextDepreciationDate] DATETIME2,
    [DepreciationPeriodsRun] INT DEFAULT 0,
    [DisposalDate] DATETIME2,
    [DisposalType] NVARCHAR(20),
    [DisposalProceeds] DECIMAL(18,2),
    [DisposalGainLoss] DECIMAL(18,2),
    [Status] NVARCHAR(20) DEFAULT 'Active',
    [AssetAccountId] INT NOT NULL,
    [AccumulatedDepAccountId] INT NOT NULL,
    [DepreciationExpenseAccountId] INT NOT NULL,
    [CreatedAt] DATETIME2 DEFAULT GETDATE(),
    [CreatedBy] NVARCHAR(450),
    [UpdatedAt] DATETIME2,
    [UpdatedBy] NVARCHAR(450),
    FOREIGN KEY ([CategoryId]) REFERENCES [finance].[AssetCategories]([Id])
);

CREATE TABLE [finance].[DepreciationRuns] (
    [Id] INT PRIMARY KEY IDENTITY(1,1),
    [RunNumber] NVARCHAR(30) NOT NULL UNIQUE,
    [RunDate] DATETIME2 NOT NULL,
    [PeriodStartDate] DATETIME2 NOT NULL,
    [PeriodEndDate] DATETIME2 NOT NULL,
    [Status] NVARCHAR(20) DEFAULT 'Draft',
    [TotalDepreciation] DECIMAL(18,2) NOT NULL,
    [AssetCount] INT NOT NULL,
    [VoucherHeaderId] INT,
    [CreatedAt] DATETIME2 DEFAULT GETDATE(),
    [CreatedBy] NVARCHAR(450),
    [PostedAt] DATETIME2,
    [PostedBy] NVARCHAR(450),
    FOREIGN KEY ([VoucherHeaderId]) REFERENCES [finance].[VoucherHeaders]([Id])
);

CREATE TABLE [finance].[AssetDepreciationEntries] (
    [Id] INT PRIMARY KEY IDENTITY(1,1),
    [FixedAssetId] INT NOT NULL,
    [DepreciationRunId] INT NOT NULL,
    [PeriodStartDate] DATETIME2 NOT NULL,
    [PeriodEndDate] DATETIME2 NOT NULL,
    [BeginningBookValue] DECIMAL(18,2) NOT NULL,
    [DepreciationAmount] DECIMAL(18,2) NOT NULL,
    [AccumulatedDepreciation] DECIMAL(18,2) NOT NULL,
    [EndingBookValue] DECIMAL(18,2) NOT NULL,
    [VoucherLineId] INT,
    FOREIGN KEY ([FixedAssetId]) REFERENCES [finance].[FixedAssets]([Id]),
    FOREIGN KEY ([DepreciationRunId]) REFERENCES [finance].[DepreciationRuns]([Id])
);

-- Budgeting
CREATE TABLE [finance].[Budgets] (
    [Id] INT PRIMARY KEY IDENTITY(1,1),
    [BudgetCode] NVARCHAR(30) NOT NULL UNIQUE,
    [BudgetName] NVARCHAR(100) NOT NULL,
    [Description] NVARCHAR(500),
    [FiscalYearId] INT NOT NULL,
    [BudgetType] NVARCHAR(20) DEFAULT 'Operating',
    [Status] NVARCHAR(20) DEFAULT 'Draft',
    [SubmittedBy] NVARCHAR(450),
    [SubmittedAt] DATETIME2,
    [ApprovedBy] NVARCHAR(450),
    [ApprovedAt] DATETIME2,
    [TotalBudget] DECIMAL(18,2) DEFAULT 0,
    [TotalActual] DECIMAL(18,2) DEFAULT 0,
    [TotalVariance] DECIMAL(18,2) DEFAULT 0,
    [CreatedAt] DATETIME2 DEFAULT GETDATE(),
    [CreatedBy] NVARCHAR(450),
    [UpdatedAt] DATETIME2,
    [UpdatedBy] NVARCHAR(450),
    FOREIGN KEY ([FiscalYearId]) REFERENCES [finance].[FiscalYears]([Id])
);

CREATE TABLE [finance].[BudgetLines] (
    [Id] INT PRIMARY KEY IDENTITY(1,1),
    [BudgetId] INT NOT NULL,
    [AccountId] INT NOT NULL,
    [CostCenterId] INT,
    [DepartmentId] INT,
    [ProjectId] INT,
    [Period01] DECIMAL(18,2) DEFAULT 0,
    [Period02] DECIMAL(18,2) DEFAULT 0,
    [Period03] DECIMAL(18,2) DEFAULT 0,
    [Period04] DECIMAL(18,2) DEFAULT 0,
    [Period05] DECIMAL(18,2) DEFAULT 0,
    [Period06] DECIMAL(18,2) DEFAULT 0,
    [Period07] DECIMAL(18,2) DEFAULT 0,
    [Period08] DECIMAL(18,2) DEFAULT 0,
    [Period09] DECIMAL(18,2) DEFAULT 0,
    [Period10] DECIMAL(18,2) DEFAULT 0,
    [Period11] DECIMAL(18,2) DEFAULT 0,
    [Period12] DECIMAL(18,2) DEFAULT 0,
    [AnnualTotal] DECIMAL(18,2) DEFAULT 0,
    [Notes] NVARCHAR(500),
    FOREIGN KEY ([BudgetId]) REFERENCES [finance].[Budgets]([Id]),
    FOREIGN KEY ([AccountId]) REFERENCES [finance].[SubsidiaryAccounts]([Id])
);

-- Cost Centers & Projects
CREATE TABLE [finance].[CostCenters] (
    [Id] INT PRIMARY KEY IDENTITY(1,1),
    [Code] NVARCHAR(20) NOT NULL UNIQUE,
    [Name] NVARCHAR(100) NOT NULL,
    [Description] NVARCHAR(500),
    [ParentCostCenterId] INT,
    [ManagerEmployeeId] INT,
    [DepartmentId] INT,
    [CostType] NVARCHAR(20) DEFAULT 'Operating',
    [AllocationMethod] NVARCHAR(20) DEFAULT 'Direct',
    [IsActive] BIT DEFAULT 1,
    [IsPostingAllowed] BIT DEFAULT 1,
    [BudgetId] INT,
    [CreatedAt] DATETIME2 DEFAULT GETDATE(),
    [CreatedBy] NVARCHAR(450),
    FOREIGN KEY ([ParentCostCenterId]) REFERENCES [finance].[CostCenters]([Id])
);

CREATE TABLE [finance].[Projects] (
    [Id] INT PRIMARY KEY IDENTITY(1,1),
    [ProjectCode] NVARCHAR(30) NOT NULL UNIQUE,
    [ProjectName] NVARCHAR(150) NOT NULL,
    [Description] NVARCHAR(500),
    [CustomerId] INT,
    [DepartmentId] INT,
    [ProjectManagerId] INT,
    [StartDate] DATETIME2 NOT NULL,
    [EndDate] DATETIME2,
    [ActualEndDate] DATETIME2,
    [BudgetAmount] DECIMAL(18,2) DEFAULT 0,
    [ActualCost] DECIMAL(18,2) DEFAULT 0,
    [Revenue] DECIMAL(18,2) DEFAULT 0,
    [Status] NVARCHAR(20) DEFAULT 'Planning',
    [CompletionPercent] DECIMAL(5,2) DEFAULT 0,
    [IsActive] BIT DEFAULT 1,
    [CreatedAt] DATETIME2 DEFAULT GETDATE(),
    [CreatedBy] NVARCHAR(450)
);

-- Extend VoucherLine with CostCenterId FK
ALTER TABLE [finance].[VoucherLines] 
ADD CONSTRAINT FK_VoucherLines_CostCenter 
FOREIGN KEY ([CostCenterId]) REFERENCES [finance].[CostCenters]([Id]);
```

---

## 5. Recommended Financial Workflow & UI Layout Architecture

### 5.1 UI Layout Architecture (RTL, Rahkaran-Style)

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                              TOP HEADER (48px)                                   │
│  [Logo] Finance Module                          [User] [Notifications] [Logout] │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  ┌──────────────────┐  ┌─────────────────────────────────────────────────────┐ │
│  │   RIGHT MENU     │  │              MAIN WORKSPACE                          │ │
│  │   (208-260px)    │  │                                                      │ │
│  │                  │  │  ┌─────────────────────────────────────────────────┐ │ │
│  │  📊 Dashboard    │  │  │  TAB BAR (Open Pages)                           │ │ │
│  │  ─────────────   │  │  │  [Dashboard] [Invoices] [Ledger] [Reports]     │ │ │
│  │  💰 Accounting   │  │  └─────────────────────────────────────────────────┘ │ │
│  │  • Chart of Accts│  │                                                      │ │
│  │  • Journal Entry │  │  ┌─────────────────────────────────────────────────┐ │ │
│  │  • Vouchers      │  │  │  CONTENT AREA                                    │ │ │
│  │  • Trial Balance │  │  │                                                   │ │ │
│  │  ─────────────   │  │  │  ┌─────────────────────────────────────────────┐│ │ │
│  │  💳 Cash & Bank  │  │  │  │  KPI CARDS ROW                               ││ │ │
│  │  • Bank Accounts │  │  │  │  [Total Assets] [Total Liabilities]          ││ │ │
│  │  • Receipts      │  │  │  │  [Net Equity] [Net Profit]                   ││ │ │
│  │  • Payments      │  │  │  └─────────────────────────────────────────────┘│ │ │
│  │  • Reconcile     │  │  │                                                   │ │ │
│  │  ─────────────   │  │  │  ┌─────────────────────────────────────────────┐│ │ │
│  │  📋 AR/AP        │  │  │  │  MAIN DATA GRID / FORM                       ││ │ │
│  │  • Customers     │  │  │  │                                               ││ │ │
│  │  • Invoices      │  │  │  │  (Context-specific content)                   ││ │ │
│  │  • Receipts      │  │  │  │                                               ││ │ │
│  │  • Aging         │  │  │  └─────────────────────────────────────────────┘│ │ │
│  │  • Vendors       │  │  │                                                   │ │ │
│  │  • Bills         │  │  │  ┌─────────────────────────────────────────────┐│ │ │
│  │  • Payments      │  │  │  │  ACTION BAR                                   ││ │ │
│  │  • Aging         │  │  │  │  [Save] [Print] [Export] [Refresh]           ││ │ │
│  │  ─────────────   │  │  │  └─────────────────────────────────────────────┘│ │ │
│  │  🏭 Fixed Assets │  │  │                                                   │ │ │
│  │  • Asset Register│  │  └─────────────────────────────────────────────────┘ │ │
│  │  • Depreciation  │  │                                                      │ │
│  │  • Disposal      │  │                                                      │ │
│  │  ─────────────   │  │                                                      │ │
│  │  📈 Budgets      │  │                                                      │ │
│  │  • Budget Setup  │  │                                                      │ │
│  │  • Variance      │  │                                                      │ │
│  │  ─────────────   │  │                                                      │ │
│  │  📑 Reports      │  │                                                      │ │
│  │  • Balance Sheet │  │                                                      │ │
│  │  • Income Stmt   │  │                                                      │ │
│  │  • Cash Flow     │  │                                                      │ │
│  │  • GL Detail     │  │                                                      │ │
│  │  • Trial Balance │  │                                                      │ │
│  │  • Aging Reports │  │                                                      │ │
│  └──────────────────┘  └─────────────────────────────────────────────────────┘ │
│                                                                                 │
│  ┌──────────────────────────────────────────────────────────────────────────┐  │
│  │  LEFT TASKBAR (140-160px) - Open Window Thumbnails                       │  │
│  │  [Dashboard ×] [Invoice #123 ×] [Trial Balance ×]                       │  │
│  └──────────────────────────────────────────────────────────────────────────┘  │
│                                                                                 │
└─────────────────────────────────────────────────────────────────────────────────┘
```

### 5.2 Key UI Components

#### 5.2.1 Financial Dashboard (Enhanced)

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│  داشورد مالی - سال 1405                                          [سال: 1405 ▼] │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐ ┌─────────────┐              │
│  │ جمع دارایی‌ها│ │ جمع بدهی‌ها  │ │ حقوق صاحبان │ │  سود خالص   │              │
│  │  12,500,000 │ │  7,200,000  │ │  5,300,000  │ │   850,000   │              │
│  │    هزار ریال │ │    هزار ریال│ │    هزار ریال│ │    هزار ریال│              │
│  └─────────────┘ └─────────────┘ └─────────────┘ └─────────────┘              │
│                                                                                 │
│  ┌─────────────────────────────────────────────────────────────────────────────┐│
│  │  نمودار درآمد و هزینه (12 ماه)                                              ││
│  │  ┌─────────────────────────────────────────────────────────────────────────┐││
│  │  │     ╱╲        ╱╲                                                        │││
│  │  │    ╱  ╲  ╱╲  ╱  ╲    ╱╲                                                │││
│  │  │   ╱    ╲╱  ╲╱    ╲  ╱  ╲    ╱╲                                         │││
│  │  │  ╱              ╲╱    ╲  ╲  ╱  ╲                                        │││
│  │  │ ╱                        ╲╱    ╲                                       │││
│  │  │╱                                                                      │││
│  │  │ فروردین اردیبهشت خرداد تیر مرداد شهریور مهر آبان آذر دی بهمن اسفند     │││
│  │  │ ─── درآمد  ─── هزینه                                                   │││
│  │  └─────────────────────────────────────────────────────────────────────────┘││
│  └─────────────────────────────────────────────────────────────────────────────┘│
│                                                                                 │
│  ┌──────────────────────────────┐ ┌──────────────────────────────────────────┐ │
│  │  اقدامات سریع                │ │  وضعیت سررسید                            │ │
│  │  ┌────────────────────────┐  │ │  ┌────────────────────────────────────┐  │ │
│  │  │ 📝 ثبت سند دستی        │  │ │  │ ⚠️ 5 فاکتور فروش معوق              │  │ │
│  │  │ 💰 دریافت وجه          │  │ │  │ ⚠️ 3 فاکتور خرید سررسید نزدیک      │  │ │
│  │  │ 💳 پرداخت به تامین‌کننده│  │ │  │ ✓ همه پرداخت‌ها به‌موقع             │  │ │
│  │  │ 📊 تراز آزمایشی         │  │ │  └────────────────────────────────────┘  │ │
│  │  │ 🏭 محاسبه استهلاک      │  │ │                                          │ │
│  │  └────────────────────────┘  │ │                                          │ │
│  └──────────────────────────────┘ └──────────────────────────────────────────┘ │
│                                                                                 │
└─────────────────────────────────────────────────────────────────────────────────┘
```

#### 5.2.2 Journal Entry Form

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│  ثبت سند حسابداری                                                [سند جدید]    │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  شماره سند: [MANUAL-14050515-abc123]    تاریخ: [1405/05/15]    نوع: [عمومی ▼]  │
│  شرح: [________________________________________________________]                │
│                                                                                 │
│  ┌─────────────────────────────────────────────────────────────────────────────┐│
│  │ ردیف │ حساب معین        │ تفصیلی       │ بدهی      │ اعتبار    │ شرح      ││
│  ├─────────────────────────────────────────────────────────────────────────────┤│
│  │  1   │ [110101 ▼]      │ [مشتری الف ▼]│ 1,000,000 │           │ فروش     ││
│  │  2   │ [410101 ▼]      │              │           │ 1,000,000 │ درآمد    ││
│  │  3   │                 │              │           │           │          ││
│  │  4   │                 │              │           │           │          ││
│  │      │                 │              │           │           │ [+ ردیف] ││
│  ├─────────────────────────────────────────────────────────────────────────────┤│
│  │ جمع:                                  │ 1,000,000 │ 1,000,000 │ ✓ تراز   ││
│  └─────────────────────────────────────────────────────────────────────────────┘│
│                                                                                 │
│  [ذخیره]  [ذخیره و ارسال]  [انصراف]  [الگو]  [تکرار]                           │
│                                                                                 │
└─────────────────────────────────────────────────────────────────────────────────┘
```

#### 5.2.3 Bank Reconciliation Screen

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│  مغایرت‌گیری بانکی - بانک ملت - حساب 1234-5678                                  │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  تاریخ صورت‌حساب: [1405/05/31]    مانده بانک: [5,230,000]    مانده دفاتر: [5,180,000] │
│  اختلاف: [50,000]                                                               │
│                                                                                 │
│  ┌──────────────────────────────────────┐ ┌────────────────────────────────────┐│
│  │  اقلام باز بانک                      │ │  اقلام باز دفاتر                   ││
│  │  ┌────────────────────────────────┐  │ │  ┌──────────────────────────────┐  ││
│  │  │ ☐ چک شماره 1234 - 500,000     │  │ │  │ ☐ واریز نامشخص - 30,000     │  ││
│  │  │ ☐ چک شماره 1235 - 200,000     │  │ │  │ ☐ کارمزد بانک - 20,000      │  ││
│  │  └────────────────────────────────┘  │ │  └──────────────────────────────┘  ││
│  └──────────────────────────────────────┘ └────────────────────────────────────┘│
│                                                                                 │
│  ┌─────────────────────────────────────────────────────────────────────────────┐│
│  │  صورت‌حساب بانک                                                            ││
│  │  ┌─────────────────────────────────────────────────────────────────────────┐││
│  │  │ تاریخ      │ شرح                    │ بدهی      │ اعتبار    │ مانده    │││
│  │  ├─────────────────────────────────────────────────────────────────────────┤││
│  │  │ 1405/05/01 │ مانده اول دوره         │           │           │ 5,000,000│││
│  │  │ 1405/05/05 │ واریز مشتری الف        │           │ 1,000,000 │ 6,000,000│││
│  │  │ 1405/05/10 │ چک شماره 1234          │ 500,000   │           │ 5,500,000│││
│  │  │ 1405/05/15 │ برداشت کارمزد          │ 20,000    │           │ 5,480,000│││
│  │  │ 1405/05/20 │ واریز مشتری ب          │           │ 500,000   │ 5,980,000│││
│  │  │ 1405/05/25 │ چک شماره 1235          │ 200,000   │           │ 5,780,000│││
│  │  │ 1405/05/31 │ سود سپرده              │           │ 50,000    │ 5,830,000│││
│  │  └─────────────────────────────────────────────────────────────────────────┘││
│  └─────────────────────────────────────────────────────────────────────────────┘│
│                                                                                 │
│  [تطبیق خودکار]  [ذخیره]  [تایید و ثبت سند]  [انصراف]                          │
│                                                                                 │
└─────────────────────────────────────────────────────────────────────────────────┘
```

### 5.3 Financial Workflows

#### 5.3.1 Invoice-to-Cash Workflow

```
┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐
│  Create  │───▶│  Approve │───▶│  Post    │───▶│  Receipt │───▶│  Reconcile│
│  Invoice │    │  Invoice │    │  to GL   │    │  Payment │    │  Bank    │
└──────────┘    └──────────┘    └──────────┘    └──────────┘    └──────────┘
     │               │               │               │               │
     ▼               ▼               ▼               ▼               ▼
  Draft →      Workflow →      Voucher →      Payment →      Bank
  Pending      Approved        Posted         Recorded       Reconciled
```

#### 5.3.2 Procure-to-Pay Workflow

```
┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐
│  Create  │───▶│  Approve │───▶│  Post    │───▶│  Process │───▶│  Reconcile│
│  Bill    │    │  Bill    │    │  to GL   │    │  Payment │    │  Bank    │
└──────────┘    └──────────┘    └──────────┘    └──────────┘    └──────────┘
     │               │               │               │               │
     ▼               ▼               ▼               ▼               ▼
  Draft →      Workflow →      Voucher →      Check/Transfer Bank
  Pending      Approved        Posted         Issued          Reconciled
```

#### 5.3.3 Record-to-Report Workflow

```
┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐
│  Record  │───▶│  Review  │───▶│  Close   │───▶│  Generate│───▶│  Publish │
│  Journal │    │  & Post  │    │  Period  │    │  Reports │    │  Reports │
└──────────┘    └──────────┘    └──────────┘    └──────────┘    └──────────┘
     │               │               │               │               │
     ▼               ▼               ▼               ▼               ▼
  Draft →      Posted →        Period →       Financial →     Management
  Entry        Verified        Locked         Statements      Package
```

---

## 6. Phased Implementation Roadmap

### 6.1 Phase Overview

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                         IMPLEMENTATION TIMELINE                                  │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  Phase 1 (Weeks 1-4): Core Financial Statements                                │
│  ████████████████████░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░ │
│  • Balance Sheet                                                                │
│  • Income Statement (P&L)                                                       │
│  • Cash Flow Statement                                                          │
│  • General Ledger Detail Report                                                 │
│                                                                                 │
│  Phase 2 (Weeks 5-8): AR/AP Enhancement                                        │
│  ░░░░░░░░░░░░░░░░████████████████████░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░ │
│  • Payment Receipts                                                             │
│  • Payment Processing                                                           │
│  • Credit/Debit Notes                                                           │
│  • Aging Reports                                                                │
│  • Payment Terms                                                                │
│                                                                                 │
│  Phase 3 (Weeks 9-12): Cash & Bank Management                                  │
│  ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░████████████████████░░░░░░░░░░░░░░░░░░░░░░░░ │
│  • Bank Account Management                                                      │
│  • Bank Statement Import                                                        │
│  • Bank Reconciliation                                                          │
│  • Check Management                                                             │
│  • Petty Cash                                                                   │
│                                                                                 │
│  Phase 4 (Weekes 13-16): Fixed Assets                                          │
│  ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░████████████████████░░░░░░ │
│  • Asset Categories                                                             │
│  • Asset Register                                                               │
│  • Depreciation Calculation                                                     │
│  • Asset Disposal                                                               │
│  • Asset Reports                                                                │
│                                                                                 │
│  Phase 5 (Weeks 17-20): Budgeting & Cost Centers                               │
│  ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░████████ │
│  • Cost Center Setup                                                            │
│  • Budget Creation                                                              │
│  • Budget Approval Workflow                                                     │
│  • Variance Analysis                                                            │
│  • Project Accounting                                                           │
│                                                                                 │
└─────────────────────────────────────────────────────────────────────────────────┘
```

### 6.2 Phase 1: Core Financial Statements (Weeks 1-4)

#### Objectives
- Implement Balance Sheet generation
- Implement Income Statement (P&L) generation
- Implement Cash Flow Statement generation
- Add General Ledger Detail report

#### Deliverables
| Week | Deliverable | Description |
|------|-------------|-------------|
| 1 | Financial Statement Service | Core service for statement generation |
| 1 | Account Balance Calculator | Calculate balances by account group |
| 2 | Balance Sheet Report | Generate balance sheet with proper formatting |
| 2 | Balance Sheet UI | Razor view with RTL layout |
| 3 | Income Statement Report | Generate P&L with proper categorization |
| 3 | Income Statement UI | Razor view with charts |
| 4 | Cash Flow Statement | Generate using indirect method |
| 4 | GL Detail Report | Account transaction detail |

#### Technical Tasks
```csharp
// New services to implement
public interface IFinancialStatementService
{
    Task<BalanceSheetDto> GenerateBalanceSheetAsync(int fiscalPeriodId, CancellationToken ct);
    Task<IncomeStatementDto> GenerateIncomeStatementAsync(int fiscalPeriodId, CancellationToken ct);
    Task<CashFlowStatementDto> GenerateCashFlowStatementAsync(int fiscalPeriodId, CancellationToken ct);
    Task<GeneralLedgerDetailDto> GenerateGlDetailAsync(int accountId, DateTime from, DateTime to, CancellationToken ct);
}

// New controller endpoints
[HttpGet("Reports/BalanceSheet")]
public async Task<IActionResult> BalanceSheet(Guid? fiscalPeriodId, CancellationToken ct)

[HttpGet("Reports/IncomeStatement")]  
public async Task<IActionResult> IncomeStatement(Guid? fiscalPeriodId, CancellationToken ct)

[HttpGet("Reports/CashFlow")]
public async Task<IActionResult> CashFlow(Guid? fiscalPeriodId, CancellationToken ct)

[HttpGet("Reports/GlDetail")]
public async Task<IActionResult> GlDetail(int? accountId, DateTime? from, DateTime? to, CancellationToken ct)
```

### 6.3 Phase 2: AR/AP Enhancement (Weeks 5-8)

#### Objectives
- Implement payment receipt processing
- Implement payment processing for vendors
- Add credit note and debit note functionality
- Create aging reports for AR and AP
- Add payment terms management

#### Deliverables
| Week | Deliverable | Description |
|------|-------------|-------------|
| 5 | Payment Transaction Entity | Database schema and entity model |
| 5 | Payment Service | Core payment processing logic |
| 6 | Payment Receipt UI | Customer payment entry form |
| 6 | Payment Processing UI | Vendor payment entry form |
| 7 | Credit/Debit Note Entities | Database schema |
| 7 | Credit/Debit Note UI | Note creation and management |
| 8 | Aging Report Service | Calculate aging buckets |
| 8 | Aging Report UI | AR/AP aging display |

#### Technical Tasks
```csharp
// Payment processing
public interface IPaymentService
{
    Task<PaymentTransaction> CreateReceiptAsync(PaymentReceiptCreateVM model, CancellationToken ct);
    Task<PaymentTransaction> CreatePaymentAsync(PaymentCreateVM model, CancellationToken ct);
    Task AllocatePaymentAsync(int paymentId, List<AllocationItem> allocations, CancellationToken ct);
}

// Aging calculation
public interface IAgingService
{
    Task<ArAgingReportDto> GenerateArAgingAsync(DateTime asOfDate, CancellationToken ct);
    Task<ApAgingReportDto> GenerateApAgingAsync(DateTime asOfDate, CancellationToken ct);
}

// Credit/Debit notes
public interface ICreditNoteService
{
    Task<CreditNote> CreateCreditNoteAsync(CreditNoteCreateVM model, CancellationToken ct);
    Task ApplyCreditNoteAsync(int creditNoteId, int invoiceId, decimal amount, CancellationToken ct);
}
```

### 6.4 Phase 3: Cash & Bank Management (Weeks 9-12)

#### Objectives
- Implement bank account management
- Add bank statement import (CSV, Excel)
- Create bank reconciliation functionality
- Implement check management
- Add petty cash tracking

#### Deliverables
| Week | Deliverable | Description |
|------|-------------|-------------|
| 9 | Bank Account Entities | Database schema and CRUD |
| 9 | Bank Account UI | Account management interface |
| 10 | Statement Import Service | CSV/Excel import logic |
| 10 | Statement Import UI | File upload and mapping |
| 11 | Reconciliation Service | Matching algorithm |
| 11 | Reconciliation UI | Side-by-side matching interface |
| 12 | Check Management | Check register and printing |
| 12 | Petty Cash | Petty cash fund management |

### 6.5 Phase 4: Fixed Assets (Weeks 13-16)

#### Objectives
- Implement asset category management
- Create fixed asset register
- Add depreciation calculation (SL, DB methods)
- Implement asset disposal
- Generate asset reports

#### Deliverables
| Week | Deliverable | Description |
|------|-------------|-------------|
| 13 | Asset Category Entities | Database schema and CRUD |
| 13 | Asset Category UI | Category management |
| 14 | Fixed Asset Entities | Database schema and CRUD |
| 14 | Fixed Asset UI | Asset register interface |
| 15 | Depreciation Service | Calculation engine |
| 15 | Depreciation Run UI | Batch depreciation processing |
| 16 | Asset Disposal | Disposal workflow |
| 16 | Asset Reports | Asset register, depreciation reports |

### 6.6 Phase 5: Budgeting & Cost Centers (Weeks 17-20)

#### Objectives
- Implement cost center hierarchy
- Create budget creation and management
- Add budget approval workflow
- Implement variance analysis
- Add project accounting basics

#### Deliverables
| Week | Deliverable | Description |
|------|-------------|-------------|
| 17 | Cost Center Entities | Database schema and CRUD |
| 17 | Cost Center UI | Hierarchy management |
| 18 | Budget Entities | Database schema and CRUD |
| 18 | Budget UI | Budget entry interface |
| 19 | Budget Approval | Workflow integration |
| 19 | Variance Service | Budget vs actual calculation |
| 20 | Variance Report UI | Variance analysis display |
| 20 | Project Accounting | Basic project tracking |

### 6.7 Success Criteria

| Phase | Success Metric |
|-------|----------------|
| Phase 1 | Balance Sheet balances (Assets = Liabilities + Equity) |
| Phase 1 | P&L matches Trial Balance net movement |
| Phase 2 | Payment allocation correctly reduces invoice balance |
| Phase 2 | Aging report matches outstanding invoice list |
| Phase 3 | Bank reconciliation difference = 0 after matching |
| Phase 3 | Check register balances with bank statement |
| Phase 4 | Depreciation expense matches manual calculation |
| Phase 4 | Asset register total matches GL control account |
| Phase 5 | Budget variance = Actual - Budget |
| Phase 5 | Cost center reports sum to company total |

---

## Appendix A: Account Structure Reference

### Standard Chart of Accounts Template

```
1 - ASSETS (Debit Nature)
├── 11 - Current Assets
│   ├── 1101 - Cash & Cash Equivalents
│   │   ├── 110101 - Cash on Hand
│   │   ├── 110102 - Bank Accounts
│   │   └── 110103 - Petty Cash
│   ├── 1102 - Accounts Receivable
│   │   ├── 110201 - Trade Receivables
│   │   └── 110202 - Other Receivables
│   ├── 1103 - Inventory
│   │   ├── 110301 - Raw Materials
│   │   ├── 110302 - Work in Progress
│   │   └── 110303 - Finished Goods
│   └── 1104 - Prepaid Expenses
│       └── 110401 - Prepaid Insurance
├── 12 - Non-Current Assets
│   ├── 1201 - Fixed Assets
│   │   ├── 120101 - Land
│   │   ├── 120102 - Buildings
│   │   ├── 120103 - Machinery & Equipment
│   │   ├── 120104 - Vehicles
│   │   └── 120105 - Office Equipment
│   ├── 1202 - Accumulated Depreciation (Credit contra)
│   └── 1203 - Intangible Assets

2 - LIABILITIES (Credit Nature)
├── 21 - Current Liabilities
│   ├── 2101 - Accounts Payable
│   │   ├── 210101 - Trade Payables
│   │   └── 210102 - Other Payables
│   ├── 2102 - Accrued Expenses
│   │   ├── 210201 - Accrued Salaries
│   │   └── 210202 - Accrued Utilities
│   ├── 2103 - Taxes Payable
│   │   ├── 210301 - VAT Payable
│   │   └── 210302 - Income Tax Payable
│   └── 2104 - Short-term Loans
└── 22 - Non-Current Liabilities
    ├── 2201 - Long-term Loans
    └── 2202 - Long-term Provisions

3 - EQUITY (Credit Nature)
├── 31 - Share Capital
│   └── 310101 - Ordinary Shares
├── 32 - Retained Earnings
│   └── 320101 - Retained Earnings
└── 33 - Current Year Earnings
    └── 330101 - Net Profit/Loss

4 - REVENUE (Credit Nature)
├── 41 - Operating Revenue
│   ├── 410101 - Sales Revenue
│   ├── 410102 - Service Revenue
│   └── 410103 - Other Operating Revenue
└── 42 - Non-Operating Revenue
    ├── 420101 - Interest Income
    ├── 420102 - Rental Income
    └── 420103 - Gain on Asset Sale

5 - EXPENSES (Debit Nature)
├── 51 - Cost of Goods Sold
│   ├── 510101 - Opening Inventory
│   ├── 510102 - Purchases
│   ├── 510103 - Direct Labor
│   ├── 510104 - Manufacturing Overhead
│   └── 510105 - Closing Inventory (Credit)
├── 52 - Operating Expenses
│   ├── 520101 - Salaries & Wages
│   ├── 520102 - Rent Expense
│   ├── 520103 - Utilities Expense
│   ├── 520104 - Depreciation Expense
│   ├── 520105 - Insurance Expense
│   ├── 520106 - Office Supplies
│   └── 520107 - Other Operating Expenses
└── 53 - Non-Operating Expenses
    ├── 530101 - Interest Expense
    ├── 530102 - Loss on Asset Sale
    └── 530103 - FX Loss
```

---

## Appendix B: Migration Scripts

### Initial Data Migration

```sql
-- Insert default asset categories
INSERT INTO [finance].[AssetCategories] ([Code], [Name], [DepreciationMethod], [UsefulLifeMonths], [SalvageValuePercent], [AssetAccountId], [AccumulatedDepreciationAccountId], [DepreciationExpenseAccountId])
VALUES 
('LAND', 'Land', 'None', 0, 0, 0, 0, 0), -- Non-depreciable
('BUILD', 'Buildings', 'SL', 240, 5, 0, 0, 0), -- 20 years
('MACH', 'Machinery & Equipment', 'SL', 120, 5, 0, 0, 0), -- 10 years
('VEH', 'Vehicles', 'SL', 60, 5, 0, 0, 0), -- 5 years
('OFFEQ', 'Office Equipment', 'SL', 36, 5, 0, 0, 0), -- 3 years
('COMPEQ', 'Computer Equipment', 'DB', 24, 0, 0, 0, 0); -- 2 years, declining balance

-- Insert default cost centers
INSERT INTO [finance].[CostCenters] ([Code], [Name], [CostType], [IsActive])
VALUES
('ADMIN', 'Administration', 'Operating', 1),
('SALES', 'Sales Department', 'Operating', 1),
('PROD', 'Production', 'Operating', 1),
('IT', 'Information Technology', 'Operating', 1),
('HR', 'Human Resources', 'Operating', 1);

-- Insert default bank accounts (placeholder)
INSERT INTO [finance].[BankAccounts] ([AccountNumber], [BankName], [BranchName], [AccountType], [CurrencyCode], [IsActive])
VALUES
('0000-0000-0000-0000', 'Bank Melli', 'Main Branch', 'Checking', 'IRR', 1);
```

---

## Appendix C: API Endpoints Summary

### New API Endpoints Required

```
# Financial Statements
GET  /Financial/Reports/BalanceSheet?fiscalPeriodId={id}
GET  /Financial/Reports/IncomeStatement?fiscalPeriodId={id}
GET  /Financial/Reports/CashFlow?fiscalPeriodId={id}
GET  /Financial/Reports/GlDetail?accountId={id}&from={date}&to={date}

# Bank Management
GET  /Financial/BankAccounts
POST /Financial/BankAccounts/Create
GET  /Financial/BankAccounts/{id}
POST /Financial/BankAccounts/{id}/Edit
POST /Financial/BankAccounts/{id}/Delete

# Bank Statements
POST /Financial/BankStatements/Import
GET  /Financial/BankStatements/{bankAccountId}

# Bank Reconciliation
GET  /Financial/Reconciliation/{bankAccountId}
POST /Financial/Reconciliation/Match
POST /Financial/Reconciliation/Complete

# Payments
GET  /Financial/Payments/Receipts
POST /Financial/Payments/CreateReceipt
GET  /Financial/Payments/Payments
POST /Financial/Payments/CreatePayment
POST /Financial/Payments/{id}/Allocate

# Credit/Debit Notes
GET  /Financial/CreditNotes
POST /Financial/CreditNotes/Create
GET  /Financial/CreditNotes/{id}
POST /Financial/CreditNotes/{id}/Apply
GET  /Financial/DebitNotes
POST /Financial/DebitNotes/Create

# Fixed Assets
GET  /Financial/Assets/Categories
POST /Financial/Assets/Categories/Create
GET  /Financial/Assets
POST /Financial/Assets/Create
GET  /Financial/Assets/{id}
POST /Financial/Assets/{id}/Edit
POST /Financial/Assets/{id}/Dispose
POST /Financial/Assets/Depreciation/Run
GET  /Financial/Assets/Depreciation/Runs

# Budgeting
GET  /Financial/Budgets
POST /Financial/Budgets/Create
GET  /Financial/Budgets/{id}
POST /Financial/Budgets/{id}/Submit
POST /Financial/Budgets/{id}/Approve
GET  /Financial/Budgets/{id}/Variance

# Cost Centers
GET  /Financial/CostCenters
POST /Financial/CostCenters/Create
GET  /Financial/CostCenters/{id}
POST /Financial/CostCenters/{id}/Edit

# Aging Reports
GET  /Financial/Reports/ArAging?asOfDate={date}
GET  /Financial/Reports/ApAging?asOfDate={date}
```

---

## Conclusion

The current financial module provides a solid foundation with:
- ✓ 4-level Chart of Accounts
- ✓ Double-entry journal system
- ✓ Invoice management (Sales/Purchase)
- ✓ Multi-currency support
- ✓ Period management with locks
- ✓ Basic tax handling

However, to achieve enterprise-grade status comparable to NetSuite, SAP FICO, or Dynamics 365 Finance, the following critical gaps must be addressed:

1. **Financial Statements** - Balance Sheet, P&L, Cash Flow
2. **AR/AP Processing** - Payments, Credit Notes, Aging
3. **Bank Management** - Accounts, Reconciliation, Checks
4. **Fixed Assets** - Register, Depreciation, Disposal
5. **Budgeting** - Creation, Approval, Variance Analysis

The proposed 5-phase implementation roadmap provides a structured approach to achieving these goals over 20 weeks, with each phase building upon the previous one.

---

*Document Version: 1.0*  
*Last Updated: August 14, 2026*  
*Next Review: After Phase 1 Completion*
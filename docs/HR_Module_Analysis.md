# HR Module — Enterprise Gap Analysis & Feature Roadmap

**Repository:** `rhyno-automation` (OfficeAutomation)
**Prepared by:** Principal HR Systems Architect / Enterprise ERP Analyst
**Date:** 2026-08-14
**Scope:** End-to-end evaluation of the Human Resources (HR) module against global enterprise HR standards (Workday, SAP SuccessFactors, BambooHR, Rahkaran/همکاران سیستم)

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Current State Inventory](#2-current-state-inventory)
3. [Current State vs Global Standard Gap Matrix](#3-current-state-vs-global-standard-gap-matrix)
4. [Complete Missing Feature Checklist (Prioritized)](#4-complete-missing-feature-checklist-prioritized)
5. [UI/UX & Workflow Architecture Recommendations](#5-uiux--workflow-architecture-recommendations)
6. [Database Schema & Data Model Enhancements](#6-database-schema--data-model-enhancements)
7. [Step-by-Step Actionable Roadmap](#7-step-by-step-actionable-roadmap)
8. [Assumptions & Notes](#8-assumptions--notes)

---

## 1. Executive Summary

The HR module in this repository is a **functional but foundational** employee-record system. It provides solid **core employee master data** (personnel file, salary history, status history, contracts, leave, loans, attendance, payroll, and insurance lists) with strong cross-cutting enterprise concerns already in place: **multi-tenancy (schema-per-tenant), row-level security, field masking, permission-based access (ABAC), audit logging, workflow engine, and Jalali/Persian calendar support**.

However, measured against global enterprise HR suites (Workday, SuccessFactors, BambooHR) and the local benchmark (Rahkaran ERP), the module is **~35–40% complete**. It is fundamentally a **"HR record-keeping"** system, not a **"full employee-lifecycle"** platform. The following pillars are **entirely absent**:

- **Talent Acquisition / Recruitment (ATS)** — no job requisitions, no applicant tracking, no interview pipeline.
- **Performance Management / OKRs / 360° reviews** — no goals, KPIs, appraisal cycles, or feedback.
- **Training & Development (LMS)** — no course catalog, enrollment, or certification tracking.
- **Employee Self-Service (ESS) / Manager Self-Service (MSS)** — the only self-service is a basic leave request (`LeavesController`); no payslip download, no personal-data update, no manager approval dashboards.
- **Shift / Roster / Biometric time capture** — attendance is a **manual monthly summary** (`HrAttendance` with PresentDays/AbsentDays/OvertimeHours), not daily clock-in/out or device integration.
- **Advanced Payroll** — payroll exists but is **simplified**: insurance is hard-coded at 7%, tax is hard-coded to 0, no allowance/deduction catalogs, no multi-currency, and **payslip generation is a placeholder** (JSON stubs, no real PDF/Excel output).
- **Organizational Chart / Succession Planning** — no reporting-line hierarchy beyond a flat `Department` + `ManagerId` on `User`.
- **Analytics & Compliance** — a single static `Reports` page with 7 counters; no turnover, headcount trend, or regulatory reporting.

**Bottom line:** To make the HR module fully self-sufficient (so an HR Manager/Specialist never needs third-party tools), the highest-value work is (1) **ESS/MSS portals**, (2) **Recruitment/ATS**, (3) **Performance/OKR**, (4) **real payroll & payslip engine**, and (5) **HR analytics**. These are detailed in the roadmap below.

---

## 2. Current State Inventory

### 2.1 Backend Architecture (C# / ASP.NET Core / EF Core)

| Layer | Location | Notes |
|-------|----------|-------|
| **Office DbContext** | `Modules/Office/Infrastructure/Persistence/OfficeDbContext.cs` | Core HR entities: `HumanCapitalEmployee`, `HumanCapitalSalaryHistory`, `HumanCapitalStatusHistory`, `HrContract`, `HrLeave`, `HrLoan`, `HrAttendance`, `Department`, `OrganizationCalendarEvent` |
| **Finance DbContext** | `Modules/Finance/Infrastructure/Persistence/FinanceDbContext.cs` | Payroll & insurance: `PayrollList`, `PayrollItem`, `InsuranceList`, `InsuranceEmployee` |
| **Workflow DbContext** | `Modules/Workflow/Infrastructure/Persistence/WorkflowDbContext.cs` | `Leave` (workflow-driven), `WorkflowInstance`, `WorkflowDecision` |
| **Identity** | `Models/User.cs` | `User : IdentityUser` with `EmployeeId`, `ManagerId`, `IsManager`, `DepartmentId`, `CanAccessHumanCapital` |

**Multi-DbContext design is already modular and clean** — HR master data lives in `OfficeDbContext`, payroll/insurance in `FinanceDbContext`, and workflow in `WorkflowDbContext`. This is a good foundation, but it creates a **cross-context coupling** that must be managed carefully (e.g., `PayrollController` and `BimehController` inject both `FinanceDbContext` and `OfficeDbContext`).

### 2.2 Existing HR Features (Mapped)

| Feature | Entity / Controller | Maturity |
|---------|---------------------|----------|
| Employee master data (personnel file) | `HumanCapitalEmployee` + `HumanCapitalController` | ✅ Solid (CRUD, search, filters, export, Jalali dates, uniqueness validation) |
| Salary history | `HumanCapitalSalaryHistory` | ✅ Solid (auto-logged on create/edit) |
| Status history (hire/terminate/resign) | `HumanCapitalStatusHistory` + `HumanCapitalProcessTypes` | ✅ Solid |
| Contracts / کارگزینی | `HrContract` + `HumanCapitalController.Contracts` | ⚠️ Basic (type, dates, salary, active flag) |
| Leave (HR-managed) | `HrLeave` + `HumanCapitalController.Leaves` | ⚠️ Basic (no accrual, no balance, no approval workflow) |
| Leave (self-service, workflow) | `Leave` + `LeavesController` | ⚠️ Basic (workflow + notifications, but no balance/accrual) |
| Loans / مساعده | `HrLoan` + `HumanCapitalController.Loans` | ⚠️ Basic (no installment schedule, no interest) |
| Attendance / کارکرد | `HrAttendance` + `HumanCapitalController.Attendance` | ❌ Manual monthly summary only |
| Payroll / حقوق | `PayrollList`/`PayrollItem` + `PayrollController` | ⚠️ Simplified (hard-coded 7% insurance, 0 tax, no payslip) |
| Insurance / بیمه | `InsuranceList`/`InsuranceEmployee` + `BimehController` | ⚠️ Basic (project-based lists, copy-from-previous-month) |
| HR Reports | `HrReportsVM` + `HumanCapitalController.Reports` | ❌ Static 7 counters |
| Organization calendar | `OrganizationCalendarEvent` + `OrganizationCalendarController` | ✅ Good (leave/payroll/invoice events integrated) |
| Department management | `Department` | ⚠️ Flat (no hierarchy) |

### 2.3 Frontend Architecture (Vanilla JS / HTML / CSS)

- **Shell:** Rahkaran-style MDI with **persistent fixed dashboard** — header (`rahkaran-header`), right sidebar (`rahkaran-sidebar`), and a **multi-tab workspace** (`rhyno-tab-column` + `tab-manager.js`). This already satisfies the "tab-based UI with persistent fixed dashboard" requirement.
- **Styling:** Tailwind CSS + custom `rahkaran-theme.css`, RTL (`dir="rtl"`), Vazirmatn font, Bootstrap Icons. **No Bootstrap framework** (removed per project history) — aligns with the "clean Vanilla JS + custom CSS" requirement.
- **Interactivity:** Vanilla JS (no React/Vue in HR views) — client-side pagination, instant search, saved filters, action dropdowns, tab panels.

---

## 3. Current State vs Global Standard Gap Matrix

Legend: ✅ Present · ⚠️ Partial · ❌ Missing

| Pillar | Sub-Capability | Current State | Workday / SuccessFactors / BambooHR | Gap |
|--------|----------------|---------------|--------------------------------------|-----|
| **1. Core HR & Lifecycle** | Employee master data | ✅ | ✅ | Low |
| | Onboarding workflow | ⚠️ (`OnboardingCompleted` bool only) | ✅ (checklists, tasks, e-sign) | **High** |
| | Offboarding / exit management | ⚠️ (status history only) | ✅ (exit checklist, asset return) | **High** |
| | Organizational chart | ❌ | ✅ (interactive org chart) | **High** |
| | Document management (HR docs) | ⚠️ (generic `DocumentArchiveItem`, not HR-specific) | ✅ (per-employee doc vault) | **Medium** |
| | Contract types & templates | ⚠️ (free-text `ContractType`) | ✅ (configurable types + templates) | **Medium** |
| | Job catalog / positions | ❌ (free-text `PositionTitle`) | ✅ (job architecture, grades) | **High** |
| **2. Time & Attendance** | Daily clock-in/out | ❌ | ✅ | **High** |
| | Shift / roster management | ❌ | ✅ (multi-shift, rotation) | **High** |
| | Overtime rules | ⚠️ (manual hours) | ✅ (rule engine) | **High** |
| | Leave request flows | ⚠️ (basic workflow) | ✅ (accrual, balance, multi-level) | **High** |
| | Leave accrual & balances | ❌ | ✅ | **High** |
| | Biometric / device integration | ❌ | ✅ (integration patterns) | **High** |
| **3. Payroll & Compensation** | Base salary | ✅ | ✅ | Low |
| | Allowances & deductions catalog | ❌ (single `Allowance` field) | ✅ (configurable components) | **High** |
| | Tax rules | ❌ (hard-coded 0) | ✅ (tax engine) | **High** |
| | Insurance rules | ⚠️ (hard-coded 7%) | ✅ (configurable) | **High** |
| | Payslip generation | ❌ (placeholder JSON) | ✅ (PDF/portal) | **High** |
| | Multi-currency | ❌ | ✅ | **Medium** |
| | Bank file / دیسکت بانکی | ❌ | ✅ | **Medium** |
| | عیدی / سنوات / پایان خدمت | ❌ | ✅ | **High** |
| **4. Talent Acquisition** | Job requisitions | ❌ | ✅ | **High** |
| | Applicant tracking (ATS) | ❌ | ✅ | **High** |
| | Interview pipeline | ❌ | ✅ | **High** |
| | Offer management | ❌ | ✅ | **High** |
| **5. Performance & OKR** | Goal setting | ❌ | ✅ | **High** |
| | KPIs | ❌ | ✅ | **High** |
| | Appraisal cycles | ❌ | ✅ | **High** |
| | 360° reviews | ❌ | ✅ | **High** |
| **6. ESS / MSS** | Employee self-service | ⚠️ (leave only) | ✅ | **High** |
| | Manager self-service | ❌ | ✅ | **High** |
| | Payslip download (ESS) | ❌ | ✅ | **High** |
| | Personal data update (ESS) | ❌ | ✅ | **High** |
| **7. Analytics & Compliance** | Turnover rate | ❌ | ✅ | **High** |
| | Headcount trends | ❌ | ✅ | **High** |
| | Audit logs | ✅ (global audit) | ✅ | Low |
| | Regulatory compliance (tax/insurance) | ⚠️ | ✅ | **Medium** |
| | Training / LMS | ❌ | ✅ | **High** |
| | Succession planning | ❌ | ✅ | **Medium** |

**Overall coverage estimate: ~35–40%** of a full enterprise HR suite.

---

## 4. Complete Missing Feature Checklist (Prioritized)

### 🔴 HIGH Priority (block self-sufficiency)

1. **Employee Self-Service (ESS) Portal**
   - View/update personal data (address, phone, emergency contact, bank account).
   - View payslips (PDF) and download.
   - Submit leave requests with **accrual balance** visibility.
   - View attendance/کارکرد history.
   - Submit loan requests.

2. **Manager Self-Service (MSS) Portal**
   - Team dashboard (direct reports, headcount, pending approvals).
   - Approve/reject leave, loan, and overtime requests.
   - View team attendance and payroll summaries.

3. **Recruitment / ATS**
   - Job requisitions (position, department, budget, approval workflow).
   - Applicant tracking (candidate profile, resume, status pipeline).
   - Interview scheduling & feedback.
   - Offer management → convert to `HumanCapitalEmployee` on hire.

4. **Performance Management & OKRs**
   - Goal setting (individual/team/company OKRs).
   - KPI definitions and tracking.
   - Appraisal cycles (self, manager, 360°).
   - Performance review forms and scoring.

5. **Real Payroll Engine & Payslip Generation**
   - Configurable allowance/deduction components (not a single `Allowance` field).
   - Tax rules (Iranian income tax brackets).
   - Insurance rules (configurable % per employee/contract).
   - **Payslip PDF generation** (replace `ExportPayslipBatch`/`ExportPdfPlaceholder` JSON stubs).
   - Bank file export (دیسکت بانکی).
   - عیدی (Eidi), سنوات (severance), پایان خدمت calculations.

6. **Time & Attendance — Daily Capture + Shift Management**
   - Daily clock-in/out records (not monthly summary).
   - Shift definitions and roster assignment.
   - Overtime rule engine.
   - Biometric device integration pattern (import/API).

7. **Leave Accrual & Balance Engine**
   - Leave types with accrual rules (استحقاقی, استعلاجی, etc.).
   - Per-employee balance tracking and carry-over.
   - Validation against balance on request.

8. **HR Analytics & Compliance Dashboard**
   - Turnover rate, headcount trend, tenure, age/gender distribution.
   - Leave liability, overtime cost, payroll cost trends.
   - Regulatory reports (tax, insurance, labor law).

### 🟡 MEDIUM Priority

9. **Organizational Chart** — interactive hierarchy from `Department` + `ManagerId`/`ManagerEmployeeId`.
10. **Job Catalog & Position Management** — replace free-text `PositionTitle` with a `JobPosition` entity (grade, family, salary band).
11. **Onboarding/Offboarding Checklists** — task templates, e-sign, asset return tracking.
12. **HR Document Vault** — per-employee document storage (contracts, certificates, IDs) linked to `DocumentArchiveItem`.
13. **Training / LMS** — course catalog, enrollment, completion/certification tracking.
14. **Multi-currency payroll** — leverage existing `Currency`/`CurrencyExchangeRate` in Finance.
15. **Succession Planning** — key-position readiness, talent pool.

### 🟢 LOW Priority

16. **Compensation benchmarking** — market salary surveys.
17. **Employee engagement surveys** — pulse surveys, eNPS.
18. **Time-off calendar heatmap** — team availability visualization.
19. **Mobile-responsive ESS** — PWA for employees.
20. **AI-assisted HR** — resume screening, attrition prediction (leverage existing `AiService`).

---

## 5. UI/UX & Workflow Architecture Recommendations

### 5.1 Tab-Based UI with Persistent Fixed Dashboard — ✅ Already Satisfied

The shell already implements a **persistent fixed dashboard** with a **multi-tab MDI workspace** (`tab-manager.js`, `rhyno-tab-column`, `rhyno-tab-bar`). Recommendations to extend it for HR:

- **Add an "HR Dashboard" as the default fixed tab** for HR users — a persistent KPI view (headcount, turnover, pending approvals, payroll cost) that stays open while other HR tabs (personnel, recruitment, performance) open on top.
- **Group HR sub-modules under a single "سرمایه انسانی" accordion** (already done in `_Layout.cshtml`), but add the missing sub-modules: استخدام (Recruitment), ارزیابی عملکرد (Performance), آموزش (Training), سلفسرویس (ESS).

### 5.2 Clean Vanilla JS + Custom CSS — ✅ Already Satisfied

- No Bootstrap; Tailwind + custom `rahkaran-theme.css` + Vanilla JS. **Maintain this.**
- For new HR views, follow the existing pattern in `Views/HumanCapital/Index.cshtml`: KPI cards → tab nav → sticky filter bar → data table with client-side pagination → Vanilla JS action dropdowns.
- **Reuse** the `_SavedFiltersPanel`, `professional-table.js`, and `Rhyno` global utilities (toast, modal, confirm) rather than introducing new frameworks.

### 5.3 Backend Modularity (Clean Code / EF Core Multi-DbContext)

- **Keep HR master data in `OfficeDbContext`**, payroll/insurance in `FinanceDbContext`, workflow in `WorkflowDbContext` — this is already correct.
- **Introduce an `HrModule` folder** (mirroring `Modules/Finance`, `Modules/Office`) to hold new HR domain services (e.g., `LeaveAccrualService`, `PayrollCalculationService`, `RecruitmentService`) instead of bloating `HumanCapitalController` (currently 1,278 lines).
- **Extract payroll calculation logic** out of `PayrollController.Calculate`/`Save` (currently hard-coded 7% insurance, 0 tax) into a dedicated `PayrollEngine` service with configurable rules.
- **Add a shared `HrDbContext` or keep cross-context via service layer** — avoid injecting both `FinanceDbContext` and `OfficeDbContext` into every controller; prefer a `PayrollService`/`InsuranceService` that encapsulates the cross-context reads.

### 5.4 Workflow Integration

- The workflow engine (`WorkflowService`, `WorkflowInstance`, `WorkflowDecision`) is already used for `Leave`. **Extend it** to:
  - Leave approval (multi-level: manager → HR).
  - Loan approval.
  - Job requisition approval.
  - Appraisal sign-off.
- Use the existing `WorkInbox` (کارتابل) as the unified approval queue for ESS/MSS requests.

---

## 6. Database Schema & Data Model Enhancements

The following C# / EF Core entities should be **added** to close the gaps. All follow the existing conventions (RTL Persian display names, `TenantId` for multi-tenancy, `IsDeleted` soft-delete, `CreatedAt`/`UpdatedAt`).

### 6.1 Core HR & Lifecycle

```csharp
// Job architecture (replaces free-text PositionTitle)
public class JobPosition
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;      // عنوان شغلی
    public string? JobFamily { get; set; }                  // خانواده شغلی
    public string? Grade { get; set; }                      // رتبه
    public decimal? MinSalary { get; set; }
    public decimal? MaxSalary { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
}

// Onboarding / Offboarding checklist
public class HrOnboardingTask
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public HumanCapitalEmployee? Employee { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public string? AssignedToUserId { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string TenantId { get; set; } = string.Empty;
}

// Per-employee HR document vault
public class HrEmployeeDocument
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public HumanCapitalEmployee? Employee { get; set; }
    public string DocumentType { get; set; } = string.Empty; // قرارداد، مدرک، گواهی
    public string FilePath { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
}
```

### 6.2 Time & Attendance / Shift

```csharp
// Daily attendance (replaces monthly summary)
public class HrDailyAttendance
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public HumanCapitalEmployee? Employee { get; set; }
    public DateTime WorkDate { get; set; }
    public DateTime? ClockIn { get; set; }
    public DateTime? ClockOut { get; set; }
    public decimal? OvertimeHours { get; set; }
    public string? Source { get; set; } // Manual | Biometric | API
    public string TenantId { get; set; } = string.Empty;
}

// Shift definition
public class HrShift
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string TenantId { get; set; } = string.Empty;
}

// Employee shift assignment (roster)
public class HrShiftAssignment
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public int ShiftId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string TenantId { get; set; } = string.Empty;
}

// Leave accrual & balance
public class HrLeaveBalance
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string LeaveType { get; set; } = string.Empty;
    public int Year { get; set; }
    public decimal EntitledDays { get; set; }
    public decimal UsedDays { get; set; }
    public decimal RemainingDays { get; set; }
    public string TenantId { get; set; } = string.Empty;
}
```

### 6.3 Payroll & Compensation

```csharp
// Configurable payroll components (allowances/deductions)
public class PayrollComponent
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;   // حق مسکن، بن، بیمه، مالیات
    public string Type { get; set; } = string.Empty;   // Allowance | Deduction
    public bool IsTaxable { get; set; }
    public bool IsInsuranceSubject { get; set; }
    public string TenantId { get; set; } = string.Empty;
}

// Employee-specific component assignment
public class PayrollComponentAssignment
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public int ComponentId { get; set; }
    public decimal Amount { get; set; }        // fixed or percentage
    public bool IsPercentage { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string TenantId { get; set; } = string.Empty;
}

// Tax rules (Iranian income tax brackets)
public class PayrollTaxBracket
{
    public int Id { get; set; }
    public int Year { get; set; }
    public decimal FromAmount { get; set; }
    public decimal? ToAmount { get; set; }
    public decimal Rate { get; set; }          // percentage
    public string TenantId { get; set; } = string.Empty;
}

// Loan installment schedule
public class HrLoanInstallment
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public HrLoan? Loan { get; set; }
    public int InstallmentNumber { get; set; }
    public decimal Amount { get; set; }
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = "در انتظار"; // در انتظار | پرداخت شده
    public string TenantId { get; set; } = string.Empty;
}
```

### 6.4 Talent Acquisition (ATS)

```csharp
public class JobRequisition
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int? DepartmentId { get; set; }
    public int? JobPositionId { get; set; }
    public int Headcount { get; set; } = 1;
    public string Status { get; set; } = "Draft"; // Draft | Approved | Open | Closed
    public DateTime? OpenDate { get; set; }
    public DateTime? CloseDate { get; set; }
    public string TenantId { get; set; } = string.Empty;
}

public class JobApplicant
{
    public int Id { get; set; }
    public int RequisitionId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? ResumePath { get; set; }
    public string Stage { get; set; } = "Applied"; // Applied | Screening | Interview | Offer | Hired | Rejected
    public DateTime AppliedAt { get; set; }
    public string TenantId { get; set; } = string.Empty;
}

public class Interview
{
    public int Id { get; set; }
    public int ApplicantId { get; set; }
    public DateTime ScheduledAt { get; set; }
    public string? InterviewerUserId { get; set; }
    public string? Feedback { get; set; }
    public int? Score { get; set; }
    public string TenantId { get; set; } = string.Empty;
}
```

### 6.5 Performance & OKR

```csharp
public class HrGoal
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Type { get; set; } = "Individual"; // Individual | Team | Company
    public decimal? TargetValue { get; set; }
    public decimal? CurrentValue { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; } = "Active";
    public string TenantId { get; set; } = string.Empty;
}

public class HrAppraisalCycle
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = "Draft"; // Draft | Active | Completed
    public string TenantId { get; set; } = string.Empty;
}

public class HrPerformanceReview
{
    public int Id { get; set; }
    public int CycleId { get; set; }
    public int EmployeeId { get; set; }
    public string? ReviewerUserId { get; set; }
    public string ReviewType { get; set; } = "Self"; // Self | Manager | Peer (360)
    public decimal? Score { get; set; }
    public string? Comments { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string TenantId { get; set; } = string.Empty;
}
```

### 6.6 Training / LMS

```csharp
public class HrTrainingCourse
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public int? DurationHours { get; set; }
    public string TenantId { get; set; } = string.Empty;
}

public class HrTrainingEnrollment
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public int EmployeeId { get; set; }
    public DateTime EnrolledAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "Enrolled"; // Enrolled | InProgress | Completed
    public string? CertificatePath { get; set; }
    public string TenantId { get; set; } = string.Empty;
}
```

### 6.7 ESS / MSS (supporting entities)

```csharp
// Employee bank account (for payroll bank file)
public class HrEmployeeBankAccount
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string? ShebaNumber { get; set; }
    public bool IsPrimary { get; set; }
    public string TenantId { get; set; } = string.Empty;
}

// Emergency contact
public class HrEmergencyContact
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Relationship { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
}
```

### 6.8 Schema Changes to Existing Entities

| Entity | Change |
|--------|--------|
| `HumanCapitalEmployee` | Add `JobPositionId` (FK), `ManagerEmployeeId` (reporting line), `BankAccountId`, `EmergencyContactId` |
| `HrLeave` | Add `LeaveBalanceId` (FK), `ApprovedByUserId`, `ApprovedAt` |
| `HrLoan` | Add `InterestRate`, `InstallmentAmount`, `FirstInstallmentDate` |
| `HrAttendance` | Deprecate in favor of `HrDailyAttendance` (or keep as monthly rollup) |
| `PayrollItem` | Replace single `Allowance` with `ICollection<PayrollItemComponent>` |
| `Department` | Add `ParentDepartmentId` (self-referencing hierarchy) |

---

## 7. Step-by-Step Actionable Roadmap

### Phase 1 — Foundation & ESS/MSS (Weeks 1–4)

1. **Add `JobPosition` entity + migration**; link `HumanCapitalEmployee.PositionTitle` → `JobPositionId`.
2. **Add `HrEmployeeBankAccount` + `HrEmergencyContact`** entities + migrations.
3. **Build ESS portal** (`EmployeeSelfServiceController`):
   - "My Profile" (view/update personal data, bank, emergency contact).
   - "My Payslips" (list + download).
   - "My Leave" (request with balance).
   - "My Attendance" (view کارکرد).
4. **Build MSS portal** (`ManagerSelfServiceController`):
   - Team dashboard (direct reports via `ManagerEmployeeId`).
   - Approve/reject leave/loan/overtime (reuse workflow + `WorkInbox`).
5. **Add `HrLeaveBalance` + accrual engine** (`LeaveAccrualService`).

### Phase 2 — Payroll & Payslip Engine (Weeks 5–8)

6. **Add `PayrollComponent`, `PayrollComponentAssignment`, `PayrollTaxBracket`** entities + migrations.
7. **Extract `PayrollEngine` service** — replace hard-coded 7% insurance / 0 tax with configurable rules.
8. **Implement real payslip PDF generation** (replace `ExportPayslipBatch`/`ExportPdfPlaceholder` JSON stubs) using the existing `ExportService`/ClosedXML or a PDF library.
9. **Add bank file export** (دیسکت بانکی) using `HrEmployeeBankAccount`.
10. **Add عیدی/سنوات/پایان خدمت** calculations to `PayrollEngine`.

### Phase 3 — Time & Attendance (Weeks 9–12)

11. **Add `HrDailyAttendance`, `HrShift`, `HrShiftAssignment`** entities + migrations.
12. **Build daily attendance capture UI** (manual + import).
13. **Add biometric integration pattern** (API endpoint / CSV import for device data).
14. **Add overtime rule engine** and link to payroll.

### Phase 4 — Recruitment / ATS (Weeks 13–16)

15. **Add `JobRequisition`, `JobApplicant`, `Interview`** entities + migrations.
16. **Build recruitment UI** (`RecruitmentController`): requisitions, applicant pipeline, interview scheduling.
17. **Add offer management** → convert applicant to `HumanCapitalEmployee` on hire (reuse existing `Create` logic).

### Phase 5 — Performance & OKR (Weeks 17–20)

18. **Add `HrGoal`, `HrAppraisalCycle`, `HrPerformanceReview`** entities + migrations.
19. **Build performance UI** (`PerformanceController`): goals, KPIs, appraisal cycles, 360° reviews.
20. **Integrate with workflow** for appraisal sign-off.

### Phase 6 — Analytics, Training & Org Chart (Weeks 21–24)

21. **Build HR analytics dashboard** (`HrAnalyticsController`): turnover, headcount trend, tenure, payroll cost.
22. **Add `HrTrainingCourse` + `HrTrainingEnrollment`** and build LMS UI.
23. **Add `ParentDepartmentId`** to `Department` and build interactive org chart.
24. **Add onboarding/offboarding checklists** (`HrOnboardingTask`) and HR document vault (`HrEmployeeDocument`).

### Phase 7 — Hardening & Compliance (Weeks 25–28)

25. **Regulatory reports** (tax, insurance, labor law) — leverage existing `TaxAnalytics`/`Mowadiyan` modules.
26. **Succession planning** (key positions, talent pool).
27. **Multi-currency payroll** (leverage `Currency`/`CurrencyExchangeRate`).
28. **Full test coverage** for payroll/accrual engines; performance & security review.

---

## 8. Assumptions & Notes

1. **Payroll tax/insurance are currently hard-coded** (`PayrollController.Calculate`/`Save` use `* 0.07m` insurance and `tax = 0m`). This is treated as a gap requiring a configurable engine.
2. **Payslip generation is a placeholder** — `ExportPayslipBatch` and `ExportPdfPlaceholder` return JSON stubs, not actual files. `PrintPayslipPdf` exists in `FinancialController` but returns a view; full PDF generation is assumed incomplete.
3. **Attendance is monthly summary only** (`HrAttendance` with `PresentDays`/`AbsentDays`/`OvertimeHours`), not daily clock-in/out. Daily capture is treated as missing.
4. **Two leave systems coexist** — `HrLeave` (HR-managed, no workflow) and `Leave` (self-service, workflow-driven). The roadmap recommends consolidating on a single leave engine with accrual/balance.
5. **`PositionTitle` and `ContractType` are free-text strings** — treated as gaps requiring catalog entities (`JobPosition`, configurable contract types).
6. **The UI already satisfies the tab-based + persistent dashboard + Vanilla JS + custom CSS requirements** — recommendations focus on extending, not rebuilding.
7. **Multi-DbContext design is already clean** — recommendations focus on extracting domain services to avoid controller bloat and cross-context coupling.
8. **All new entities follow existing conventions** (RTL Persian display names, `TenantId`, `IsDeleted`, `CreatedAt`/`UpdatedAt`) and should be registered in the appropriate DbContext (`OfficeDbContext` for HR master data, `FinanceDbContext` for payroll/insurance).

---

*This report is based on a full scan of the C# backend (Models, Controllers, Services, DbContexts, Migrations) and the Vanilla JS/HTML/CSS frontend (Views, Layout, tab-manager).*
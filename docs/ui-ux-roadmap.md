# UI/UX And Product Roadmap

## Competitive Modernization Plan

### Phase 1: Operational Inbox And Persistent Notifications

- Done: persisted notification model, notification center page, unread/read state, severity, link target, source module, expiration, and header integration.
- Done: create notifications for new letters, letter approval routing, leave approval/rejection, leave requests sent to HR/admin roles, low-stock alerts, invoice deadlines, warehouse transfer decisions, financial approval decisions, and security-sensitive audit events.
- Partial: central work inbox combines approval tasks, unread letters, deadline alerts, notifications, and assigned actions with filters by module and urgency.
- Next: add assigned-owner targeting for financial decision notifications instead of broadcasting to finance users.

### Phase 2: Unified Workflow Engine

- Done: workflow instances, steps, decisions, delegations, due dates, and SLA state foundation tables are in place.
- Done: shared workflow routing service now resolves configured routes, advances letter approval steps, records decision history for letters and leaves, and exposes assigned workflow steps in the work inbox.
- Done: basic workflow decision timeline is visible on letter and leave detail pages.
- Done: Financial invoice decisions and warehouse transfer requests are connected to workflow instances and decision history.
- Done: receipt, issuance, counting, and transfer warehouse approvals now create workflow instances and record decisions.
- Next: add management UI for workflow delegations and precise financial invoice assignees.
- Add per-document workflow timeline with comments, attachments, and decision history.

### Phase 3: Document Management System

- Extend archive with document versioning, related document links, confidentiality level, retention policy, and full audit timeline.
- Add attachment support consistently to letters, invoices, warehouse documents, employee records, and workflow decisions.
- Add OCR/search-ready metadata as a later optional capability.

### Phase 4: Reporting, Import, Export, And BI

- Add reusable export jobs, import batches, template versions, validation reports, and rollback-safe import processing.
- Add saved report views, scheduled reports, role-filtered dashboards, and management KPIs.
- Expand financial, inventory, HR, audit, and workflow reports with drill-down links.

### Phase 5: UI/UX Redesign

- Redesign navigation around role workspaces: executive, finance, warehouse, HR, administrator, and regular employee.
- Standardize list/detail/form pages with shared toolbar, saved filters, bulk actions, empty states, timeline, and side panels.
- Remove legacy CSS page by page and keep `design-system.css` and scoped page CSS as the source of truth.
- Improve mobile flows for approvals, notifications, letters, and quick search.

### Phase 6: Integration And Product Readiness

- Add REST API endpoints, API keys/OAuth strategy, integration audit logs, and documentation.
- Add tenant/company/branch boundaries if this product is sold to multiple organizations.
- Add backup/restore verification, deployment health checks, seed/sample data, and onboarding.
- Add accessibility checks, Persian copy cleanup, and browser/device QA.

## Design System Consolidation

- Treat `wwwroot/css/design-system.css` as the source of truth for spacing, radius, surfaces, shadows, typography, focus states, and reusable cards.
- Keep layout shell rules in `wwwroot/css/app-shell.css`; do not add new layout styles inline in Razor views.
- New page-specific CSS should be small and scoped to the page. If a rule is shared by two or more pages, move it to `design-system.css`.
- Phase out broad overrides in `main.css` and `visual-refresh.css` by moving stable patterns into design-system tokens and components.
- Avoid `!important` for new styles unless overriding third-party CSS with no better hook.

## Vendor Assets

- Bootstrap and jQuery are loaded locally from `wwwroot/lib`.
- Bootstrap Icons still needs a local package under `wwwroot/lib/bootstrap-icons` before the final CDN dependency can be removed.
- External assets should be versioned in `wwwroot/lib` and referenced with `asp-append-version`.

## Product Backlog

- Done: global command palette foundation is available with `Ctrl+K` and builds commands from permitted sidebar links.
- Partial: header notification menu exists, but notifications are query-based and not persisted yet.
- Partial: workflow status normalization exists in `WorkflowService`, but a full approval engine is not implemented yet.
- Foundation only: notification center, approval engine, and import/export remain UI/service foundations until dedicated backend schema and migrations are added.
- Next: real notification center with persisted notifications, read/unread state, severity, link target, and per-user delivery.
- Next: activity timeline for every document using a common timeline partial backed by audit logs and workflow events.
- Next: bulk actions with row selection, server-side permission validation, preview impact, execution, and audit trail.
- Next: standard export/import with shared export service, import templates, validation report, and rollback-safe import batches.
- Next: role-based dashboards with widgets filtered by department, role, and assigned approvals.
- Next: better onboarding and empty states with first-run cards, sample actions, and next-step guidance for empty lists.
- Next: unified approval system for leave, finance, warehouse, letters, and future modules.

## Backend Schema Roadmap

- Notifications: add `Notifications` and `NotificationReceipts` tables for per-user delivery, read/unread state, severity, link targets, expiration, and audit-safe lifecycle updates.
- Approvals: add approval definitions, approval instances, steps, decisions, delegations, escalation rules, and module document bindings instead of module-specific status checks.
- Import/export: add import batch records, import row validation results, template versions, export job records, file metadata, and rollback/retry status.

## CSS Migration Status

- Done: `_Layout.cshtml` shell styles moved to `wwwroot/css/app-shell.css`.
- Done: `main.css` is now a small entrypoint; old content is split into `main-foundation.css` and `main-legacy.css`.
- Done: `visual-refresh.css` is now a small entrypoint; old content is split into `visual-refresh-core.css` and `visual-refresh-legacy.css`.
- Done: inline Razor styles were removed from Financial, Warehouse, Security, Home, and HumanCapital views and moved to scoped page CSS files.
- Next: continue shrinking `main-legacy.css` and `visual-refresh-legacy.css` page by page, then delete unused selectors once coverage is verified.

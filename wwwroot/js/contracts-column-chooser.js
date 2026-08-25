/**
 * Rhyno Contracts Index — Column Customization
 *
 * Provides:
 *   1. Column chooser dropdown (toggle)
 *   2. Load saved column preferences from server
 *   3. Render column checkboxes dynamically
 *   4. Apply: show/hide columns
 *   5. Save: AJAX POST to save preference
 *   6. Reset to default (show all columns)
 */

document.addEventListener('DOMContentLoaded', function () {
    'use strict';

    // ── Column definitions (data-column-id → display name) ──
    var COLUMNS = {
        Id: 'ردیف',
        ContractNumber: 'شماره قرارداد',
        Title: 'عنوان',
        ClientName: 'مشتری',
        ContractType: 'نوع',
        TotalAmount: 'مبلغ کل',
        PaidAmount: 'پرداخت‌شده',
        RemainingBalance: 'باقی‌مانده',
        PaymentStatus: 'وضعیت پرداخت',
        StartDate: 'تاریخ شروع',
        EndDate: 'تاریخ پایان',
        Status: 'وضعیت',
        ResponsibleUser: 'مسئول پیگیری',
        Actions: 'عملیات'
    };

    var DEFAULT_COLUMNS = ['ContractNumber', 'Title', 'ClientName', 'ContractType', 'TotalAmount', 'Status', 'StartDate', 'EndDate', 'ResponsibleUser', 'Actions'];

    var tableWrapper = document.querySelector('[data-table-wrapper]');
    if (!tableWrapper) return;

    // ── State ──
    var savedColumnsJson = tableWrapper.dataset.savedColumns || null;
    var currentColumns = null; // null = all shown

    // Parse saved columns
    if (savedColumnsJson) {
        try {
            currentColumns = JSON.parse(savedColumnsJson);
        } catch (e) {
            currentColumns = null;
        }
    }

    // ── Apply visibility based on saved columns ──
    function applyVisibility(cols) {
        var allColumnIds = Object.keys(COLUMNS);
        allColumnIds.forEach(function (id) {
            var headers = tableWrapper.querySelectorAll('th[data-column-id="' + id + '"]');
            var cells = tableWrapper.querySelectorAll('td[data-column-id="' + id + '"]');

            var visible = (cols === null) || cols.indexOf(id) !== -1;
            var display = visible ? '' : 'none';

            headers.forEach(function (el) { el.style.display = display; });
            cells.forEach(function (el) { el.style.display = display; });
        });
    }

    // ── Render column checkboxes in the chooser ──
    function renderColumnCheckboxes(cols) {
        var list = tableWrapper.querySelector('[data-column-list]');
        if (!list) return;

        var html = '';
        var allColumnIds = Object.keys(COLUMNS);

        allColumnIds.forEach(function (id) {
            var visible = (cols === null) || cols.indexOf(id) !== -1;
            var checked = visible ? 'checked' : '';
            html += '<label class="flex items-center gap-2 cursor-pointer text-[11px] text-slate-700 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-700/50 rounded px-1.5 py-1 transition">'
                + '<input type="checkbox" class="column-checkbox" data-column-id="' + id + '" ' + checked + ' style="width:14px;height:14px;accent-color:#14b8a6;flex-shrink:0;">'
                + '<span>' + COLUMNS[id] + '</span>'
                + '</label>';
        });

        list.innerHTML = html;
    }

    // ── Kick off ──
    applyVisibility(currentColumns);
    renderColumnCheckboxes(currentColumns);

// ═══════════════════════════════════════════════════════════════
    //  Exposed functions (called from onclick in toolbar)
    // ═══════════════════════════════════════════════════════════════

    // Toggle column chooser dropdown
    window.toggleColumnChooser = function (event) {
        event.stopPropagation();
        var menu = tableWrapper.querySelector('[data-column-menu]');
        if (!menu) return;
        menu.classList.toggle('hidden');
        if (!menu.classList.contains('hidden')) {
            var close = function (e) {
                menu.classList.add('hidden');
                document.removeEventListener('click', close);
            };
            setTimeout(function () { document.addEventListener('click', close); }, 10);
        }
    };

    // Apply current checkbox state to the table
    window.applyColumnChanges = function () {
        var checkboxes = tableWrapper.querySelectorAll('.column-checkbox');
        var selected = [];
        checkboxes.forEach(function (cb) {
            if (cb.checked) {
                selected.push(cb.dataset.columnId);
            }
        });

        // Apply to table
        currentColumns = selected.length > 0 ? selected : null;
        applyVisibility(currentColumns);

        // Persist to server via AJAX
        persistColumns(selected);

        // Close dropdown
        var menu = tableWrapper.querySelector('[data-column-menu]');
        if (menu) menu.classList.add('hidden');
    };

    // Reset to default (all columns shown)
    window.resetColumnDefaults = function () {
        currentColumns = null;
        applyVisibility(null);
        renderColumnCheckboxes(null);

        // Persist empty preference (null = all)
        var csrfToken = '';
        var metaTag = document.querySelector('meta[name="request-verification-token"]');
        if (metaTag) csrfToken = metaTag.getAttribute('content');

        fetch('/Contracts/SaveColumnPreference', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': csrfToken
            },
            body: JSON.stringify({ columnNames: [] })
        }).then(function (r) { return r.json(); })
            .then(function (result) {
                if (result.success) {
                    AppUI.toast(result.message, 'success');
                } else {
                    AppUI.toast(result.message, 'danger');
                }
            })
            .catch(function (err) {
                console.error('Save column preference error:', err);
                AppUI.toast('خطا در ذخیره تنظیمات ستون‌ها.', 'danger');
            });
    };

    // ── Persist to server ──
    function persistColumns(columnNames) {
        var csrfToken = '';
        var metaTag = document.querySelector('meta[name="request-verification-token"]');
        if (metaTag) csrfToken = metaTag.getAttribute('content');

        fetch('/Contracts/SaveColumnPreference', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': csrfToken
            },
            body: JSON.stringify({ columnNames: columnNames })
        }).then(function (r) { return r.json(); })
            .then(function (result) {
                if (result.success) {
                    AppUI.toast(result.message, 'success');
                } else {
                    AppUI.toast(result.message, 'warning');
                }
            })
            .catch(function (err) {
                console.error('Save column preference error:', err);
                AppUI.toast('خطا در ذخیره تنظیمات ستون‌ها.', 'danger');
            });
    }
});
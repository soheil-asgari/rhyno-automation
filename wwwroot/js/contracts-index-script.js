/**
 * Rhyno Contracts Index — Loading States & Bulk Actions
 *
 * Provides:
 *   1. Loading overlay on filter form submit
 *   2. Auto-submit on filter <select> change
 *   3. Pagination link loading spinner
 *   4. Checkbox selection (single / select-all)
 *   5. Bulk action toolbar with dropdown
 *   6. Confirmation via AppUI.confirm
 *   7. AJAX POST to /Contracts/BulkAction
 *   8. Toast notification on result
 */

document.addEventListener('DOMContentLoaded', function () {
    'use strict';

    // ── Jalali Date Picker Logic ──────────────────────────────
    var PERSIAN_MONTHS = ['فروردین', 'اردیبهشت', 'خرداد', 'تیر', 'مرداد', 'شهریور', 'مهر', 'آبان', 'آذر', 'دی', 'بهمن', 'اسفند'];
    var PERSIAN_WEEKDAYS = ['ش', 'ی', 'د', 'س', 'چ', 'پ', 'ج'];

    function toPersianDigits(str) {
        return String(str).replace(/\d/g, function (d) { return '۰۱۲۳۴۵۶۷۸۹'[d]; });
    }

    function writeJalaliToHidden(input, jalali) {
        var wrapper = input.closest('.jalali-datepicker-wrapper');
        var hidden = wrapper ? wrapper.querySelector('input.jalali-date-hidden') : null;
        if (hidden) {
            if (!jalali) {
                hidden.value = '';
                return;
            }
            try {
                var parts = jalali.trim().split('/');
                if (parts.length === 3) {
                    var y = parseInt(parts[0], 10);
                    var m = parseInt(parts[1], 10);
                    var d = parseInt(parts[2], 10);
                    if (!isNaN(y) && !isNaN(m) && !isNaN(d) && typeof persianDate !== 'undefined') {
                        var pd = new persianDate([y, m, d]);
                        var g = pd.toDate();
                        hidden.value = g.getFullYear() + '-' + String(g.getMonth() + 1).padStart(2, '0') + '-' + String(g.getDate()).padStart(2, '0');
                    }
                }
            } catch (e) { }
        }
    }

    function createDatepicker(input) {
        var wrapper = input.closest('.jalali-datepicker-wrapper') || input.parentElement;
        if (!wrapper) return;
        wrapper.style.position = 'relative';

        var popup = wrapper.querySelector('.vanilla-jalali-popup');
        if (!popup) {
            popup = document.createElement('div');
            popup.className = 'vanilla-jalali-popup';
            wrapper.appendChild(popup);
        }

        var viewYear = 0, viewMonth = 0;

        function render() {
            if (typeof persianDate === 'undefined') return;
            var today = new persianDate();
            if (!viewYear) viewYear = today.year();
            if (!viewMonth) viewMonth = today.month();

            var daysInMonth = today.daysInMonth(viewYear, viewMonth);
            var firstWeekday = today.getFirstWeekDayOfMonth(viewYear, viewMonth);

            var html = '<div style="display:flex;align-items:center;justify-content:space-between;padding:8px 10px;border-bottom:1px solid #f1f5f9;">';
            html += '<button type="button" class="vjp-nav-btn" data-nav="prev">&rsaquo;</button>';
            html += '<span style="font-size:12px;font-weight:700;color:#334155;">' + PERSIAN_MONTHS[viewMonth - 1] + ' ' + toPersianDigits(viewYear) + '</span>';
            html += '<button type="button" class="vjp-nav-btn" data-nav="next">&lsaquo;</button>';
            html += '</div>';

            html += '<div style="display:grid;grid-template-columns:repeat(7,1fr);padding:4px 6px;border-bottom:1px solid #f1f5f9;">';
            PERSIAN_WEEKDAYS.forEach(function (w) {
                html += '<div style="text-align:center;font-size:10px;font-weight:700;color:#94a3b8;padding:2px 0;">' + w + '</div>';
            });
            html += '</div>';

            html += '<div style="display:grid;grid-template-columns:repeat(7,1fr);padding:6px;">';
            for (var i = 0; i < firstWeekday; i++) { html += '<div></div>'; }
            for (var day = 1; day <= daysInMonth; day++) {
                var isToday = (day === today.date() && viewMonth === today.month() && viewYear === today.year());
                html += '<button type="button" class="vjp-day-btn' + (isToday ? ' is-today' : '') + '" data-day="' + day + '">' + toPersianDigits(day) + '</button>';
            }
            html += '</div>';

            popup.innerHTML = html;

            popup.querySelector('[data-nav="prev"]')?.addEventListener('click', function (e) {
                e.stopPropagation(); e.preventDefault();
                viewMonth--; if (viewMonth < 1) { viewMonth = 12; viewYear--; } render();
            });
            popup.querySelector('[data-nav="next"]')?.addEventListener('click', function (e) {
                e.stopPropagation(); e.preventDefault();
                viewMonth++; if (viewMonth > 12) { viewMonth = 1; viewYear++; } render();
            });
            popup.querySelectorAll('[data-day]').forEach(function (btn) {
                btn.addEventListener('click', function (e) {
                    e.stopPropagation(); e.preventDefault();
                    var day = parseInt(btn.getAttribute('data-day'), 10);
                    var selected = new persianDate([viewYear, viewMonth, day]);
                    var jalali = selected.format('YYYY/MM/DD');
                    input.value = jalali;
                    writeJalaliToHidden(input, jalali);
                    popup.style.display = 'none';
                });
            });
        }

        input.addEventListener('click', function (e) {
            e.stopPropagation();
            document.querySelectorAll('.vanilla-jalali-popup').forEach(function (p) { if (p !== popup) p.style.display = 'none'; });
            viewYear = 0; viewMonth = 0;
            render();
            popup.style.display = 'block';
        });
    }

    document.querySelectorAll('.jalali-date-input').forEach(createDatepicker);
    document.addEventListener('click', function (e) {
        if (!e.target.closest('.jalali-datepicker-wrapper')) {
            document.querySelectorAll('.vanilla-jalali-popup').forEach(function (p) { p.style.display = 'none'; });
        }
    });

    // ── Filter loading overlay ──
    var filterForm = document.querySelector('[data-filter-form]');
    var tableWrapper = document.querySelector('[data-table-wrapper]');

    if (filterForm && tableWrapper) {
        filterForm.addEventListener('submit', function () {
            if (!tableWrapper.querySelector('.contracts-loading-overlay')) {
                var overlay = document.createElement('div');
                overlay.className = 'contracts-loading-overlay';
                overlay.innerHTML = '<span class="rhyno-spinner"></span><span>در حال جستجو...</span>';
                tableWrapper.appendChild(overlay);
            }
        });

        filterForm.querySelectorAll('select').forEach(function (sel) {
            sel.addEventListener('change', function () {
                filterForm.requestSubmit();
            });
        });
    }

    // ── Pagination loading state ──
    document.querySelectorAll('[data-page-link]').forEach(function (link) {
        link.addEventListener('click', function (e) {
            var paginationContainer = this.closest('.flex');
            if (!paginationContainer) return;
            if (paginationContainer.querySelector('.pagination-loading')) return;

            e.preventDefault();
            var href = this.getAttribute('href');
            if (!href || href === '#') return;

            var spinner = document.createElement('span');
            spinner.className = 'pagination-loading';
            spinner.innerHTML = '<span class="rhyno-spinner" style="width:12px;height:12px;border-width:2px;"></span>';
            paginationContainer.appendChild(spinner);

            setTimeout(function () {
                window.location.href = href;
            }, 80);
        });
    });

    // ═══════════════════════════════════════════════════════════════
    //  Bulk Actions
    // ═══════════════════════════════════════════════════════════════

    var selectAllCheckbox = document.getElementById('selectAllCheckbox');
    var bulkActionBar = document.getElementById('bulkActionBar');
    var bulkSelectedCount = document.getElementById('bulkSelectedCount');
    var bulkActionSelect = document.getElementById('bulkActionSelect');
    var bulkActionApplyBtn = document.getElementById('bulkActionApplyBtn');
    var bulkActionClearBtn = document.getElementById('bulkActionClearBtn');
    var bulkNewStatus = document.getElementById('bulkNewStatus');
    var bulkAssignUser = document.getElementById('bulkAssignUser');
    var bulkActionSubControls = document.getElementById('bulkActionSubControls');

    function getSelectedIds() {
        var ids = [];
        document.querySelectorAll('.row-checkbox:checked').forEach(function (cb) {
            var val = parseInt(cb.value, 10);
            if (!isNaN(val)) ids.push(val);
        });
        return ids;
    }

    function updateBulkUI() {
        var ids = getSelectedIds();
        var count = ids.length;

        if (count > 0) {
            bulkActionBar.style.display = 'block';
            bulkSelectedCount.textContent = count + ' انتخاب';
            bulkActionApplyBtn.disabled = false;
        } else {
            bulkActionBar.style.display = 'none';
            bulkActionSubControls.style.display = 'none';
            bulkNewStatus.style.display = 'none';
            bulkAssignUser.style.display = 'none';
            bulkActionApplyBtn.disabled = true;
        }

        var allRows = document.querySelectorAll('.row-checkbox');
        var checkedRows = document.querySelectorAll('.row-checkbox:checked');
        if (selectAllCheckbox) {
            selectAllCheckbox.checked = allRows.length > 0 && checkedRows.length === allRows.length;
            selectAllCheckbox.indeterminate = checkedRows.length > 0 && checkedRows.length < allRows.length;
        }

        document.querySelectorAll('tbody tr').forEach(function (tr) {
            var cb = tr.querySelector('.row-checkbox');
            if (cb) {
                tr.classList.toggle('bulk-row-selected', cb.checked);
            }
        });
    }

    if (selectAllCheckbox) {
        selectAllCheckbox.addEventListener('change', function () {
            var isChecked = this.checked;
            document.querySelectorAll('.row-checkbox').forEach(function (cb) { cb.checked = isChecked; });
            updateBulkUI();
        });
    }

    document.addEventListener('change', function (e) {
        if (e.target.classList.contains('row-checkbox')) { updateBulkUI(); }
    });

    if (bulkActionSelect) {
        bulkActionSelect.addEventListener('change', function () {
            var action = this.value;
            bulkNewStatus.style.display = 'none';
            bulkAssignUser.style.display = 'none';
            bulkActionSubControls.style.display = 'none';
            if (action === 'status') {
                bulkNewStatus.style.display = 'inline-block';
                bulkActionSubControls.style.display = 'flex';
            } else if (action === 'assign') {
                bulkAssignUser.style.display = 'inline-block';
                bulkActionSubControls.style.display = 'flex';
            }
        });
    }

    if (bulkActionClearBtn) {
        bulkActionClearBtn.addEventListener('click', function () {
            document.querySelectorAll('.row-checkbox').forEach(function (cb) { cb.checked = false; });
            if (selectAllCheckbox) selectAllCheckbox.checked = false;
            if (bulkActionSelect) bulkActionSelect.value = '';
            bulkNewStatus.style.display = 'none';
            bulkAssignUser.style.display = 'none';
            bulkActionSubControls.style.display = 'none';
            updateBulkUI();
        });
    }

    if (bulkActionApplyBtn) {
        bulkActionApplyBtn.addEventListener('click', async function () {
            var ids = getSelectedIds();
            if (ids.length === 0) { AppUI.toast('لطفاً حداقل یک قرارداد را انتخاب کنید.', 'warning'); return; }

            var action = bulkActionSelect ? bulkActionSelect.value : '';
            if (!action) { AppUI.toast('لطفاً یک عملیات انتخاب کنید.', 'warning'); return; }

            var payload = { IDs: ids, Action: action };

            if (action === 'status') {
                var ns = bulkNewStatus ? bulkNewStatus.value : '';
                if (!ns) { AppUI.toast('لطفاً وضعیت جدید را انتخاب کنید.', 'warning'); return; }
                payload.NewStatus = ns;
            } else if (action === 'assign') {
                var au = bulkAssignUser ? bulkAssignUser.value : '';
                if (!au) { AppUI.toast('لطفاً کاربر مورد نظر را انتخاب کنید.', 'warning'); return; }
                payload.AssignToUserId = au;
            }

            var confirmOpts = { title: 'عملیات گروهی', message: '', confirmText: 'تایید', cancelText: 'انصراف' };
            switch (action) {
                case 'status':
                    confirmOpts.message = 'آیا از تغییر وضعیت ' + ids.length + ' قرارداد به «' + payload.NewStatus + '» مطمئن هستید؟';
                    confirmOpts.severity = 'warning'; break;
                case 'assign':
                    confirmOpts.message = 'آیا از اختصاص ' + ids.length + ' قرارداد به کاربر انتخاب‌شده مطمئن هستید؟';
                    confirmOpts.severity = 'info'; break;
                case 'archive':
                    confirmOpts.message = 'آیا از بایگانی ' + ids.length + ' قرارداد مطمئن هستید؟';
                    confirmOpts.severity = 'warning'; break;
                case 'delete':
                    confirmOpts.message = 'آیا از حذف ' + ids.length + ' قرارداد مطمئن هستید؟ این عملیات غیرقابل بازگشت است!';
                    confirmOpts.severity = 'danger'; confirmOpts.confirmText = 'حذف'; break;
            }

            var csrfToken = '';
            var metaTag = document.querySelector('meta[name="request-verification-token"]');
            if (metaTag) csrfToken = metaTag.getAttribute('content');

            var confirmed = await AppUI.confirm(confirmOpts);
            if (!confirmed) return;

            // Enable loading state on bulkActionBar and disable controls
            if (bulkActionBar) {
                bulkActionBar.classList.add('is-loading');
            }
            if (bulkActionSelect) bulkActionSelect.disabled = true;
            if (bulkActionClearBtn) bulkActionClearBtn.disabled = true;
            if (bulkNewStatus) bulkNewStatus.disabled = true;
            if (bulkAssignUser) bulkAssignUser.disabled = true;
            AppUI.loading(bulkActionApplyBtn, true);
            bulkActionApplyBtn.innerHTML = '<span class="rhyno-spinner" style="width:12px;height:12px;border-width:2px;display:inline-block;vertical-align:middle;margin-left:4px;"></span>در حال اجرا...';

            try {
                var resp = await fetch('/Contracts/BulkAction', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrfToken },
                    body: JSON.stringify(payload)
                });
                if (!resp.ok) throw new Error('خطا در ارتباط با سرور (' + resp.status + ')');
                var result = await resp.json();
                if (result.success || result.successCount > 0) {
                    AppUI.toast(result.message || 'عملیات با موفقیت انجام شد.', 'success');
                } else {
                    AppUI.toast(result.message || 'عملیات با خطا مواجه شد.', 'danger');
                }
                if (result.errors && result.errors.length > 0) {
                    var errList = result.errors.slice(0, 3).join(' | ');
                    if (errList) AppUI.toast(errList, 'warning');
                }
                if (result.successCount > 0) {
                    setTimeout(function () { window.location.reload(); }, 1200);
                }
            } catch (err) {
                AppUI.toast('خطا: ' + err.message, 'danger');
                console.error('Bulk action error:', err);
            } finally {
                if (bulkActionBar) {
                    bulkActionBar.classList.remove('is-loading');
                }
                if (bulkActionSelect) bulkActionSelect.disabled = false;
                if (bulkActionClearBtn) bulkActionClearBtn.disabled = false;
                if (bulkNewStatus) bulkNewStatus.disabled = false;
                if (bulkAssignUser) bulkAssignUser.disabled = false;
                AppUI.loading(bulkActionApplyBtn, false);
                bulkActionApplyBtn.textContent = 'اعمال';
            }
        });
    }
});
// ── Export: Contract List (Excel/PDF) ──
function toggleExportDropdown(event) {
    event.stopPropagation();
    var menu = document.querySelector('[data-export-menu]');
    if (!menu) return;
    menu.classList.toggle('hidden');
    if (!menu.classList.contains('hidden')) {
        var close = function(e) { menu.classList.add('hidden'); document.removeEventListener('click', close); };
        setTimeout(function() { document.addEventListener('click', close); }, 10);
    }
}
function exportContracts(format) {
    var params = new URLSearchParams(window.location.search);
    params.set('format', format);
    AppUI.loading(document.body, true);

    var dropdownBtn = document.getElementById('exportContractsDropdownBtn');
    var excelBtn = document.getElementById('exportContractsExcelBtn');
    var pdfBtn = document.getElementById('exportContractsPdfBtn');
    var targetBtn = format === 'excel' ? excelBtn : pdfBtn;

    var originalTargetHtml = targetBtn ? targetBtn.innerHTML : null;
    var originalDropdownHtml = dropdownBtn ? dropdownBtn.innerHTML : null;

    if (dropdownBtn) {
        dropdownBtn.disabled = true;
        dropdownBtn.classList.add('is-loading');
        dropdownBtn.innerHTML = '<span class="rhyno-spinner" style="width:12px;height:12px;border-width:2px;display:inline-block;vertical-align:middle;margin-left:4px;"></span>در حال خروجی...';
    }
    if (excelBtn) {
        excelBtn.disabled = true;
        excelBtn.classList.add('opacity-50', 'cursor-not-allowed');
    }
    if (pdfBtn) {
        pdfBtn.disabled = true;
        pdfBtn.classList.add('opacity-50', 'cursor-not-allowed');
    }
    if (targetBtn) {
        targetBtn.innerHTML = '<span class="rhyno-spinner" style="width:12px;height:12px;border-width:2px;display:inline-block;vertical-align:middle;margin-left:4px;"></span>در حال ساخت فایل...';
    }

    var ext = format === 'excel' ? 'xlsx' : format;
    fetch('/Contracts/Export/Contracts?' + params.toString())
        .then(function(r) {
            // Check Content-Type to detect error JSON vs actual file
            var contentType = r.headers.get('Content-Type') || '';
            if (!r.ok || contentType.indexOf('json') !== -1) {
                // Server returned an error (BadRequest JSON), parse it
                return r.json().then(function(errData) {
                    throw new Error(errData.message || 'Export failed (server error)');
                });
            }
            return r.blob();
        })
        .then(function(blob) {
            var url = URL.createObjectURL(blob);
            var a = document.createElement('a');
            a.href = url;
            a.download = 'Contracts_' + new Date().toISOString().slice(0, 10) + '.' + ext;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            URL.revokeObjectURL(url);
            AppUI.toast('Export completed.', 'success');
        })
        .catch(function(err) {
            AppUI.toast('Export failed: ' + err.message, 'danger');
            console.error('Export error:', err);
        })
        .finally(function() {
            AppUI.loading(document.body, false);
            if (dropdownBtn) {
                dropdownBtn.disabled = false;
                dropdownBtn.classList.remove('is-loading');
                if (originalDropdownHtml) dropdownBtn.innerHTML = originalDropdownHtml;
            }
            if (excelBtn) {
                excelBtn.disabled = false;
                excelBtn.classList.remove('opacity-50', 'cursor-not-allowed');
                if (format === 'excel' && originalTargetHtml) excelBtn.innerHTML = originalTargetHtml;
            }
            if (pdfBtn) {
                pdfBtn.disabled = false;
                pdfBtn.classList.remove('opacity-50', 'cursor-not-allowed');
                if (format === 'pdf' && originalTargetHtml) pdfBtn.innerHTML = originalTargetHtml;
            }
            var menu = document.querySelector('[data-export-menu]');
            if (menu) menu.classList.add('hidden');
        });
}

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

            AppUI.loading(bulkActionApplyBtn, true);
            bulkActionApplyBtn.textContent = 'در حال اجرا...';

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
    fetch('/Contracts/Export/Contracts?' + params.toString())
        .then(function(r) {
            if (!r.ok) throw new Error('Export failed');
            return r.blob();
        })
        .then(function(blob) {
            var url = URL.createObjectURL(blob);
            var a = document.createElement('a');
            a.href = url;
            a.download = 'Contracts_' + new Date().toISOString().slice(0, 10) + '.' + format;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            URL.revokeObjectURL(url);
            AppUI.toast('Export completed.', 'success');
        })
        .catch(function(err) {
            AppUI.toast('Export failed: ' + err.message, 'danger');
        })
        .finally(function() {
            AppUI.loading(document.body, false);
            var menu = document.querySelector('[data-export-menu]');
            if (menu) menu.classList.add('hidden');
        });
}

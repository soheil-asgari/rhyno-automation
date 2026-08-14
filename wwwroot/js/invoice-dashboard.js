/**
 * Invoice Dashboard — Client-Side Logic
 * Search, pagination, detail modal, tax recalculation.
 * Rahkaran/DimGray RTL design system.
 */
(function () {
    'use strict';
    const csrfToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    let currentPage = 1, currentId = null;
    const $ = (sel) => document.querySelector(sel);
    const $$ = (sel) => document.querySelectorAll(sel);

    function showLoading() {
        const tbody = $('#invTableBody');
        if (!tbody) return;
        tbody.innerHTML = '<tr><td colspan="9" class="text-center py-6"><div class="inline-flex items-center gap-2 text-slate-400"><span class="rhyno-spinner" style="width:18px;height:18px;border-width:2px;"></span><span>در حال بارگذاری...</span></div></td></tr>';
    }

    function showError(message) {
        const tbody = $('#invTableBody');
        if (!tbody) return;
        tbody.innerHTML = `<tr><td colspan="9" class="text-center py-6">
            <div class="flex flex-col items-center gap-2 text-rose-500">
                <i class="bi bi-exclamation-triangle text-lg"></i>
                <span>${message || 'دریافت اطلاعات با خطا مواجه شد.'}</span>
                <button type="button" class="mt-1 inline-flex items-center gap-1 px-3 py-1.5 border border-slate-200 dark:border-slate-700 rounded-lg text-xs font-bold text-slate-600 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-800 transition" onclick="window.invRetrySearch && invRetrySearch()">
                    <i class="bi bi-arrow-clockwise"></i> تلاش مجدد
                </button>
            </div>
        </td></tr>`;
    }

    function doSearch(page = 1) {
        currentPage = page;
        const params = new URLSearchParams({
            query: $('#invSearchInput')?.value || '',
            dateFrom: $('#invDateFrom')?.value || '',
            dateTo: $('#invDateTo')?.value || '',
            sellerFilter: $('#invSellerFilter')?.value || '',
            page,
            pageSize: 25
        });

        showLoading();

        fetch(`/InvoiceDashboard/Search?${params}`)
            .then(r => r.json())
            .then(data => {
                if (data.success) {
                    renderTable(data.items);
                    updatePagination(data.page, data.totalPages);
                } else {
                    showError(data.message || 'دریافت اطلاعات با خطا مواجه شد.');
                }
            })
            .catch(() => showError('ارتباط با سرور برقرار نشد. لطفاً دوباره تلاش کنید.'));
    }

    // Expose retry for the error state button
    window.invRetrySearch = () => doSearch(currentPage);

    function renderTable(items) {
        const tbody = $('#invTableBody');
        if (!tbody) return;

        if (!items || items.length === 0) {
            tbody.innerHTML = '<tr><td colspan="9" class="text-center text-muted">موردی یافت نشد.</td></tr>';
            return;
        }

        tbody.innerHTML = items.map(item => `
            <tr data-id="${item.id}">
                <td class="inv-cell-number">${esc(item.invoiceNumber)}</td>
                <td>${esc(item.dateShamsi)}</td>
                <td>${esc(item.sellerName)}</td>
                <td>${esc(item.buyerName)}</td>
                <td class="inv-cell-amount">${toPersian(item.totalAmount)}</td>
                <td class="inv-cell-amount">${toPersian(item.vatAmount)}</td>
                <td class="inv-cell-amount">${toPersian(item.finalAmount)}</td>
                <td><span class="inv-status-badge inv-status-${(item.status || '').toLowerCase()}">${esc(item.status)}</span></td>
                <td>
                    <button type="button" class="sg-btn sg-btn-sm sg-btn-secondary inv-detail-btn" data-id="${item.id}" title="جزئیات">
                        <i class="bi bi-eye"></i>
                    </button>
                </td>
            </tr>`).join('');

        bindDetailButtons();
    }

    function updatePagination(page, total) {
        const pageInfo = $('#invPageInfo');
        const prevPage = $('#invPrevPage');
        const nextPage = $('#invNextPage');

        if (pageInfo) pageInfo.textContent = `صفحه ${page} از ${total}`;
        if (prevPage) prevPage.disabled = page <= 1;
        if (nextPage) nextPage.disabled = page >= total;
    }

    function bindDetailButtons() {
        $$('.inv-detail-btn').forEach(btn => btn.addEventListener('click', () => openDetail(btn.getAttribute('data-id'))));
    }

    function openDetail(id) {
        currentId = id;
        fetch(`/InvoiceDashboard/Detail/${id}`)
            .then(r => r.text())
            .then(html => {
                const detailBody = $('#invDetailBody');
                const detailOverlay = $('#invDetailOverlay');

                if (detailBody) detailBody.innerHTML = html;
                if (detailOverlay) detailOverlay.classList.remove('hidden');
            })
            .catch(err => console.error('Detail load failed:', err));
    }

    function closeDetail() {
        const detailOverlay = $('#invDetailOverlay');
        if (detailOverlay) detailOverlay.classList.add('hidden');
        currentId = null;
    }

    async function recalculateTax() {
        if (!currentId) return;

        const btn = $('#invRecalcTaxBtn');
        if (btn) {
            btn.disabled = true;
            btn.textContent = 'در حال محاسبه...';
        }

        try {
            const resp = await fetch(`/InvoiceDashboard/RecalculateTax?id=${currentId}`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded',
                    'RequestVerificationToken': csrfToken
                }
            });

            const result = await resp.json();

            if (result.success) {
                alert(result.message);
                openDetail(currentId);
                doSearch(currentPage);
            } else {
                alert(result.message || 'خطا در محاسبه مجدد مالیات.');
            }
        } catch (err) {
            console.error(err);
            alert('خطا در محاسبه مجدد مالیات.');
        } finally {
            if (btn) {
                btn.disabled = false;
                btn.textContent = 'محاسبه مجدد مالیات';
            }
        }
    }

    function esc(s) {
        return (s || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    }

    function toPersian(n) {
        return (n || 0).toLocaleString('fa-IR');
    }

    document.addEventListener('DOMContentLoaded', () => {
        $('#invSearchBtn')?.addEventListener('click', () => doSearch(1));
        $('#invSearchInput')?.addEventListener('keydown', e => { if (e.key === 'Enter') doSearch(1); });
        $('#invClearBtn')?.addEventListener('click', () => {
            const invSearchInput = $('#invSearchInput');
            const invDateFrom = $('#invDateFrom');
            const invDateTo = $('#invDateTo');
            const invSellerFilter = $('#invSellerFilter');

            if (invSearchInput) invSearchInput.value = '';
            if (invDateFrom) invDateFrom.value = '';
            if (invDateTo) invDateTo.value = '';
            if (invSellerFilter) invSellerFilter.value = '';

            doSearch(1);
        });
        $('#invPrevPage')?.addEventListener('click', () => { if (currentPage > 1) doSearch(currentPage - 1); });
        $('#invNextPage')?.addEventListener('click', () => doSearch(currentPage + 1));
        $('#invDetailClose')?.addEventListener('click', closeDetail);
        $('#invDetailCancel')?.addEventListener('click', closeDetail);
        $('#invDetailOverlay')?.addEventListener('click', e => { if (e.target === $('#invDetailOverlay')) closeDetail(); });
        $('#invRecalcTaxBtn')?.addEventListener('click', recalculateTax);
        bindDetailButtons();
    });
})();

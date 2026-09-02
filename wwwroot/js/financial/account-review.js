/* ═══════════════════════════════════════════════════════════
   مرور حساب‌ها (Account Review) — Vanilla JS, ES6+
   ═══════════════════════════════════════════════════════════ */
(function () {
    'use strict';

    const state = {
        level: 1,
        levelName: 'گروه',
        rows: [],
        selectedCode: null,
        selectedName: null,
        page: 1,
        pageSize: 50,
        totalFiltered: 0,
        drillHistory: [],
    };

    const $ = (id) => document.getElementById(id);
    const qs = (sel, ctx) => (ctx || document).querySelector(sel);
    const qsa = (sel, ctx) => Array.from((ctx || document).querySelectorAll(sel));

    /* ═══ Safe DOM getter — null-safe .value read ═══ */
    const getSafeVal = (id, fallback = '') => {
        const el = document.getElementById(id);
        return el && el.value !== undefined ? el.value : fallback;
    };

    const levelNames = {
        1: 'گروه', 2: 'کل', 3: 'معین', 4: 'سطح چهار',
        5: 'سطح پنج', 6: 'سطح شش', 7: 'ارز', 8: 'پیگیری', 9: 'حساب تفصیلی'
    };

    const RTL_CHARS = /[\u0600-\u06FF\u0750-\u077F\u08A0-\u08FF\uFB50-\uFDFF\uFE70-\uFEFF]/;

    function formatNumber(value) {
        if (value === null || value === undefined) return '0';
        return Number(value).toLocaleString('fa-IR', { maximumFractionDigits: 2 });
    }

    function getSelectedMulti(menuId) {
        const menu = $(menuId);
        if (!menu) return [];
        return qsa('input[type="checkbox"]:checked', menu).map(cb => cb.value);
    }

    function updateMultiSummary(summaryId, menuId) {
        const summary = $(summaryId);
        const checked = getSelectedMulti(menuId);
        if (!checked.length) {
            summary.textContent = 'همه';
        } else {
            summary.textContent = checked.length + ' مورد انتخاب شدند';
        }
    }

    /* ═══ buildQuery — uses getSafeVal throughout ═══ */
    function buildQuery() {
        const params = new URLSearchParams();
        params.set('level', state.level);
        params.set('fromFiscalYear', getSafeVal('fromFiscalYear'));
        params.set('toFiscalYear', getSafeVal('toFiscalYear'));
        params.set('accountType', getSafeVal('accountType'));
        params.set('detailType', getSafeVal('detailType'));
        params.set('voucherTypes', getSelectedMulti('voucherTypeMenu').join(','));
        params.set('voucherStates', getSafeVal('voucherStates'));
        params.set('filter', getSafeVal('filterMode', 'month'));
        const wbEl = document.getElementById('withBalance');
        params.set('withBalance', wbEl && wbEl.checked ? 'true' : 'false');

        const mode = getSafeVal('filterMode', 'month');
        if (mode === 'month') {
            params.set('fromMonth', getSafeVal('fromMonth'));
            params.set('toMonth', getSafeVal('toMonth'));
        } else if (mode === 'date') {
            params.set('fromDate', getSafeVal('fromDate'));
            params.set('toDate', getSafeVal('toDate'));
        } else if (mode === 'number') {
            params.set('fromNumber', getSafeVal('fromNumber'));
            params.set('toNumber', getSafeVal('toNumber'));
        }
        return params.toString();
    }

    function renderRows() {
        const tbody = $('arTableBody');
        const foot = $('arTableFoot');
        tbody.innerHTML = '';

        if (!state.rows.length) {
            tbody.innerHTML = '<tr class="ar-empty"><td colspan="7">سطری برای نمایش وجود ندارد.</td></tr>';
            foot.style.display = 'none';
            $('arPagerSummary').textContent = 'موارد ۰ تا ۰';
            $('arCount').textContent = 'تعداد رکوردها: ۰';
            return;
        }

        const search = (getSafeVal('arSearchBox') || '').trim();
        let filtered = state.rows;
        if (search) {
            filtered = state.rows.filter(r =>
                (r.code || '').includes(search) || (r.name || '').includes(search)
            );
        }

        state.totalFiltered = filtered.length;

        const totalPages = Math.max(1, Math.ceil(filtered.length / state.pageSize));
        state.page = Math.min(state.page, totalPages);
        const start = (state.page - 1) * state.pageSize;
        const paged = filtered.slice(start, start + state.pageSize);

        paged.forEach((row) => {
            const tr = document.createElement('tr');
            tr.dataset.code = row.code || '';
            tr.dataset.name = row.name || '';
            tr.dataset.hasChildren = row.hasChildren ? 'true' : 'false';
            if (row.code === state.selectedCode) tr.classList.add('selected');

            const sources = (row.source || '').split('،').filter(Boolean);
            const badges = sources.map(s => `<span class="ar-source-badge">${s.trim()}</span>`).join(' ');

            const codeDisplay = row.hasChildren
                ? `<a href="#" class="ar-drill-link" data-drill="${row.code}">${row.code || ''}</a>`
                : (row.code || '');

            tr.innerHTML = `
                <td>${codeDisplay}</td>
                <td>${row.name || ''}</td>
                <td class="ar-num">${formatNumber(row.debit)}</td>
                <td class="ar-num">${formatNumber(row.credit)}</td>
                <td class="ar-num">${formatNumber(row.debitBalance || 0)}</td>
                <td class="ar-num">${formatNumber(row.creditBalance || 0)}</td>
                <td>${badges || '—'}</td>`;
            tbody.appendChild(tr);
        });

        // Drill-down click
        qsa('.ar-drill-link', tbody).forEach(a => {
            a.addEventListener('click', (e) => {
                e.preventDefault();
                e.stopPropagation();
                const code = a.dataset.drill;
                const row = state.rows.find(r => r.code === code);
                if (row && row.hasChildren) {
                    drillDown(row.code, row.name);
                }
            });
        });

        // Row click for selection
        qsa('tr', tbody).forEach(tr => {
            tr.addEventListener('click', () => {
                qsa('tr.selected', tbody).forEach(r => r.classList.remove('selected'));
                tr.classList.add('selected');
                state.selectedCode = tr.dataset.code;
                state.selectedName = tr.dataset.name;
            });
            tr.addEventListener('dblclick', () => {
                if (tr.dataset.hasChildren === 'true') {
                    drillDown(tr.dataset.code, tr.dataset.name);
                }
            });
        });

        // Footer totals
        const totalRows = state.rows;
        const totalDebit = totalRows.reduce((s, r) => s + (r.debit || 0), 0);
        const totalCredit = totalRows.reduce((s, r) => s + (r.credit || 0), 0);
        const totalDebitBal = totalRows.reduce((s, r) => s + (r.debitBalance || 0), 0);
        const totalCreditBal = totalRows.reduce((s, r) => s + (r.creditBalance || 0), 0);

        $('arTotalDebit').textContent = formatNumber(totalDebit);
        $('arTotalCredit').textContent = formatNumber(totalCredit);
        $('arTotalDebitBal').textContent = formatNumber(totalDebitBal);
        $('arTotalCreditBal').textContent = formatNumber(totalCreditBal);
        foot.style.display = '';

        // Pager
        const from = start + 1;
        const to = Math.min(start + state.pageSize, filtered.length);
        $('arPagerSummary').textContent = `موارد ${formatNumber(from)} تا ${formatNumber(to)}`;
        $('arCount').textContent = `تعداد رکوردها: ${formatNumber(state.totalFiltered)}`;

        renderPageSelect(totalPages);
    }

    function renderPageSelect(totalPages) {
        const sel = $('pageSelect');
        sel.innerHTML = '';
        for (let i = 1; i <= totalPages; i++) {
            const opt = document.createElement('option');
            opt.value = i;
            opt.textContent = `صفحه ${i}`;
            if (i === state.page) opt.selected = true;
            sel.appendChild(opt);
        }
        $('btnFirst').disabled = state.page <= 1;
        $('btnPrev').disabled = state.page <= 1;
        $('btnNext').disabled = state.page >= totalPages;
        $('btnLast').disabled = state.page >= totalPages;
    }

    function goToPage(page) {
        const totalPages = Math.max(1, Math.ceil(state.totalFiltered / state.pageSize));
        state.page = Math.max(1, Math.min(page, totalPages));
        renderRows();
    }

    function drillDown(code, name) {
        state.drillHistory.push({ level: state.level, code: state.selectedCode, name: state.selectedName });
        state.selectedCode = code;
        state.selectedName = name;
        fetchData();
    }

    function goBack() {
        if (state.drillHistory.length) {
            const prev = state.drillHistory.pop();
            state.level = prev.level;
            state.selectedCode = prev.code;
            state.selectedName = prev.name;
            qsa('.ar-level').forEach(b => {
                b.classList.toggle('active', Number(b.dataset.level) === state.level);
            });
            fetchData();
        }
    }

    /* ═══ fetchData — wrapped in try/catch with inline error ═══ */
    async function fetchData() {
        const tbody = $('arTableBody');
        tbody.innerHTML = '<tr class="ar-empty"><td colspan="7">در حال بارگذاری...</td></tr>';

        try {
            const qs = buildQuery();
            const url = state.selectedCode
                ? `/AccountReview/Children?parentCode=${encodeURIComponent(state.selectedCode)}&${qs}`
                : `/AccountReview/Data?${qs}`;

            const res = await fetch(url, { headers: { 'Accept': 'application/json' } });
            if (!res.ok) throw new Error('HTTP ' + res.status);
            const data = await res.json();

            state.rows = data.rows || [];
            state.page = 1;

            let path = data.accountPath || `${levelNames[state.level]}: —`;
            if (state.selectedCode) {
                const historyParts = state.drillHistory.map(h => `${levelNames[h.level] || 'حساب'}: ${h.code || h.name || ''}`);
                path = historyParts.join(' <span class="ar-path-sep">›</span> ') + ' <span class="ar-path-sep">›</span> ' + path;
            }
            $('arPath').innerHTML = path;

            renderRows();
        } catch (err) {
            console.error('Error loading review data:', err);
            tbody.innerHTML = '<tr class="ar-empty"><td colspan="7">⚠️ خطا در بارگذاری داده‌ها. لطفاً مجدد تلاش کنید.</td></tr>';
        }
    }

    function setLevel(level, btn) {
        state.level = level;
        state.levelName = levelNames[level] || 'گروه';
        state.selectedCode = null;
        state.selectedName = null;
        state.drillHistory = [];
        qsa('.ar-level').forEach(b => b.classList.remove('active'));
        if (btn) btn.classList.add('active');
        fetchData();
    }

    function updateFilterFields() {
        const mode = getSafeVal('filterMode');
        $('fromMonthField').classList.toggle('ar-hidden', mode !== 'month');
        $('toMonthField').classList.toggle('ar-hidden', mode !== 'month');
        $('fromDateField').classList.toggle('ar-hidden', mode !== 'date');
        $('toDateField').classList.toggle('ar-hidden', mode !== 'date');
        $('fromNumberField').classList.toggle('ar-hidden', mode !== 'number');
        $('toNumberField').classList.toggle('ar-hidden', mode !== 'number');
    }

    /* ═══ Filter panel: collapsed by default ═══ */
    function toggleFilterPanel() {
        const grid = $('filterGrid');
        const toggle = $('filterToggle');
        if (!grid) return;
        const isHidden = grid.classList.contains('ar-hidden');
        grid.classList.toggle('ar-hidden');
        toggle.textContent = isHidden ? '▾' : '›';
    }

    function toggleMultiMenu(btn) {
        const target = btn.dataset.target;
        const menu = qs(`.ar-multi-menu[data-menu="${target}"]`);
        if (!menu) return;
        qsa('.ar-multi-menu').forEach(m => {
            if (m !== menu) m.classList.add('ar-hidden');
        });
        menu.classList.toggle('ar-hidden');
    }

    function closeAllMenus() {
        qsa('.ar-multi-menu').forEach(m => m.classList.add('ar-hidden'));
        qsa('.ar-dropdown-menu').forEach(m => m.classList.add('ar-hidden'));
    }

    async function showTransactions() {
        const code = state.selectedCode;
        if (!code) return;

        try {
            const qs = new URLSearchParams();
            qs.set('level', state.level);
            qs.set('code', code);
            qs.set('fromFiscalYear', getSafeVal('fromFiscalYear'));
            qs.set('toFiscalYear', getSafeVal('toFiscalYear'));
            qs.set('voucherTypes', getSelectedMulti('voucherTypeMenu').join(','));

            const res = await fetch(`/AccountReview/Transactions?${qs.toString()}`, {
                headers: { 'Accept': 'application/json' }
            });
            if (!res.ok) throw new Error('HTTP ' + res.status);
            const data = await res.json();

            const tbody = $('txModalBody');
            tbody.innerHTML = '';
            if (!data || !data.length) {
                tbody.innerHTML = '<tr class="ar-empty"><td colspan="7">گردشی برای این حساب یافت نشد.</td></tr>';
            } else {
                data.forEach(tx => {
                    const tr = document.createElement('tr');
                    const balance = (tx.debit || 0) - (tx.credit || 0);
                    tr.innerHTML = `
                        <td>${tx.voucherNumber || ''}</td>
                        <td>${tx.voucherDate || ''}</td>
                        <td>${tx.description || ''}</td>
                        <td>${tx.journalType || ''}</td>
                        <td class="ar-num">${formatNumber(tx.debit)}</td>
                        <td class="ar-num">${formatNumber(tx.credit)}</td>
                        <td class="ar-num">${formatNumber(balance)}</td>
                        <td>${tx.sourceModule || '—'}</td>`;
                    tbody.appendChild(tr);
                });
            }
            $('txModal').classList.remove('ar-hidden');
        } catch (err) {
            console.error('Error loading transactions:', err);
        }
    }

    function printReport() {
        const win = window.open('', '_blank', 'width=900,height=700');
        if (!win) return;
        const rows = state.rows.map(r => `<tr>
            <td>${r.code || ''}</td>
            <td>${r.name || ''}</td>
            <td style="text-align:left">${formatNumber(r.debit)}</td>
            <td style="text-align:left">${formatNumber(r.credit)}</td>
            <td style="text-align:left">${formatNumber(r.debitBalance || 0)}</td>
            <td style="text-align:left">${formatNumber(r.creditBalance || 0)}</td>
        </tr>`).join('');
        win.document.write(`<html dir="rtl"><head><title>مرور حساب‌ها</title>
            <style>body{font-family:Vazir,Tahoma;padding:20px}table{width:100%;border-collapse:collapse;font-size:12px}
            th,td{border:1px solid #ccc;padding:6px 8px;text-align:right}th{background:#f1f5f9}
            .num{text-align:left}</style></head><body>
            <h2>مرور حساب‌ها — ${levelNames[state.level]}</h2>
            <table><thead><tr><th>کد</th><th>عنوان</th><th class="num">بدهکار</th><th class="num">بستانکار</th><th class="num">مانده بدهکار</th><th class="num">مانده بستانکار</th></tr></thead>
            <tbody>${rows}</tbody></table></body></html>`);
        win.document.close();
        win.print();
    }

    function exportCsv() {
        if (!state.rows.length) return;
        const header = ['کد', 'عنوان', 'بدهکار', 'بستانکار', 'مانده بدهکار', 'مانده بستانکار', 'منبع'];
        const lines = state.rows.map(r =>
            [r.code, r.name, r.debit, r.credit, r.debitBalance || 0, r.creditBalance || 0, r.source]
                .map(v => `"${(v ?? '').toString().replace(/"/g, '""')}"`).join(',')
        );
        const csv = '\uFEFF' + [header.join(','), ...lines].join('\n');
        const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
        const a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = 'account-review.csv';
        a.click();
        URL.revokeObjectURL(a.href);
    }

    function init() {
        // Level sidebar
        qsa('.ar-level').forEach(btn => {
            btn.addEventListener('click', () => setLevel(Number(btn.dataset.level), btn));
        });

        // Apply / Refresh — only on explicit user action
        $('btnApply').addEventListener('click', () => {
            state.selectedCode = null;
            state.drillHistory = [];
            fetchData();
        });
        $('btnRefilter').addEventListener('click', () => {
            state.selectedCode = null;
            state.drillHistory = [];
            fetchData();
        });

        // Print & export
        $('btnPrint').addEventListener('click', printReport);
        $('btnExport').addEventListener('click', exportCsv);

        // Transactions modal
        $('btnTransactions').addEventListener('click', showTransactions);
        qsa('[data-close]').forEach(el => {
            el.addEventListener('click', () => {
                const target = $(el.dataset.close);
                if (target) target.classList.add('ar-hidden');
            });
        });
        qsa('.ar-modal-overlay').forEach(overlay => {
            overlay.addEventListener('click', (e) => {
                if (e.target === overlay) overlay.classList.add('ar-hidden');
            });
        });

        // Filter toggle — legend click only, no fetchData
        $('filterLegend').addEventListener('click', toggleFilterPanel);

        // Filter mode change
        $('filterMode').addEventListener('change', updateFilterFields);

        // Multi-select dropdowns
        qsa('.ar-multi-btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.stopPropagation();
                toggleMultiMenu(btn);
            });
        });
        qsa('.ar-multi-menu input[type="checkbox"]').forEach(cb => {
            cb.addEventListener('change', () => {
                const menu = cb.closest('.ar-multi-menu');
                if (!menu) return;
                const menuId = menu.dataset.menu;
                const summaryId = menuId === 'detailType' ? 'sumDetailType' : 'sumVoucherTypes';
                updateMultiSummary(summaryId, menuId + 'Menu');
            });
        });

        // Review by dropdown
        $('btnReviewBy').addEventListener('click', (e) => {
            e.stopPropagation();
            const menu = qs('#dropReviewBy .ar-dropdown-menu');
            if (menu) menu.classList.toggle('ar-hidden');
        });
        qsa('#dropReviewBy .ar-opt').forEach(opt => {
            opt.addEventListener('click', () => {
                const radio = qs('input[type="radio"]', opt);
                if (radio) {
                    const levelMap = { group: 1, general: 2, subsidiary: 3, detail: 4 };
                    const lvl = levelMap[radio.value] || 1;
                    const btn = qs(`.ar-level[data-level="${lvl}"]`);
                    if (btn) setLevel(lvl, btn);
                }
                qs('#dropReviewBy .ar-dropdown-menu').classList.add('ar-hidden');
            });
        });

        // Search
        $('arSearchBox').addEventListener('input', () => {
            state.page = 1;
            renderRows();
        });

        // Pager
        $('btnFirst').addEventListener('click', () => goToPage(1));
        $('btnPrev').addEventListener('click', () => goToPage(state.page - 1));
        $('pageSelect').addEventListener('change', () => goToPage(Number(getSafeVal('pageSelect'))));
        $('btnNext').addEventListener('click', () => goToPage(state.page + 1));
        $('btnLast').addEventListener('click', () => goToPage(Infinity));

        // Global click to close dropdowns
        document.addEventListener('click', closeAllMenus);

        // Keyboard shortcuts
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape') {
                closeAllMenus();
                qsa('.ar-modal-overlay:not(.ar-hidden)').forEach(m => m.classList.add('ar-hidden'));
            }
            if (e.key === 'F5') {
                e.preventDefault();
                fetchData();
            }
        });

        updateFilterFields();
        // ⚡ DO NOT auto-fetchData on init; only load on explicit "بروز رسانی"
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
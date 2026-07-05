(() => {
    const form = document.getElementById('trialBalanceForm');
    const stateEl = document.getElementById('trialBalanceInitialState');
    const grid = document.getElementById('trialBalanceGrid');
    const head = document.getElementById('trialBalanceHead');
    const body = document.getElementById('trialBalanceBody');
    const foot = document.getElementById('trialBalanceFoot');
    const status = document.getElementById('trialBalanceStatus');
    const periodName = document.getElementById('trialBalancePeriodName');
    const periodRange = document.getElementById('trialBalancePeriodRange');
    const printPeriodLabel = document.getElementById('printPeriodLabel');
    const printGeneratedAt = document.getElementById('printGeneratedAt');
    const printReportCode = document.getElementById('printReportCode');
    const jsonLink = document.getElementById('trialBalanceJsonLink');
    const printButton = document.getElementById('trialBalancePrintButton');
    const sixColumnField = document.getElementById('sixColumnField');
    const modeButtons = Array.from(document.querySelectorAll('.tb-mode-button'));
    const fiscalPeriodSelect = document.getElementById('fiscalPeriodId');
    const includeMoatagh = document.getElementById('includeMoatagh');
    const groupByFloatingDetail = document.getElementById('groupByFloatingDetail');
    const nonZeroOnly = document.getElementById('nonZeroOnly');

    if (!form || !stateEl || !grid || !head || !body || !foot || !status || !periodName || !periodRange || !jsonLink || !sixColumnField) {
        return;
    }

    const initialState = JSON.parse(stateEl.textContent || '{}');
    const persianDate = new Intl.DateTimeFormat('fa-IR-u-ca-persian', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit'
    });
    const printDateTime = new Intl.DateTimeFormat('fa-IR-u-ca-persian', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit'
    });
    const amountFormatter = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });

    const getProp = (obj, camelName, pascalName = null) => {
        if (!obj || typeof obj !== 'object') {
            return undefined;
        }

        if (Object.prototype.hasOwnProperty.call(obj, camelName)) {
            return obj[camelName];
        }

        if (pascalName && Object.prototype.hasOwnProperty.call(obj, pascalName)) {
            return obj[pascalName];
        }

        return undefined;
    };

    const normalizeRow = (row) => ({
        accountCode: getProp(row, 'accountCode', 'AccountCode') || '',
        accountName: getProp(row, 'accountName', 'AccountName') || '',
        floatingDetailCode: getProp(row, 'floatingDetailCode', 'FloatingDetailCode') || null,
        floatingDetailName: getProp(row, 'floatingDetailName', 'FloatingDetailName') || null,
        openingDebit: Number(getProp(row, 'openingDebit', 'OpeningDebit') || 0),
        openingCredit: Number(getProp(row, 'openingCredit', 'OpeningCredit') || 0),
        periodDebit: Number(getProp(row, 'periodDebit', 'PeriodDebit') || 0),
        periodCredit: Number(getProp(row, 'periodCredit', 'PeriodCredit') || 0),
        closingDebit: Number(getProp(row, 'closingDebit', 'ClosingDebit') || 0),
        closingCredit: Number(getProp(row, 'closingCredit', 'ClosingCredit') || 0)
    });

    const normalizeReport = (report) => {
        const source = report || {};
        const rawRows = getProp(source, 'rows', 'Rows');
        return {
            fiscalPeriodId: getProp(source, 'fiscalPeriodId', 'FiscalPeriodId') || '',
            fiscalPeriodName: getProp(source, 'fiscalPeriodName', 'FiscalPeriodName') || '',
            startDate: getProp(source, 'startDate', 'StartDate') || null,
            endDate: getProp(source, 'endDate', 'EndDate') || null,
            openingDebitTotal: Number(getProp(source, 'openingDebitTotal', 'OpeningDebitTotal') || 0),
            openingCreditTotal: Number(getProp(source, 'openingCreditTotal', 'OpeningCreditTotal') || 0),
            periodDebitTotal: Number(getProp(source, 'periodDebitTotal', 'PeriodDebitTotal') || 0),
            periodCreditTotal: Number(getProp(source, 'periodCreditTotal', 'PeriodCreditTotal') || 0),
            closingDebitTotal: Number(getProp(source, 'closingDebitTotal', 'ClosingDebitTotal') || 0),
            closingCreditTotal: Number(getProp(source, 'closingCreditTotal', 'ClosingCreditTotal') || 0),
            rows: Array.isArray(rawRows) ? rawRows.map(normalizeRow) : []
        };
    };

    let currentState = {
        fiscalPeriodId: initialState.fiscalPeriodId,
        includeMoatagh: Boolean(initialState.includeMoatagh),
        groupByFloatingDetail: Boolean(initialState.groupByFloatingDetail),
        sixColumn: Boolean(initialState.sixColumn),
        report: normalizeReport(initialState.report)
    };

    let currentSort = { key: 'accountCode', direction: 'asc' };

    const getColumnOrder = (groupByFloatingDetail, sixColumn) => {
        const columns = ['accountCode', 'accountName'];
        if (groupByFloatingDetail) {
            columns.push('floatingDetailCode', 'floatingDetailName');
        }
        if (sixColumn) {
            columns.push('openingDebit', 'openingCredit');
        }
        columns.push('periodDebit', 'periodCredit', 'closingDebit', 'closingCredit');
        return columns;
    };

    const formatDate = (value) => {
        if (!value) {
            return '-';
        }

        const date = new Date(value);
        return Number.isNaN(date.getTime()) ? '-' : persianDate.format(date);
    };

    const formatAmount = (value) => {
        const numeric = Number(value || 0);
        return numeric === 0 ? '' : amountFormatter.format(numeric);
    };

    const activeSortClass = (key) => currentSort.key === key ? ' active-sort-col' : '';
    const sortIndicator = (key) => {
        if (currentSort.key !== key) {
            return '';
        }

        return currentSort.direction === 'asc' ? '▲' : '▼';
    };

    const createSortableHeader = (label, key, extraClass = '') =>
        `<th rowspan="2" class="${extraClass}${activeSortClass(key)}" data-sort-key="${key}" data-sort-indicator="${sortIndicator(key)}">${label}</th>`;

    const buildHeader = (groupByFloatingDetail, sixColumn) => {
        const firstRow = [];
        const secondRow = [];

        firstRow.push(createSortableHeader('کد معین', 'accountCode', 'tb-col-code'));
        firstRow.push(createSortableHeader('نام معین', 'accountName', 'tb-col-name'));

        if (groupByFloatingDetail) {
            firstRow.push(createSortableHeader('کد تفصیلی', 'floatingDetailCode', 'tb-col-code'));
            firstRow.push(createSortableHeader('نام تفصیلی', 'floatingDetailName', 'tb-col-name'));
        }

        if (sixColumn) {
            firstRow.push('<th colspan="2">مانده ابتدای دوره</th>');
            secondRow.push(`<th class="tb-col-amount${activeSortClass('openingDebit')}" data-sort-key="openingDebit" data-sort-indicator="${sortIndicator('openingDebit')}">بدهکار</th>`);
            secondRow.push(`<th class="tb-col-amount${activeSortClass('openingCredit')}" data-sort-key="openingCredit" data-sort-indicator="${sortIndicator('openingCredit')}">بستانکار</th>`);
        }

        firstRow.push('<th colspan="2">گردش طی دوره</th>');
        firstRow.push('<th colspan="2">مانده پایان دوره</th>');
        secondRow.push(`<th class="tb-col-amount${activeSortClass('periodDebit')}" data-sort-key="periodDebit" data-sort-indicator="${sortIndicator('periodDebit')}">بدهکار</th>`);
        secondRow.push(`<th class="tb-col-amount${activeSortClass('periodCredit')}" data-sort-key="periodCredit" data-sort-indicator="${sortIndicator('periodCredit')}">بستانکار</th>`);
        secondRow.push(`<th class="tb-col-amount${activeSortClass('closingDebit')}" data-sort-key="closingDebit" data-sort-indicator="${sortIndicator('closingDebit')}">بدهکار</th>`);
        secondRow.push(`<th class="tb-col-amount${activeSortClass('closingCredit')}" data-sort-key="closingCredit" data-sort-indicator="${sortIndicator('closingCredit')}">بستانکار</th>`);

        head.innerHTML = `<tr>${firstRow.join('')}</tr><tr>${secondRow.join('')}</tr>`;
    };

    const compareValues = (left, right, key) => {
        const leftValue = left?.[key];
        const rightValue = right?.[key];
        const leftNumeric = typeof leftValue === 'number';
        const rightNumeric = typeof rightValue === 'number';

        if (leftNumeric || rightNumeric) {
            return Number(leftValue || 0) - Number(rightValue || 0);
        }

        return String(leftValue || '').localeCompare(String(rightValue || ''), 'fa-IR', { numeric: true, sensitivity: 'base' });
    };

    const getVisibleRows = (report) => {
        const rows = Array.isArray(report.rows) ? [...report.rows] : [];
        const filtered = nonZeroOnly?.checked
            ? rows.filter((row) => Number(row.closingDebit || 0) !== 0 || Number(row.closingCredit || 0) !== 0)
            : rows;

        return filtered.sort((left, right) => {
            const result = compareValues(left, right, currentSort.key);
            return currentSort.direction === 'asc' ? result : -result;
        });
    };

    const buildBody = (report, groupByFloatingDetail, sixColumn) => {
        const rows = getVisibleRows(report);
        const fixedCols = groupByFloatingDetail ? 4 : 2;
        const totalCols = fixedCols + (sixColumn ? 6 : 4);
        const columnOrder = getColumnOrder(groupByFloatingDetail, sixColumn);
        const sortedColumnIndex = columnOrder.indexOf(currentSort.key);
        const sortedCellClass = (index) => index === sortedColumnIndex ? ' sorted-column-cell' : '';

        if (rows.length === 0) {
            body.innerHTML = `<tr><td colspan="${totalCols}" class="tb-empty">برای فیلترهای انتخاب‌شده، ردیفی یافت نشد.</td></tr>`;
            return;
        }

        body.innerHTML = rows.map((row) => {
            let columnIndex = 0;
            const cells = [
                `<td class="tb-cell-code${sortedCellClass(columnIndex++)}" data-sort-col="accountCode">${row.accountCode || ''}</td>`,
                `<td class="tb-cell-name${sortedCellClass(columnIndex++)}" data-sort-col="accountName">${row.accountName || ''}</td>`
            ];

            if (groupByFloatingDetail) {
                cells.push(`<td class="tb-cell-code${sortedCellClass(columnIndex++)}" data-sort-col="floatingDetailCode">${row.floatingDetailCode || '-'}</td>`);
                cells.push(`<td class="tb-cell-name${sortedCellClass(columnIndex++)}" data-sort-col="floatingDetailName">${row.floatingDetailName || '-'}</td>`);
            }

            if (sixColumn) {
                cells.push(`<td class="tb-cell-amount${sortedCellClass(columnIndex++)}" data-sort-col="openingDebit">${formatAmount(row.openingDebit)}</td>`);
                cells.push(`<td class="tb-cell-amount${sortedCellClass(columnIndex++)}" data-sort-col="openingCredit">${formatAmount(row.openingCredit)}</td>`);
            }

            cells.push(`<td class="tb-cell-amount${sortedCellClass(columnIndex++)}" data-sort-col="periodDebit">${formatAmount(row.periodDebit)}</td>`);
            cells.push(`<td class="tb-cell-amount${sortedCellClass(columnIndex++)}" data-sort-col="periodCredit">${formatAmount(row.periodCredit)}</td>`);
            cells.push(`<td class="tb-cell-amount${sortedCellClass(columnIndex++)}" data-sort-col="closingDebit">${formatAmount(row.closingDebit)}</td>`);
            cells.push(`<td class="tb-cell-amount${sortedCellClass(columnIndex++)}" data-sort-col="closingCredit">${formatAmount(row.closingCredit)}</td>`);
            return `<tr>${cells.join('')}</tr>`;
        }).join('');
    };

    const buildFoot = (report, groupByFloatingDetail, sixColumn) => {
        const fixedCols = groupByFloatingDetail ? 4 : 2;
        const closingDiff = Math.abs(Number(report.closingDebitTotal || 0) - Number(report.closingCreditTotal || 0));
        const isUnbalanced = closingDiff > 0.0001;
        const label = isUnbalanced
            ? `جمع کل <span class="tb-balance-flag">عدم توازن ${amountFormatter.format(closingDiff)}</span>`
            : 'جمع کل';

        const cells = [`<td colspan="${fixedCols}" class="tb-foot-label">${label}</td>`];

        if (sixColumn) {
            cells.push(`<td class="tb-cell-amount">${formatAmount(report.openingDebitTotal)}</td>`);
            cells.push(`<td class="tb-cell-amount">${formatAmount(report.openingCreditTotal)}</td>`);
        }

        cells.push(`<td class="tb-cell-amount">${formatAmount(report.periodDebitTotal)}</td>`);
        cells.push(`<td class="tb-cell-amount">${formatAmount(report.periodCreditTotal)}</td>`);
        cells.push(`<td class="tb-cell-amount">${formatAmount(report.closingDebitTotal)}</td>`);
        cells.push(`<td class="tb-cell-amount">${formatAmount(report.closingCreditTotal)}</td>`);

        foot.innerHTML = `<tr class="${isUnbalanced ? 'is-unbalanced' : ''}">${cells.join('')}</tr>`;
        status.classList.toggle('is-alert', isUnbalanced);
        if (isUnbalanced) {
            status.textContent = 'هشدار عدم توازن';
        }
    };

    const syncMeta = (report) => {
        const rangeLabel = `${formatDate(report.startDate)} تا ${formatDate(report.endDate)}`;
        periodName.textContent = report.fiscalPeriodName || 'بدون دوره';
        periodRange.textContent = rangeLabel;

        if (printPeriodLabel) {
            printPeriodLabel.textContent = `${report.fiscalPeriodName || 'بدون دوره'} | ${rangeLabel}`;
        }

        if (printGeneratedAt) {
            printGeneratedAt.textContent = `تاریخ گزارش‌گیری: ${printDateTime.format(new Date())}`;
        }

        if (printReportCode) {
            printReportCode.textContent = 'کد گزارش: FI-TR-04 / نسخه ۱.۰';
        }
    };

    const syncModeButtons = (sixColumn) => {
        modeButtons.forEach((button) => {
            button.classList.toggle('is-active', String(sixColumn) === button.dataset.sixColumn);
        });
    };

    const syncJsonLink = () => {
        const url = new URL(jsonLink.href, window.location.origin);
        url.searchParams.set('fiscalPeriodId', fiscalPeriodSelect?.value || '');
        url.searchParams.set('includeMoatagh', String(includeMoatagh?.checked || false));
        url.searchParams.set('groupByFloatingDetail', String(groupByFloatingDetail?.checked || false));
        url.searchParams.set('sixColumn', String(sixColumnField.value === 'true'));
        url.searchParams.set('format', 'json');
        jsonLink.href = url.pathname + url.search;
    };

    const render = () => {
        grid.dataset.mode = currentState.sixColumn ? 'six' : 'four';
        syncModeButtons(currentState.sixColumn);
        syncMeta(currentState.report);
        buildHeader(currentState.groupByFloatingDetail, currentState.sixColumn);
        buildBody(currentState.report, currentState.groupByFloatingDetail, currentState.sixColumn);
        buildFoot(currentState.report, currentState.groupByFloatingDetail, currentState.sixColumn);
        syncJsonLink();

        if (!status.classList.contains('is-alert')) {
            status.textContent = 'به‌روز';
        }
    };

    const fetchReport = async () => {
        status.classList.remove('is-alert');
        status.classList.add('is-loading');
        status.textContent = 'در حال بارگذاری...';

        const url = new URL(form.dataset.jsonUrl || form.action, window.location.origin);
        url.searchParams.set('fiscalPeriodId', fiscalPeriodSelect?.value || '');
        url.searchParams.set('includeMoatagh', String(includeMoatagh?.checked || false));
        url.searchParams.set('groupByFloatingDetail', String(groupByFloatingDetail?.checked || false));
        url.searchParams.set('sixColumn', String(sixColumnField.value === 'true'));
        url.searchParams.set('format', 'json');

        try {
            const response = await fetch(url.toString(), {
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }

            currentState = {
                fiscalPeriodId: fiscalPeriodSelect?.value || '',
                includeMoatagh: Boolean(includeMoatagh?.checked),
                groupByFloatingDetail: Boolean(groupByFloatingDetail?.checked),
                sixColumn: sixColumnField.value === 'true',
                report: normalizeReport(await response.json())
            };

            render();
        } catch (error) {
            status.classList.remove('is-loading');
            status.classList.add('is-alert');
            status.textContent = 'خطا در دریافت تراز';
            if (window.AppUI?.toast) {
                window.AppUI.toast('بازیابی تراز آزمایشی با خطا مواجه شد.', 'danger');
            }
            return;
        }

        status.classList.remove('is-loading');
    };

    modeButtons.forEach((button) => {
        button.addEventListener('click', () => {
            sixColumnField.value = button.dataset.sixColumn === 'true' ? 'true' : 'false';
            fetchReport();
        });
    });

    head.addEventListener('click', (event) => {
        const th = event.target.closest('th[data-sort-key]');
        if (!th) {
            return;
        }

        const nextKey = th.dataset.sortKey;
        if (!nextKey) {
            return;
        }

        if (currentSort.key === nextKey) {
            currentSort.direction = currentSort.direction === 'asc' ? 'desc' : 'asc';
        } else {
            currentSort = { key: nextKey, direction: 'asc' };
        }

        render();
    });

    nonZeroOnly?.addEventListener('change', render);

    printButton?.addEventListener('click', () => {
        if (printGeneratedAt) {
            printGeneratedAt.textContent = `تاریخ گزارش‌گیری: ${printDateTime.format(new Date())}`;
        }
        window.print();
    });

    [fiscalPeriodSelect, includeMoatagh, groupByFloatingDetail].forEach((element) => {
        element?.addEventListener('change', fetchReport);
    });

    form.addEventListener('submit', (event) => {
        event.preventDefault();
        fetchReport();
    });

    render();
})();

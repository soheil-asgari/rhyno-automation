(() => {
    const tables = document.querySelectorAll('[data-pro-table]');
    if (!tables.length) return;

    const normalize = value => (value || '').toString().trim().toLowerCase();
    const getRows = table => Array.from(table.tBodies[0]?.rows || []);

    const savePreference = async (key, state) => {
        try {
            localStorage.setItem(`pro-table:${key}`, JSON.stringify(state));
            await fetch(`/table-preferences/${encodeURIComponent(key)}`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(state)
            });
        } catch {
            localStorage.setItem(`pro-table:${key}`, JSON.stringify(state));
        }
    };

    const loadPreference = async key => {
        const local = localStorage.getItem(`pro-table:${key}`);
        if (local) {
            try { return JSON.parse(local); } catch { }
        }

        try {
            const response = await fetch(`/table-preferences/${encodeURIComponent(key)}`);
            if (response.ok) return await response.json();
        } catch { }

        return {};
    };

    const buildToolbar = table => {
        const key = table.dataset.proTable;
        const wrapper = document.createElement('div');
        wrapper.className = 'pro-table-toolbar';
        wrapper.innerHTML = `
            <div class="pro-table-tools">
                <input class="form-control form-control-sm pro-table-search" type="search" placeholder="فیلتر سریع همین جدول">
                <select class="form-select form-select-sm pro-table-page-size">
                    <option value="10">10 ردیف</option>
                    <option value="25">25 ردیف</option>
                    <option value="50">50 ردیف</option>
                    <option value="100">100 ردیف</option>
                </select>
                <button type="button" class="btn btn-sm btn-outline-secondary pro-table-columns"><i class="bi bi-layout-three-columns"></i></button>
                <button type="button" class="btn btn-sm btn-outline-success pro-table-export"><i class="bi bi-file-earmark-excel"></i></button>
                <button type="button" class="btn btn-sm btn-outline-dark pro-table-print"><i class="bi bi-printer"></i></button>
            </div>
            <div class="pro-table-columns-panel"></div>
            <div class="pro-table-status"></div>
        `;

        table.parentElement?.before(wrapper);
        return { key, toolbar: wrapper };
    };

    const toCsv = table => {
        const visibleColumns = Array.from(table.tHead.rows[0].cells).map((_, index) => !table.querySelector(`th:nth-child(${index + 1})`)?.classList.contains('pro-table-hidden'));
        const lines = [];
        const headers = Array.from(table.tHead.rows[0].cells).filter((_, index) => visibleColumns[index]).map(cell => `"${cell.innerText.replaceAll('"', '""')}"`);
        lines.push(headers.join(','));
        getRows(table)
            .filter(row => row.style.display !== 'none')
            .forEach(row => {
                lines.push(Array.from(row.cells).filter((_, index) => visibleColumns[index]).map(cell => `"${cell.innerText.trim().replaceAll('"', '""')}"`).join(','));
            });
        return '\uFEFF' + lines.join('\n');
    };

    const downloadCsv = (table, key) => {
        const blob = new Blob([toCsv(table)], { type: 'text/csv;charset=utf-8;' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `${key || 'table'}-filtered.csv`;
        link.click();
        URL.revokeObjectURL(url);
    };

    tables.forEach(async table => {
        const { key, toolbar } = buildToolbar(table);
        const state = Object.assign({ query: '', pageSize: 25, page: 1, hidden: [] }, await loadPreference(key));
        const search = toolbar.querySelector('.pro-table-search');
        const pageSize = toolbar.querySelector('.pro-table-page-size');
        const status = toolbar.querySelector('.pro-table-status');
        const columnsPanel = toolbar.querySelector('.pro-table-columns-panel');

        search.value = state.query || '';
        pageSize.value = String(state.pageSize || 25);

        Array.from(table.tHead.rows[0].cells).forEach((cell, index) => {
            cell.dataset.columnIndex = index;
            cell.classList.add('pro-table-sortable');
            const id = `${key}-col-${index}`;
            const label = document.createElement('label');
            label.className = 'form-check form-check-inline pro-table-column-toggle';
            label.innerHTML = `<input class="form-check-input" type="checkbox" id="${id}" data-column="${index}" ${state.hidden.includes(index) ? '' : 'checked'}><span class="form-check-label">${cell.innerText}</span>`;
            columnsPanel.appendChild(label);
        });

        const applyColumns = () => {
            Array.from(table.rows).forEach(row => {
                Array.from(row.cells).forEach((cell, index) => {
                    cell.classList.toggle('pro-table-hidden', state.hidden.includes(index));
                });
            });
        };

        const apply = () => {
            const query = normalize(search.value);
            const rows = getRows(table);
            const filtered = rows.filter(row => !query || normalize(row.innerText).includes(query));
            const pageCount = Math.max(1, Math.ceil(filtered.length / state.pageSize));
            state.page = Math.min(state.page || 1, pageCount);
            const start = (state.page - 1) * state.pageSize;
            const end = start + state.pageSize;

            rows.forEach(row => row.style.display = 'none');
            filtered.slice(start, end).forEach(row => row.style.display = '');
            status.innerHTML = `
                <span>${filtered.length} نتیجه</span>
                <button type="button" class="btn btn-sm btn-outline-secondary pro-prev" ${state.page <= 1 ? 'disabled' : ''}>قبلی</button>
                <span>${state.page} / ${pageCount}</span>
                <button type="button" class="btn btn-sm btn-outline-secondary pro-next" ${state.page >= pageCount ? 'disabled' : ''}>بعدی</button>
            `;
        };

        const persist = () => savePreference(key, {
            query: search.value,
            pageSize: Number(pageSize.value),
            page: state.page,
            hidden: state.hidden
        });

        toolbar.addEventListener('click', event => {
            if (event.target.closest('.pro-table-columns')) columnsPanel.classList.toggle('show');
            if (event.target.closest('.pro-table-export')) downloadCsv(table, key);
            if (event.target.closest('.pro-table-print')) window.print();
            if (event.target.closest('.pro-prev')) {
                state.page = Math.max(1, state.page - 1);
                apply();
                persist();
            }
            if (event.target.closest('.pro-next')) {
                state.page += 1;
                apply();
                persist();
            }
        });

        columnsPanel.addEventListener('change', event => {
            const input = event.target.closest('[data-column]');
            if (!input) return;
            const index = Number(input.dataset.column);
            state.hidden = input.checked ? state.hidden.filter(item => item !== index) : Array.from(new Set([...state.hidden, index]));
            applyColumns();
            persist();
        });

        search.addEventListener('input', () => {
            state.query = search.value;
            state.page = 1;
            apply();
            persist();
        });

        pageSize.addEventListener('change', () => {
            state.pageSize = Number(pageSize.value);
            state.page = 1;
            apply();
            persist();
        });

        table.tHead.addEventListener('click', event => {
            const header = event.target.closest('th');
            if (!header) return;
            const index = Number(header.dataset.columnIndex);
            const rows = getRows(table);
            const asc = header.dataset.sort !== 'asc';
            rows.sort((a, b) => normalize(a.cells[index]?.innerText).localeCompare(normalize(b.cells[index]?.innerText), 'fa') * (asc ? 1 : -1));
            rows.forEach(row => table.tBodies[0].appendChild(row));
            Array.from(table.tHead.querySelectorAll('th')).forEach(item => item.removeAttribute('data-sort'));
            header.dataset.sort = asc ? 'asc' : 'desc';
            apply();
        });

        applyColumns();
        apply();
    });
})();

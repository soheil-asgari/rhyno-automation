(() => {
    const gridId = 'Finance_Vouchers';
    const savedViewSelect = document.getElementById('savedViewSelect');
    const savedViewName = document.getElementById('savedViewName');
    const saveViewButton = document.getElementById('saveViewButton');
    const grid = document.getElementById('financeVoucherGrid');
    const voucherForm = document.getElementById('voucherForm');
    const voucherTableBody = document.getElementById('voucherLinesBody');
    const voucherSubmitButton = document.getElementById('saveVoucherButton') || voucherForm?.querySelector('button[type="submit"], .ledger-submit');
    const addVoucherRowButton = document.getElementById('addVoucherRowButton');
    const removeVoucherRowButton = document.getElementById('removeVoucherRowButton');
    const voucherEntryMode = document.getElementById('voucherEntryMode');
    const workflowToolbar = document.getElementById('voucherWorkflowToolbar');
    const workflowCurrentStatus = document.getElementById('workflowCurrentStatus');
    const workflowActionButtons = Array.from(document.querySelectorAll('.workflow-action-button'));
    const antiforgeryToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const voucherFieldOrder = ['account', 'floatingDetail', 'currency', 'rate', 'foreign', 'debit', 'credit', 'narration'];
    const accountOptions = JSON.parse(voucherTableBody?.dataset.accountOptions || '[]');
    const currencyOptions = JSON.parse(voucherTableBody?.dataset.currencyOptions || '[]');
    const floatingDetailCache = new Map();
    const selectedVoucher = { id: null, status: null };

    function setupTabs() {
        const tabs = Array.from(document.querySelectorAll('[data-ledger-tab]'));
        const panes = Array.from(document.querySelectorAll('[data-ledger-pane]'));
        if (tabs.length === 0 || panes.length === 0) return;

        const activate = (name) => {
            tabs.forEach((tab) => {
                const isActive = tab.dataset.ledgerTab === name;
                tab.classList.toggle('is-active', isActive);
                tab.setAttribute('aria-selected', isActive ? 'true' : 'false');
                tab.tabIndex = isActive ? 0 : -1;
            });
            panes.forEach((pane) => pane.classList.toggle('is-active', pane.dataset.ledgerPane === name));
        };

        const moveFocus = (currentIndex, delta) => {
            const nextIndex = (currentIndex + delta + tabs.length) % tabs.length;
            const nextTab = tabs[nextIndex];
            activate(nextTab.dataset.ledgerTab);
            nextTab.focus();
        };

        tabs.forEach((tab, index) => {
            tab.addEventListener('click', () => activate(tab.dataset.ledgerTab));
            tab.addEventListener('keydown', (event) => {
                if (event.key === 'ArrowLeft') {
                    event.preventDefault();
                    moveFocus(index, 1);
                } else if (event.key === 'ArrowRight') {
                    event.preventDefault();
                    moveFocus(index, -1);
                } else if (event.key === 'Home') {
                    event.preventDefault();
                    activate(tabs[0].dataset.ledgerTab);
                    tabs[0].focus();
                } else if (event.key === 'End') {
                    event.preventDefault();
                    activate(tabs[tabs.length - 1].dataset.ledgerTab);
                    tabs[tabs.length - 1].focus();
                }
            });
        });

        activate(tabs.find((tab) => tab.classList.contains('is-active'))?.dataset.ledgerTab || tabs[0].dataset.ledgerTab);
    }

    function parseNumber(value) {
        const parsed = Number.parseFloat(value);
        return Number.isFinite(parsed) ? parsed : 0;
    }

    function isReadOnlyStatus(status) {
        return status === 'Reviewed' || status === 'Approved' || status === 'Permanent';
    }

    function setControlDisabled(control, disabled) {
        if (!control) return;
        control.disabled = disabled;
        control.classList.toggle('is-disabled', disabled);
    }

    function enforceMutualExclusivity(row, changedField) {
        const debit = row.querySelector('.line-debit');
        const credit = row.querySelector('.line-credit');
        if (!debit || !credit) return;

        const debitValue = parseNumber(debit.value);
        const creditValue = parseNumber(credit.value);

        if (changedField === 'debit' && debitValue > 0) {
            credit.value = '0.00';
            setControlDisabled(credit, true);
            setControlDisabled(debit, false);
            return;
        }

        if (changedField === 'credit' && creditValue > 0) {
            debit.value = '0.00';
            setControlDisabled(debit, true);
            setControlDisabled(credit, false);
            return;
        }

        if (debitValue <= 0 && creditValue <= 0) {
            setControlDisabled(debit, false);
            setControlDisabled(credit, false);
            return;
        }

        setControlDisabled(credit, debitValue > 0);
        setControlDisabled(debit, creditValue > 0);
    }

    function recalculateLine(row) {
        const currency = row.querySelector('.currency-id-field');
        const rate = row.querySelector('.fx-rate');
        const foreign = row.querySelector('.fx-foreign');
        const debit = row.querySelector('.line-debit');
        const credit = row.querySelector('.line-credit');
        if (!currency || !rate || !foreign || !debit || !credit || !currency.value || !foreign.value) return;

        const amount = Math.round(Math.abs(parseNumber(foreign.value) * parseNumber(rate.value)) * 100) / 100;
        if (parseNumber(credit.value) > 0 && parseNumber(debit.value) === 0) {
            credit.value = amount.toFixed(2);
            enforceMutualExclusivity(row, 'credit');
            return;
        }

        debit.value = amount.toFixed(2);
        credit.value = '0.00';
        enforceMutualExclusivity(row, 'debit');
    }

    async function loadLatestRate(row) {
        const currencyInput = row.querySelector('.currency-lookup-input');
        const currency = row.querySelector('.currency-id-field');
        const rate = row.querySelector('.fx-rate');
        const voucherDate = document.querySelector('[name="Voucher.VoucherDate"]');
        if (!currency?.value || !rate) {
            if (rate) rate.value = '1';
            return;
        }

        const url = `/Financial/GetLatestCurrencyRate?currencyId=${encodeURIComponent(currency.value)}&rateDate=${encodeURIComponent(voucherDate?.value || '')}`;
        const response = await fetch(url, { headers: { Accept: 'application/json' } });
        if (!response.ok) return;

        const payload = await response.json();
        rate.value = payload.exchangeRate || 1;
        if (currencyInput && !currencyInput.value && payload.displayName) {
            currencyInput.value = payload.displayName;
        }
        recalculateLine(row);
    }

    function focusCell(cell) {
        if (!cell) return;
        cell.focus();
        if (typeof cell.select === 'function' && cell.tagName === 'INPUT') {
            cell.select();
        }
    }

    function getRowCells(row) {
        return voucherFieldOrder.map((field) => row.querySelector(`[data-nav="${field}"]`)).filter(Boolean);
    }

    function getNextEditableCell(row, currentField) {
        const currentIndex = voucherFieldOrder.indexOf(currentField);
        if (currentIndex === -1) return null;
        for (let index = currentIndex + 1; index < voucherFieldOrder.length; index += 1) {
            const nextCell = row.querySelector(`[data-nav="${voucherFieldOrder[index]}"]`);
            if (nextCell && !nextCell.disabled) return nextCell;
        }
        return null;
    }

    function getPreviousEditableCell(row, currentField) {
        const currentIndex = voucherFieldOrder.indexOf(currentField);
        if (currentIndex === -1) return null;
        for (let index = currentIndex - 1; index >= 0; index -= 1) {
            const previousCell = row.querySelector(`[data-nav="${voucherFieldOrder[index]}"]`);
            if (previousCell && !previousCell.disabled) return previousCell;
        }
        return null;
    }

    function focusSameColumnRow(row, fieldName, delta) {
        if (!voucherTableBody) return false;
        const rows = Array.from(voucherTableBody.querySelectorAll('.voucher-line-row'));
        const currentIndex = rows.indexOf(row);
        if (currentIndex === -1) return false;
        const targetRow = rows[currentIndex + delta];
        if (!targetRow) return false;
        const targetCell = targetRow.querySelector(`[data-nav="${fieldName}"]`);
        if (!targetCell || targetCell.disabled) return false;
        focusCell(targetCell);
        return true;
    }

    function escapeHtml(value) {
        return String(value)
            .replaceAll('&', '&amp;')
            .replaceAll('<', '&lt;')
            .replaceAll('>', '&gt;')
            .replaceAll('"', '&quot;')
            .replaceAll("'", '&#39;');
    }

    function highlightMatch(value, term) {
        const normalizedTerm = String(term || '').trim();
        if (!normalizedTerm) return escapeHtml(value);
        const source = String(value || '');
        const index = source.toLowerCase().indexOf(normalizedTerm.toLowerCase());
        if (index < 0) return escapeHtml(source);
        const before = escapeHtml(source.slice(0, index));
        const match = escapeHtml(source.slice(index, index + normalizedTerm.length));
        const after = escapeHtml(source.slice(index + normalizedTerm.length));
        return `${before}<strong class="highlight-match">${match}</strong>${after}`;
    }

    function filterLookupItems(options, term) {
        const normalized = String(term || '').trim().toLowerCase();
        const decorate = (item) => ({
            ...item,
            highlightedCode: highlightMatch(item.code, term),
            highlightedName: highlightMatch(item.name, term)
        });

        if (!normalized) return options.slice(0, 12).map(decorate);
        return options
            .filter((item) =>
                item.code.toLowerCase().includes(normalized) ||
                item.name.toLowerCase().includes(normalized) ||
                item.text.toLowerCase().includes(normalized))
            .slice(0, 12)
            .map(decorate);
    }

    function renderLookupMenu(menu, items, activeIndex) {
        if (!menu) return;
        menu.innerHTML = '';
        if (items.length === 0) {
            menu.hidden = true;
            return;
        }

        const kind = menu.dataset.lookupKind || 'account';
        const fragment = document.createDocumentFragment();
        items.forEach((item, index) => {
            const entry = document.createElement('div');
            entry.className = `${kind}-lookup-item${index === activeIndex ? ' is-active' : ''}`;
            entry.dataset.index = String(index);
            entry.innerHTML = `<span class="${kind}-lookup-code">${item.highlightedCode}</span><span class="${kind}-lookup-name">${item.highlightedName}</span>`;
            fragment.appendChild(entry);
        });
        menu.appendChild(fragment);
        menu.hidden = false;
    }

    function closeLookup(lookup, inputSelector, menuSelector) {
        const menu = lookup.querySelector(menuSelector);
        const input = lookup.querySelector(inputSelector);
        lookup.dataset.lookupOpen = 'false';
        lookup.dataset.activeIndex = '-1';
        lookup.dataset.filteredItems = '[]';
        if (menu) {
            menu.hidden = true;
            menu.innerHTML = '';
        }
        if (input) {
            input.removeAttribute('aria-expanded');
        }
    }

    function getLookupElements(lookup) {
        return {
            hiddenField: lookup.querySelector('.account-id-field, .currency-id-field, .floating-detail-id-field'),
            input: lookup.querySelector('.account-lookup-input, .currency-lookup-input, .floating-detail-lookup-input'),
            menu: lookup.querySelector('.account-lookup-menu, .currency-lookup-menu, .floating-detail-lookup-menu')
        };
    }

    function selectLookupItem(lookup, item, config) {
        const { hiddenField, input } = getLookupElements(lookup);
        if (!hiddenField || !input) return;
        hiddenField.value = item.id;
        input.value = item.text;
        closeLookup(lookup, config.inputSelector, config.menuSelector);
    }

    function openLookup(lookup, term, activeIndex, options, config) {
        const { input, menu } = getLookupElements(lookup);
        if (!input || !menu) return;
        const items = filterLookupItems(options, term);
        lookup.dataset.lookupOpen = items.length > 0 ? 'true' : 'false';
        lookup.dataset.activeIndex = items.length > 0 ? String(Math.max(0, Math.min(activeIndex, items.length - 1))) : '-1';
        lookup.dataset.filteredItems = JSON.stringify(items);
        input.setAttribute('aria-expanded', items.length > 0 ? 'true' : 'false');
        menu.dataset.lookupKind = config.kind;
        renderLookupMenu(menu, items, Number.parseInt(lookup.dataset.activeIndex || '0', 10));
    }

    function moveLookupSelection(lookup, delta) {
        const items = JSON.parse(lookup.dataset.filteredItems || '[]');
        if (items.length === 0) return;
        const currentIndex = Number.parseInt(lookup.dataset.activeIndex || '0', 10);
        const nextIndex = (currentIndex + delta + items.length) % items.length;
        lookup.dataset.activeIndex = String(nextIndex);
        renderLookupMenu(lookup.querySelector('.account-lookup-menu, .currency-lookup-menu, .floating-detail-lookup-menu'), items, nextIndex);
    }

    function commitActiveLookup(lookup, config) {
        const items = JSON.parse(lookup.dataset.filteredItems || '[]');
        const activeIndex = Number.parseInt(lookup.dataset.activeIndex || '0', 10);
        if (items.length === 0 || activeIndex < 0 || activeIndex >= items.length) return false;
        selectLookupItem(lookup, items[activeIndex], config);
        return true;
    }

    function resolveLookupFromText(lookup, options, config) {
        const { input } = getLookupElements(lookup);
        if (!input) return false;
        const items = filterLookupItems(options, input.value);
        if (items.length === 0) return false;
        selectLookupItem(lookup, items[0], config);
        return true;
    }

    function normalizeRowIndexes() {
        if (!voucherTableBody) return;
        Array.from(voucherTableBody.querySelectorAll('.voucher-line-row')).forEach((row, index) => {
            row.dataset.rowIndex = String(index);
            const indexField = row.querySelector('.voucher-line-index');
            if (indexField) indexField.value = String(index);
            row.querySelectorAll('input, select, textarea').forEach((field) => {
                if (field.name) field.name = field.name.replace(/Voucher\.Lines\[\d+\]/g, `Voucher.Lines[${index}]`);
                if (field.id) field.id = field.id.replace(/Voucher_Lines_\d+__/g, `Voucher_Lines_${index}__`);
            });
        });
        voucherTableBody.dataset.nextIndex = String(voucherTableBody.querySelectorAll('.voucher-line-row').length);
    }

    function resetFloatingDetail(row) {
        const hidden = row.querySelector('.floating-detail-id-field');
        const input = row.querySelector('.floating-detail-lookup-input');
        const lookup = row.querySelector('.floating-detail-lookup');
        if (hidden) hidden.value = '';
        if (input) input.value = '';
        if (lookup) closeLookup(lookup, '.floating-detail-lookup-input', '.floating-detail-lookup-menu');
    }

    function setFloatingDetailEnabled(row, enabled) {
        const input = row.querySelector('.floating-detail-lookup-input');
        if (!input) return;
        setControlDisabled(input, !enabled);
        input.disabled = !enabled;
    }

    async function loadFloatingDetailOptions(subsidiaryAccountId) {
        if (!subsidiaryAccountId) return [];
        if (floatingDetailCache.has(subsidiaryAccountId)) return floatingDetailCache.get(subsidiaryAccountId);

        const response = await fetch(`/Financial/GetFloatingDetailsForSubsidiaryAccount?subsidiaryAccountId=${encodeURIComponent(subsidiaryAccountId)}`, {
            headers: { Accept: 'application/json' }
        });
        if (!response.ok) return [];
        const items = await response.json();
        floatingDetailCache.set(subsidiaryAccountId, items);
        return items;
    }

    async function syncFloatingDetailLookup(row) {
        const subsidiaryAccountId = row.querySelector('.account-id-field')?.value || '';
        resetFloatingDetail(row);
        if (!subsidiaryAccountId) {
            setFloatingDetailEnabled(row, false);
            return [];
        }

        const options = await loadFloatingDetailOptions(subsidiaryAccountId);
        row.dataset.floatingDetailOptions = JSON.stringify(options);
        setFloatingDetailEnabled(row, options.length > 0);
        return options;
    }

    function clearRowValues(row) {
        row.querySelectorAll('input, select').forEach((field) => {
            if (field.classList.contains('voucher-line-index')) return;
            if (field.classList.contains('account-id-field')) {
                field.value = '0';
                return;
            }
            if (field.classList.contains('currency-id-field') || field.classList.contains('floating-detail-id-field')) {
                field.value = '';
                return;
            }
            if (field.classList.contains('account-lookup-input') || field.classList.contains('currency-lookup-input') || field.classList.contains('floating-detail-lookup-input')) {
                field.value = '';
                return;
            }
            if (field.matches('select')) {
                field.selectedIndex = 0;
                return;
            }
            if (field.classList.contains('fx-rate')) {
                field.value = '1';
                return;
            }
            if (field.classList.contains('line-debit') || field.classList.contains('line-credit')) {
                field.value = '0.00';
                return;
            }
            field.value = '';
        });

        const accountLookup = row.querySelector('.account-lookup');
        const floatingLookup = row.querySelector('.floating-detail-lookup');
        const currencyLookup = row.querySelector('.currency-lookup');
        if (accountLookup) closeLookup(accountLookup, '.account-lookup-input', '.account-lookup-menu');
        if (floatingLookup) closeLookup(floatingLookup, '.floating-detail-lookup-input', '.floating-detail-lookup-menu');
        if (currencyLookup) closeLookup(currencyLookup, '.currency-lookup-input', '.currency-lookup-menu');
        setFloatingDetailEnabled(row, false);
        setControlDisabled(row.querySelector('.line-debit'), false);
        setControlDisabled(row.querySelector('.line-credit'), false);
    }

    function getActiveVoucherRow() {
        const activeElement = document.activeElement;
        return activeElement instanceof HTMLElement ? activeElement.closest('.voucher-line-row') : null;
    }

    function createVoucherRow() {
        if (!voucherTableBody || selectedVoucher.id !== null && isReadOnlyStatus(selectedVoucher.status)) return null;
        const sourceRow = voucherTableBody.querySelector('.voucher-line-row:last-child');
        if (!sourceRow) return null;

        const newRow = sourceRow.cloneNode(true);
        voucherTableBody.appendChild(newRow);
        clearRowValues(newRow);
        normalizeRowIndexes();
        wireVoucherRow(newRow);
        return newRow;
    }

    function removeVoucherRow(row) {
        if (!voucherTableBody || !row || selectedVoucher.id !== null && isReadOnlyStatus(selectedVoucher.status)) return;
        const rows = Array.from(voucherTableBody.querySelectorAll('.voucher-line-row'));
        if (rows.length <= 1) {
            clearRowValues(row);
            focusCell(row.querySelector('[data-nav="account"]'));
            return;
        }

        const currentIndex = rows.indexOf(row);
        row.remove();
        normalizeRowIndexes();
        const nextRows = Array.from(voucherTableBody.querySelectorAll('.voucher-line-row'));
        const focusTarget = nextRows[Math.max(0, currentIndex - 1)] || nextRows[currentIndex] || nextRows[0];
        focusCell(focusTarget?.querySelector('[data-nav="account"]'));
    }

    function handleRowAdvance(row, currentField) {
        const nextCell = getNextEditableCell(row, currentField);
        if (nextCell) {
            focusCell(nextCell);
            return;
        }
        const newRow = createVoucherRow();
        focusCell(newRow?.querySelector('[data-nav="account"]'));
    }

    function handleRowReverse(row, currentField) {
        const previousCell = getPreviousEditableCell(row, currentField);
        if (previousCell) {
            focusCell(previousCell);
            return;
        }
        if (!voucherTableBody) return;
        const rows = Array.from(voucherTableBody.querySelectorAll('.voucher-line-row'));
        const currentIndex = rows.indexOf(row);
        const previousRow = rows[currentIndex - 1];
        if (!previousRow) return;
        const previousRowCells = getRowCells(previousRow).filter((cell) => !cell.disabled);
        focusCell(previousRowCells[previousRowCells.length - 1]);
    }

    function getLookupByField(row, fieldName) {
        if (fieldName === 'account') return row.querySelector('.account-lookup');
        if (fieldName === 'floatingDetail') return row.querySelector('.floating-detail-lookup');
        if (fieldName === 'currency') return row.querySelector('.currency-lookup');
        return null;
    }

    function getLookupOptionsForRow(row, config) {
        if (config.kind === 'floating-detail') {
            return JSON.parse(row.dataset.floatingDetailOptions || '[]');
        }
        return config.options;
    }

    function applyVoucherSelectionState() {
        const status = selectedVoucher.status;
        const readOnly = selectedVoucher.id !== null && isReadOnlyStatus(status);
        document.querySelector('.ledger-ops')?.classList.toggle('is-readonly', readOnly);

        if (voucherEntryMode) {
            voucherEntryMode.textContent = selectedVoucher.id === null
                ? 'حالت ورود: سند جدید'
                : `حالت مشاهده: سند ${selectedVoucher.id} - ${status}`;
        }

        if (workflowCurrentStatus) {
            workflowCurrentStatus.textContent = selectedVoucher.id === null
                ? 'وضعیت جاری: بدون انتخاب'
                : `وضعیت جاری: ${status}`;
        }

        voucherForm?.querySelectorAll('.voucher-cell, .voucher-top-grid input, .voucher-top-grid select').forEach((field) => {
            if (!(field instanceof HTMLElement)) return;
            const shouldDisable = readOnly;
            if (field instanceof HTMLInputElement || field instanceof HTMLSelectElement || field instanceof HTMLTextAreaElement) {
                field.disabled = shouldDisable;
                if ('readOnly' in field) field.readOnly = shouldDisable;
                field.classList.toggle('is-disabled', shouldDisable);
            }
        });

        if (addVoucherRowButton) {
            addVoucherRowButton.disabled = readOnly;
            addVoucherRowButton.hidden = readOnly;
        }
        if (removeVoucherRowButton) {
            removeVoucherRowButton.disabled = readOnly;
            removeVoucherRowButton.hidden = readOnly;
        }
        if (voucherSubmitButton) {
            voucherSubmitButton.disabled = readOnly;
            voucherSubmitButton.hidden = readOnly;
        }

        workflowActionButtons.forEach((button) => {
            const targetStatus = button.dataset.targetStatus;
            const disabled =
                selectedVoucher.id === null ||
                status === 'Permanent' ||
                (status === 'Draft' && targetStatus !== 'Reviewed') ||
                (status === 'Reviewed' && targetStatus !== 'Approved') ||
                (status === 'Approved' && targetStatus !== 'Permanent') ||
                (status === targetStatus);
            button.disabled = disabled;
        });
    }

    function selectVoucherRow(row) {
        if (!grid) return;
        grid.querySelectorAll('.voucher-grid-row').forEach((item) => item.classList.toggle('is-selected', item === row));
        selectedVoucher.id = row ? Number.parseInt(row.dataset.voucherId || '', 10) : null;
        selectedVoucher.status = row?.dataset.voucherStatus || null;
        applyVoucherSelectionState();
    }

    async function handleWorkflowStatusChange(targetStatus) {
        if (!selectedVoucher.id || !workflowToolbar) return;
        const response = await fetch(workflowToolbar.dataset.changeStatusUrl || '', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                Accept: 'application/json',
                RequestVerificationToken: antiforgeryToken
            },
            body: JSON.stringify({
                voucherId: selectedVoucher.id,
                targetStatus
            })
        });

        const payload = await response.json().catch(() => ({ success: false, message: 'خطا در تغییر وضعیت سند.' }));
        if (!response.ok || payload.success === false) {
            window.alert(payload.message || 'تغییر وضعیت سند انجام نشد.');
            return;
        }

        const selectedRow = grid?.querySelector(`.voucher-grid-row[data-voucher-id="${selectedVoucher.id}"]`);
        if (selectedRow) {
            selectedRow.dataset.voucherStatus = payload.status;
            const statusCell = selectedRow.querySelector('[data-column-id="status"] .status-chip');
            if (statusCell) statusCell.textContent = payload.status;
            selectVoucherRow(selectedRow);
        }
    }

    function wireLookup(row, config) {
        const lookup = row.querySelector(config.lookupSelector);
        if (!lookup) return;
        const { input, menu } = getLookupElements(lookup);
        if (!input || !menu) return;
        menu.dataset.lookupKind = config.kind;

        input.addEventListener('input', async () => {
            const hiddenField = lookup.querySelector(config.hiddenSelector);
            if (hiddenField) hiddenField.value = '';
            const options = getLookupOptionsForRow(row, config);
            openLookup(lookup, input.value, 0, options, config);
        });

        input.addEventListener('focus', async () => {
            if (config.kind === 'floating-detail' && input.disabled) return;
            if (config.kind === 'floating-detail' && input.value.trim().length === 0) {
                const options = getLookupOptionsForRow(row, config);
                if (options.length > 0) {
                    openLookup(lookup, '', 0, options, config);
                }
                return;
            }
            if (input.value.trim().length > 0) {
                openLookup(lookup, input.value, 0, getLookupOptionsForRow(row, config), config);
            }
        });

        input.addEventListener('blur', () => {
            window.setTimeout(() => closeLookup(lookup, config.inputSelector, config.menuSelector), 120);
        });

        input.addEventListener('keydown', async (event) => {
            const isLookupOpen = lookup.dataset.lookupOpen === 'true';

            if (event.key === 'ArrowDown' && isLookupOpen) {
                event.preventDefault();
                moveLookupSelection(lookup, 1);
                return;
            }

            if (event.key === 'ArrowUp' && isLookupOpen) {
                event.preventDefault();
                moveLookupSelection(lookup, -1);
                return;
            }

            if (event.key === 'Enter' && isLookupOpen) {
                event.preventDefault();
                if (commitActiveLookup(lookup, config)) {
                    if (config.fieldName === 'account') {
                        await syncFloatingDetailLookup(row);
                    } else if (config.fieldName === 'currency') {
                        await loadLatestRate(row);
                    }
                    handleRowAdvance(row, config.fieldName);
                }
                return;
            }

            if ((event.key === 'Enter' || event.key === 'Tab') && !isLookupOpen) {
                if (event.shiftKey) return;
                event.preventDefault();
                resolveLookupFromText(lookup, getLookupOptionsForRow(row, config), config);
                if (config.fieldName === 'account') {
                    await syncFloatingDetailLookup(row);
                } else if (config.fieldName === 'currency') {
                    await loadLatestRate(row);
                }
                handleRowAdvance(row, config.fieldName);
                return;
            }

            if (event.key === 'Tab' && isLookupOpen) {
                event.preventDefault();
                commitActiveLookup(lookup, config);
                if (config.fieldName === 'account') {
                    await syncFloatingDetailLookup(row);
                } else if (config.fieldName === 'currency') {
                    await loadLatestRate(row);
                }
                handleRowAdvance(row, config.fieldName);
                return;
            }

            if (event.key === 'Escape' && isLookupOpen) {
                event.preventDefault();
                closeLookup(lookup, config.inputSelector, config.menuSelector);
            }
        });

        menu.addEventListener('mousedown', async (event) => {
            const item = event.target.closest(`.${config.kind}-lookup-item`);
            if (!item) return;
            event.preventDefault();
            const items = JSON.parse(lookup.dataset.filteredItems || '[]');
            const selected = items[Number.parseInt(item.dataset.index || '0', 10)];
            if (!selected) return;
            selectLookupItem(lookup, selected, config);
            if (config.fieldName === 'account') {
                await syncFloatingDetailLookup(row);
            } else if (config.fieldName === 'currency') {
                await loadLatestRate(row);
            }
            handleRowAdvance(row, config.fieldName);
        });
    }

    function wireVoucherRow(row) {
        wireLookup(row, {
            kind: 'account',
            fieldName: 'account',
            options: accountOptions,
            lookupSelector: '.account-lookup',
            inputSelector: '.account-lookup-input',
            hiddenSelector: '.account-id-field',
            menuSelector: '.account-lookup-menu'
        });
        wireLookup(row, {
            kind: 'floating-detail',
            fieldName: 'floatingDetail',
            options: [],
            lookupSelector: '.floating-detail-lookup',
            inputSelector: '.floating-detail-lookup-input',
            hiddenSelector: '.floating-detail-id-field',
            menuSelector: '.floating-detail-lookup-menu'
        });
        wireLookup(row, {
            kind: 'currency',
            fieldName: 'currency',
            options: currencyOptions,
            lookupSelector: '.currency-lookup',
            inputSelector: '.currency-lookup-input',
            hiddenSelector: '.currency-id-field',
            menuSelector: '.currency-lookup-menu'
        });

        row.querySelector('.fx-rate')?.addEventListener('input', () => recalculateLine(row));
        row.querySelector('.fx-foreign')?.addEventListener('input', () => recalculateLine(row));
        row.querySelector('.line-debit')?.addEventListener('input', () => enforceMutualExclusivity(row, 'debit'));
        row.querySelector('.line-credit')?.addEventListener('input', () => enforceMutualExclusivity(row, 'credit'));

        getRowCells(row).forEach((cell) => {
            cell.addEventListener('keydown', (event) => {
                if (selectedVoucher.id !== null && isReadOnlyStatus(selectedVoucher.status)) {
                    event.preventDefault();
                    return;
                }

                const fieldName = cell.dataset.nav;
                const lookup = getLookupByField(row, fieldName);
                const isLookupOpen = lookup?.dataset.lookupOpen === 'true';
                const isAdvanceKey = event.key === 'Enter' || event.key === 'Tab';
                const isDeleteShortcut = event.key === 'Delete' || (event.altKey && event.key.toLowerCase() === 'd');

                if (!isLookupOpen && event.key === 'ArrowDown') {
                    if (focusSameColumnRow(row, fieldName, 1)) event.preventDefault();
                    return;
                }

                if (!isLookupOpen && event.key === 'ArrowUp') {
                    if (focusSameColumnRow(row, fieldName, -1)) event.preventDefault();
                    return;
                }

                if (event.key === 'Tab' && event.shiftKey) {
                    event.preventDefault();
                    handleRowReverse(row, fieldName);
                    return;
                }

                if ((fieldName === 'account' || fieldName === 'floatingDetail' || fieldName === 'currency') && isAdvanceKey) {
                    return;
                }

                if (fieldName !== 'account' && fieldName !== 'floatingDetail' && fieldName !== 'currency' && isAdvanceKey && !event.shiftKey) {
                    event.preventDefault();
                    handleRowAdvance(row, fieldName);
                    return;
                }

                if (isDeleteShortcut) {
                    event.preventDefault();
                    removeVoucherRow(row);
                }
            });
        });

        setFloatingDetailEnabled(row, false);
        enforceMutualExclusivity(row, '');
    }

    function setupVoucherGridShortcuts() {
        if (!voucherTableBody || !voucherForm) return;
        Array.from(voucherTableBody.querySelectorAll('.voucher-line-row')).forEach((row) => wireVoucherRow(row));
        normalizeRowIndexes();
        applyVoucherSelectionState();

        voucherForm.addEventListener('keydown', (event) => {
            const activeElement = document.activeElement;
            const isInsideVoucherGrid = activeElement instanceof HTMLElement && activeElement.closest('#voucherLinesTable');
            if (!isInsideVoucherGrid) return;
            if (selectedVoucher.id !== null && isReadOnlyStatus(selectedVoucher.status)) {
                event.preventDefault();
                return;
            }

            if (event.ctrlKey && event.key.toLowerCase() === 's') {
                event.preventDefault();
                voucherSubmitButton?.click();
                return;
            }

            if (event.key === 'Insert' || (event.altKey && event.key.toLowerCase() === 'n')) {
                event.preventDefault();
                const newRow = createVoucherRow();
                focusCell(newRow?.querySelector('[data-nav="account"]'));
            }
        });

        addVoucherRowButton?.addEventListener('click', () => {
            const newRow = createVoucherRow();
            focusCell(newRow?.querySelector('[data-nav="account"]'));
        });

        removeVoucherRowButton?.addEventListener('click', () => {
            const activeRow = getActiveVoucherRow() || voucherTableBody.querySelector('.voucher-line-row:last-child');
            if (activeRow) {
                removeVoucherRow(activeRow);
            }
        });
    }

    function setupVoucherWorkflow() {
        if (!grid) return;
        grid.querySelectorAll('.voucher-grid-row').forEach((row) => {
            row.addEventListener('click', () => selectVoucherRow(row));
        });

        workflowActionButtons.forEach((button) => {
            button.addEventListener('click', async () => {
                if (button.disabled) return;
                await handleWorkflowStatusChange(button.dataset.targetStatus || '');
            });
        });

        applyVoucherSelectionState();
    }

    function getCurrentColumnLayout() {
        if (!grid) return [];
        return Array.from(grid.querySelectorAll('thead th')).map((header, index) => ({
            columnId: header.dataset.columnId,
            order: index + 1,
            width: Math.round(header.getBoundingClientRect().width),
            isVisible: !header.hidden
        })).filter((item) => item.columnId);
    }

    function applyColumnLayout(columnLayoutJson) {
        if (!grid || !columnLayoutJson) return;
        let layout;
        try {
            layout = JSON.parse(columnLayoutJson);
        } catch {
            return;
        }

        const visibleColumns = new Set(layout.filter((item) => item.isVisible !== false).map((item) => item.columnId));
        grid.querySelectorAll('[data-column-id]').forEach((cell) => {
            cell.hidden = !visibleColumns.has(cell.dataset.columnId);
        });
    }

    async function loadSavedViews() {
        if (!savedViewSelect) return;
        const response = await fetch(`/api/saved-views/${encodeURIComponent(gridId)}`, { headers: { Accept: 'application/json' } });
        if (!response.ok) return;

        const views = await response.json();
        savedViewSelect.querySelectorAll('option:not(:first-child)').forEach((item) => item.remove());
        views.forEach((view) => {
            const option = document.createElement('option');
            option.value = view.id;
            option.textContent = view.name;
            option.dataset.columnLayoutJson = view.columnLayoutJson;
            option.dataset.filterQueryJson = view.filterQueryJson;
            savedViewSelect.appendChild(option);
        });
    }

    savedViewSelect?.addEventListener('change', () => {
        const selected = savedViewSelect.selectedOptions[0];
        applyColumnLayout(selected?.dataset.columnLayoutJson);
    });

    saveViewButton?.addEventListener('click', async () => {
        const name = savedViewName?.value?.trim();
        if (!name) {
            savedViewName?.focus();
            return;
        }

        const payload = {
            name,
            targetGridId: gridId,
            columnLayoutJson: JSON.stringify(getCurrentColumnLayout()),
            filterQueryJson: JSON.stringify({ logic: 'and', filters: [] })
        };

        const response = await fetch('/api/saved-views', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
            body: JSON.stringify(payload)
        });

        if (!response.ok) return;
        await loadSavedViews();
        if (savedViewName) savedViewName.value = '';
    });

    setupTabs();
    setupVoucherGridShortcuts();
    setupVoucherWorkflow();
    loadSavedViews();
})();

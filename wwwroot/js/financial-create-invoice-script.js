(() => {
            const pageData = (() => {
                const el = document.getElementById('invoiceCreateData');
                return el ? JSON.parse(el.textContent) : {};
            })();
            const body = document.getElementById('invoiceItemsBody');
            const tpl = document.getElementById('invoiceRowTemplate');
            const invoiceNumberInput = document.getElementById('InvoiceNumber');
            const invoiceTypeSelect = document.getElementById('InvoiceType');
            const invoiceNumberHint = document.getElementById('invoiceNumberHint');
            const submitButton = document.getElementById('submitInvoiceButton');
            const previewPanel = document.getElementById('invoicePreviewPanel');
            const previewButton = document.getElementById('previewInvoiceButton');
            const form = document.getElementById('invoiceForm');
            const purchaseOnlyFields = document.querySelectorAll('.purchase-only');
            const saleOnlyFields = document.querySelectorAll('.sale-only');
            const receiptSelect = document.getElementById('WarehouseReceiptId');
            const employerSelect = document.getElementById('EmployerId');
            const partyNameInput = document.getElementById('PartyName');

            const subTotalInput = document.querySelector('[name="SubTotal"]');
            const vatInput = document.querySelector('[name="VatAmount"]');
            const grandInput = document.querySelector('[name="GrandTotal"]');
            let duplicateInvoiceNumber = false;
            let previousReceiptId = receiptSelect?.value || '';
            const draftKey = 'rhyno.invoice.draft.create';

            function togglePurchaseFields() {
                const isPurchase = (invoiceTypeSelect?.value || '') === 'Purchase';
                purchaseOnlyFields.forEach(field => field.classList.toggle('d-none', !isPurchase));
                saleOnlyFields.forEach(field => field.classList.toggle('d-none', isPurchase));
                if (isPurchase) {
                    if (employerSelect) employerSelect.value = '';
                    if (partyNameInput) partyNameInput.readOnly = false;
                } else if (employerSelect && partyNameInput) {
                    const opt = employerSelect.options[employerSelect.selectedIndex];
                    partyNameInput.value = opt?.text && opt.value ? opt.text : '';
                    partyNameInput.readOnly = true;
                }
            }

            employerSelect?.addEventListener('change', () => {
                if (!partyNameInput) return;
                const opt = employerSelect.options[employerSelect.selectedIndex];
                partyNameInput.value = opt?.text && opt.value ? opt.text : '';
            });

            function updateNames() {
                [...body.querySelectorAll('tr')].forEach((row, index) => {
                    row.querySelectorAll('[data-field]').forEach(field => {
                        const fieldName = field.getAttribute('data-field');
                        field.name = `Items[${index}].${fieldName}`;
                    });
                });
            }

            function calculateRow(row) {
                const quantity = Number(row.querySelector('[data-field="Quantity"]')?.value || 0);
                const unitPrice = Number(row.querySelector('[data-field="UnitPrice"]')?.value || 0);

                const subTotal = quantity * unitPrice;
                const vat = subTotal * 0.10;
                const grand = subTotal + vat;

                row.querySelector('[data-field="LineSubTotal"]').value = subTotal.toFixed(2);
                row.querySelector('[data-field="LineVatAmount"]').value = vat.toFixed(2);
                row.querySelector('[data-field="LineGrandTotal"]').value = grand.toFixed(2);
            }

            function calculateTotals() {
                const rows = [...body.querySelectorAll('tr')];
                let totalSub = 0;
                let totalVat = 0;
                let totalGrand = 0;

                rows.forEach(row => {
                    totalSub += Number(row.querySelector('[data-field="LineSubTotal"]')?.value || 0);
                    totalVat += Number(row.querySelector('[data-field="LineVatAmount"]')?.value || 0);
                    totalGrand += Number(row.querySelector('[data-field="LineGrandTotal"]')?.value || 0);
                });

                subTotalInput.value = totalSub.toFixed(2);
                vatInput.value = totalVat.toFixed(2);
                grandInput.value = totalGrand.toFixed(2);
                renderPreview();
            }

            function renderPreview() {
                if (!previewPanel) return;
                const warnings = [];
                const rows = [...body.querySelectorAll('tr')];
                if (!invoiceNumberInput?.value?.trim()) warnings.push('شماره فاکتور وارد نشده است.');
                if (!(document.getElementById('PartyName')?.value || document.getElementById('EmployerId')?.value)) warnings.push('طرف معامله مشخص نشده است.');
                if (!rows.some(row => Number(row.querySelector('[data-field="Quantity"]')?.value || 0) > 0 && Number(row.querySelector('[data-field="UnitPrice"]')?.value || 0) > 0)) warnings.push('حداقل یک ردیف معتبر لازم است.');
                if (Number(subTotalInput.value || 0) <= 0) warnings.push('جمع جزء باید بزرگتر از صفر باشد.');
                previewPanel.innerHTML = `
                    <div class="fw-semibold mb-2">پیش‌نمایش سریع</div>
                    <div class="d-flex justify-content-between"><span>شماره</span><span>${invoiceNumberInput?.value || '-'}</span></div>
                    <div class="d-flex justify-content-between"><span>نوع</span><span>${invoiceTypeSelect?.value === 'Purchase' ? 'خرید' : 'فروش'}</span></div>
                    <div class="d-flex justify-content-between"><span>طرف معامله</span><span>${document.getElementById('PartyName')?.value || document.getElementById('EmployerId')?.selectedOptions?.[0]?.text || '-'}</span></div>
                    <hr class="my-2">
                    <div class="d-flex justify-content-between"><span>جمع جزء</span><strong>${subTotalInput.value || '0'}</strong></div>
                    <div class="d-flex justify-content-between"><span>VAT</span><strong>${vatInput.value || '0'}</strong></div>
                    <div class="d-flex justify-content-between"><span>جمع کل</span><strong>${grandInput.value || '0'}</strong></div>
                    <div class="mt-3">
                        <div class="small fw-semibold mb-1">هشدارها</div>
                        ${warnings.length ? `<ul class="small mb-0">${warnings.map(item => `<li>${item}</li>`).join('')}</ul>` : '<div class="small text-success">هشدار مشخصی وجود ندارد.</div>'}
                    </div>
                `;
                submitButton.disabled = Boolean(warnings.length) || duplicateInvoiceNumber;
            }

            function saveDraft() {
                const draft = {
                    invoiceNumber: invoiceNumberInput?.value || '',
                    invoiceType: invoiceTypeSelect?.value || '',
                    dateShamsi: document.getElementById('DateShamsi')?.value || '',
                    partyName: document.getElementById('PartyName')?.value || '',
                    nationalCodeOrEconomicId: document.getElementById('NationalCodeOrEconomicId')?.value || '',
                    notes: document.getElementById('Notes')?.value || '',
                    items: [...body.querySelectorAll('tr')].map(row => ({
                        productId: row.querySelector('[data-field="ProductId"]')?.value || '',
                        itemName: row.querySelector('[data-field="ItemName"]')?.value || '',
                        quantity: row.querySelector('[data-field="Quantity"]')?.value || '',
                        unitPrice: row.querySelector('[data-field="UnitPrice"]')?.value || ''
                    }))
                };
                localStorage.setItem(draftKey, JSON.stringify(draft));
            }

            function restoreDraft() {
                try {
                    const raw = localStorage.getItem(draftKey);
                    if (!raw) return;
                    const draft = JSON.parse(raw);
                    if (draft.invoiceNumber) invoiceNumberInput.value = draft.invoiceNumber;
                    if (draft.invoiceType) invoiceTypeSelect.value = draft.invoiceType;
                    if (draft.dateShamsi) document.getElementById('DateShamsi').value = draft.dateShamsi;
                    if (draft.partyName) document.getElementById('PartyName').value = draft.partyName;
                    if (draft.nationalCodeOrEconomicId) document.getElementById('NationalCodeOrEconomicId').value = draft.nationalCodeOrEconomicId;
                    if (draft.notes) document.getElementById('Notes').value = draft.notes;
                    if (Array.isArray(draft.items) && draft.items.length) {
                        clearRows();
                        draft.items.forEach(item => addRow(item));
                    }
                    renderPreview();
                } catch { }
            }

            function setInvoiceNumberState(isDuplicate, message) {
                duplicateInvoiceNumber = Boolean(isDuplicate);
                if (duplicateInvoiceNumber && message) {
                    invoiceNumberHint.textContent = message;
                    invoiceNumberHint.classList.remove('d-none');
                    invoiceNumberInput.classList.add('is-invalid');
                } else {
                    invoiceNumberHint.textContent = '';
                    invoiceNumberHint.classList.add('d-none');
                    invoiceNumberInput.classList.remove('is-invalid');
                }
                submitButton.disabled = duplicateInvoiceNumber;
            }

            async function checkInvoiceNumberDuplicate() {
                const invoiceNumber = (invoiceNumberInput?.value || '').trim();
                const invoiceType = invoiceTypeSelect?.value || 'Sale';

                if (!invoiceNumber) {
                    setInvoiceNumberState(false, '');
                    return;
                }

                const url = (pageData.checkUrl || '')
                    + `?invoiceNumber=${encodeURIComponent(invoiceNumber)}`
                    + `&invoiceType=${encodeURIComponent(invoiceType)}`;

                try {
                    const response = await fetch(url, { method: 'GET', headers: { 'X-Requested-With': 'XMLHttpRequest' } });
                    if (!response.ok) {
                        setInvoiceNumberState(false, '');
                        return;
                    }

                    const result = await response.json();
                    setInvoiceNumberState(result.isDuplicate, result.message || '');
                } catch {
                    setInvoiceNumberState(false, '');
                }
            }

            function attachRowEvents(row) {
                row.querySelectorAll('.line-calc').forEach(input => {
                    input.addEventListener('input', () => {
                        calculateRow(row);
                        calculateTotals();
                    });
                });
            }

            function addRow(item = null) {
                const clone = tpl.content.cloneNode(true);
                const row = clone.querySelector('tr');
                body.appendChild(clone);

                attachRowEvents(row);

                if (item) {
                    row.querySelector('[data-field="ProductId"]').value = item.productId || '';
                    row.querySelector('[data-field="ItemName"]').value = item.itemName || '';
                    row.querySelector('[data-field="Quantity"]').value = item.quantity || 0;
                    row.querySelector('[data-field="UnitPrice"]').value = item.unitPrice || 0;
                    row.querySelector('[data-field="LineSubTotal"]').value = item.lineSubTotal || 0;
                    row.querySelector('[data-field="LineVatAmount"]').value = item.lineVatAmount || 0;
                    row.querySelector('[data-field="LineGrandTotal"]').value = item.lineGrandTotal || 0;
                }

                calculateRow(row);
                updateNames();
                calculateTotals();
            }

            function clearRows() {
                body.innerHTML = '';
                updateNames();
                calculateTotals();
            }

            function hasManualRows() {
                const rows = [...body.querySelectorAll('tr')];
                return rows.some(row => {
                    const productId = (row.querySelector('[data-field="ProductId"]')?.value || '').trim();
                    const itemName = (row.querySelector('[data-field="ItemName"]')?.value || '').trim();
                    const quantity = Number(row.querySelector('[data-field="Quantity"]')?.value || 0);
                    const unitPrice = Number(row.querySelector('[data-field="UnitPrice"]')?.value || 0);
                    return Boolean(productId || itemName || quantity > 0 || unitPrice > 0);
                });
            }

            async function populateItemsFromReceipt(skipConfirm = false) {
                const isPurchase = (invoiceTypeSelect?.value || '') === 'Purchase';
                const receiptId = receiptSelect?.value;

                if (!isPurchase || !receiptId) {
                    previousReceiptId = receiptId || '';
                    return;
                }

                if (!skipConfirm && hasManualRows()) {
                    const shouldReplace = window.confirm('با انتخاب این رسید انبار، اقلام فعلی جدول پاک و جایگزین خواهند شد. آیا مطمئن هستید؟');
                    if (!shouldReplace) {
                        if (receiptSelect) {
                            receiptSelect.value = previousReceiptId;
                        }
                        return;
                    }
                }

                const url = (pageData.receiptItemsUrl || '')
                    + `?receiptId=${encodeURIComponent(receiptId)}`;

                try {
                    const response = await fetch(url, { method: 'GET', headers: { 'X-Requested-With': 'XMLHttpRequest' } });
                    if (!response.ok) {
                        if (receiptSelect) {
                            receiptSelect.value = previousReceiptId;
                        }
                        return;
                    }

                    const result = await response.json();
                    const mappedItems = Array.isArray(result?.items) ? result.items : [];
                    if (!mappedItems.length) {
                        clearRows();
                        addRow();
                        previousReceiptId = receiptId;
                        return;
                    }

                    clearRows();
                    mappedItems.forEach(item => addRow(item));
                    previousReceiptId = receiptId;
                } catch {
                    if (receiptSelect) {
                        receiptSelect.value = previousReceiptId;
                    }
                }
            }

            document.getElementById('addInvoiceRow').addEventListener('click', () => addRow());

            body.addEventListener('click', (event) => {
                const removeButton = event.target.closest('.removeRow');
                if (!removeButton) return;
                removeButton.closest('tr')?.remove();
                if (!body.querySelector('tr')) {
                    addRow();
                }
                updateNames();
                calculateTotals();
            });

            invoiceNumberInput?.addEventListener('blur', checkInvoiceNumberDuplicate);
            invoiceNumberInput?.addEventListener('change', checkInvoiceNumberDuplicate);
            [invoiceNumberInput, invoiceTypeSelect, document.getElementById('DateShamsi'), document.getElementById('PartyName'), document.getElementById('NationalCodeOrEconomicId'), document.getElementById('Notes')].forEach(el => el?.addEventListener('input', saveDraft));
            invoiceTypeSelect?.addEventListener('change', () => {
                togglePurchaseFields();
                checkInvoiceNumberDuplicate();
            });
            receiptSelect?.addEventListener('change', () => populateItemsFromReceipt(false));
            previewButton?.addEventListener('click', renderPreview);
            form?.addEventListener('input', renderPreview);

            form?.addEventListener('submit', async (event) => {
                await checkInvoiceNumberDuplicate();
                if (duplicateInvoiceNumber) {
                    event.preventDefault();
                    invoiceNumberInput?.focus();
                }
            });

            const initialItems = pageData.items || [];
            if (initialItems.length) {
                initialItems.forEach(addRow);
            } else {
                addRow();
            }

            togglePurchaseFields();
            checkInvoiceNumberDuplicate();
            restoreDraft();
            setInterval(saveDraft, 10000);
            renderPreview();

            if ((invoiceTypeSelect?.value || '') === 'Purchase' && receiptSelect?.value) {
                populateItemsFromReceipt(true);
            }
        })();

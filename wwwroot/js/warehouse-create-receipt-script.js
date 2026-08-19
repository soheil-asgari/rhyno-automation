(() => {
            const body = document.getElementById('receiptItemsBody');
            const tpl = document.getElementById('receiptRowTemplate');
            const vendorSelect = document.getElementById('VendorId');
            const warehouseSelect = document.getElementById('WarehouseId');

            function money(v) { return Number(v || 0).toLocaleString('fa-IR'); }
            function qty(v) { return Number(v || 0).toFixed(3); }

            function updateNames() {
                [...body.querySelectorAll('tr')].forEach((row, index) => {
                    row.querySelectorAll('[data-field]').forEach(field => {
                        field.name = `Items[${index}].${field.getAttribute('data-field')}`;
                    });
                });
            }

            function updatePreview() {
                const rows = [...body.querySelectorAll('tr')];
                let totalQty = 0;
                let totalAmount = 0;
                rows.forEach(row => {
                    const q = Number(row.querySelector('[data-field="Quantity"]').value || 0);
                    const p = Number(row.querySelector('[data-field="UnitPrice"]').value || 0);
                    totalQty += q;
                    totalAmount += q * p;
                });

                document.getElementById('receiptRowCount').textContent = rows.length;
                document.getElementById('receiptTotalQty').textContent = qty(totalQty);
                document.getElementById('receiptTotalAmount').textContent = money(totalAmount);
                document.getElementById('previewReceiptNumber').textContent = document.getElementById('ReceiptNumber').value || '-';
                document.getElementById('previewDateShamsi').textContent = document.getElementById('DateShamsi').value || '-';
                document.getElementById('previewWarehouse').textContent = warehouseSelect.selectedOptions[0]?.text || '-';
                document.getElementById('previewVendor').textContent = vendorSelect.selectedOptions[0]?.text || 'انتخاب نشده';
                document.getElementById('previewLines').textContent = rows.length;
                document.getElementById('previewAmount').textContent = money(totalAmount);
            }

            function addRow(item) {
                const clone = tpl.content.cloneNode(true);
                const row = clone.querySelector('tr');
                body.appendChild(clone);
                if (item) {
                    row.querySelector('[data-field="ProductId"]').value = item.productId || '';
                    row.querySelector('[data-field="Quantity"]').value = item.quantity || '';
                    row.querySelector('[data-field="UnitPrice"]').value = item.unitPrice || '';
                }
                row.querySelectorAll('input, select').forEach(el => el.addEventListener('input', () => {
                    updateNames();
                    updatePreview();
                }));
                row.querySelector('[data-field="ProductId"]').addEventListener('change', updatePreview);
                updateNames();
                updatePreview();
            }

            document.getElementById('addReceiptRow').addEventListener('click', () => addRow());
            body.addEventListener('click', e => {
                if (!e.target.closest('.removeRow')) return;
                e.target.closest('tr')?.remove();
                if (!body.querySelector('tr')) addRow();
                updateNames();
                updatePreview();
            });

            [vendorSelect, warehouseSelect, document.getElementById('ReceiptNumber'), document.getElementById('DateShamsi')].forEach(el => {
                el?.addEventListener('input', updatePreview);
                el?.addEventListener('change', updatePreview);
            });

            const itemsEl = document.getElementById('whReceiptItems');
            const initialItems = itemsEl ? JSON.parse(itemsEl.textContent) : [];
            if (initialItems.length) initialItems.forEach(addRow); else addRow();
        })();

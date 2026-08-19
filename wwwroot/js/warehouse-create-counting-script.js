(() => {
            const stockEl = document.getElementById('whCountingStock');
            const stockSnapshot = stockEl ? JSON.parse(stockEl.textContent) : [];
            const body = document.getElementById('countingItemsBody');
            const tpl = document.getElementById('countingRowTemplate');
            const stockMap = new Map(stockSnapshot.map(item => [Number(item.productId), item]));
            const warehouseSelect = document.getElementById('WarehouseId');

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
                let totalDiscrepancy = 0;
                let warningCount = 0;
                rows.forEach(row => {
                    const productId = Number(row.querySelector('[data-field="ProductId"]').value || 0);
                    const physical = Number(row.querySelector('[data-field="PhysicalQuantity"]').value || 0);
                    const feedback = row.querySelector('.row-feedback');
                    const stock = stockMap.get(productId);
                    if (!productId) {
                        feedback.textContent = 'در انتظار انتخاب کالا';
                        feedback.className = 'small text-muted row-feedback';
                        row.querySelector('[data-field="SystemQuantity"]').value = '';
                        row.querySelector('[data-field="DiscrepancyQuantity"]').value = '';
                        return;
                    }

                    if (!stock) {
                        feedback.textContent = 'موجودی سیستمی پیدا نشد';
                        feedback.className = 'small text-warning row-feedback';
                        warningCount++;
                        row.querySelector('[data-field="SystemQuantity"]').value = 0;
                    } else {
                        row.querySelector('[data-field="SystemQuantity"]').value = stock.currentQuantity;
                        feedback.textContent = `حداقل: ${stock.minimumStock} | موجودی: ${Number(stock.currentQuantity).toFixed(3)}`;
                        feedback.className = 'small text-success row-feedback';
                    }

                    const system = Number(row.querySelector('[data-field="SystemQuantity"]').value || 0);
                    const discrepancy = physical - system;
                    row.querySelector('[data-field="DiscrepancyQuantity"]').value = discrepancy.toFixed(3);
                    totalDiscrepancy += discrepancy;
                    if (Math.abs(discrepancy) > 0) warningCount++;
                });

                document.getElementById('countingRowCount').textContent = rows.length;
                document.getElementById('countingDiscrepancy').textContent = qty(totalDiscrepancy);
                document.getElementById('countingWarnings').textContent = warningCount;
                document.getElementById('previewDocumentNumber').textContent = document.getElementById('DocumentNumber').value || '-';
                document.getElementById('previewDateShamsi').textContent = document.getElementById('DateShamsi').value || '-';
                document.getElementById('previewWarehouse').textContent = warehouseSelect.selectedOptions[0]?.text || '-';
                document.getElementById('previewLines').textContent = rows.length;
                document.getElementById('previewDiscrepancy').textContent = qty(totalDiscrepancy);
            }

            function addRow(item) {
                const clone = tpl.content.cloneNode(true);
                const row = clone.querySelector('tr');
                body.appendChild(clone);
                if (item) {
                    row.querySelector('[data-field="ProductId"]').value = item.productId || '';
                    row.querySelector('[data-field="SystemQuantity"]').value = item.systemQuantity || 0;
                    row.querySelector('[data-field="PhysicalQuantity"]').value = item.physicalQuantity || 0;
                    row.querySelector('[data-field="DiscrepancyQuantity"]').value = item.discrepancyQuantity || 0;
                }
                row.querySelectorAll('input, select').forEach(el => el.addEventListener('input', () => {
                    updateNames();
                    updatePreview();
                }));
                row.querySelector('[data-field="ProductId"]').addEventListener('change', updatePreview);
                updateNames();
                updatePreview();
            }

            document.getElementById('addCountingRow').addEventListener('click', () => addRow());
            body.addEventListener('click', e => {
                if (!e.target.closest('.removeRow')) return;
                e.target.closest('tr')?.remove();
                if (!body.querySelector('tr')) addRow();
                updateNames();
                updatePreview();
            });

            [warehouseSelect, document.getElementById('DocumentNumber'), document.getElementById('DateShamsi')].forEach(el => {
                el?.addEventListener('input', updatePreview);
                el?.addEventListener('change', updatePreview);
            });

            const itemsEl = document.getElementById('whCountingItems');
            const initialItems = itemsEl ? JSON.parse(itemsEl.textContent) : [];
            if (initialItems.length) initialItems.forEach(addRow); else addRow();
        })();

(() => {
            const stockEl = document.getElementById('whIssuanceStock');
            const stockSnapshot = stockEl ? JSON.parse(stockEl.textContent) : [];
            const body = document.getElementById('issuanceItemsBody');
            const tpl = document.getElementById('issuanceRowTemplate');
            const stockMap = new Map(stockSnapshot.map(item => [Number(item.productId), item]));
            const warehouseSelect = document.getElementById('WarehouseId');
            const employerSelect = document.getElementById('EmployerId');

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
                let warningCount = 0;
                rows.forEach(row => {
                    const productId = Number(row.querySelector('[data-field="ProductId"]').value || 0);
                    const q = Number(row.querySelector('[data-field="Quantity"]').value || 0);
                    totalQty += q;
                    const feedback = row.querySelector('.row-feedback');
                    const stock = stockMap.get(productId);
                    if (!productId) {
                        feedback.textContent = 'در انتظار انتخاب کالا';
                        feedback.className = 'text-[11px] text-slate-400 row-feedback';
                        return;
                    }
                    if (!stock) {
                        feedback.textContent = 'موجودی این کالا ثبت نشده است';
                        feedback.className = 'text-[11px] text-amber-600 row-feedback';
                        warningCount++;
                        return;
                    }
                    const remaining = stock.currentQuantity - q;
                    if (remaining < 0) {
                        feedback.textContent = `کمبود موجودی: ${remaining.toFixed(3)}`;
                        feedback.className = 'text-[11px] text-rose-600 font-bold row-feedback';
                        warningCount++;
                    } else if (remaining <= stock.minimumStock) {
                        feedback.textContent = `بعد از خروج به حداقل نزدیک می‌شود: ${remaining.toFixed(3)}`;
                        feedback.className = 'text-[11px] text-amber-600 row-feedback';
                        warningCount++;
                    } else {
                        feedback.textContent = `موجودی فعلی: ${Number(stock.currentQuantity).toFixed(3)}`;
                        feedback.className = 'text-[11px] text-emerald-600 row-feedback';
                    }
                });

                document.getElementById('issuanceRowCount').textContent = rows.length;
                document.getElementById('issuanceTotalQty').textContent = qty(totalQty);
                document.getElementById('issuanceWarnings').textContent = warningCount;
                document.getElementById('previewIssuanceNumber').textContent = document.getElementById('IssuanceNumber').value || '-';
                document.getElementById('previewDateShamsi').textContent = document.getElementById('DateShamsi').value || '-';
                document.getElementById('previewWarehouse').textContent = warehouseSelect.selectedOptions[0]?.text || '-';
                document.getElementById('previewEmployer').textContent = employerSelect.selectedOptions[0]?.text || 'انتخاب نشده';
                document.getElementById('previewLines').textContent = rows.length;
                document.getElementById('previewWarnings').textContent = warningCount;
            }

            function addRow(item) {
                const clone = tpl.content.cloneNode(true);
                const row = clone.querySelector('tr');
                body.appendChild(clone);
                if (item) {
                    row.querySelector('[data-field="ProductId"]').value = item.productId || '';
                    row.querySelector('[data-field="Quantity"]').value = item.quantity || '';
                }
                row.querySelectorAll('input, select').forEach(el => el.addEventListener('input', () => {
                    updateNames();
                    updatePreview();
                }));
                row.querySelector('[data-field="ProductId"]').addEventListener('change', updatePreview);
                updateNames();
                updatePreview();
            }

            document.getElementById('addIssuanceRow').addEventListener('click', () => addRow());
            body.addEventListener('click', e => {
                if (!e.target.closest('.removeRow')) return;
                e.target.closest('tr')?.remove();
                if (!body.querySelector('tr')) addRow();
                updateNames();
                updatePreview();
            });

            [warehouseSelect, employerSelect, document.getElementById('IssuanceNumber'), document.getElementById('DateShamsi')].forEach(el => {
                el?.addEventListener('input', updatePreview);
                el?.addEventListener('change', updatePreview);
            });

            const itemsEl = document.getElementById('whIssuanceItems');
            const initialItems = itemsEl ? JSON.parse(itemsEl.textContent) : [];
            if (initialItems.length) initialItems.forEach(addRow); else addRow();
        })();

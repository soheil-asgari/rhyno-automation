(() => {
            'use strict';

            const rowsContainer = document.getElementById('payrollRows');
            const rowTemplate = document.getElementById('payrollRowTemplate');
            const monthField = document.getElementById('payrollMonth');
            const yearField = document.getElementById('payrollYear');
            const addRowBtn = document.getElementById('addEmployeeRowBtn');
            const form = document.getElementById('payrollForm');

            // ── Anti-forgery token ────────────────────────────────────
            const getToken = () => {
                const input = document.querySelector('input[name="__RequestVerificationToken"]');
                if (input && input.value) return input.value;
                const meta = document.querySelector('meta[name="request-verification-token"]');
                return meta ? meta.getAttribute('content') : '';
            };

            // ── Number formatting helpers ─────────────────────────────
            const toNumber = (val) => {
                const n = parseFloat(String(val || '').replace(/[^\d.-]/g, ''));
                return isNaN(n) ? 0 : n;
            };
            const formatRial = (val) => {
                return Math.round(val).toLocaleString('fa-IR') + ' ریال';
            };

            // ── Row creation ──────────────────────────────────────────
            const createRow = (data) => {
                if (!rowsContainer || !rowTemplate) return null;
                const frag = rowTemplate.content.cloneNode(true);
                const tr = frag.querySelector('tr');
                const nameInput = tr.querySelector('[data-field="FullName"]');
                const idInput = tr.querySelector('[data-field="HumanCapitalEmployeeId"]');
                const baseInput = tr.querySelector('[data-field="BaseSalary"]');
                const insuranceInput = tr.querySelector('[data-field="Insurance"]');
                const taxInput = tr.querySelector('[data-field="Tax"]');
                const netInput = tr.querySelector('[data-field="NetPayable"]');
                const suggestions = tr.querySelector('.employee-suggestions');

                if (data) {
                    nameInput.value = data.fullName || '';
                    idInput.value = data.id || '';
                    baseInput.value = data.currentSalary != null ? data.currentSalary : '';
                    insuranceInput.value = data.insuranceDeduction != null ? data.insuranceDeduction : '';
                    taxInput.value = data.tax != null ? data.tax : '';
                    netInput.value = data.netPayable != null ? data.netPayable : '';
                }

                // ── Autocomplete wiring (Vanilla JS fetch → /HumanCapital/SearchEmployees) ──
                let debounceTimer = null;
                nameInput.addEventListener('input', () => {
                    clearTimeout(debounceTimer);
                    const q = nameInput.value.trim();
                    if (q.length < 2) {
                        suggestions.classList.add('hidden');
                        suggestions.innerHTML = '';
                        return;
                    }
                    debounceTimer = setTimeout(() => {
                        fetch('/HumanCapital/SearchEmployees?q=' + encodeURIComponent(q), {
                            credentials: 'same-origin',
                            headers: { 'Accept': 'application/json' }
                        })
                            .then(r => r.json())
                            .then(items => {
                                suggestions.innerHTML = '';
                                if (!items || !items.length) {
                                    suggestions.innerHTML = '<div class="px-3 py-2 text-[11px] text-slate-400">موردی یافت نشد.</div>';
                                    suggestions.classList.remove('hidden');
                                    return;
                                }
                                items.forEach(emp => {
                                    const item = document.createElement('button');
                                    item.type = 'button';
                                    item.className = 'w-full text-right px-3 py-2 text-[11px] text-slate-700 hover:bg-slate-50 dark:hover:bg-slate-800 transition cursor-pointer border-0 bg-transparent';
                                    item.innerHTML = '<span class="font-bold">' + emp.fullName + '</span>' +
                                        '<span class="block text-[10px] text-slate-400">کد: ' + emp.personnelCode + ' • ' + (emp.positionTitle || '—') + '</span>';
                                    item.addEventListener('click', () => {
                                        nameInput.value = emp.fullName;
                                        idInput.value = emp.id;
                                        baseInput.value = emp.currentSalary != null ? emp.currentSalary : '';
                                        insuranceInput.value = Math.round((toNumber(emp.currentSalary)) * 0.07 * 100) / 100;
                                        taxInput.value = 0;
                                        recalcRow(tr);
                                        suggestions.classList.add('hidden');
                                        suggestions.innerHTML = '';
                                    });
                                    suggestions.appendChild(item);
                                });
                                suggestions.classList.remove('hidden');
                            })
                            .catch(() => {
                                suggestions.innerHTML = '<div class="px-3 py-2 text-[11px] text-rose-500">خطا در جستجو.</div>';
                                suggestions.classList.remove('hidden');
                            });
                    }, 250);
                });

                // Close suggestions on outside click
                document.addEventListener('click', (e) => {
                    if (!tr.contains(e.target)) {
                        suggestions.classList.add('hidden');
                    }
                });

                // ── Row recalculation ────────────────────────────────
                const recalcRow = (row) => {
                    const base = toNumber(row.querySelector('[data-field="BaseSalary"]').value);
                    const insurance = toNumber(row.querySelector('[data-field="Insurance"]').value);
                    const tax = toNumber(row.querySelector('[data-field="Tax"]').value);
                    const net = base - insurance - tax;
                    row.querySelector('[data-field="NetPayable"]').value = Math.round(net * 100) / 100;
                    recalcTotals();
                };

                // Event delegation for numeric inputs
                [baseInput, insuranceInput, taxInput].forEach(inp => {
                    inp.addEventListener('input', () => recalcRow(tr));
                });

                rowsContainer.appendChild(tr);
                recalcTotals();
                return tr;
            };

            // ── Totals recalculation ─────────────────────────────────
            const recalcTotals = () => {
                let totalBase = 0, totalInsurance = 0, totalTax = 0, totalNet = 0;
                rowsContainer.querySelectorAll('tr').forEach(tr => {
                    totalBase += toNumber(tr.querySelector('[data-field="BaseSalary"]')?.value);
                    totalInsurance += toNumber(tr.querySelector('[data-field="Insurance"]')?.value);
                    totalTax += toNumber(tr.querySelector('[data-field="Tax"]')?.value);
                    totalNet += toNumber(tr.querySelector('[data-field="NetPayable"]')?.value);
                });
                document.getElementById('totalBaseSalary').textContent = formatRial(totalBase);
                document.getElementById('totalInsurance').textContent = formatRial(totalInsurance);
                document.getElementById('totalTax').textContent = formatRial(totalTax);
                document.getElementById('totalNetPayable').textContent = formatRial(totalNet);
            };

            // ── Add row button ───────────────────────────────────────
            addRowBtn?.addEventListener('click', () => createRow());

            // ── Form submit (Save) ───────────────────────────────────
            form?.addEventListener('submit', (e) => {
                e.preventDefault();

                const rows = [];
                rowsContainer.querySelectorAll('tr').forEach(tr => {
                    const id = tr.querySelector('[data-field="HumanCapitalEmployeeId"]')?.value;
                    if (!id) return;
                    rows.push({
                        humanCapitalEmployeeId: parseInt(id, 10),
                        employeeName: tr.querySelector('[data-field="FullName"]')?.value || '',
                        baseSalary: toNumber(tr.querySelector('[data-field="BaseSalary"]')?.value),
                        allowance: 0,
                        overtime: 0,
                        insuranceDeduction: toNumber(tr.querySelector('[data-field="Insurance"]')?.value),
                        tax: toNumber(tr.querySelector('[data-field="Tax"]')?.value),
                        netPayable: toNumber(tr.querySelector('[data-field="NetPayable"]')?.value)
                    });
                });

                if (!rows.length) {
                    alert('حداقل یک کارمند باید انتخاب شود.');
                    return;
                }

                const payload = {
                    month: parseInt(monthField?.value || '0', 10),
                    year: parseInt(yearField?.value || '0', 10),
                    finalize: false,
                    items: rows
                };

                const token = getToken();
                fetch('/Payroll/Save', {
                    method: 'POST',
                    credentials: 'same-origin',
                    headers: {
                        'Content-Type': 'application/json',
                        'Accept': 'application/json',
                        'RequestVerificationToken': token
                    },
                    body: JSON.stringify(payload)
                })
                    .then(async (response) => {
                        const data = await response.json().catch(() => ({}));
                        if (response.ok && data.success) {
                            alert(data.message || 'لیست حقوق با موفقیت ذخیره شد.');
                            if (data.redirectUrl) {
                                window.location.href = data.redirectUrl;
                            }
                        } else {
                            alert(data.message || 'خطا در ذخیره‌سازی.');
                        }
                    })
                    .catch(() => {
                        alert('ارتباط با سرور برقرار نشد.');
                    });
            });

            // ── Initialize: render existing rows from server model ──
            const itemsDataEl = document.getElementById('payrollItemsData');
            const rawItems = itemsDataEl ? JSON.parse(itemsDataEl.textContent) : [];
            const existingItems = rawItems.map(i => ({
                id: i.humanCapitalEmployeeId ?? i.HumanCapitalEmployeeId,
                fullName: i.employeeName ?? i.EmployeeName,
                currentSalary: i.baseSalary != null ? i.baseSalary : (i.BaseSalary != null ? i.BaseSalary : null),
                insuranceDeduction: i.insuranceDeduction != null ? i.insuranceDeduction : (i.InsuranceDeduction != null ? i.InsuranceDeduction : null),
                tax: i.tax != null ? i.tax : (i.Tax != null ? i.Tax : null),
                netPayable: i.netPayable != null ? i.netPayable : (i.NetPayable != null ? i.NetPayable : null)
            }));

            if (rowsContainer && rowTemplate) {
                if (existingItems && existingItems.length) {
                    existingItems.forEach(item => createRow(item));
                } else {
                    createRow();
                }
            }
        })();

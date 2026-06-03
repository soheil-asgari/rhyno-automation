(() => {
    function initInsuranceForm(config) {
        const rowsContainer = document.getElementById(config.rowsContainerId);
        const rowTemplate = document.getElementById(config.rowTemplateId);
        const addRowButton = document.getElementById(config.addRowButtonId);
        const employeeCountLabel = document.getElementById(config.employeeCountLabelId);
        const form = document.getElementById(config.formId);
        const copyPreviousMonthButton = document.getElementById(config.copyPreviousMonthButtonId);

        const projectNameField = document.getElementById(config.projectNameFieldId);
        const managerNameField = document.getElementById(config.managerNameFieldId);
        const monthField = document.getElementById(config.monthFieldId);
        const yearField = document.getElementById(config.yearFieldId);

        const statusField = config.statusFieldId ? document.getElementById(config.statusFieldId) : null;
        const idField = config.idFieldId ? document.getElementById(config.idFieldId) : null;
        const saveAjaxButton = config.saveAjaxButtonId ? document.getElementById(config.saveAjaxButtonId) : null;

        const antiForgeryToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

        let debounceTimer;

        function normalizeDigits(value) {
            return (value || '')
                .replace(/۰/g, '0')
                .replace(/۱/g, '1')
                .replace(/۲/g, '2')
                .replace(/۳/g, '3')
                .replace(/۴/g, '4')
                .replace(/۵/g, '5')
                .replace(/۶/g, '6')
                .replace(/۷/g, '7')
                .replace(/۸/g, '8')
                .replace(/۹/g, '9')
                .trim();
        }

        async function searchEmployees(query) {
            try {
                const response = await fetch(`/Bimeh/GetEmployees?search=${encodeURIComponent(query)}`);
                if (!response.ok) {
                    return [];
                }

                const data = await response.json();
                return Array.isArray(data) ? data : [];
            } catch {
                return [];
            }
        }

        function updateEmployeeCount() {
            const count = rowsContainer.querySelectorAll('tr').length;
            employeeCountLabel.textContent = `تعداد کارکنان: ${count}`;
        }

        function updateRowIndexes() {
            const rows = rowsContainer.querySelectorAll('tr');
            rows.forEach((row, index) => {
                row.querySelectorAll('[data-field]').forEach(field => {
                    const fieldName = field.getAttribute('data-field');
                    field.name = `Employees[${index}].${fieldName}`;
                    field.id = `Employees_${index}__${fieldName}`;
                });
            });
        }

        function attachSolarPicker(input) {
            if (!input || input.dataset.pickerAttached) {
                return;
            }

            if (typeof window.$ === 'undefined' || typeof window.$.fn?.persianDatepicker === 'undefined') {
                return;
            }

            try {
                window.$(input).persianDatepicker({
                    format: 'YYYY/MM/DD',
                    autoClose: true,
                    initialValue: false,
                    observer: true,
                    altField: input
                });
                input.dataset.pickerAttached = 'true';
            } catch {
                // ignore
            }
        }

        function setRowFromEmployee(tr, employee) {
            tr.querySelector('[data-field="FullName"]').value = employee.fullName || '';
            tr.querySelector('[data-field="HumanCapitalEmployeeId"]').value = employee.id || '';
            tr.querySelector('[data-field="JobTitle"]').value = employee.positionTitle || '';
            tr.querySelector('[data-field="StartWorkSolar"]').value = employee.hireDateShamsi || '';
            const salaryField = tr.querySelector('[data-field="Salary"]');
            salaryField.value = Number(employee.currentSalary || 0).toFixed(2);
            salaryField.dispatchEvent(new Event('input', { bubbles: true }));
        }

        function attachEmployeeAutocomplete(tr) {
            const searchInput = tr.querySelector('.employee-search');
            const suggestionsDiv = tr.querySelector('.employee-suggestions');
            const hiddenIdField = tr.querySelector('[data-field="HumanCapitalEmployeeId"]');

            searchInput.addEventListener('input', (event) => {
                clearTimeout(debounceTimer);
                hiddenIdField.value = '';

                const query = event.target.value.trim();
                if (query.length < 2) {
                    suggestionsDiv.innerHTML = '';
                    suggestionsDiv.style.display = 'none';
                    return;
                }

                debounceTimer = setTimeout(async () => {
                    const employees = await searchEmployees(query);
                    if (!employees.length) {
                        suggestionsDiv.innerHTML = '';
                        suggestionsDiv.style.display = 'none';
                        return;
                    }

                    suggestionsDiv.innerHTML = employees.map(emp => `
                        <button type="button" class="employee-suggestion-item" data-id="${emp.id}">
                            <div><strong>${emp.fullName}</strong> (${emp.personnelCode})</div>
                            <small>${emp.positionTitle || '-'} - حقوق پایه: ${(emp.currentSalary || 0).toLocaleString('fa-IR')} ریال</small>
                        </button>
                    `).join('');

                    suggestionsDiv.style.display = 'block';

                    suggestionsDiv.querySelectorAll('.employee-suggestion-item').forEach(item => {
                        item.addEventListener('click', () => {
                            const selected = employees.find(emp => String(emp.id) === item.getAttribute('data-id'));
                            if (!selected) {
                                return;
                            }

                            setRowFromEmployee(tr, selected);
                            suggestionsDiv.innerHTML = '';
                            suggestionsDiv.style.display = 'none';
                        });
                    });
                }, 250);
            });

            searchInput.addEventListener('blur', () => {
                setTimeout(() => {
                    suggestionsDiv.style.display = 'none';
                }, 150);
            });
        }

        function addRow(employee) {
            const clone = rowTemplate.content.cloneNode(true);
            const tr = clone.querySelector('tr');
            rowsContainer.appendChild(clone);

            attachEmployeeAutocomplete(tr);
            tr.querySelectorAll('.solar-date-field').forEach(attachSolarPicker);

            if (employee) {
                tr.querySelector('[data-field="HumanCapitalEmployeeId"]').value = employee.humanCapitalEmployeeId || '';
                tr.querySelector('[data-field="FullName"]').value = employee.fullName || '';
                tr.querySelector('[data-field="JobTitle"]').value = employee.jobTitle || '';
                tr.querySelector('[data-field="StartWorkSolar"]').value = employee.startWorkSolar || '';
                tr.querySelector('[data-field="EndWorkSolar"]').value = employee.endWorkSolar || '';
                tr.querySelector('[data-field="WorkDays"]').value = employee.workDays ?? 0;
                const salaryField = tr.querySelector('[data-field="Salary"]');
                salaryField.value = employee.salary ?? 0;
                salaryField.dispatchEvent(new Event('input', { bubbles: true }));
            }

            updateRowIndexes();
            updateEmployeeCount();
        }

        function renderRows(employees) {
            rowsContainer.innerHTML = '';
            if (employees && employees.length) {
                employees.forEach(addRow);
            } else {
                addRow();
            }
        }

        function collectRowsPayload() {
            const rows = [...rowsContainer.querySelectorAll('tr')];
            return rows.map(row => ({
                humanCapitalEmployeeId: Number(row.querySelector('[data-field="HumanCapitalEmployeeId"]')?.value || 0) || null,
                fullName: row.querySelector('[data-field="FullName"]')?.value?.trim() || '',
                jobTitle: row.querySelector('[data-field="JobTitle"]')?.value?.trim() || '',
                startWorkSolar: normalizeDigits(row.querySelector('[data-field="StartWorkSolar"]')?.value || ''),
                endWorkSolar: normalizeDigits(row.querySelector('[data-field="EndWorkSolar"]')?.value || ''),
                workDays: Number(row.querySelector('[data-field="WorkDays"]')?.value || 0),
                salary: Number(row.querySelector('[data-field="Salary"]')?.value || 0)
            })).filter(item => item.humanCapitalEmployeeId && item.fullName && item.jobTitle && item.startWorkSolar);
        }

        addRowButton?.addEventListener('click', () => addRow());

        rowsContainer.addEventListener('click', (event) => {
            const removeButton = event.target.closest('.removeRow');
            if (!removeButton) {
                return;
            }

            removeButton.closest('tr')?.remove();

            if (!rowsContainer.querySelector('tr')) {
                addRow();
            } else {
                updateRowIndexes();
                updateEmployeeCount();
            }
        });

        copyPreviousMonthButton?.addEventListener('click', async () => {
            const month = Number(monthField.value || 0);
            const year = Number(yearField.value || 0);

            if (month < 1 || month > 12 || year < 1300 || year > 1600) {
                alert('ماه و سال معتبر وارد کنید.');
                return;
            }

            try {
                const response = await fetch(`/Bimeh/CopyFromPreviousMonth?month=${month}&year=${year}`);
                const payload = await response.json();

                if (!response.ok || !payload?.success) {
                    alert(payload?.message || 'امکان دریافت اطلاعات ماه قبل وجود ندارد.');
                    return;
                }

                if (payload.data?.projectName) {
                    projectNameField.value = payload.data.projectName;
                }
                if (payload.data?.managerName) {
                    managerNameField.value = payload.data.managerName;
                }

                renderRows(payload.data?.employees || []);
            } catch {
                alert('خطا در دریافت اطلاعات ماه قبل.');
            }
        });

        if (config.mode === 'edit' && saveAjaxButton) {
            saveAjaxButton.addEventListener('click', async () => {
                const payload = {
                    id: Number(idField?.value || 0),
                    projectName: projectNameField.value?.trim() || '',
                    managerName: managerNameField.value?.trim() || '',
                    month: Number(monthField.value || 0),
                    year: Number(yearField.value || 0),
                    status: statusField?.value || 'Draft',
                    employees: collectRowsPayload()
                };

                if (!payload.employees.length) {
                    alert('حداقل یک ردیف معتبر ثبت کنید.');
                    return;
                }

                try {
                    const response = await fetch('/Bimeh/SaveInsuranceAjax', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                            'RequestVerificationToken': antiForgeryToken || ''
                        },
                        body: JSON.stringify(payload)
                    });

                    const result = await response.json();
                    if (!response.ok || !result?.success) {
                        alert(result?.message || 'ذخیره انجام نشد.');
                        return;
                    }

                    window.location.href = `/Bimeh/Edit/${result.id}`;
                } catch {
                    alert('خطا در ذخیره اطلاعات.');
                }
            });
        }

        form?.addEventListener('submit', () => {
            const rows = rowsContainer.querySelectorAll('tr');
            rows.forEach(row => {
                const startField = row.querySelector('[data-field="StartWorkSolar"]');
                const endField = row.querySelector('[data-field="EndWorkSolar"]');

                if (startField) {
                    startField.value = normalizeDigits(startField.value);
                }
                if (endField) {
                    endField.value = normalizeDigits(endField.value);
                }
            });
        });

        const initialEmployees = Array.isArray(config.initialEmployees) ? config.initialEmployees : [];
        if (initialEmployees.length) {
            renderRows(initialEmployees);
        } else {
            addRow();
        }
    }

    window.initInsuranceForm = initInsuranceForm;
})();

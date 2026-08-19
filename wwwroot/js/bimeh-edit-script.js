(() => {
            const dataEl = document.getElementById('bimehData');
            const employees = dataEl ? (JSON.parse(dataEl.textContent) || []) : [];
            const initialEmployees = employees.map(e => ({
                humanCapitalEmployeeId: e.humanCapitalEmployeeId ?? e.HumanCapitalEmployeeId,
                fullName: e.fullName ?? e.FullName,
                jobTitle: e.jobTitle ?? e.JobTitle,
                startWorkSolar: e.startWorkSolar ?? e.StartWorkSolar,
                endWorkSolar: e.endWorkSolar ?? e.EndWorkSolar,
                workDays: e.workDays ?? e.WorkDays,
                salary: e.salary ?? e.Salary
            }));

            window.initInsuranceForm({
                mode: 'edit',
                formId: 'bimehEditForm',
                rowsContainerId: 'employeeRows',
                rowTemplateId: 'employeeRowTemplate',
                addRowButtonId: 'addRow',
                employeeCountLabelId: 'employeeCountLabel',
                copyPreviousMonthButtonId: 'copyPreviousMonth',
                projectNameFieldId: 'projectNameField',
                managerNameFieldId: 'managerNameField',
                monthFieldId: 'monthField',
                yearFieldId: 'yearField',
                statusFieldId: 'statusField',
                idFieldId: 'idField',
                saveAjaxButtonId: 'saveInsuranceAjax',
                initialEmployees
            });

            document.getElementById('employeeRows').addEventListener('input', event => {
                if (!event.target.classList.contains('salary-input')) return;
                const row = event.target.closest('tr');
                const salary = Number(event.target.value || 0);
                row.querySelector('[data-insurance-cell]').textContent = `${(salary * 0.07).toLocaleString('fa-IR')} ریال`;
            });

            document.getElementById('employeeRows').querySelectorAll('tr').forEach(row => {
                const salary = Number(row.querySelector('.salary-input')?.value || 0);
                const cell = row.querySelector('[data-insurance-cell]');
                if (cell) cell.textContent = `${(salary * 0.07).toLocaleString('fa-IR')} ریال`;
            });

            document.getElementById('saveInsuranceAjax')?.addEventListener('click', () => {
                if (window.AppUI?.toast) {
                    window.AppUI.toast('ذخیره سریع در حال ارسال است.', 'info');
                }
            });
        })();

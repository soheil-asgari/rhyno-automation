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
                mode: 'create',
                formId: 'bimehCreateForm',
                rowsContainerId: 'employeeRows',
                rowTemplateId: 'employeeRowTemplate',
                addRowButtonId: 'addRow',
                employeeCountLabelId: 'employeeCountLabel',
                copyPreviousMonthButtonId: 'copyPreviousMonth',
                projectNameFieldId: 'projectNameField',
                managerNameFieldId: 'managerNameField',
                monthFieldId: 'monthField',
                yearFieldId: 'yearField',
                initialEmployees
            });

            document.getElementById('employeeRows').addEventListener('input', event => {
                if (!event.target.classList.contains('salary-input')) return;
                const row = event.target.closest('tr');
                const salary = Number(event.target.value || 0);
                row.querySelector('[data-insurance-cell]').textContent = `${(salary * 0.07).toLocaleString('fa-IR')} ریال`;
            });
        })();

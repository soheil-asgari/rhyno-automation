// Vanilla JS: Auto-format salary input with thousand separators
    (() => {
        const salaryInput = document.querySelector('#BaseSalary');
        if (!salaryInput) return;
        salaryInput.addEventListener('input', function () {
            const raw = this.value.replace(/[^0-9]/g, '');
            this.value = raw;
        });
    })();

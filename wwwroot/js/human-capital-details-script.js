(() => {
        const salarySalary = document.getElementById('SalaryIncrease_NewSalary');
        const salaryPrev = document.getElementById('SalaryIncrease_PromotionTitle');
        const salaryReason = document.getElementById('SalaryIncrease_Reason');
        const salaryDate = document.getElementById('SalaryIncrease_EffectiveDate');
        const salaryPreview = document.getElementById('salaryPreview');
        const statusType = document.getElementById('StatusChange_StatusType');
        const statusDate = document.getElementById('StatusChange_EffectiveDate');
        const statusRef = document.getElementById('StatusChange_ReferenceNumber');
        const statusDesc = document.getElementById('StatusChange_Description');
        const statusExit = document.getElementById('StatusChange_ExitReason');
        const statusPreview = document.getElementById('statusPreview');

        const renderSalary = () => {
            if (!salaryPreview) return;
            const salary = salarySalary?.value || '';
            const title = salaryPrev?.value || '';
            const reason = salaryReason?.value || '';
            const date = salaryDate?.value || '';
            salaryPreview.textContent = `تاریخ: ${date || '-'}\nحقوق جدید: ${salary || '-'}\nعنوان ارتقا: ${title || '-'}\nدلیل: ${reason || '-'}`;
        };

        const renderStatus = () => {
            if (!statusPreview) return;
            const type = statusType?.value || '';
            const date = statusDate?.value || '';
            const reference = statusRef?.value || '';
            const description = statusDesc?.value || '';
            const exitReason = statusExit?.value || '';
            statusPreview.textContent = `نوع: ${type || '-'}\nتاریخ: ${date || '-'}\nمرجع: ${reference || '-'}\nشرح: ${description || '-'}${exitReason ? `\nدلیل خروج: ${exitReason}` : ''}`;
        };

        // Safe event listener attachment - skip null elements
        [salarySalary, salaryPrev, salaryReason, salaryDate].forEach(input => {
            if (input) input.addEventListener('input', renderSalary);
        });
        [statusType, statusDate, statusRef, statusDesc, statusExit].forEach(input => {
            if (input) input.addEventListener('input', renderStatus);
        });
        renderSalary();
        renderStatus();
    })();

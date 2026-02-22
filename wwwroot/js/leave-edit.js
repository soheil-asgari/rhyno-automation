(function () {
    const form = document.getElementById('leaveEditForm');
    if (!form) return;

    const startFa = document.getElementById('StartDateFa');
    const endFa = document.getElementById('EndDateFa');

    const start = document.getElementById('StartDate'); // hidden (میلادی)
    const end = document.getElementById('EndDate');     // hidden (میلادی)

    const daysHint = document.getElementById('daysHint');
    const modelSummary = document.getElementById('modelSummary');

    function isoOnly(val) {
        // گاهی asp-for ممکنه "2026-02-22T00:00:00" بده
        return (val || '').split('T')[0];
    }

    function parseIsoDate(iso) {
        if (!iso) return null;
        const d = new Date(iso + "T00:00:00");
        return isNaN(d.getTime()) ? null : d;
    }

    function gregIsoToJalali(iso) {
        const d = parseIsoDate(isoOnly(iso));
        if (!d) return '';
        return new persianDate(d).format('YYYY/MM/DD');
    }

    function updateDaysHint() {
        const s = parseIsoDate(isoOnly(start.value));
        const e = parseIsoDate(isoOnly(end.value));

        if (!s || !e) {
            daysHint.textContent = '';
            daysHint.classList.remove('text-danger');
            return;
        }

        const diff = Math.floor((e - s) / (1000 * 60 * 60 * 24)) + 1; // inclusive
        if (diff <= 0) {
            daysHint.textContent = 'تاریخ پایان باید بعد/مساوی تاریخ شروع باشد.';
            daysHint.classList.add('text-danger');
        } else {
            daysHint.textContent = `مدت مرخصی: ${diff} روز`;
            daysHint.classList.remove('text-danger');
        }
    }

    // مقدار اولیه (وقتی Edit باز میشه)
    startFa.value = gregIsoToJalali(start.value);
    endFa.value = gregIsoToJalali(end.value);
    updateDaysHint();

    // DatePicker شمسی
    $(startFa).pDatepicker({
        format: 'YYYY/MM/DD',
        autoClose: true,
        initialValue: false,
        onSelect: function (unix) {
            // unix از تاریخ شمسی انتخابی
            start.value = new persianDate(unix).toCalendar('gregorian').format('YYYY-MM-DD');
            startFa.value = new persianDate(unix).format('YYYY/MM/DD');
            updateDaysHint();
        }
    });

    $(endFa).pDatepicker({
        format: 'YYYY/MM/DD',
        autoClose: true,
        initialValue: false,
        onSelect: function (unix) {
            end.value = new persianDate(unix).toCalendar('gregorian').format('YYYY-MM-DD');
            endFa.value = new persianDate(unix).format('YYYY/MM/DD');
            updateDaysHint();
        }
    });

    // جلوگیری از ثبت اگر ترتیب تاریخ غلطه
    form.addEventListener('submit', function (ev) {
        const s = parseIsoDate(isoOnly(start.value));
        const e = parseIsoDate(isoOnly(end.value));

        if (s && e && e < s) {
            ev.preventDefault();
            ev.stopPropagation();
            if (modelSummary) {
                modelSummary.textContent = 'تاریخ‌ها صحیح نیستند: تاریخ پایان باید بعد/مساوی تاریخ شروع باشد.';
                modelSummary.classList.remove('d-none');
            }
        }
    });
})();
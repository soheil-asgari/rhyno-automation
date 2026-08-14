function testUpdateEvent() {
    const testYear = 1404;
    const testMonth = 12;
    const testDay = 20;
    updateEventDisplay(testYear, testMonth, testDay, `${testDay} اسفند`);
}
(function () {
    // تاریخ و ساعت فارسی
    const dateEl = document.getElementById('current-persian-date');
    const timeEl = document.getElementById('current-time-persian');

    function pad(n) { return String(n).padStart(2, '0'); }

    function updateClock() {
        const now = new Date();
        if (dateEl) {
            dateEl.textContent = new Intl.DateTimeFormat('fa-IR-u-ca-persian', {
                year: 'numeric', month: '2-digit', day: '2-digit'
            }).format(now);
        }
        if (timeEl) {
            timeEl.textContent = `${pad(now.getHours())}:${pad(now.getMinutes())}`;
        }
    }

    updateClock();
    setInterval(updateClock, 1000 * 30);

    // جستجوی ساده داخل کارت‌ها/بخش‌ها
    const search = document.getElementById('dashSearch');
    const clearBtn = document.getElementById('dashSearchClear');
    const targets = [
        ...Array.from(document.querySelectorAll('#statsArea a.stat-card2')),
        ...Array.from(document.querySelectorAll('#dashContent .dash-card')),
        ...Array.from(document.querySelectorAll('.dash-banner'))
    ];

    function norm(s) {
        return (s || '').toString().trim().toLowerCase();
    }

    function applyFilter() {
        const q = norm(search?.value);
        if (q) clearBtn?.classList.remove('d-none');
        else clearBtn?.classList.add('d-none');

        targets.forEach(el => {
            const hay = norm(el.getAttribute('data-keywords')) + ' ' + norm(el.textContent);
            el.style.display = (!q || hay.includes(q)) ? '' : 'none';
        });
    }

    search?.addEventListener('input', applyFilter);
    clearBtn?.addEventListener('click', function () {
        search.value = '';
        search.focus();
        applyFilter();
    });

    applyFilter();
})();
let m = moment();

async function updateEventDisplay(year, month, day, label = "امروز") {
    const loader = document.getElementById('eventLoader');
    const content = document.getElementById('eventContent');
    const display = document.getElementById('eventDisplay');
    const retryBtn = document.getElementById('eventRetry');

    if (loader) loader.style.display = 'flex';
    if (content) content.style.opacity = '0.4';
    if (display) display.innerText = "در حال دریافت...";
    if (retryBtn) retryBtn.style.display = 'none';

    try {
        const apiUrl = `https://pnldev.com/api/calender?year=${year}&month=${month}&day=${day}&holiday=true`;

        const response = await fetch(apiUrl);
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }
        const data = await response.json();
        await new Promise(r => setTimeout(r, 600));

        let eventText = "مناسبت خاصی نیست";
        let holidayInfo = "";

        if (data && data.result && data.result.holiday === true) {
            holidayInfo = " (تعطیل رسمی)";
        }

        if (data && data.result && Array.isArray(data.result.event) && data.result.event.length > 0) {
            eventText = data.result.event.join(" - ");
        }

        if (display) {
            display.innerText = `${label}: ${eventText}${holidayInfo}`;
        }

    } catch (e) {
        if (display) {
            display.innerText = "دریافت اطلاعات مناسبت‌ها با خطا مواجه شد.";
        }
        // Show a retry action
        if (retryBtn) retryBtn.style.display = 'inline-flex';
    } finally {
        if (loader) loader.style.display = 'none';
        if (content) content.style.opacity = '1';
    }
}

function renderCalendar() {
    const container = document.getElementById('daysContainer');
    const monthDisplay = document.getElementById('monthDisplay');
    if (!container || !monthDisplay) return;

    container.innerHTML = '';
    monthDisplay.innerText = m.locale('fa').format('jMMMM jYYYY');

    const daysInMonth = m.jDaysInMonth();
    let startDayOfWeek = m.clone().startOf('jMonth').day() + 1;
    if (startDayOfWeek === 7) startDayOfWeek = 0;

    for (let i = 0; i < startDayOfWeek; i++) {
        const span = document.createElement('span');
        span.className = 'off';
        container.appendChild(span);
    }

    for (let i = 1; i <= daysInMonth; i++) {
        const span = document.createElement('span');
        span.innerText = i.toLocaleString('fa-IR');
        span.style.cursor = 'pointer';
        span.style.padding = '5px';

        const currentIterDate = m.clone().jDate(i);
        if (currentIterDate.isSame(moment(), 'day')) span.style.background = '#4f46e5';
        if (currentIterDate.isSame(moment(), 'day')) span.style.color = '#fff';
        if (currentIterDate.isSame(moment(), 'day')) span.style.borderRadius = '5px';
        if (currentIterDate.day() === 5) span.style.color = '#ef4444';

        span.onclick = () => {
            const label = `${i} ${m.locale('fa').format('jMMMM')}`;
            updateEventDisplay(currentIterDate.jYear(), currentIterDate.jMonth() + 1, i, label);
        };
        container.appendChild(span);
    }
}

document.addEventListener('DOMContentLoaded', () => {
    renderCalendar();
    updateEventDisplay(m.jYear(), m.jMonth() + 1, m.jDate());

    document.getElementById('prevMonth').onclick = () => { m.add(1, 'jMonth'); renderCalendar(); };
    document.getElementById('nextMonth').onclick = () => { m.subtract(1, 'jMonth'); renderCalendar(); };
    document.querySelector('.btn-today').onclick = () => { m = moment(); renderCalendar(); updateEventDisplay(m.jYear(), m.jMonth() + 1, m.jDate()); };

    // Retry button for the calendar widget
    const retryBtn = document.getElementById('eventRetry');
    if (retryBtn) {
        retryBtn.addEventListener('click', () => updateEventDisplay(m.jYear(), m.jMonth() + 1, m.jDate()));
    }
});
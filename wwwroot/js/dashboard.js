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

    // اگر Chart.js داری، اینجا میشه مقداردهی کرد.
    // اگر نداری، همین canvas خالی می‌مونه و UI نمی‌شکنه.
})();
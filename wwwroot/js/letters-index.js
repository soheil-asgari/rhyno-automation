(function () {
    const search = document.getElementById('lettersSearch');
    const clearBtn = document.getElementById('lettersSearchClear');
    // تغییر از tbody به ظرف جدید کارت‌ها
    const container = document.querySelector('.letters-container');
    const emptySearch = document.getElementById('lettersEmptySearch');
    const countEl = document.getElementById('lettersCount');

    // اگر المان‌های حیاتی نبودند، کل تابع را متوقف نکن، فقط جستجو را غیرفعال کن
    if (search && container) {
        const rows = Array.from(container.querySelectorAll('.letter-row-card'));

        function normalize(str) {
            return (str || '').toString().trim().toLowerCase();
        }

        function applyFilter() {
            const q = normalize(search.value);
            let visible = 0;

            rows.forEach(r => {
                // چون در ساختار جدید دیتاست‌ها را ندارید، از متن داخل کارت استفاده می‌کنیم
                const content = normalize(r.innerText);
                const show = !q || content.includes(q);
                r.style.display = show ? '' : 'none';
                if (show) visible++;
            });

            if (countEl) countEl.textContent = visible;
            if (q && visible === 0) emptySearch?.classList.remove('d-none');
            else emptySearch?.classList.add('d-none');

            if (q) clearBtn?.classList.remove('d-none');
            else clearBtn?.classList.add('d-none');
        }

        search.addEventListener('input', applyFilter);
        clearBtn?.addEventListener('click', function () {
            search.value = '';
            search.focus();
            applyFilter();
        });
    }

    // --- مدیریت کلیک دکمه‌ها (خارج از شرط جستجو قرار گرفت تا همیشه کار کند) ---
    document.addEventListener('click', function (e) {
        // دکمه مشاهده
        const viewBtn = e.target.closest('.view');
        if (viewBtn) {
            // اجازه بده رفتار پیش‌فرض (لینک) انجام شود
            return;
        }

        // دکمه چاپ
        const printBtn = e.target.closest('.print');
        if (printBtn) {
            e.preventDefault();
            window.print();
        }

        // دکمه بیشتر
        const moreBtn = e.target.closest('.more');
        if (moreBtn) {
            e.preventDefault();
            if (window.Swal) {
                Swal.fire({
                    icon: 'info',
                    title: 'عملیات',
                    text: 'این بخش در حال توسعه است.',
                    confirmButtonText: 'تایید'
                });
            } else {
                console.log("این بخش در حال توسعه است.");
            }
        }
    });
})();
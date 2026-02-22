(function () {
    // Sidebar toggle (mobile)
    const sidebar = document.getElementById('appSidebar');
    const overlay = document.getElementById('sidebarOverlay');
    const btnOpen = document.getElementById('btnOpenSidebar');
    const btnClose = document.getElementById('btnCloseSidebar');

    function openSidebar() {
        if (!sidebar || !overlay) return;
        sidebar.classList.add('is-open');
        overlay.classList.remove('d-none');
    }
    function closeSidebar() {
        if (!sidebar || !overlay) return;
        sidebar.classList.remove('is-open');
        overlay.classList.add('d-none');
    }

    btnOpen?.addEventListener('click', openSidebar);
    btnClose?.addEventListener('click', closeSidebar);
    overlay?.addEventListener('click', closeSidebar);

    // Active link auto (بدون نیاز به ifهای Razor)
    const path = (window.location.pathname || '').toLowerCase();
    document.querySelectorAll('a.sidebar-link').forEach(a => {
        const href = (a.getAttribute('href') || '').toLowerCase();
        if (!href || href === '#' || href.startsWith('javascript')) return;

        if (path === href || path.startsWith(href + '/') || (href !== '/' && path.startsWith(href))) {
            a.classList.add('active');
        }
    });

    // Coming soon UX
    function toast(msg) {
        if (window.Swal) {
            Swal.fire({ toast: true, position: 'top-end', icon: 'info', title: msg, showConfirmButton: false, timer: 2200, timerProgressBar: true });
        } else {
            alert(msg);
        }
    }

    document.addEventListener('click', (e) => {
        const el = e.target.closest('a.sidebar-link--comingsoon');
        if (!el) return;
        e.preventDefault();
        const title = el.getAttribute('data-title') || 'این بخش';
        toast(`${title} در حال توسعه است.`);
    });

    // Persian date/time
    function pad(n) { return String(n).padStart(2, '0'); }
    function updatePersianDateTime() {
        const now = new Date();
        const dateEl = document.getElementById('current-persian-date');
        const timeEl = document.getElementById('current-time-persian');

        if (dateEl) {
            dateEl.textContent = new Intl.DateTimeFormat('fa-IR-u-ca-persian', { year: 'numeric', month: 'long', day: 'numeric' }).format(now);
        }
        if (timeEl) {
            timeEl.textContent = `${pad(now.getHours())}:${pad(now.getMinutes())}`;
        }
    }
    updatePersianDateTime();
    setInterval(updatePersianDateTime, 60000);

    // Daily background (با احتیاط: اگر خوانایی مشکل داشت، overlay توی css/styles.css اضافه کن)
    function setDailyBackground() {
        const bgImages = [
            '/images/backgrounds/bg-1.jpg',
            '/images/backgrounds/bg-2.jpg',
            '/images/backgrounds/bg-3.jpg',
            '/images/backgrounds/bg-4.jpg',
            '/images/backgrounds/bg-5.jpg',
            '/images/backgrounds/bg-6.jpg',
            '/images/backgrounds/bg-7.jpg',
            '/images/backgrounds/bg-8.jpg'
        ];
        const now = new Date();
        const start = new Date(now.getFullYear(), 0, 0);
        const dayOfYear = Math.floor((now - start) / (1000 * 60 * 60 * 24));
        document.body.style.backgroundImage = `url('${bgImages[dayOfYear % bgImages.length]}')`;
    }
    setDailyBackground();

    // DataTables: فقط جدول‌هایی که خودت کلاس می‌دی
    // به‌جای .table:not(.dt-skip) => فقط .js-datatable
    function initDataTables() {
        if (!window.jQuery || !jQuery.fn || !jQuery.fn.DataTable) return;

        jQuery('table.js-datatable').each(function () {
            if (!jQuery.fn.DataTable.isDataTable(this)) {
                jQuery(this).DataTable({
                    language: { url: "https://cdn.datatables.net/plug-ins/1.13.7/i18n/fa.json" },
                    pageLength: 10,
                    lengthMenu: [5, 10, 25],
                    retrieve: true
                });
            }
        });
    }
    // چون اسکریپت‌ها defer هستند، DOM آماده است
    initDataTables();
})();



document.addEventListener('DOMContentLoaded', () => {
    const sidebar = document.querySelector('.sidebar-right');
    const overlay = document.getElementById('sidebarOverlay');
    const btnOpen = document.getElementById('btnOpenSidebar');

    function open() { sidebar?.classList.add('is-open'); overlay?.classList.add('show'); }
    function close() { sidebar?.classList.remove('is-open'); overlay?.classList.remove('show'); }

    btnOpen?.addEventListener('click', open);
    overlay?.addEventListener('click', close);
});
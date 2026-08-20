/**
 * Rhyno Layout JS – Minimal bootstrap-free utilities
 * Core functionality (dropdowns, modals, sidebar, confirm, toast) is
 * initialized inline in _Layout.cshtml via window.Rhyno.
 *
 * This file provides legacy helpers that other scripts may depend on.
 */

(function () {
    // Expose Rhyno globally if not already defined (rare edge-case for pages
    // that may load this script before inline scripts)
    if (!window.Rhyno) {
        window.Rhyno = {
            toggleDropdown: function () { },
            closeAllDropdowns: function () { },
            showModal: function (id) {
                var el = document.getElementById(id);
                if (el) { el.classList.add('open'); document.body.style.overflow = 'hidden'; }
            },
            hideModal: function (id) {
                var el = document.getElementById(id);
                if (el) { el.classList.remove('open'); document.body.style.overflow = ''; }
            },
            openSidebar: function () {
                var s = document.getElementById('appSidebar');
                var o = document.getElementById('sidebarOverlay');
                if (s) s.classList.add('is-open');
                if (o) o.classList.remove('hidden');
            },
            closeSidebar: function () {
                var s = document.getElementById('appSidebar');
                var o = document.getElementById('sidebarOverlay');
                if (s) s.classList.remove('is-open');
                if (o) o.classList.add('hidden');
            },
            confirm: function (msg, cb) {
                var m = document.getElementById('appConfirmMessage');
                if (m) m.textContent = msg || 'آیا مطمئن هستید؟';
                window.Rhyno._confirmCallback = cb;
                this.showModal('appConfirmDialog');
            },
            toast: function (msg, type) {
                type = type || 'info';
                var colors = { success: 'bg-emerald-600', error: 'bg-rose-600', warning: 'bg-amber-600', info: 'bg-teal-600' };
                var icons = { success: 'bi-check-circle', error: 'bi-x-circle', warning: 'bi-exclamation-triangle', info: 'bi-info-circle' };
                var t = document.createElement('div');
                t.className = 'fixed bottom-4 left-4 z-[200] flex items-center gap-2 rounded-[3px] ' + (colors[type] || colors.info) + ' px-3 py-1.5 text-[11px] font-bold text-white shadow-lg transition-all duration-300 translate-y-4 opacity-0';
                t.innerHTML = '<i class="bi ' + (icons[type] || icons.info) + '"></i> ' + msg;
                document.body.appendChild(t);
                requestAnimationFrame(function () { t.classList.remove('translate-y-4', 'opacity-0'); });
                setTimeout(function () {
                    t.classList.add('translate-y-4', 'opacity-0');
                    setTimeout(function () { t.remove(); }, 300);
                }, 3000);
            }
        };
    }

    // Active link highlighting based on current path
    var path = (window.location.pathname || '').toLowerCase();
    document.querySelectorAll('a.sidebar-link').forEach(function (a) {
        var href = (a.getAttribute('href') || '').toLowerCase();
        if (!href || href === '#' || href.startsWith('javascript')) return;
        if (path === href || path.startsWith(href + '/') || (href !== '/' && path.startsWith(href))) {
            a.classList.add('sidebar-active');
        }
    });

    // Coming-soon UX (legacy – preserved for backward compatibility)
    document.addEventListener('click', function (e) {
        var el = e.target.closest('a.sidebar-link--comingsoon');
        if (!el) return;
        e.preventDefault();
        if (window.Rhyno && window.Rhyno.confirm) {
            window.Rhyno.confirm('این بخش در حال توسعه است.', function () { });
        } else {
            alert('این بخش در حال توسعه است.');
        }
    });

    // Persian date/time for legacy elements (if .js-datatable based tables need it)
    function pad(n) { return String(n).padStart(2, '0'); }
    function updatePersianDateTime() {
        var dateEl = document.getElementById('current-persian-date');
        var timeEl = document.getElementById('current-time-persian');
        var now = new Date();
        if (dateEl) {
            dateEl.textContent = new Intl.DateTimeFormat('fa-IR-u-ca-persian', { year: 'numeric', month: 'long', day: 'numeric' }).format(now);
        }
        if (timeEl) {
            timeEl.textContent = pad(now.getHours()) + ':' + pad(now.getMinutes());
        }
    }
    updatePersianDateTime();
    setInterval(updatePersianDateTime, 60000);

    // DataTables initialization (kept for backward compat – pages using DataTables
    // include their own JS and will attach to .js-datatable class)
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
    initDataTables();
})();
/**
 * Rhyno Contracts Index — Loading States
 *
 * Provides:
 *   1. Loading overlay on filter form submit
 *   2. Auto-submit on filter <select> change
 *   3. Pagination link loading spinner
 */
document.addEventListener('DOMContentLoaded', function () {
    'use strict';

    // ── Filter loading overlay ──
    var filterForm = document.querySelector('[data-filter-form]');
    var tableWrapper = document.querySelector('[data-table-wrapper]');

    if (filterForm && tableWrapper) {
        // Show overlay on filter submit
        filterForm.addEventListener('submit', function () {
            if (!tableWrapper.querySelector('.contracts-loading-overlay')) {
                var overlay = document.createElement('div');
                overlay.className = 'contracts-loading-overlay';
                overlay.innerHTML = '<span class="rhyno-spinner"></span><span>در حال جستجو...</span>';
                tableWrapper.appendChild(overlay);
            }
        });

        // Auto-submit on filter <select> changes
        filterForm.querySelectorAll('select').forEach(function (sel) {
            sel.addEventListener('change', function () {
                filterForm.requestSubmit();
            });
        });
    }

    // ── Pagination loading state ──
    // Intercept pagination links to show a loading spinner
    document.querySelectorAll('[data-page-link]').forEach(function (link) {
        link.addEventListener('click', function (e) {
            var paginationContainer = this.closest('.flex');
            if (!paginationContainer) return;
            // Only intercept if no loading spinner already shown
            if (paginationContainer.querySelector('.pagination-loading')) return;

            e.preventDefault();
            var href = this.getAttribute('href');
            if (!href || href === '#') return;

            // Show spinner next to pagination
            var spinner = document.createElement('span');
            spinner.className = 'pagination-loading';
            spinner.innerHTML = '<span class="rhyno-spinner" style="width:12px;height:12px;border-width:2px;"></span>';
            paginationContainer.appendChild(spinner);

            // Navigate after brief delay to let the spinner render
            setTimeout(function () {
                window.location.href = href;
            }, 80);
        });
    });
});
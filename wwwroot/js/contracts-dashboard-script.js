/**
 * Rhyno Contracts Dashboard — Loading States
 *
 * Provides:
 *   1. Skeleton shimmer on initial paint for metric cards
 *   2. Fade-out of skeleton after cards are painted
 *   3. Navigation loading overlay for internal links
 */
document.addEventListener('DOMContentLoaded', function () {
    'use strict';

    var metricsGrid = document.querySelector('[data-dashboard-metrics]');
    if (!metricsGrid) return;

    // ── Show skeleton shimmer briefly on first paint ──
    // Add a skeleton wrapper so the cards pulse briefly while the browser finishes layout
    metricsGrid.classList.add('dashboard-skeleton-initial');
    setTimeout(function () {
        metricsGrid.classList.remove('dashboard-skeleton-initial');
    }, 400);

    // ── Intercept navigation links for a full-page loading overlay ──
    // When clicking links that navigate away, briefly show a loading overlay
    // for a smoother perceived transition
    document.querySelectorAll('a:not([data-close-modal]):not([onclick])').forEach(function (link) {
        var href = link.getAttribute('href');
        if (!href || href === '#' || href.startsWith('javascript:')) return;
        if (link.hasAttribute('data-guard-ignore')) return;

        link.addEventListener('click', function (e) {
            if (e.defaultPrevented) return;
            var overlay = document.createElement('div');
            overlay.className = 'fixed inset-0 z-[9999] bg-white/80 dark:bg-slate-900/80 flex items-center justify-center gap-2';
            overlay.innerHTML = '<span class="rhyno-spinner"></span><span class="text-[11px] text-slate-500">در حال بارگذاری...</span>';
            document.body.appendChild(overlay);
            // Brief delay to let the spinner render before navigation
            setTimeout(function () {
                // overlay stays — browser navigates away
            }, 50);
        });
    });
});
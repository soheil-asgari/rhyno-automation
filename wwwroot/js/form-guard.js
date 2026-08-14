/**
 * Rhyno Form Guard — Double-Submit Prevention & Loading States
 * Provides a shared, dependency-free guard for all forms across the app.
 *
 * Usage:
 *   <form data-guard-submit> ... </form>          → explicitly guarded
 *   <form method="post"> ... </form>              → auto-guarded (POST forms)
 *   <form data-guard-ignore> ... </form>          → opt-out (e.g. search/filter)
 *   <form data-guard-submit data-guard-message="در حال ثبت..."> ... </form>
 *
 * Behavior:
 *   - Disables all submit buttons on submit.
 *   - Adds a spinner + loading text to the clicked submit button.
 *   - Re-enables buttons if the request fails (page not navigated).
 *   - Prevents duplicate submissions via a per-form lock.
 *   - Auto-binds ALL POST forms (opt-out via data-guard-ignore).
 *   - Search/filter GET forms are NOT auto-guarded (multiple submissions intentional).
 */
(function () {
    'use strict';

    const SPINNER_HTML = '<span class="rhyno-btn-spinner" aria-hidden="true"></span>';

    function createSpinner() {
        const span = document.createElement('span');
        span.className = 'rhyno-btn-spinner';
        span.setAttribute('aria-hidden', 'true');
        return span;
    }

    function shouldGuard(form) {
        // Explicit opt-out always wins.
        if (form.hasAttribute('data-guard-ignore')) return false;

        // Explicit opt-in.
        if (form.hasAttribute('data-guard-submit')) return true;

        // Auto-guard POST forms (but not search/filter GET forms).
        const method = (form.getAttribute('method') || 'get').toLowerCase();
        if (method === 'post') return true;

        return false;
    }

    function guardForm(form) {
        if (form.dataset.guardBound === '1') return;
        if (!shouldGuard(form)) return;
        form.dataset.guardBound = '1';

        form.addEventListener('submit', function (e) {
            if (form.dataset.guardLocked === '1') {
                e.preventDefault();
                e.stopPropagation();
                return;
            }

            // Only guard if the form is actually valid (native validation).
            if (typeof form.checkValidity === 'function' && !form.checkValidity()) {
                return; // Let the browser show validation messages.
            }

            form.dataset.guardLocked = '1';

            const submitter = e.submitter || form.querySelector('button[type="submit"], input[type="submit"]');
            const buttons = form.querySelectorAll('button[type="submit"], input[type="submit"]');

            buttons.forEach(function (btn) {
                btn.disabled = true;
                btn.classList.add('is-loading');
                btn.setAttribute('aria-busy', 'true');
            });

            if (submitter && submitter.tagName === 'BUTTON') {
                const originalText = submitter.innerHTML;
                submitter.dataset.originalHtml = originalText;
                const spinner = createSpinner();
                submitter.prepend(spinner);
                submitter.classList.add('has-spinner');
            }

            // Re-enable after a timeout in case the page does not navigate
            // (e.g. AJAX form that stays on the page). This prevents a
            // permanently-disabled button if the request fails silently.
            setTimeout(function () {
                if (form.dataset.guardLocked === '1') {
                    releaseForm(form);
                }
            }, 15000);
        });
    }

    function releaseForm(form) {
        form.dataset.guardLocked = '0';
        const buttons = form.querySelectorAll('button[type="submit"], input[type="submit"]');
        buttons.forEach(function (btn) {
            btn.disabled = false;
            btn.classList.remove('is-loading', 'has-spinner');
            btn.removeAttribute('aria-busy');
            if (btn.dataset.originalHtml) {
                btn.innerHTML = btn.dataset.originalHtml;
                delete btn.dataset.originalHtml;
            }
        });
    }

    // Expose a manual release API for AJAX flows.
    window.RhynoFormGuard = {
        release: releaseForm,
        lock: function (form) { form.dataset.guardLocked = '1'; },
        guard: guardForm
    };

    // Auto-bind all forms (POST forms + explicit data-guard-submit).
    function init() {
        document.querySelectorAll('form').forEach(guardForm);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    // Also bind dynamically-added forms via MutationObserver.
    if (typeof MutationObserver !== 'undefined') {
        const observer = new MutationObserver(function (mutations) {
            mutations.forEach(function (mutation) {
                mutation.addedNodes.forEach(function (node) {
                    if (node.nodeType !== 1) return;
                    if (node.matches && node.matches('form')) {
                        guardForm(node);
                    }
                    if (node.querySelectorAll) {
                        node.querySelectorAll('form').forEach(guardForm);
                    }
                });
            });
        });
        observer.observe(document.documentElement, { childList: true, subtree: true });
    }
})();
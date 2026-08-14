/**
 * Rhyno Modal Focus Trap — Accessible Focus Management
 * Provides a shared, dependency-free focus trap for all vanilla JS modals.
 *
 * Behavior:
 *   - On open: moves focus to the first meaningful interactive element.
 *   - Traps Tab / Shift+Tab inside the modal.
 *   - Prevents focus from escaping to the background.
 *   - On close: restores focus to the element that opened the modal.
 *   - ESC closes the modal (where the caller opts in).
 *   - Backdrop click closes the modal (where the caller opts in).
 *
 * Usage:
 *   RhynoModalFocus.open(modalEl, { trigger: triggerEl, onClose: fn });
 *   RhynoModalFocus.close(modalEl);
 *
 * The utility auto-wires to elements with [data-modal-focus] when opened via
 * the shared openModal/closeModal helpers.
 */
(function () {
    'use strict';

    var FOCUSABLE_SELECTOR = [
        'a[href]',
        'button:not([disabled])',
        'input:not([disabled]):not([type="hidden"])',
        'select:not([disabled])',
        'textarea:not([disabled])',
        '[tabindex]:not([tabindex="-1"])',
        '[contenteditable="true"]'
    ].join(',');

    var openModals = []; // stack of { el, trigger, lastFocused }

    function getFocusable(el) {
        if (!el) return [];
        return Array.prototype.slice.call(el.querySelectorAll(FOCUSABLE_SELECTOR))
            .filter(function (node) {
                // Skip elements that are not visible
                var style = window.getComputedStyle(node);
                return style.display !== 'none' && style.visibility !== 'hidden' && node.offsetParent !== null;
            });
    }

    function findFirstFocusable(el) {
        var items = getFocusable(el);
        // Prefer the first input/select/textarea/button (meaningful interactive element)
        return items[0] || null;
    }

    function trapKeydown(e) {
        var top = openModals[openModals.length - 1];
        if (!top) return;

        if (e.key === 'Tab') {
            var focusable = getFocusable(top.el);
            if (focusable.length === 0) {
                e.preventDefault();
                return;
            }

            var first = focusable[0];
            var last = focusable[focusable.length - 1];
            var active = document.activeElement;

            if (e.shiftKey) {
                if (active === first || !top.el.contains(active)) {
                    e.preventDefault();
                    last.focus();
                }
            } else {
                if (active === last || !top.el.contains(active)) {
                    e.preventDefault();
                    first.focus();
                }
            }
        }
    }

    function open(el, opts) {
        opts = opts || {};
        if (!el) return;

        // If already open, just re-focus
        var existing = openModals.find(function (m) { return m.el === el; });
        if (existing) {
            var f = findFirstFocusable(el);
            if (f) f.focus();
            return;
        }

        var trigger = opts.trigger || document.activeElement;
        var lastFocused = document.activeElement;

        openModals.push({ el: el, trigger: trigger, lastFocused: lastFocused });

        // Move focus to the first meaningful interactive element
        var first = findFirstFocusable(el);
        if (first) {
            first.focus();
        } else if (el.setAttribute) {
            el.setAttribute('tabindex', '-1');
            el.focus();
        }

        // Trap Tab inside the modal
        document.addEventListener('keydown', trapKeydown, true);

        // ESC closes the modal where appropriate
        if (opts.escClose !== false) {
            el.addEventListener('keydown', function onEsc(e) {
                if (e.key === 'Escape') {
                    e.stopPropagation();
                    close(el);
                    if (opts.onClose) opts.onClose();
                }
            });
        }

        // Backdrop click closes the modal where appropriate
        if (opts.backdropClose !== false) {
            el.addEventListener('click', function onBackdrop(e) {
                if (e.target === el) {
                    close(el);
                    if (opts.onClose) opts.onClose();
                }
            });
        }
    }

    function close(el) {
        var idx = openModals.findIndex(function (m) { return m.el === el; });
        if (idx < 0) return;

        var entry = openModals[idx];
        openModals.splice(idx, 1);

        // Restore focus to the element that opened the modal
        if (entry.trigger && entry.trigger.focus && typeof entry.trigger.focus === 'function') {
            try { entry.trigger.focus(); } catch (e) { /* ignore */ }
        } else if (entry.lastFocused && entry.lastFocused.focus) {
            try { entry.lastFocused.focus(); } catch (e) { /* ignore */ }
        }

        if (openModals.length === 0) {
            document.removeEventListener('keydown', trapKeydown, true);
        }
    }

    function closeTop() {
        var top = openModals[openModals.length - 1];
        if (top) close(top.el);
    }

    // Expose public API
    window.RhynoModalFocus = {
        open: open,
        close: close,
        closeTop: closeTop,
        isOpen: function (el) { return openModals.some(function (m) { return m.el === el; }); }
    };
})();
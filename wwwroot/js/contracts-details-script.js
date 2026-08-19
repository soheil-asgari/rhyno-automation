(function() {
            'use strict';

            // ── Vanilla JS Tab Switcher ────────────────────────────────
            function initTabs() {
                var container = document.querySelector('[data-contract-tabs]');
                if (!container) return;
                var buttons = container.querySelectorAll('.tab-btn');
                var panels = container.querySelectorAll('.tab-panel');

                function activateTab(tabId) {
                    // Update button states
                    buttons.forEach(function(btn) {
                        var isActive = btn.getAttribute('data-tab') === tabId;
                        btn.className = 'tab-btn px-4 py-2.5 border-b-2 transition ' +
                            (isActive
                                ? 'border-teal-500 text-teal-600 dark:text-teal-400'
                                : 'border-transparent text-slate-500 dark:text-slate-400 hover:text-slate-700 dark:hover:text-slate-300');
                    });

                    // Show/hide panels
                    panels.forEach(function(panel) {
                        var isTarget = panel.id === 'tab-' + tabId;
                        if (isTarget) {
                            panel.classList.remove('hidden');
                        } else {
                            panel.classList.add('hidden');
                        }
                    });
                }

                buttons.forEach(function(btn) {
                    btn.addEventListener('click', function(e) {
                        e.preventDefault();
                        var tabId = btn.getAttribute('data-tab');
                        if (tabId) activateTab(tabId);
                    });
                });
            }

            // ── Vanilla JS Modal helpers (with focus trap) ──────────────
            window.openModal = function(modalId) {
                var el = document.getElementById(modalId);
                if (!el) {
                    console.warn('Modal not found:', modalId);
                    return;
                }
                el.style.display = 'flex';
                document.body.style.overflow = 'hidden';
                // Focus trap: move focus into the modal, trap Tab, restore on close
                if (window.RhynoModalFocus) {
                    var trigger = document.querySelector('[data-modal-target="' + modalId + '"]');
                    RhynoModalFocus.open(el, { trigger: trigger });
                }
                // Focus the first input in the modal
                setTimeout(function() {
                    var firstInput = el.querySelector('input, select, textarea');
                    if (firstInput) firstInput.focus();
                }, 100);
            };

            window.closeModal = function(modalId) {
                var el = document.getElementById(modalId);
                if (!el) return;
                el.style.display = 'none';
                document.body.style.overflow = '';
                if (window.RhynoModalFocus) {
                    RhynoModalFocus.close(el);
                }
            };

            // Close modal on backdrop click (handled by focus trap utility)
            document.addEventListener('click', function(e) {
                if (e.target.classList.contains('rhyno-modal-overlay') && e.target.style.display === 'flex') {
                    e.target.style.display = 'none';
                    document.body.style.overflow = '';
                    if (window.RhynoModalFocus) {
                        RhynoModalFocus.close(e.target);
                    }
                }
            });

            // Escape key to close modals (handled by focus trap utility)
            document.addEventListener('keydown', function(e) {
                if (e.key === 'Escape') {
                    document.querySelectorAll('.rhyno-modal-overlay').forEach(function(el) {
                        if (el.style.display === 'flex') {
                            el.style.display = 'none';
                            document.body.style.overflow = '';
                            if (window.RhynoModalFocus) {
                                RhynoModalFocus.close(el);
                            }
                        }
                    });
                }
            });

            // ── Wire up Bootstrap-free modal triggers (event delegation) ─
            document.addEventListener('click', function(e) {
                var trigger = e.target.closest('[data-modal-target]');
                if (!trigger) return;
                e.preventDefault();
                e.stopPropagation();
                openModal(trigger.getAttribute('data-modal-target'));
            });

            document.addEventListener('DOMContentLoaded', function() {
                initTabs();
            });
        })();

document.addEventListener("DOMContentLoaded", function() {
            const buttons = document.querySelectorAll('.tab-btn');
            const panes = document.querySelectorAll('.tab-pane');

            // ── Legacy hash map ──────────────────────────────────────────
            // Maps old #tab-xxx hashes to new clean hash names for backward
            // compatibility with bookmarks / external links that may still
            // reference the old format.
            const legacyHashMap = {
                '#tab-general':  '#general',
                '#tab-security': '#security',
                '#tab-system':   '#system',
                '#tab-ui':       '#appearance',
                '#tab-signature':'#signature',
                '#tab-adduser':  '#users',
                '#tab-branding': '#branding'
            };

            const activeClasses = ['active', 'border-teal-500', 'text-teal-700', 'dark:text-teal-300', 'font-black'];
            const inactiveClasses = ['border-transparent', 'text-slate-500', 'dark:text-slate-400', 'font-bold'];

            function activateTab(targetSelector) {
                const targetPane = document.querySelector(targetSelector);
                if (!targetPane) return;

                // Hide all panes
                panes.forEach(pane => {
                    pane.classList.add('hidden');
                    pane.classList.remove('block');
                });

                // Reset all buttons styling
                buttons.forEach(btn => {
                    activeClasses.forEach(c => btn.classList.remove(c));
                    inactiveClasses.forEach(c => btn.classList.add(c));
                });

                // Find and activate the matching button
                const matchingBtn = document.querySelector(`.tab-btn[data-target="${targetSelector}"]`);
                if (matchingBtn) {
                    activeClasses.forEach(c => matchingBtn.classList.add(c));
                    inactiveClasses.forEach(c => matchingBtn.classList.remove(c));
                }

                // Show target pane
                targetPane.classList.remove('hidden');
                targetPane.classList.add('block');

                // Save selected tab in storage & history
                localStorage.setItem('rhyno.settings.activeTab', targetSelector);
                if (history.replaceState) {
                    history.replaceState(null, '', targetSelector);
                }

                // Dispatch CustomEvent for signature resizing etc
                document.dispatchEvent(new CustomEvent('rhyno:settings-tab-shown', { detail: { target: targetSelector } }));
            }

            buttons.forEach(button => {
                button.addEventListener('click', function() {
                    activateTab(button.getAttribute('data-target'));
                });
            });

            // Auto-click the stored tab or the first active tab on load
            // When loaded inside a shell tab iframe (_tab=1), always default to
            // the first tab to avoid localStorage contamination from prior visits.
            const isInsideTab = new URLSearchParams(window.location.search).has('_tab');
            let storedTarget = null;
            if (!isInsideTab) {
                storedTarget = window.location.hash || localStorage.getItem('rhyno.settings.activeTab');

                // ── Backward compatibility: resolve legacy #tab-* hashes ──
                if (storedTarget && legacyHashMap[storedTarget]) {
                    storedTarget = legacyHashMap[storedTarget];
                }
            }
            let tabToClick = null;
            if (storedTarget) {
                tabToClick = document.querySelector(`.tab-btn[data-target="${storedTarget}"]`);
            }
            if (!tabToClick) {
                tabToClick = document.querySelector('.tab-btn.active') || document.querySelector('.tab-btn');
            }
            if (tabToClick) {
                // Use activateTab so the URL hash is updated to the clean format
                activateTab(tabToClick.getAttribute('data-target'));
            }
        });

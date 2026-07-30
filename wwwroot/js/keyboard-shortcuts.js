/**
 * Keyboard Shortcuts Manager - مدیریت کلیدهای میانبر
 * نسخه 1.0 - راینو ERP
 * 
 * میانبرها:
 *   Ctrl+K         : Command Palette (search)
 *   Ctrl+D         : داشبورد اصلی
 *   Ctrl+I         : کارتابل
 *   Ctrl+F         : فوکوس روی جستجوی صفحه (در صورت وجود)
 *   Ctrl+N         : ایجاد جدید (context-aware)
 *   Ctrl+Shift+F   : جستجوی سراسری
 *   Ctrl+Shift+T   : تغییر تم
 *   Esc            : بستن مودال‌ها / منوها
 *   /              : فوکوس روی جستجوی سریع
 *   ?              : نمایش راهنمای میانبرها
 *   g + h          : برو به خانه (Vim-style)
 *   g + i          : برو به کارتابل
 *   j/k            : پیمایش در لیست‌ها
 *   Tab/Shift+Tab  : پیمایش در فرم‌ها
 */
(function () {
    'use strict';

    const SHORTCUTS_KEY = 'rhyno.shortcuts.help.shown';

    // Shortcut registry
    let shortcuts = new Map();
    let isHelpVisible = false;
    let searchInput = null;
    let keySequence = '';

    /**
     * Register default shortcuts
     */
    function registerDefaults() {
        // Navigation shortcuts
        register('ctrl+k', openCommandPalette, 'Command Palette', 'باز کردن جستجوی فرمان‌ها');
        register('ctrl+d', () => navigateTo('/'), 'داشبورد اصلی', 'بازگشت به صفحه اصلی');
        register('ctrl+i', () => navigateTo('/WorkInbox'), 'کارتابل', 'رفتن به کارتابل');
        register('ctrl+shift+f', () => navigateTo('/Search'), 'جستجوی سراسری', 'جستجو در کل سیستم');
        register('ctrl+shift+t', toggleTheme, 'تغییر تم', 'تغییر بین تم روشن و تاریک');
        register('escape', closeAllModals, 'بستن', 'بستن پنجره‌ها و مودال‌ها');

        // Quick actions
        register('/', focusQuickSearch, 'جستجوی سریع', 'فوکوس روی فیلد جستجوی صفحه');
        register('?', toggleHelp, 'راهنمای میانبرها', 'نمایش یا پنهان کردن این راهنما');
        register('ctrl+n', triggerCreate, 'ایجاد جدید', 'ایجاد آیتم جدید در صفحه جاری');

        // Vim-style navigation (in list pages)
        register('g', startGoSequence, 'Go...', 'آغاز پیمایش Vim-style (g + h = خانه)');
        register('j', scrollNext, 'پایین', 'رفتن به آیتم بعدی در لیست');
        register('k', scrollPrev, 'بالا', 'رفتن به آیتم قبلی در لیست');

        // Form navigation
        register('tab', focusNext, 'بعدی', 'فوکوس روی فیلد بعدی');
        register('shift+tab', focusPrev, 'قبلی', 'فوکوس روی فیلد قبلی');
    }

    /**
     * Register a shortcut
     */
    function register(keys, handler, label, description) {
        shortcuts.set(keys.toLowerCase(), {
            keys,
            handler,
            label,
            description
        });
    }

    /**
     * Handle keydown events
     */
    function handleKeyDown(event) {
        const target = event.target;
        const tagName = target.tagName.toLowerCase();

        // Don't intercept when typing in inputs (except for specific shortcuts)
        const isInputFocused = tagName === 'input' || tagName === 'textarea' || tagName === 'select' || target.isContentEditable;

        const key = event.key.toLowerCase();
        const ctrl = event.ctrlKey || event.metaKey;
        const shift = event.shiftKey;
        const alt = event.altKey;

        // Support both Persian (؟) and English (?) question mark
        const isQuestionMark = key === '?' || key === '؟' || (event.code === 'Slash' && event.shiftKey);

        // Build key combo
        let combo = '';
        if (isQuestionMark) {
            combo = '?';
        } else {
            if (ctrl) combo += 'ctrl+';
            if (shift) combo += 'shift+';
            if (alt) combo += 'alt+';
            combo += key;
        }

        // Handle Vim-style sequences
        if (keySequence) {
            const seq = keySequence + '+' + key;
            if (seq === 'g+h') {
                navigateTo('/');
                keySequence = '';
                event.preventDefault();
                return;
            }
            if (seq === 'g+i') {
                navigateTo('/WorkInbox');
                keySequence = '';
                event.preventDefault();
                return;
            }
            // Reset sequence after 1 second
            setTimeout(() => { keySequence = ''; }, 1000);
            keySequence = '';
        }

        // Check for registered shortcut
        const shortcut = shortcuts.get(combo);
        if (shortcut) {
            // Allow some shortcuts even when input is focused
            const alwaysAllowed = ['escape', 'ctrl+d', 'ctrl+i', 'ctrl+shift+f', 'ctrl+shift+t', '?', 'ctrl+k'];
            if (!isInputFocused || alwaysAllowed.includes(combo)) {
                event.preventDefault();
                event.stopPropagation();
                shortcut.handler();
                return;
            }
        }

        // Vim-style navigation with j/k (only when not in input)
        if (!isInputFocused) {
            if (key === 'j') {
                scrollNext();
                return;
            }
            if (key === 'k') {
                scrollPrev();
                return;
            }
        }
    }

    /**
     * Open command palette
     */
    function openCommandPalette() {
        const palette = document.getElementById('appCommandPalette');
        if (palette) {
            const modal = new bootstrap.Modal(palette);
            modal.show();
            setTimeout(() => {
                const input = document.getElementById('appCommandInput');
                if (input) input.focus();
            }, 150);
        }
    }

    /**
     * Navigate to a URL
     */
    function navigateTo(url) {
        window.location.href = url;
    }

    /**
     * Toggle theme
     */
    function toggleTheme() {
        const html = document.documentElement;
        const isDark = html.classList.contains('dark') || html.classList.contains('theme-dark');
        const newPref = isDark ? 'light' : 'dark';

        html.classList.remove('theme-light', 'theme-dark', 'dark');
        html.setAttribute('data-theme', newPref);
        document.body?.setAttribute('data-theme', newPref);

        if (newPref === 'dark') {
            html.classList.add('theme-dark', 'dark');
        } else {
            html.classList.add('theme-light');
        }

        localStorage.setItem('rhyno.theme.preference', newPref === 'dark' ? 'Dark' : 'Light');
        window.dispatchEvent(new Event('themechange'));

        showToast(`تم ${isDark ? 'روشن' : 'تاریک'} فعال شد`, 'success');
    }

    /**
     * Focus quick search input
     */
    function focusQuickSearch() {
        searchInput = document.querySelector('[name="q"]') || document.querySelector('input[type="search"]');
        if (searchInput) {
            searchInput.focus();
            searchInput.select();
        }
    }

    /**
     * Close all modals
     */
    function closeAllModals() {
        const modals = document.querySelectorAll('.modal.show');
        modals.forEach(modal => {
            const instance = bootstrap.Modal.getInstance(modal);
            if (instance) instance.hide();
        });

        // Close sidebar on mobile
        const sidebar = document.getElementById('appSidebar');
        if (sidebar && sidebar.classList.contains('show')) {
            document.getElementById('btnCloseSidebar')?.click();
        }

        // Close dropdowns
        const dropdowns = document.querySelectorAll('.dropdown-menu.show');
        dropdowns.forEach(d => d.classList.remove('show'));
    }

    /**
     * Trigger create action
     */
    function triggerCreate() {
        // Find create button on current page
        const createBtn = document.querySelector('[data-action="create"], .btn-create, a[href*="Create"]');
        if (createBtn) {
            createBtn.click();
        }
    }

    /**
     * Start go-to sequence (g key)
     */
    function startGoSequence() {
        keySequence = 'g';
        showToast('برای خانه g+h و برای کارتابل g+i را فشار دهید', 'info');
    }

    /**
     * Scroll to next item in list
     */
    function scrollNext() {
        const activeElement = document.activeElement;
        if (activeElement && activeElement.tagName === 'TR') {
            const nextRow = activeElement.nextElementSibling;
            if (nextRow) {
                nextRow.focus();
                nextRow.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
            }
        }
    }

    /**
     * Scroll to previous item in list
     */
    function scrollPrev() {
        const activeElement = document.activeElement;
        if (activeElement && activeElement.tagName === 'TR') {
            const prevRow = activeElement.previousElementSibling;
            if (prevRow) {
                prevRow.focus();
                prevRow.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
            }
        }
    }

    /**
     * Focus next input
     */
    function focusNext() {
        // Default tab behavior - already handled by browser
    }

    /**
     * Focus previous input
     */
    function focusPrev() {
        // Default shift+tab behavior - already handled by browser
    }

    /**
     * Toggle help modal
     */
    function toggleHelp() {
        if (isHelpVisible) {
            hideHelp();
        } else {
            showHelp();
        }
    }

    /**
     * Show shortcuts help modal
     */
    function showHelp() {
        if (isHelpVisible) return;
        isHelpVisible = true;

        const darkMode = document.documentElement.classList.contains('dark');

        const overlay = document.createElement('div');
        overlay.className = 'shortcuts-help-overlay';
        overlay.style.cssText = 'position:fixed;inset:0;z-index:10000;background:rgba(15,23,42,0.6);backdrop-filter:blur(4px);display:flex;align-items:center;justify-content:center;padding:20px;';

        const grouped = new Map();
        shortcuts.forEach((shortcut, combo) => {
            const group = shortcut.label || 'عمومی';
            if (!grouped.has(group)) grouped.set(group, []);
            grouped.get(group).push(shortcut);
        });

        let shortcutsHtml = '';
        grouped.forEach((items, group) => {
            shortcutsHtml += `<div style="margin-bottom:12px;">
                <h4 style="margin:0 0 6px;font-size:12px;font-weight:700;text-transform:uppercase;color:${darkMode ? '#5eead4' : '#0d9488'};">${group}</h4>`;
            items.forEach(s => {
                const keysDisplay = s.keys.split('+').map(k => {
                    const keyMap = {
                        'ctrl': 'Ctrl',
                        'shift': '⇧',
                        'alt': 'Alt',
                        'escape': 'Esc',
                        'tab': 'Tab',
                        '/': '/',
                        '?': '?',
                        'j': 'J',
                        'k': 'K',
                        'g': 'G',
                        'n': 'N',
                        'd': 'D',
                        'i': 'I',
                        'f': 'F',
                        't': 'T',
                        'k': 'K'
                    };
                    return keyMap[k] || k.toUpperCase();
                }).join(' + ');

                shortcutsHtml += `<div style="display:flex;align-items:center;justify-content:space-between;padding:4px 0;font-size:12px;">
                    <span style="color:${darkMode ? '#94a3b8' : '#64748b'};">${s.description}</span>
                    <kbd style="background:${darkMode ? '#334155' : '#f1f5f9'};border:1px solid ${darkMode ? '#475569' : '#cbd5e1'};border-radius:6px;padding:2px 8px;font-family:monospace;font-size:11px;font-weight:600;color:${darkMode ? '#e2e8f0' : '#0f172a'};direction:ltr;display:inline-block;">${keysDisplay}</kbd>
                </div>`;
            });
            shortcutsHtml += '</div>';
        });

        overlay.innerHTML = `
            <div style="background:${darkMode ? '#1e293b' : 'white'};border-radius:16px;box-shadow:0 25px 60px rgba(0,0,0,0.4);max-width:480px;width:100%;max-height:80vh;overflow-y:auto;direction:rtl;">
                <div style="padding:20px 24px;border-bottom:1px solid ${darkMode ? '#334155' : '#e2e8f0'};display:flex;align-items:center;justify-content:space-between;">
                    <div>
                        <h2 style="margin:0;font-size:18px;font-weight:800;color:${darkMode ? '#f1f5f9' : '#0f172a'};">کلیدهای میانبر</h2>
                        <p style="margin:4px 0 0;font-size:12px;color:${darkMode ? '#64748b' : '#94a3b8'};">میانبرهای کیبورد برای پیمایش سریع‌تر</p>
                    </div>
                    <button class="shortcuts-help-close" style="background:transparent;border:none;cursor:pointer;font-size:20px;color:${darkMode ? '#94a3b8' : '#64748b'};padding:4px 8px;">✕</button>
                </div>
                <div style="padding:16px 24px;">${shortcutsHtml}</div>
                <div style="padding:12px 24px;border-top:1px solid ${darkMode ? '#334155' : '#e2e8f0'};font-size:11px;color:${darkMode ? '#475569' : '#94a3b8'};text-align:center;">برای بستن، Esc یا ? را فشار دهید</div>
            </div>
        `;

        document.body.appendChild(overlay);

        // Close handlers
        overlay.querySelector('.shortcuts-help-close').addEventListener('click', hideHelp);
        overlay.addEventListener('click', (e) => {
            if (e.target === overlay) hideHelp();
        });

        // ESC to close
        const escHandler = (e) => {
            if (e.key === 'Escape') {
                hideHelp();
                document.removeEventListener('keydown', escHandler);
            }
        };
        document.addEventListener('keydown', escHandler);

        localStorage.setItem(SHORTCUTS_KEY, 'true');
    }

    /**
     * Hide help modal
     */
    function hideHelp() {
        isHelpVisible = false;
        const overlay = document.querySelector('.shortcuts-help-overlay');
        if (overlay) {
            overlay.style.opacity = '0';
            overlay.style.transition = 'opacity 0.2s';
            setTimeout(() => overlay.remove(), 200);
        }
    }

    /**
     * Show a toast notification
     */
    function showToast(message, type) {
        const existing = document.querySelector('.keyboard-toast');
        if (existing) existing.remove();

        const toast = document.createElement('div');
        toast.className = 'keyboard-toast';
        const bgColor = type === 'success' ? '#059669' : type === 'info' ? '#0d9488' : '#475569';
        toast.style.cssText = `position:fixed;bottom:24px;left:50%;transform:translateX(-50%);z-index:10001;background:${bgColor};color:white;padding:10px 20px;border-radius:10px;font-size:13px;font-weight:600;box-shadow:0 4px 20px rgba(0,0,0,0.3);transition:all 0.3s;opacity:0;direction:rtl;`;
        toast.textContent = message;
        document.body.appendChild(toast);

        requestAnimationFrame(() => {
            toast.style.opacity = '1';
            toast.style.transform = 'translateX(-50%) translateY(-8px)';
        });

        setTimeout(() => {
            toast.style.opacity = '0';
            toast.style.transform = 'translateX(-50%) translateY(0)';
            setTimeout(() => toast.remove(), 300);
        }, 2500);
    }

    /**
     * Initialize shortcuts
     */
    function init() {
        registerDefaults();
        document.addEventListener('keydown', handleKeyDown);

        // Show help hint on first visit
        if (!localStorage.getItem(SHORTCUTS_KEY)) {
            setTimeout(() => {
                showToast('راهنما: برای دیدن کلیدهای میانبر، ؟ را فشار دهید', 'info');
            }, 3000);
        }

        // Make table rows focusable for j/k navigation
        document.addEventListener('click', (e) => {
            const row = e.target.closest('tr');
            if (row) {
                // Remove focus from other rows
                document.querySelectorAll('tr[tabindex]').forEach(r => {
                    if (r !== row) r.removeAttribute('tabindex');
                });
                row.setAttribute('tabindex', '0');
            }
        });

        console.log('[Rhyno] Keyboard shortcuts initialized. Press ? to see all shortcuts.');
    }

    // Auto-init
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    // Expose API
    window.RhynoShortcuts = {
        register,
        toggleHelp,
        showHelp,
        hideHelp
    };
})();
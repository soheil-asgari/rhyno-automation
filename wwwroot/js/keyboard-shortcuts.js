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
            // Vanilla JS modal — replaces bootstrap.Modal
            palette.classList.remove('hidden');
            palette.style.display = 'flex';
            document.body.classList.add('rhyno-modal-open');
            setTimeout(() => {
                const input = document.getElementById('appCommandInput');
                if (input) input.focus();
            }, 150);
            // Close on Escape or click outside (handled by global Esc handler)
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
        // Vanilla JS — close any element with 'modal' class and 'show' or 'is-active'
        const modals = document.querySelectorAll('.modal.show, .modal.is-active, [data-modal-active]');
        modals.forEach(modal => {
            if (modal.classList.contains('hidden')) return;
            modal.classList.add('hidden');
            modal.style.display = 'none';
            modal.classList.remove('is-active');
            modal.removeAttribute('data-modal-active');
        });
        // Also close command palette specifically
        const palette = document.getElementById('appCommandPalette');
        if (palette && !palette.classList.contains('hidden')) {
            palette.classList.add('hidden');
            palette.style.display = 'none';
        }
        document.body.classList.remove('rhyno-modal-open');

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
     * Show shortcuts help drawer
     */
    function showHelp() {
        if (isHelpVisible) return;
        isHelpVisible = true;

        const darkMode = document.documentElement.classList.contains('dark');

        // Backdrop
        const overlay = document.createElement('div');
        overlay.id = 'shortcutsHelpBackdrop';
        overlay.className = 'fixed inset-0 z-40 bg-slate-950/50 backdrop-blur-sm transition-opacity duration-200 opacity-0';
        overlay.setAttribute('aria-hidden', 'true');

        // Drawer panel (RTL: opens from right)
        const drawer = document.createElement('div');
        drawer.id = 'shortcutsHelpDrawer';
        drawer.className = 'fixed inset-y-0 right-0 z-50 w-full max-w-sm bg-white/95 dark:bg-slate-900/95 backdrop-blur-xl shadow-2xl border-r border-slate-200/80 dark:border-slate-800/80 transform translate-x-full transition-transform duration-300 ease-out flex flex-col';
        drawer.setAttribute('role', 'dialog');
        drawer.setAttribute('aria-modal', 'true');
        drawer.setAttribute('aria-label', 'کلیدهای میانبر');

        const grouped = new Map();
        // Group shortcuts by a friendly category derived from their description/label
        const categoryOrder = ['پیمایش', 'عملیات سریع', 'پیمایش Vim', 'فرم‌ها'];
        const categoryMap = {
            'Command Palette': 'پیمایش',
            'داشبورد اصلی': 'پیمایش',
            'کارتابل': 'پیمایش',
            'جستجوی سراسری': 'پیمایش',
            'تغییر تم': 'عملیات سریع',
            'بستن': 'عملیات سریع',
            'جستجوی سریع': 'عملیات سریع',
            'راهنمای میانبرها': 'عملیات سریع',
            'ایجاد جدید': 'عملیات سریع',
            'Go...': 'پیمایش Vim',
            'پایین': 'پیمایش Vim',
            'بالا': 'پیمایش Vim',
            'بعدی': 'فرم‌ها',
            'قبلی': 'فرم‌ها'
        };

        shortcuts.forEach((shortcut) => {
            const category = categoryMap[shortcut.label] || 'عمومی';
            if (!grouped.has(category)) grouped.set(category, []);
            grouped.get(category).push(shortcut);
        });

        const sortedCategories = Array.from(grouped.keys()).sort((a, b) => {
            const idxA = categoryOrder.indexOf(a);
            const idxB = categoryOrder.indexOf(b);
            if (idxA === -1 && idxB === -1) return a.localeCompare(b, 'fa');
            if (idxA === -1) return 1;
            if (idxB === -1) return -1;
            return idxA - idxB;
        });

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
            't': 'T'
        };

        let shortcutsHtml = '';
        sortedCategories.forEach((category) => {
            const items = grouped.get(category);
            shortcutsHtml += `
                <div class="mb-5">
                    <h4 class="text-[11px] font-bold uppercase tracking-wider text-teal-600 dark:text-teal-400 mb-2">${category}</h4>
                    <div class="space-y-2">
                        ${items.map(s => {
                            const keysDisplay = s.keys.split('+').map(k => keyMap[k] || k.toUpperCase()).join(' + ');
                            return `
                                <div class="flex items-center justify-between gap-3 py-1.5 text-xs">
                                    <span class="text-slate-600 dark:text-slate-300">${s.description}</span>
                                    <kbd class="flex-shrink-0 rounded-md border border-slate-200 dark:border-slate-700 bg-slate-100 dark:bg-slate-800 px-2 py-0.5 font-mono font-semibold text-slate-800 dark:text-slate-200 ltr" dir="ltr">${keysDisplay}</kbd>
                                </div>`;
                        }).join('')}
                    </div>
                </div>`;
        });

        drawer.innerHTML = `
            <div class="flex items-center justify-between px-5 py-4 border-b border-slate-200/80 dark:border-slate-800/80 flex-shrink-0">
                <div>
                    <h2 class="text-base font-black text-slate-900 dark:text-white">کلیدهای میانبر</h2>
                    <p class="text-[11px] text-slate-500 dark:text-slate-400 mt-0.5">میانبرهای کیبورد برای پیمایش سریع‌تر</p>
                </div>
                <button id="shortcutsHelpClose" type="button" class="grid h-8 w-8 place-items-center rounded-lg text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 hover:text-slate-600 dark:hover:text-slate-300 transition-colors">
                    <i class="bi bi-x-lg"></i>
                </button>
            </div>
            <div class="flex-1 overflow-y-auto p-5">${shortcutsHtml}</div>
            <div class="px-5 py-3 border-t border-slate-200/80 dark:border-slate-800/80 text-[11px] text-center text-slate-400 dark:text-slate-500 flex-shrink-0">
                برای بستن، Esc یا ? را فشار دهید
            </div>
        `;

        document.body.appendChild(overlay);
        document.body.appendChild(drawer);

        // Animate in
        requestAnimationFrame(() => {
            overlay.classList.remove('opacity-0');
            drawer.classList.remove('translate-x-full');
        });

        // Close handlers
        const closeDrawer = () => {
            overlay.classList.add('opacity-0');
            drawer.classList.add('translate-x-full');
            setTimeout(() => {
                overlay.remove();
                drawer.remove();
                isHelpVisible = false;
            }, 300);
        };

        drawer.querySelector('#shortcutsHelpClose').addEventListener('click', closeDrawer);
        overlay.addEventListener('click', closeDrawer);

        // ESC to close
        const escHandler = (e) => {
            if (e.key === 'Escape') {
                closeDrawer();
                document.removeEventListener('keydown', escHandler);
            }
        };
        document.addEventListener('keydown', escHandler);

        localStorage.setItem(SHORTCUTS_KEY, 'true');
    }

    /**
     * Hide help drawer
     */
    function hideHelp() {
        const overlay = document.getElementById('shortcutsHelpBackdrop');
        const drawer = document.getElementById('shortcutsHelpDrawer');
        if (overlay && drawer) {
            overlay.classList.add('opacity-0');
            drawer.classList.add('translate-x-full');
            setTimeout(() => {
                overlay.remove();
                drawer.remove();
                isHelpVisible = false;
            }, 300);
        } else {
            isHelpVisible = false;
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

        // Keyboard shortcuts initialized silently (no console noise in production).
    }

    // Auto-init
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    // Bind button triggers (drawer buttons with data-open-shortcuts)
    document.querySelectorAll('[data-open-shortcuts]').forEach(btn => {
        btn.addEventListener('click', (e) => {
            e.preventDefault();
            toggleHelp();
        });
    });

    // Expose API
    window.RhynoShortcuts = {
        register,
        toggleHelp,
        showHelp,
        hideHelp
    };
})();
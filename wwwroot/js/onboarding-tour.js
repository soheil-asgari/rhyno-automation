/**
 * Onboarding Tour - راهنمای تعاملی شروع به کار
 * نسخه 1.0 - راینو ERP
 */
(function () {
    'use strict';

    const STORAGE_KEY = 'rhyno.onboarding.completed';
    const STEP_ACTIVE_CLASS = 'onboarding-highlight';

    // Steps definition
    const defaultSteps = [
        {
            target: '[data-nav-id="home-index"]',
            title: 'خوش آمدید به راینو ERP',
            content: 'این سیستم جامع مدیریت سازمانی است. از طریق این منو می‌توانید به بخش‌های مختلف دسترسی داشته باشید.',
            placement: 'right',
            icon: 'bi-hand-index-thumb'
        },
        {
            target: '#sidebarMenuSearch',
            title: 'جستجوی سریع در منو',
            content: 'با تایپ نام بخش مورد نظر، سریع‌تر به مقصد برسید. از کلیدهای میانبر Ctrl+K هم می‌توانید استفاده کنید.',
            placement: 'bottom',
            icon: 'bi-search'
        },
        {
            target: '[data-nav-id="workinbox-index"]',
            title: 'کارتابل شما',
            content: 'تمام وظایف، تاییدیه‌ها و درخواست‌های در انتظار شما اینجا نمایش داده می‌شوند.',
            placement: 'right',
            icon: 'bi-inboxes'
        },
        {
            target: '#headerDate',
            title: 'تنظیمات و پروفایل',
            content: 'تاریخ شمسی، اعلان‌ها، تنظیمات پروفایل و تم سیستم از این بخش قابل دسترسی است.',
            placement: 'bottom',
            icon: 'bi-person-circle'
        },
        {
            target: '.sidebar-module:first-of-type',
            title: 'ماژول‌های سیستم',
            content: 'ماژول‌های مختلف مانند مالی، انبار و منابع انسانی از این بخش‌ها قابل دسترسی هستند. با کلیک روی هر بخش، زیرمنوها باز می‌شوند.',
            placement: 'right',
            icon: 'bi-grid-3x3-gap'
        }
    ];

    // Tour state
    let currentStep = 0;
    let tourOverlay = null;
    let tourTooltip = null;
    let isActive = false;
    let steps = [...defaultSteps];

    /**
     * Create DOM elements for the tour
     */
    function createTourElements() {
        // Overlay
        tourOverlay = document.createElement('div');
        tourOverlay.className = 'onboarding-overlay';
        tourOverlay.style.cssText = 'position:fixed;inset:0;z-index:9998;background:rgba(15,23,42,0.7);backdrop-filter:blur(2px);transition:opacity 0.3s;';

        // Tooltip
        tourTooltip = document.createElement('div');
        tourTooltip.className = 'onboarding-tooltip';
        tourTooltip.setAttribute('role', 'dialog');
        tourTooltip.setAttribute('aria-live', 'polite');
        tourTooltip.setAttribute('aria-label', 'راهنمای شروع به کار');
        tourTooltip.style.cssText = 'position:fixed;z-index:9999;background:white;border-radius:16px;box-shadow:0 20px 60px rgba(0,0,0,0.3);padding:20px;max-width:340px;width:90%;direction:rtl;font-family:Tahoma,sans-serif;transition:all 0.3s ease;';

        // Apply dark mode if active
        if (document.documentElement.classList.contains('dark')) {
            tourTooltip.style.background = '#1e293b';
            tourTooltip.style.color = '#e2e8f0';
        }
    }

    /**
     * Show a specific step
     */
    function showStep(index) {
        if (index >= steps.length) {
            endTour();
            return;
        }

        currentStep = index;
        const step = steps[index];
        const target = document.querySelector(step.target);

        if (!target) {
            // Skip invisible targets
            showStep(index + 1);
            return;
        }

        // Update tooltip content
        const darkMode = document.documentElement.classList.contains('dark');
        tourTooltip.innerHTML = `
            <div style="display:flex;align-items:start;gap:12px;">
                <div style="width:44px;height:44px;border-radius:12px;background:${darkMode ? 'rgba(20,184,166,0.15)' : '#f0fdfa'};display:grid;place-items:center;flex-shrink:0;">
                    <i class="bi ${step.icon || 'bi-info-circle'}" style="font-size:22px;color:${darkMode ? '#5eead4' : '#0d9488'};"></i>
                </div>
                <div style="flex:1;min-width:0;">
                    <h3 style="margin:0 0 6px;font-size:15px;font-weight:700;color:${darkMode ? '#f1f5f9' : '#0f172a'};">${step.title}</h3>
                    <p style="margin:0 0 12px;font-size:13px;line-height:1.7;color:${darkMode ? '#94a3b8' : '#64748b'};">${step.content}</p>
                </div>
            </div>
            <div style="display:flex;align-items:center;justify-content:space-between;margin-top:8px;">
                <div style="display:flex;gap:6px;">
                    ${steps.map((_, i) => `<span style="width:8px;height:8px;border-radius:50%;background:${i === index ? (darkMode ? '#5eead4' : '#0d9488') : (darkMode ? '#334155' : '#cbd5e1')};"></span>`).join('')}
                </div>
                <div style="display:flex;gap:8px;">
                    ${index > 0 ? `<button class="onboarding-prev" style="padding:6px 14px;border:1px solid ${darkMode ? '#334155' : '#e2e8f0'};background:transparent;border-radius:8px;cursor:pointer;font-size:12px;font-weight:600;color:${darkMode ? '#cbd5e1' : '#475569'};">قبلی</button>` : ''}
                    ${index < steps.length - 1
                        ? `<button class="onboarding-next" style="padding:6px 14px;background:${darkMode ? '#0d9488' : '#0d9488'};border:none;border-radius:8px;cursor:pointer;font-size:12px;font-weight:700;color:white;">بعدی</button>`
                        : `<button class="onboarding-finish" style="padding:6px 14px;background:#10b981;border:none;border-radius:8px;cursor:pointer;font-size:12px;font-weight:700;color:white;">متوجه شدم!</button>`
                    }
                    <button class="onboarding-skip" style="padding:6px 10px;background:transparent;border:none;cursor:pointer;font-size:12px;color:${darkMode ? '#64748b' : '#94a3b8'};">رد کردن</button>
                </div>
            </div>
        `;

        // Position tooltip near target
        positionTooltip(target, step.placement || 'right');

        // Highlight target
        target.style.position = 'relative';
        target.style.zIndex = '9999';
        target.style.boxShadow = '0 0 0 4px rgba(20,184,166,0.5), 0 0 20px rgba(20,184,166,0.3)';
        target.style.borderRadius = '8px';
        target.style.transition = 'all 0.3s ease';
        target.classList.add(STEP_ACTIVE_CLASS);

        // Add event listeners
        const nextBtn = tourTooltip.querySelector('.onboarding-next');
        const prevBtn = tourTooltip.querySelector('.onboarding-prev');
        const finishBtn = tourTooltip.querySelector('.onboarding-finish');
        const skipBtn = tourTooltip.querySelector('.onboarding-skip');

        if (nextBtn) nextBtn.addEventListener('click', (e) => { e.stopPropagation(); nextStep(); });
        if (prevBtn) prevBtn.addEventListener('click', (e) => { e.stopPropagation(); prevStep(); });
        if (finishBtn) finishBtn.addEventListener('click', (e) => { e.stopPropagation(); endTour(); });
        if (skipBtn) skipBtn.addEventListener('click', (e) => { e.stopPropagation(); endTour(); });

        // Scroll target into view
        target.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }

    /**
     * Position the tooltip relative to the target element
     */
    function positionTooltip(target, placement) {
        const targetRect = target.getBoundingClientRect();
        const tooltipWidth = 340;
        const tooltipHeight = tourTooltip.offsetHeight || 200;
        const gap = 16;

        let top, left;

        switch (placement) {
            case 'right':
                top = targetRect.top + (targetRect.height - tooltipHeight) / 2;
                left = targetRect.right + gap;
                // If not enough space on right, show on left
                if (left + tooltipWidth > window.innerWidth - 20) {
                    left = targetRect.left - tooltipWidth - gap;
                }
                break;
            case 'left':
                top = targetRect.top + (targetRect.height - tooltipHeight) / 2;
                left = targetRect.left - tooltipWidth - gap;
                break;
            case 'top':
                top = targetRect.top - tooltipHeight - gap;
                left = targetRect.left + (targetRect.width - tooltipWidth) / 2;
                break;
            case 'bottom':
            default:
                top = targetRect.bottom + gap;
                left = targetRect.left + (targetRect.width - tooltipWidth) / 2;
                break;
        }

        // Clamp to viewport
        top = Math.max(20, Math.min(top, window.innerHeight - tooltipHeight - 20));
        left = Math.max(20, Math.min(left, window.innerWidth - tooltipWidth - 20));

        tourTooltip.style.top = top + 'px';
        tourTooltip.style.left = left + 'px';
    }

    /**
     * Move to next step
     */
    function nextStep() {
        removeHighlight(currentStep);
        showStep(currentStep + 1);
    }

    /**
     * Move to previous step
     */
    function prevStep() {
        removeHighlight(currentStep);
        showStep(Math.max(0, currentStep - 1));
    }

    /**
     * Remove highlight from a step
     */
    function removeHighlight(index) {
        if (index >= steps.length) return;
        const step = steps[index];
        const target = document.querySelector(step.target);
        if (target) {
            target.style.position = '';
            target.style.zIndex = '';
            target.style.boxShadow = '';
            target.style.borderRadius = '';
            target.classList.remove(STEP_ACTIVE_CLASS);
        }
    }

    /**
     * Handle keyboard shortcuts during tour
     */
    function handleKeyboard(e) {
        if (!isActive) return;
        switch (e.key) {
            case 'ArrowRight':
            case 'ArrowDown':
                e.preventDefault();
                nextStep();
                break;
            case 'ArrowLeft':
            case 'ArrowUp':
                e.preventDefault();
                prevStep();
                break;
            case 'Escape':
                e.preventDefault();
                endTour();
                break;
        }
    }

    /**
     * End the tour
     */
    function endTour() {
        isActive = false;
        removeHighlight(currentStep);

        if (tourOverlay) {
            tourOverlay.style.opacity = '0';
            setTimeout(() => {
                if (tourOverlay.parentNode) tourOverlay.parentNode.removeChild(tourOverlay);
                if (tourTooltip.parentNode) tourTooltip.parentNode.removeChild(tourTooltip);
            }, 300);
        }

        document.removeEventListener('keydown', handleKeyboard);

        // Mark as completed
        localStorage.setItem(STORAGE_KEY, 'true');

        // Fade out animation
        if (tourTooltip) {
            tourTooltip.style.opacity = '0';
            tourTooltip.style.transform = 'scale(0.95)';
        }
    }

    /**
     * Start the onboarding tour
     */
    function startTour(customSteps) {
        // Check if already completed
        if (localStorage.getItem(STORAGE_KEY) === 'true') {
            return;
        }

        // Check if on main pages (not login, error, etc.)
        const isAppPage = document.querySelector('#appSidebar') !== null;
        if (!isAppPage) return;

        if (customSteps && customSteps.length > 0) {
            steps = customSteps;
        }

        isActive = true;
        createTourElements();

        document.body.appendChild(tourOverlay);
        document.body.appendChild(tourTooltip);

        // Add click event to overlay (skip)
        tourOverlay.addEventListener('click', (e) => {
            if (e.target === tourOverlay) {
                // Don't close on overlay click - user must use buttons
            }
        });

        // Keyboard navigation - registered once for the entire tour
        document.addEventListener('keydown', handleKeyboard);

        // Start from first step
        setTimeout(() => showStep(0), 500);
    }

    /**
     * Reset tour (allow replay)
     */
    function resetTour() {
        localStorage.removeItem(STORAGE_KEY);
    }

    // Auto-start after page load (with delay for DOM to render)
    window.addEventListener('load', () => {
        setTimeout(() => {
            startTour();
        }, 1500);
    });

    // Expose API
    window.RhynoOnboarding = {
        start: startTour,
        end: endTour,
        reset: resetTour,
        isCompleted: () => localStorage.getItem(STORAGE_KEY) === 'true'
    };

    console.log('[Rhyno] Onboarding Tour initialized. Call RhynoOnboarding.reset() to replay.');
})();
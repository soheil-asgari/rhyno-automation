window.AppUI = window.AppUI || {};

(() => {
    const showToast = (message, tone = 'primary') => {
        const containerId = 'appToastContainer';
        let container = document.getElementById(containerId);
        if (!container) {
            container = document.createElement('div');
            container.id = containerId;
            container.className = 'toast-container position-fixed bottom-0 start-0 p-3';
            container.style.zIndex = '1080';
            document.body.appendChild(container);
        }

        const toast = document.createElement('div');
        toast.className = `toast align-items-center text-bg-${tone} border-0`;
        toast.setAttribute('role', 'alert');
        toast.innerHTML = `
            <div class="d-flex">
                <div class="toast-body">${message}</div>
                <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>
            </div>`;
        container.appendChild(toast);
        const instance = bootstrap.Toast.getOrCreateInstance(toast, { delay: 3500 });
        toast.addEventListener('hidden.bs.toast', () => toast.remove());
        instance.show();
    };

    const setLoading = (element, loading) => {
        if (!element) return;
        element.toggleAttribute('disabled', loading);
        element.classList.toggle('is-loading', loading);
        element.dataset.loading = loading ? '1' : '0';
    };

const CONFIRM_SEVERITY = {
        danger: { icon: 'bi-exclamation-triangle-fill', color: '#e11d48', bg: '#fef2f2', btnBg: '#e11d48' },
        warning: { icon: 'bi-exclamation-circle-fill', color: '#f59e0b', bg: '#fffbeb', btnBg: '#f59e0b' },
        info: { icon: 'bi-info-circle-fill', color: '#3b82f6', bg: '#eff6ff', btnBg: '#3b82f6' },
        success: { icon: 'bi-check-circle-fill', color: '#10b981', bg: '#f0fdf4', btnBg: '#10b981' }
    };
    const confirm = (options) => {
        const modalEl = document.getElementById('appConfirmDialog');
        const msgEl = document.getElementById('appConfirmMessage');
        const okBtn = document.getElementById('appConfirmOk');
        const cancelBtn = document.getElementById('appConfirmCancel');
        const titleEl = document.getElementById('appConfirmTitle');
        const iconEl = document.getElementById('appConfirmIcon');

        const opts = typeof options === 'string' ? { message: options } : (options || {});
        const message = opts.message || 'آیا مطمئن هستید؟';
        const title = opts.title || 'تایید عملیات';
        const severity = opts.severity || 'info';
        const confirmText = opts.confirmText || 'تایید';
        const cancelText = opts.cancelText || 'انصراف';

        if (!modalEl || !msgEl || !okBtn) {
            return Promise.resolve(window.confirm(message));
        }

        const sev = CONFIRM_SEVERITY[severity] || CONFIRM_SEVERITY.info;

        if (titleEl) titleEl.textContent = title;
        msgEl.textContent = message;
        if (iconEl) {
            iconEl.style.background = sev.color;
            iconEl.innerHTML = `<i class="bi ${sev.icon}"></i>`;
        }
        okBtn.textContent = confirmText;
        okBtn.style.background = sev.btnBg;
        if (cancelBtn) cancelBtn.textContent = cancelText;

        modalEl.style.display = 'flex';
        document.body.style.overflow = 'hidden';

        return new Promise((resolve) => {
            const cleanup = () => {
                okBtn.removeEventListener('click', onConfirm);
                if (cancelBtn) cancelBtn.removeEventListener('click', onCancel);
                modalEl.querySelectorAll('.confirm-dialog-close')
                    .forEach(btn => btn.removeEventListener('click', onCancel));
            };
            const onConfirm = () => { hideConfirm('appConfirmDialog'); cleanup(); resolve(true); };
            const onCancel = () => { hideConfirm('appConfirmDialog'); cleanup(); resolve(false); };
            okBtn.addEventListener('click', onConfirm);
            if (cancelBtn) cancelBtn.addEventListener('click', onCancel);
            modalEl.querySelectorAll('.confirm-dialog-close')
                .forEach(btn => btn.addEventListener('click', onCancel));
        });
    };

    const hideConfirm = (id) => {
        const el = document.getElementById(id || 'appConfirmDialog');
        if (el) { el.style.display = 'none'; document.body.style.overflow = ''; }
    };

    const emptyState = (icon, title, description) => `
        <div class="empty-state text-center py-4">
            <i class="bi ${icon} fs-2 d-block mb-2"></i>
            <div class="fw-semibold">${title}</div>
            <div class="small text-muted">${description}</div>
        </div>`;

    const skeletonRows = (count = 5, columns = 6) => {
        const row = Array.from({ length: columns }, () => '<td><div class="placeholder-glow"><span class="placeholder col-12"></span></div></td>').join('');
        return Array.from({ length: count }, () => `<tr class="skeleton-row">${row}</tr>`).join('');
    };

    const fetchJsonRetry = async (url, options = {}, retries = 2) => {
        let lastError;
        for (let attempt = 0; attempt <= retries; attempt += 1) {
            try {
                const response = await fetch(url, options);
                if (!response.ok) {
                    const body = await response.text();
                    throw new Error(body || `Request failed: ${response.status}`);
                }
                return await response.json();
            } catch (error) {
                lastError = error;
                if (attempt < retries) {
                    await new Promise(resolve => setTimeout(resolve, 350 * (attempt + 1)));
                    continue;
                }
            }
        }

        throw lastError || new Error('Request failed');
    };

    const initCommandPalette = () => {
        const modalEl = document.getElementById('appCommandPalette');
        const input = document.getElementById('appCommandInput');
        const list = document.getElementById('appCommandList');
        if (!modalEl || !input || !list || !window.bootstrap) {
            return;
        }

        const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
        const normalize = (value) => String(value || '')
            .replace(/[ي]/g, 'ی')
            .replace(/[ك]/g, 'ک')
            .replace(/\s+/g, ' ')
            .trim()
            .toLocaleLowerCase('fa-IR');

        const commands = Array.from(document.querySelectorAll('#appSidebar a.sidebar-link[href]'))
            .map((link) => {
                const title = link.querySelector('.link-text')?.textContent?.trim() || link.textContent.trim();
                const group = link.closest('.accordion-item')?.querySelector('.accordion-button')?.textContent?.trim() || 'عمومی';
                const iconClass = link.querySelector('i')?.className || 'bi bi-arrow-left';
                return {
                    title,
                    group,
                    href: link.getAttribute('href'),
                    iconClass,
                    haystack: normalize(`${title} ${group}`)
                };
            })
            .filter((item, index, array) =>
                item.title &&
                item.href &&
                array.findIndex((candidate) => candidate.href === item.href && candidate.title === item.title) === index);

        let activeIndex = 0;
        let visibleCommands = [];

        const render = () => {
            const query = normalize(input.value);
            visibleCommands = commands
                .filter((command) => !query || command.haystack.includes(query))
                .slice(0, 12);
            activeIndex = Math.min(activeIndex, Math.max(visibleCommands.length - 1, 0));

            if (visibleCommands.length === 0) {
                list.innerHTML = '<div class="command-palette-empty">فرمانی پیدا نشد.</div>';
                return;
            }

            list.innerHTML = visibleCommands.map((command, index) => `
                <button type="button" class="command-item ${index === activeIndex ? 'is-active' : ''}" data-index="${index}">
                    <i class="${command.iconClass}"></i>
                    <span><strong>${command.title}</strong><small>${command.group}</small></span>
                    <span class="command-item-badge">باز کردن</span>
                </button>`).join('');
        };

        const runActive = () => {
            const command = visibleCommands[activeIndex];
            if (!command?.href) {
                return;
            }

            window.location.assign(command.href);
        };

        input.addEventListener('input', () => {
            activeIndex = 0;
            render();
        });

        list.addEventListener('mousemove', (event) => {
            const item = event.target.closest('.command-item');
            if (!item) {
                return;
            }

            activeIndex = Number(item.dataset.index || 0);
            render();
        });

        list.addEventListener('click', (event) => {
            const item = event.target.closest('.command-item');
            if (!item) {
                return;
            }

            activeIndex = Number(item.dataset.index || 0);
            runActive();
        });

        input.addEventListener('keydown', (event) => {
            if (event.key === 'ArrowDown') {
                event.preventDefault();
                activeIndex = Math.min(activeIndex + 1, Math.max(visibleCommands.length - 1, 0));
                render();
            } else if (event.key === 'ArrowUp') {
                event.preventDefault();
                activeIndex = Math.max(activeIndex - 1, 0);
                render();
            } else if (event.key === 'Enter') {
                event.preventDefault();
                runActive();
            }
        });

        document.addEventListener('keydown', (event) => {
            if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
                event.preventDefault();
                input.value = '';
                render();
                modal.show();
                setTimeout(() => input.focus(), 80);
            }
        });

        modalEl.addEventListener('shown.bs.modal', () => {
            render();
            input.focus();
        });
    };

    const initIframePrinting = () => {
        document.addEventListener('click', (e) => {
            const btn = e.target.closest('.print-invoice-btn');
            if (btn) {
                e.preventDefault();
                const url = btn.getAttribute('href');
                if (!url || url === '#' || url.startsWith('javascript:')) return;

                // Open temporary print popup
                const printWindow = window.open(url, '_blank', 'width=950,height=850,scrollbars=yes,resizable=yes');
                if (printWindow) {
                    printWindow.focus();
                }
            }
        });
    };
/**
     * Auto-wire data-confirm-* attributes on any element.
     * When clicked, shows a confirm dialog and only proceeds if confirmed.
     * Supported: data-confirm-title, data-confirm-message, data-confirm-severity,
     *            data-confirm-confirm-text, data-confirm-cancel-text
     */
    const initDataConfirmAttributes = () => {
        document.addEventListener('click', async (e) => {
            const el = e.target.closest('[data-confirm-message]');
            if (!el) return;

            const message = el.getAttribute('data-confirm-message');
            if (!message) return;

            e.preventDefault();
            e.stopPropagation();

            const confirmed = await confirm({
                title: el.getAttribute('data-confirm-title') || undefined,
                message: message,
                severity: el.getAttribute('data-confirm-severity') || 'warning',
                confirmText: el.getAttribute('data-confirm-confirm-text') || undefined,
                cancelText: el.getAttribute('data-confirm-cancel-text') || undefined
            });

            if (!confirmed) return;

            const tag = el.tagName.toLowerCase();
            if (tag === 'a') {
                const href = el.getAttribute('href');
                if (href && href !== '#' && !href.startsWith('javascript:')) {
                    window.location.href = href;
                }
            } else if (tag === 'button' || tag === 'input') {
                const form = el.closest('form');
                if (form && el.getAttribute('type') === 'submit') {
                    form.requestSubmit(el);
                } else if (form && el.hasAttribute('data-form-submit')) {
                    form.requestSubmit();
                } else {
                    el.click();
                }
            }
        });
    };

    window.AppUI.toast = showToast;
    window.AppUI.loading = setLoading;
    window.AppUI.confirm = confirm;
    window.AppUI.hideConfirm = hideConfirm;
    window.AppUI.empty = emptyState;
    window.AppUI.skeletonRows = skeletonRows;
    window.AppUI.fetchJsonRetry = fetchJsonRetry;

    const initAll = () => {
        initCommandPalette();
        initIframePrinting();
        initDataConfirmAttributes();
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initAll);
    } else {
        initAll();
    }
})();

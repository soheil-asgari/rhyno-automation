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

    const confirm = (message, onOk) => {
        const modalEl = document.getElementById('appConfirmDialog');
        const msgEl = document.getElementById('appConfirmMessage');
        const okBtn = document.getElementById('appConfirmOk');
        if (!modalEl || !msgEl || !okBtn) {
            if (window.confirm(message)) onOk?.();
            return;
        }

        msgEl.textContent = message;
        const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
        okBtn.onclick = () => {
            modal.hide();
            onOk?.();
        };
        modal.show();
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

    window.AppUI.toast = showToast;
    window.AppUI.loading = setLoading;
    window.AppUI.confirm = confirm;
    window.AppUI.empty = emptyState;
    window.AppUI.skeletonRows = skeletonRows;
    window.AppUI.fetchJsonRetry = fetchJsonRetry;

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initCommandPalette);
    } else {
        initCommandPalette();
    }
})();

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

    window.AppUI.toast = showToast;
    window.AppUI.loading = setLoading;
    window.AppUI.confirm = confirm;
    window.AppUI.empty = emptyState;
    window.AppUI.skeletonRows = skeletonRows;
    window.AppUI.fetchJsonRetry = fetchJsonRetry;
})();

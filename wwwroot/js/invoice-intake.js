/**
 * Tax & Invoice Intake — Client-Side Logic
 * Handles drag-and-drop multi-upload, file preview, metadata editing,
 * OCR processing invocation, and status display.
 * Targets Rahkaran/DimGray enterprise design system (RTL, compact).
 */
(function () {
    'use strict';

    const root = document.querySelector('.invoice-intake-root');
    if (!root) return;

    const maxFiles = parseInt(root.dataset.maxFiles) || 50;
    const maxFileMb = parseInt(root.dataset.maxFileMb) || 20;

    /* ── DOM references ──────────────────────────────────────── */
    const dropzone = document.getElementById('intakeDropzone');
    const fileInput = document.getElementById('intakeFileInput');
    const dropzoneContent = document.getElementById('intakeDropzoneContent');
    const progressEl = document.getElementById('intakeProgress');
    const progressFill = document.getElementById('intakeProgressFill');
    const progressText = document.getElementById('intakeProgressText');
    const errorEl = document.getElementById('intakeError');
    const successEl = document.getElementById('intakeSuccess');
    const cardsGrid = document.getElementById('intakeCardsGrid');
    const emptyState = document.getElementById('intakeEmptyState');

    /* Modal refs */
    const modalOverlay = document.getElementById('intakeModalOverlay');
    const modalClose = document.getElementById('intakeModalClose');
    const modalCancel = document.getElementById('intakeModalCancel');
    const modalSave = document.getElementById('intakeModalSave');
    const previewOverlay = document.getElementById('intakePreviewOverlay');
    const previewClose = document.getElementById('intakePreviewClose');
    const previewBody = document.getElementById('intakePreviewBody');
    const previewTitle = document.getElementById('intakePreviewTitle');

    /* CSRF token */
    const csrfToken = (() => {
        const meta = document.querySelector('meta[name="request-verification-token"]');
        if (meta) return meta.content;
        const form = document.querySelector('form[id="rhGlobalAntiForgery"]');
        if (form) {
            const input = form.querySelector('input[name="__RequestVerificationToken"]');
            return input ? input.value : '';
        }
        const af = document.querySelector('input[name="__RequestVerificationToken"]');
        return af ? af.value : '';
    })();

    /* ── State ─────────────────────────────────────────────────── */
    let items = [];

    /* ── Initial load ──────────────────────────────────────────── */
    fetch('/InvoiceIntake/List')
        .then(r => r.json())
        .then(data => {
            if (data.success && data.items) {
                items = data.items;
                renderAll();
            }
        })
        .catch(() => { });

    /* ── Event binding ─────────────────────────────────────────── */
    dropzone.addEventListener('click', () => fileInput.click());
    fileInput.addEventListener('change', (e) => handleFiles(e.target.files));

    ['dragenter', 'dragover', 'dragleave', 'drop'].forEach(evt => {
        dropzone.addEventListener(evt, preventDefaults, false);
        document.body.addEventListener(evt, preventDefaults, false);
    });

    dropzone.addEventListener('dragenter', () => dropzone.classList.add('drag-active'));
    dropzone.addEventListener('dragover', () => dropzone.classList.add('drag-active'));
    dropzone.addEventListener('dragleave', (e) => {
        if (!dropzone.contains(e.relatedTarget)) dropzone.classList.remove('drag-active');
    });
    dropzone.addEventListener('drop', (e) => {
        dropzone.classList.remove('drag-active');
        handleFiles(e.dataTransfer.files);
    });

    /* Modal events */
    modalClose.addEventListener('click', closeModal);
    modalCancel.addEventListener('click', closeModal);
    modalSave.addEventListener('click', saveMetadata);
    modalOverlay.addEventListener('click', (e) => {
        if (e.target === modalOverlay) closeModal();
    });
    previewClose.addEventListener('click', closePreview);
    previewOverlay.addEventListener('click', (e) => {
        if (e.target === previewOverlay) closePreview();
    });
    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') { closeModal(); closePreview(); }
    });

    /* ── Upload handler ─────────────────────────────────────── */
    function handleFiles(fileList) {
        if (!fileList || fileList.length === 0) return;

        const files = Array.from(fileList);
        const allowed = ['.png', '.jpg', '.jpeg', '.pdf'];
        const valid = files.filter(f => {
            const ext = '.' + f.name.split('.').pop().toLowerCase();
            return allowed.includes(ext);
        });

        if (valid.length === 0) {
            showError('فرمت فایل(های) انتخابی پشتیبانی نمی‌شود. فقط PNG, JPG و PDF مجاز است.');
            return;
        }

        const oversized = valid.filter(f => f.size > maxFileMb * 1024 * 1024);
        if (oversized.length > 0) {
            showError(`حجم ${oversized.length} فایل بیش از ${maxFileMb} مگابایت است.`);
            return;
        }

        const remaining = maxFiles - items.length;
        const toUpload = valid.slice(0, remaining);

        if (toUpload.length === 0) {
            showError(`حداکثر ${maxFiles} فایل قابل بارگذاری است.`);
            return;
        }
        if (toUpload.length < valid.length) {
            showSuccess(`${toUpload.length} فایل بارگذاری شد. (${valid.length - toUpload.length} فایل به دلیل محدودیت تعداد رد شد.)`);
        }

        uploadFiles(toUpload);
    }

    async function uploadFiles(files) {
        showProgress(0, `در حال بارگذاری ۰ از ${files.length} فایل...`);

        const formData = new FormData();
        files.forEach(f => formData.append('files', f));

        try {
            const resp = await fetch('/InvoiceIntake/Upload', {
                method: 'POST',
                body: formData,
                headers: { 'RequestVerificationToken': csrfToken }
            });

            const data = await resp.json();

            if (data.items && data.items.length > 0) {
                data.items.forEach(item => {
                    const existing = items.findIndex(i => i.id === item.id);
                    if (existing >= 0) items[existing] = item;
                    else items.push(item);
                });
                showSuccess(data.message);
            }

            if (data.errors && data.errors.length > 0) {
                showError(data.errors.join('<br>'));
            }

            showProgress(100, `${data.items ? data.items.length : 0} فایل بارگذاری شد.`);
        } catch (err) {
            showError('خطا در ارتباط با سرور. لطفاً دوباره تلاش کنید.');
        } finally {
            setTimeout(() => hideProgress(), 1500);
            /* ── Rendering ──────────────────────────────────────────── */
            function renderAll() {
                if (items.length === 0) {
                    cardsGrid.innerHTML = '';
                    emptyState.classList.remove('hidden');
                    return;
                }

                emptyState.classList.add('hidden');
                cardsGrid.innerHTML = items.map(item => buildCard(item)).join('');
                bindCardEvents();
            }

            function buildCard(item) {
                const statusLabel = statusLabels[item.status] || item.status;
                const statusClass = 'intake-card-status-' + (item.status || 'pending').toLowerCase();
                const hasFields = item.fields && Object.values(item.fields).some(v => v);
                const errors = (item.errors && item.errors.length > 0) ? item.errors : [];
                const schemaIssues = (item.schemaIssues && item.schemaIssues.length > 0) ? item.schemaIssues : [];

                let thumbHtml = '';
                if (item.isImagePreviewable && item.previewUrl) {
                    thumbHtml = `<img src="${escapeHtml(item.previewUrl)}" alt="${escapeHtml(item.fileName)}" loading="lazy" />`;
                } else if (item.isPdf) {
                    thumbHtml = '<div class="intake-card-thumb-pdf"><i class="bi bi-file-earmark-pdf"></i></div>';
                } else {
                    thumbHtml = '<div class="intake-card-thumb-pdf"><i class="bi bi-file-earmark"></i></div>';
                }

                let fieldsHtml = '';
                if (hasFields) {
                    fieldsHtml = '<div class="intake-card-fields"><dl>';
                    if (item.fields.invoiceNumber)
                        fieldsHtml += `<dt>شماره:</dt><dd>${escapeHtml(item.fields.invoiceNumber)}</dd>`;
                    if (item.fields.dateShamsi)
                        fieldsHtml += `<dt>تاریخ:</dt><dd>${escapeHtml(item.fields.dateShamsi)}</dd>`;
                    if (item.fields.totalAmount != null)
                        fieldsHtml += `<dt>مبلغ:</dt><dd>${formatAmount(item.fields.totalAmount)}</dd>`;
                    if (item.fields.sellerName)
                        fieldsHtml += `<dt>فروشنده:</dt><dd>${escapeHtml(item.fields.sellerName)}</dd>`;
                    if (item.fields.buyerName)
                        fieldsHtml += `<dt>خریدار:</dt><dd>${escapeHtml(item.fields.buyerName)}</dd>`;
                    fieldsHtml += '</dl></div>';
                }

                let errorHtml = '';
                if (errors.length > 0) {
                    errorHtml = '<div class="intake-card-error">' + errors.map(escapeHtml).join('<br>') + '</div>';
                }
                if (schemaIssues.length > 0) {
                    errorHtml += '<div class="intake-card-error" style="color:#f08046;">' +
                        schemaIssues.map(s => escapeHtml(s.message)).join('<br>') + '</div>';
                }

                const confidence = item.ocrConfidence > 0
                    ? ` — دقت OCR: ${(item.ocrConfidence).toFixed(0)}%`
                    : '';

                return `
        <div class="intake-card" data-id="${item.id}">
            <div class="intake-card-thumb" data-action="preview" data-id="${item.id}">
                ${thumbHtml}
                <span class="intake-card-status-badge ${statusClass}">${statusLabel}</span>
            </div>
            <div class="intake-card-body">
                <div class="intake-card-filename" title="${escapeHtml(item.fileName)}">${escapeHtml(item.fileName)}</div>
                <div class="intake-card-meta">${formatSize(item.fileSize)}${confidence}</div>
                ${fieldsHtml}
                ${errorHtml}
            </div>
            <div class="intake-card-actions">
                <button class="sg-btn sg-btn-small" data-action="process" data-id="${item.id}">
                    <i class="bi bi-cpu"></i> پردازش
                </button>
                <button class="sg-btn sg-btn-small" data-action="edit" data-id="${item.id}">
                    <i class="bi bi-pencil"></i> ویرایش
                </button>
                <button class="sg-btn sg-btn-small sg-btn-danger" data-action="delete" data-id="${item.id}">
                    <i class="bi bi-trash"></i>
                </button>
            </div>
        </div>`;
            }

            function bindCardEvents() {
                cardsGrid.querySelectorAll('[data-action]').forEach(el => {
                    el.addEventListener('click', function (e) {
                        e.stopPropagation();
                        const action = this.dataset.action;
                        const id = this.dataset.id;
                        const item = items.find(i => i.id === id);
                        if (!item) return;

                        switch (action) {
                            case 'preview': openPreview(item); break;
                            case 'process': processItem(item); break;
                            case 'edit': openEditModal(item); break;
                            case 'delete': deleteItem(item); break;
                        }
                    });
                });
            }

            renderAll();
            fileInput.value = '';
        }
    }

    /* ── OCR Process ────────────────────────────────────────── */
    async function processItem(item) {
        updateItemStatus(item.id, 'Processing');
        renderAll();

        try {
            const resp = await fetch(`/InvoiceIntake/Process?id=${item.id}`, {
                method: 'POST',
                headers: { 'RequestVerificationToken': csrfToken }
            });

            const data = await resp.json();
            const idx = items.findIndex(i => i.id === item.id);
            if (idx >= 0 && data.item) {
                items[idx] = data.item;
            }

            if (data.success) {
                showSuccess(data.message);
            } else {
                showError(data.message || 'پردازش ناموفق بود.');
            }
        } catch (err) {
            updateItemStatus(item.id, 'Failed');
            showError('خطا در پردازش OCR. لطفاً دوباره تلاش کنید.');
        } finally {
            renderAll();
        }
    }

    /* ── Delete ─────────────────────────────────────────────── */
    async function deleteItem(item) {
        if (!confirm(`آیا از حذف «${item.fileName}» اطمینان دارید؟`)) return;

        try {
            const resp = await fetch(`/InvoiceIntake/Delete?id=${item.id}`, {
                method: 'POST',
                headers: { 'RequestVerificationToken': csrfToken }
            });

            const data = await resp.json();
            if (data.success) {
                items = items.filter(i => i.id !== item.id);
                showSuccess(data.message);
            } else {
                showError(data.message);
            }
        } catch (err) {
            showError('خطا در حذف فایل.');
        }

        renderAll();
    }

    /* ── Preview ────────────────────────────────────────────── */
    function openPreview(item) {
        previewTitle.innerHTML = '<i class="bi bi-eye"></i> ' + escapeHtml(item.fileName);

        if (item.isImagePreviewable && item.previewUrl) {
            previewBody.innerHTML = `<img src="${escapeHtml(item.previewUrl)}" alt="${escapeHtml(item.fileName)}" />`;
        } else if (item.isPdf && item.previewUrl) {
            previewBody.innerHTML = `<iframe src="${escapeHtml(item.previewUrl)}" title="${escapeHtml(item.fileName)}"></iframe>`;
        } else {
            previewBody.innerHTML = '<p class="text-muted">پیش‌نمایش برای این نوع فایل در دسترس نیست.</p>';
        }

        previewOverlay.classList.remove('hidden');
    }

    function closePreview() {
        previewOverlay.classList.add('hidden');
        previewBody.innerHTML = '';
    }

    /* ── Edit Metadata Modal ────────────────────────────────── */
    function openEditModal(item) {
        document.getElementById('modalFileId').value = item.id;
        document.getElementById('modalInvoiceNumber').value = (item.fields && item.fields.invoiceNumber) || '';
        document.getElementById('modalDateShamsi').value = (item.fields && item.fields.dateShamsi) || '';
        document.getElementById('modalTotalAmount').value = (item.fields && item.fields.totalAmount != null) ? item.fields.totalAmount : '';
        document.getElementById('modalSellerName').value = (item.fields && item.fields.sellerName) || '';
        document.getElementById('modalBuyerName').value = (item.fields && item.fields.buyerName) || '';
        modalOverlay.classList.remove('hidden');
    }

    function closeModal() {
        modalOverlay.classList.add('hidden');
    }

    async function saveMetadata() {
        const id = document.getElementById('modalFileId').value;
        const invoiceNumber = document.getElementById('modalInvoiceNumber').value.trim();
        const dateShamsi = document.getElementById('modalDateShamsi').value.trim();
        const totalAmountRaw = document.getElementById('modalTotalAmount').value.trim();
        const sellerName = document.getElementById('modalSellerName').value.trim();
        const buyerName = document.getElementById('modalBuyerName').value.trim();

        const totalAmount = totalAmountRaw ? parseFloat(totalAmountRaw.replace(/[,\s]/g, '')) : null;

        const payload = { invoiceNumber, dateShamsi, totalAmount, sellerName, buyerName };

        try {
            const resp = await fetch(`/InvoiceIntake/UpdateMetadata?id=${id}`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': csrfToken
                },
                body: JSON.stringify(payload)
            });

            const data = await resp.json();
            const idx = items.findIndex(i => i.id === id);
            if (idx >= 0 && data.item) {
                items[idx] = data.item;
            }

            if (data.success) {
                showSuccess(data.message);
            } else {
                showError(data.message || 'خطا در ذخیره متادیتا.');
            }

            closeModal();
            renderAll();
        } catch (err) {
            showError('خطا در ارتباط با سرور.');
        }
    }

    /* ── Helpers ────────────────────────────────────────────── */
    function updateItemStatus(id, status) {
        const item = items.find(i => i.id === id);
        if (item) item.status = status;
    }

    function showProgress(pct, msg) {
        progressEl.classList.remove('hidden');
        progressFill.style.width = Math.min(100, Math.max(0, pct)) + '%';
        progressText.textContent = msg;
    }

    function hideProgress() {
        progressEl.classList.add('hidden');
    }

    function showError(msg) {
        errorEl.innerHTML = msg;
        errorEl.classList.remove('hidden');
        setTimeout(function () { errorEl.classList.add('hidden'); }, 6000);
    }

    function showSuccess(msg) {
        successEl.innerHTML = msg;
        successEl.classList.remove('hidden');
        setTimeout(function () { successEl.classList.add('hidden'); }, 4000);
    }

    function preventDefaults(e) {
        e.preventDefault();
        e.stopPropagation();
    }

    var statusLabels = {
        'Pending': 'در انتظار',
        'Processing': 'در حال پردازش',
        'Completed': 'تکمیل شده',
        'Failed': 'ناموفق'
    };

    function formatSize(bytes) {
        if (bytes < 1024) return bytes + ' B';
        if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
        return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
    }

    function formatAmount(val) {
        if (val == null) return '';
        return Number(val).toLocaleString('fa-IR') + ' ریال';
    }

    function escapeHtml(str) {
        if (!str) return '';
        var div = document.createElement('div');
        div.textContent = str;
        return div.innerHTML;
    }
})();
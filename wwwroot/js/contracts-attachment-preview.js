// ── Attachment Preview Modal ────────────────────────────────────
(function () {
    'use strict';

    var modal = document.getElementById('attachmentPreviewModal');
    if (!modal) return;

    var closeBtn = document.getElementById('previewModalClose');
    var loadingEl = document.getElementById('previewLoading');
    var contentEl = document.getElementById('previewContent');
    var titleEl = document.getElementById('previewModalTitle');
    var footerEl = document.getElementById('previewFooter');
    var fileInfoEl = document.getElementById('previewFileInfo');
    var downloadLink = document.getElementById('previewDownloadLink');

    // Content containers
    var pdfContainer = document.getElementById('previewPdfContainer');
    var pdfFrame = document.getElementById('previewPdfFrame');
    var imgContainer = document.getElementById('previewImageContainer');
    var imgEl = document.getElementById('previewImage');
    var textContainer = document.getElementById('previewTextContainer');
    var textEl = document.getElementById('previewText');
    var unsupportedEl = document.getElementById('previewUnsupported');
    var unsupportedDetail = document.getElementById('previewUnsupportedDetail');
    var errorEl = document.getElementById('previewError');

    /** Determine if a content type / extension is previewable */
    var imageExts = ['.jpg', '.jpeg', '.png', '.gif', '.bmp', '.tiff', '.tif', '.webp', '.svg'];
    var textExts = ['.txt', '.csv', '.log', '.json', '.xml', '.md', '.yaml', '.yml', '.ini', '.cfg', '.config'];

    function getPreviewType(fileName, contentType) {
        var ext = (fileName || '').toLowerCase();
        var dotIdx = ext.lastIndexOf('.');
        ext = dotIdx >= 0 ? ext.substring(dotIdx) : '';

        if (ext === '.pdf') return 'pdf';
        if (imageExts.indexOf(ext) >= 0) return 'image';
        if (textExts.indexOf(ext) >= 0) return 'text';

        var ct = (contentType || '').toLowerCase();
        if (ct.indexOf('pdf') >= 0) return 'pdf';
        if (ct.indexOf('image') >= 0) return 'image';
        if (ct.indexOf('text') >= 0) return 'text';

        return 'unsupported';
    }

    /** Format file size */
    function formatSize(bytes) {
        if (!bytes) return '';
        if (bytes < 1024) return bytes + ' B';
        if (bytes < 1048576) return (bytes / 1024).toFixed(1) + ' KB';
        return (bytes / 1048576).toFixed(1) + ' MB';
    }

    function hideAllPanels() {
        [pdfContainer, imgContainer, textContainer, unsupportedEl, errorEl].forEach(function (el) {
            if (el) el.classList.add('hidden');
        });
    }

    /** Open the preview modal for a given attachment */
    window.openAttachmentPreview = function (attachmentId, fileName, contentType, fileSize, downloadUrl) {
        if (!modal) return;

        // Reset UI
        hideAllPanels();
        loadingEl.classList.remove('hidden');
        contentEl.classList.add('hidden');
        footerEl.classList.add('hidden');
        titleEl.textContent = 'پیش‌نمایش: ' + fileName;
        modal.classList.remove('hidden');
        document.body.classList.add('overflow-hidden');

        var previewType = getPreviewType(fileName, contentType);
        var previewUrl = '/Contracts/Attachment/' + attachmentId + '/Preview';

        if (previewType === 'pdf') {
            // دریافت PDF به صورت Blob با Content-Type خنثی (application/octet-stream)
            // تا IDM نتواند هدر application/pdf را ببیند
            var blobUrl = previewUrl + '?blob=1';
            fetch(blobUrl)
                .then(function (resp) {
                    if (!resp.ok) throw new Error('HTTP ' + resp.status);
                    return resp.blob();
                })
                .then(function (blob) {
                    // بازسازی Blob با Content-Type صحیح PDF تا Chrome PDFium فعال شود
                    var pdfBlob = new Blob([blob], { type: 'application/pdf' });
                    var objUrl = URL.createObjectURL(pdfBlob);
                    // لود Blob در iframe — Chrome PDFium رندر را بر عهده می‌گیرد
                    pdfFrame.src = objUrl;
                    pdfContainer.innerHTML = '';
                    pdfContainer.appendChild(pdfFrame);
                    loadingEl.classList.add('hidden');
                    contentEl.classList.remove('hidden');
                    pdfContainer.classList.remove('hidden');
                    showFooter(fileName, fileSize, downloadUrl || previewUrl);
                })
                .catch(function () {
                    // fallback: مستقیم در iframe با URL اصلی (IDM ممکن است بگیرد)
                    pdfFrame.src = previewUrl;
                    loadingEl.classList.add('hidden');
                    contentEl.classList.remove('hidden');
                    pdfContainer.classList.remove('hidden');
                    showFooter(fileName, fileSize, downloadUrl || previewUrl);
                });
        } else if (previewType === 'image') {
            imgEl.onload = function () {
                loadingEl.classList.add('hidden');
                contentEl.classList.remove('hidden');
                imgContainer.classList.remove('hidden');
                showFooter(fileName, fileSize, downloadUrl);
            };
            imgEl.onerror = function () { showError(); };
            imgEl.src = previewUrl;
        } else if (previewType === 'text') {
            var xhr = new XMLHttpRequest();
            xhr.open('GET', previewUrl, true);
            xhr.onload = function () {
                if (xhr.status === 200) {
                    try {
                        var ct = xhr.getResponseHeader('Content-Type') || '';
                        if (ct.indexOf('application/json') >= 0) {
                            var resp = JSON.parse(xhr.responseText);
                            if (resp.previewUnavailable) {
                                loadingEl.classList.add('hidden');
                                contentEl.classList.remove('hidden');
                                unsupportedEl.classList.remove('hidden');
                                unsupportedDetail.textContent = resp.fileName + ' (' + (resp.contentType || '') + ', ' + formatSize(resp.fileSize) + ')';
                                showFooter(fileName, fileSize, downloadUrl);
                                return;
                            }
                        }
                    } catch (e) { /* not JSON, treat as plain text */ }
                    loadingEl.classList.add('hidden');
                    contentEl.classList.remove('hidden');
                    textContainer.classList.remove('hidden');
                    textEl.textContent = xhr.responseText;
                    showFooter(fileName, fileSize, downloadUrl);
                } else { showError(); }
            };
            xhr.onerror = function () { showError(); };
            xhr.send();
        } else {
            // Unsupported — hit endpoint to confirm
            var xhr2 = new XMLHttpRequest();
            xhr2.open('GET', previewUrl, true);
            xhr2.setRequestHeader('Accept', 'application/json');
            xhr2.onload = function () {
                loadingEl.classList.add('hidden');
                contentEl.classList.remove('hidden');
                if (xhr2.status === 200) {
                    try {
                        var resp = JSON.parse(xhr2.responseText);
                        if (resp.previewUnavailable) {
                            unsupportedEl.classList.remove('hidden');
                            unsupportedDetail.textContent = resp.fileName + ' (' + (resp.contentType || '') + ', ' + formatSize(resp.fileSize) + ')';
                            showFooter(fileName, fileSize, downloadUrl);
                        } else { showError(); }
                    } catch (e) { showError(); }
                } else { showError(); }
            };
            xhr2.onerror = function () { showError(); };
            xhr2.send();
        }
    };

    function showFooter(fileName, fileSize, downloadUrl) {
        footerEl.classList.remove('hidden');
        fileInfoEl.textContent = fileName + (fileSize ? ' — ' + formatSize(fileSize) : '');
        downloadLink.href = downloadUrl || '#';
    }

    function showError() {
        loadingEl.classList.add('hidden');
        contentEl.classList.remove('hidden');
        errorEl.classList.remove('hidden');
    }

    // ── Close Modal ──────────────────────────────────────────────

    function closeModal() {
        modal.classList.add('hidden');
        document.body.style.overflow = '';
        // Clean up all panel states
        pdfFrame.src = '';
        imgEl.removeAttribute('src');
        imgEl.onload = null;
        imgEl.onerror = null;
        textEl.textContent = '';
        hideAllPanels();
    }

    closeBtn.addEventListener('click', closeModal);

    modal.addEventListener('click', function (e) {
        if (e.target === modal) closeModal();
    });

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && !modal.classList.contains('hidden')) {
            closeModal();
        }
    });

})();
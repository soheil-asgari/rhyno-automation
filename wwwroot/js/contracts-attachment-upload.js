(function () {
    'use strict';

    var tp = document.getElementById('tab-attachments');
    if (!tp) return;
    var cid = tp.getAttribute('data-contract-id');
    if (!cid) return;

    var dz = document.getElementById('contractDropzone');
    var fi = document.getElementById('contractFileInput');
    var ee = document.getElementById('contractUploadErrors');
    var qe = document.getElementById('contractUploadQueue');
    var ab = document.getElementById('contractAttachmentsBody');
    var ae = document.getElementById('contractAttachmentsEmpty');
    var al = document.getElementById('contractAttachmentsList');
    var LIM = 20 * 1024 * 1024;
    var EXT = ['.pdf', '.doc', '.docx', '.xls', '.xlsx', '.jpg', '.jpeg', '.png', '.gif', '.tiff', '.tif', '.bmp', '.zip', '.rar', '.7z', '.txt'];

    // ── Modal form (prevent redirect on submit) ──────────────
    var modalForm = document.querySelector('#uploadAttachmentModal form');
    if (modalForm) {
        modalForm.addEventListener('submit', function (e) {
            e.preventDefault();
            var fileInput = modalForm.querySelector('input[type="file"]');
            if (!fileInput || !fileInput.files || !fileInput.files.length) return;
            hf(fileInput.files);
            fileInput.value = '';
            // close modal
            var modal = modalForm.closest('.fixed');
            if (modal) {
                modal.classList.add('hidden');
                document.body.classList.remove('overflow-hidden');
            }
        });
    }

    if (!dz || !fi) return;

    function csrf() {
        var i = document.querySelector('input[name=__RequestVerificationToken]');
        if (i) return i.value;
        return '';
    }

    function esc(s) {
        if (!s) return '';
        var d = document.createElement('div');
        d.textContent = s;
        return d.innerHTML;
    }

    function sz(b) {
        if (b < 1024) return b + ' B';
        if (b < 1048576) return (b / 1024).toFixed(1) + ' KB';
        return (b / 1048576).toFixed(1) + ' MB';
    }

    function ext(fn) {
        var i = fn.lastIndexOf('.');
        return i >= 0 ? fn.substring(i).toLowerCase() : '';
    }

    function val(fs) {
        var e = [], v = [];
        for (var i = 0; i < fs.length; i++) {
            var f = fs[i], x = ext(f.name);
            if (EXT.indexOf(x) === -1) {
                e.push({ file: f, msg: 'نوع فایل ' + esc(x || '(بدون پسوند)') + ' مجاز نیست.' });
                continue;
            }
            if (f.size > LIM) {
                e.push({ file: f, msg: 'فایل ' + esc(f.name) + ' (' + sz(f.size) + ') بزرگتر از حد مجاز (20 MB) است.' });
                continue;
            }
            v.push(f);
        }
        return { errors: e, valid: v };
    }

    function se(er) {
        if (!er.length) { ee.classList.remove('active'); ee.innerHTML = ''; return; }
        var h = '';
        for (var i = 0; i < er.length; i++) {
            h += '<div class="contract-upload-error-item"><i class="bi bi-exclamation-triangle-fill"></i><span>' + er[i].msg + '</span></div>';
        }
        ee.innerHTML = h;
        ee.classList.add('active');
    }

    function ce() { ee.classList.remove('active'); ee.innerHTML = ''; }

    function aq(f) {
        var id = 'cq-' + Date.now() + '-' + Math.random().toString(36).substring(2, 8);
        var h = '<div class="contract-upload-item" data-queue-id="' + id + '">' +
            '<span class="contract-upload-item-name" title="' + esc(f.name) + '"><i class="bi bi-file-earmark ml-1"></i>' + esc(f.name) + '</span>' +
            '<span class="contract-upload-item-status" id="' + id + '-status">در انتظار...</span>' +
            '<div class="contract-upload-item-bar"><div class="contract-upload-item-fill" id="' + id + '-fill"></div></div>' +
            '</div>';
        qe.classList.add('active');
        qe.insertAdjacentHTML('beforeend', h);
        return id;
    }

    function uq(id, p, t, c) {
        var fl = document.getElementById(id + '-fill');
        var st = document.getElementById(id + '-status');
        if (fl) { fl.style.width = Math.min(100, Math.max(0, p)) + '%'; fl.className = 'contract-upload-item-fill'; if (c) fl.classList.add(c); }
        if (st) st.textContent = t;
    }

    function fq(id, ok, msg) {
        uq(id, 100, ok ? '✅' : '❌ ' + msg, ok ? 'success' : 'error');
    }

    function ul(f, qid) {
        var fd = new FormData();
        fd.append('file', f);
        fd.append('contractId', cid);
        fd.append('__RequestVerificationToken', csrf());
        var x = new XMLHttpRequest();
        x.upload.addEventListener('progress', function (e) {
            if (e.lengthComputable) uq(qid, Math.round((e.loaded / e.total) * 100), Math.round((e.loaded / e.total) * 100) + '%');
        });
        x.addEventListener('load', function () {
            if (x.status >= 200 && x.status < 300) {
                try {
                    var r = JSON.parse(x.responseText);
                    if (r.success) { fq(qid, true, ''); ar(r.attachment); }
                    else { fq(qid, false, r.message || 'خطا در بارگذاری'); }
                } catch (ex) { fq(qid, false, 'پاسخ نامعتبر سرور'); }
                // Remove queue item after delay
                setTimeout(function () {
                    var qi = qe.querySelector('[data-queue-id="' + qid + '"]');
                    if (qi) { qi.remove(); if (!qe.querySelector('.contract-upload-item')) qe.classList.remove('active'); }
                }, 3000);
            } else { fq(qid, false, 'خطای ' + x.status); }
        });
        x.addEventListener('error', function () { fq(qid, false, 'خطای شبکه'); });
        x.addEventListener('abort', function () { fq(qid, false, 'لغو شد'); });
        x.open('POST', '/Contracts/UploadAttachment', true);
        x.setRequestHeader('X-Requested-With', 'XMLHttpRequest');
        x.send(fd);
    }

    function ar(a) {
        if (!a) return;
        var s = a.fileSize > 1024 ? Math.round(a.fileSize / 1024) + ' KB' : a.fileSize + ' B';
        var downloadUrl = '/Contracts/DownloadAttachment/' + a.id;
        var h = '<tr class="hover:bg-slate-50/50 dark:hover:bg-slate-800/30 transition" data-attachment-id="' + esc(a.id.toString()) + '">' +
            '<td class="px-3 py-2 font-bold text-slate-800 dark:text-slate-200"><i class="bi bi-file-earmark ml-1 text-slate-400"></i>' + esc(a.fileName) + '</td>' +
            '<td class="px-3 py-2 text-slate-600">' + esc(a.contentType || '-') + '</td>' +
            '<td class="px-3 py-2 font-mono tabular-nums text-slate-600">' + s + '</td>' +
            '<td class="px-3 py-2 text-slate-600">' + esc(a.uploadedByUserName || '-') + '</td>' +
            '<td class="px-3 py-2 font-mono tabular-nums text-slate-600">' + esc(a.createdAt || '-') + '</td>' +
            '<td class="px-3 py-2 text-center"><div class="inline-flex gap-1">' +
            '<button type="button" onclick="openAttachmentPreview(' + a.id + ', \'' + esc(a.fileName).replace(/'/g, "\\'") + '\', \'' + esc(a.contentType || '') + '\', ' + a.fileSize + ', \'' + downloadUrl + '\')" class="rounded-lg border border-slate-200 bg-white dark:bg-slate-950 px-2 py-1 text-[9px] font-bold text-slate-600 hover:bg-sky-50 hover:text-sky-600 hover:border-sky-200 dark:border-slate-800 dark:text-slate-400 dark:hover:bg-sky-950/20 dark:hover:text-sky-400 transition cursor-pointer" title="پیش‌نمایش"><i class="bi bi-eye"></i></button>' +
            '<a href="' + downloadUrl + '" class="rounded-lg border border-slate-200 bg-white dark:bg-slate-950 px-2 py-1 text-[9px] font-bold text-slate-600 hover:bg-slate-50 dark:border-slate-800 dark:text-slate-400 dark:hover:bg-slate-800 no-underline transition" title="دانلود"><i class="bi bi-download"></i></a>' +
            '<button type="button" onclick="deleteAttachment(' + a.id + ')" class="rounded-lg border border-slate-200 bg-white dark:bg-slate-950 px-2 py-1 text-[9px] font-bold text-slate-600 hover:bg-rose-50 hover:text-rose-600 hover:border-rose-200 dark:border-slate-800 dark:text-slate-400 dark:hover:bg-rose-950/20 dark:hover:text-rose-400 transition" title="حذف"><i class="bi bi-trash"></i></button>' +
            '</div></td></tr>';
        if (ab) { ab.insertAdjacentHTML('beforeend', h); }
        else {
            var th = '<div class="overflow-x-auto"><table class="w-full text-[11px]"><thead><tr class="border-b border-slate-100 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-950/30">' +
                '<th class="text-right px-3 py-2 font-bold text-slate-500">نام فایل</th>' +
                '<th class="text-right px-3 py-2 font-bold text-slate-500">نوع</th>' +
                '<th class="text-right px-3 py-2 font-bold text-slate-500">حجم</th>' +
                '<th class="text-right px-3 py-2 font-bold text-slate-500">بارگذاری‌کننده</th>' +
                '<th class="text-right px-3 py-2 font-bold text-slate-500">تاریخ</th>' +
                '<th class="text-center px-3 py-2 font-bold text-slate-500">عملیات</th>' +
                '</tr></thead><tbody class="divide-y divide-slate-50 dark:divide-slate-800/60" id="contractAttachmentsBody">' + h + '</tbody></table></div>';
            if (al) {
                if (ae) ae.remove();
                var ex = al.querySelector('.overflow-x-auto');
                if (ex) ex.querySelector('tbody').insertAdjacentHTML('beforeend', h);
                else al.insertAdjacentHTML('beforeend', th);
            }
        }
        // Re-bind ab reference
        ab = document.getElementById('contractAttachmentsBody');
    }

    // ── Delete via AJAX ────────────────────────────────────
    window.deleteAttachment = function (attachmentId) {
        if (!confirm('فایل حذف شود؟')) return;
        var fd = new FormData();
        fd.append('attachmentId', attachmentId);
        fd.append('contractId', cid);
        fd.append('__RequestVerificationToken', csrf());
        var x = new XMLHttpRequest();
        x.addEventListener('load', function () {
            if (x.status >= 200 && x.status < 300) {
                try {
                    var r = JSON.parse(x.responseText);
                    if (r.success) {
                        // Remove row
                        var row = document.querySelector('[data-attachment-id="' + attachmentId + '"]');
                        if (row) row.remove();
                        // Show empty state if no rows left
                        if (ab && !ab.querySelector('tr')) {
                            var tableWrapper = document.querySelector('#contractAttachmentsList .overflow-x-auto');
                            if (tableWrapper) tableWrapper.remove();
                            if (ae) ae.classList.remove('hidden');
                            // Show empty state if it was removed, recreate
                            if (!document.getElementById('contractAttachmentsEmpty')) {
                                var emptyHtml = '<div class="text-center py-6" id="contractAttachmentsEmpty">' +
                                    '<div class="inline-flex items-center justify-center h-10 w-10 rounded-full bg-slate-100 dark:bg-slate-800 mb-2">' +
                                    '<i class="bi bi-paperclip text-slate-400"></i></div>' +
                                    '<p class="text-[11px] text-slate-400 dark:text-slate-500">هیچ ضمیمه‌ای وجود ندارد.</p></div>';
                                var list = document.getElementById('contractAttachmentsList');
                                if (list) list.insertAdjacentHTML('beforeend', emptyHtml);
                            }
                        }
                    } else { alert(r.message || 'خطا در حذف فایل'); }
                } catch (ex) { alert('خطا در حذف فایل'); }
            } else { alert('خطا در حذف فایل'); }
        });
        x.addEventListener('error', function () { alert('خطای شبکه'); });
        x.open('POST', '/Contracts/DeleteAttachment', true);
        x.setRequestHeader('X-Requested-With', 'XMLHttpRequest');
        x.send(fd);
    };

    function hf(list) {
        ce();
        var fs = [];
        for (var i = 0; i < list.length; i++) fs.push(list[i]);
        var r = val(fs);
        se(r.errors);
        for (var j = 0; j < r.valid.length; j++) {
            var qid = aq(r.valid[j]);
            ul(r.valid[j], qid);
        }
    }

    function pd(e) { e.preventDefault(); e.stopPropagation(); }

    dz.addEventListener('click', function (e) { fi.click(); });
    fi.addEventListener('change', function (e) {
        if (e.target.files && e.target.files.length > 0) hf(e.target.files);
        e.target.value = '';
    });
    ['dragenter', 'dragover', 'dragleave', 'drop'].forEach(function (ev) {
        dz.addEventListener(ev, pd, false);
        document.body.addEventListener(ev, pd, false);
    });
    dz.addEventListener('dragenter', function (e) { pd(e); dz.classList.add('drag-over'); }, false);
    dz.addEventListener('dragover', function (e) { pd(e); dz.classList.add('drag-over'); }, false);
    dz.addEventListener('dragleave', function (e) { pd(e); if (!dz.contains(e.relatedTarget)) dz.classList.remove('drag-over'); }, false);
    dz.addEventListener('drop', function (e) { pd(e); dz.classList.remove('drag-over'); if (e.dataTransfer.files && e.dataTransfer.files.length > 0) hf(e.dataTransfer.files); }, false);

    // ── Convert server-rendered delete forms to AJAX ──────
    document.querySelectorAll('#contractAttachmentsList form[action*="DeleteAttachment"]').forEach(function (form) {
        form.addEventListener('submit', function (e) {
            e.preventDefault();
            var attId = form.querySelector('input[name="attachmentId"]');
            if (attId) window.deleteAttachment(parseInt(attId.value));
        });
    });

})();
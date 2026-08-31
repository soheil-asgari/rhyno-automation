(function () {
    'use strict';

    var tp = document.getElementById('tab-attachments');
    if (!tp) {
        console.warn('[Attachments] المان #tab-attachments یافت نشد.');
        return;
    }
    var cid = tp.getAttribute('data-contract-id');
    if (!cid) {
        console.warn('[Attachments] شناسه قرارداد (data-contract-id) یافت نشد.');
        return;
    }

    var dz = document.getElementById('contractDropzone');
    var fi = document.getElementById('contractFileInput');
    var ee = document.getElementById('contractUploadErrors');
    var qe = document.getElementById('contractUploadQueue');
    var ab = document.getElementById('contractAttachmentsBody');
    var ae = document.getElementById('contractAttachmentsEmpty');
    var al = document.getElementById('contractAttachmentsList');
    var LIM = 20 * 1024 * 1024;
    var EXT = ['.pdf', '.doc', '.docx', '.xls', '.xlsx', '.jpg', '.jpeg', '.png', '.gif', '.tiff', '.tif', '.bmp', '.zip', '.rar', '.7z', '.txt'];

    // ── مدال تاییدیه اختصاصی با Tailwind بدون وابستگی خارجی ──────────────
    function showLocalConfirm(title, message, onConfirm) {
        var existingModal = document.getElementById('rhynoConfirmModal');
        if (existingModal) existingModal.remove();

        var modalHtml = 
            '<div id="rhynoConfirmModal" class="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-sm p-4 transition-opacity">' +
                '<div class="bg-white dark:bg-slate-900 rounded-2xl shadow-2xl max-w-sm w-full p-5 border border-slate-200 dark:border-slate-800 transform transition-all">' +
                    '<div class="flex items-center gap-3 mb-3 text-rose-600 dark:text-rose-500">' +
                        '<div class="flex items-center justify-center h-9 w-9 rounded-xl bg-rose-50 dark:bg-rose-950/50">' +
                            '<i class="bi bi-trash text-lg"></i>' +
                        '</div>' +
                        '<h3 class="font-bold text-sm text-slate-800 dark:text-slate-100">' + title + '</h3>' +
                    '</div>' +
                    '<p class="text-xs text-slate-600 dark:text-slate-400 mb-6 leading-relaxed">' + message + '</p>' +
                    '<div class="flex justify-end gap-2">' +
                        '<button type="button" id="btnConfirmCancel" class="px-3.5 py-2 text-xs font-bold text-slate-600 hover:bg-slate-100 dark:text-slate-400 dark:hover:bg-slate-800 rounded-xl transition cursor-pointer">انصراف</button>' +
                        '<button type="button" id="btnConfirmOk" class="px-3.5 py-2 text-xs font-bold text-white bg-rose-600 hover:bg-rose-700 rounded-xl shadow-sm transition cursor-pointer">حذف شود</button>' +
                    '</div>' +
                '</div>' +
            '</div>';

        document.body.insertAdjacentHTML('beforeend', modalHtml);

        var modal = document.getElementById('rhynoConfirmModal');
        var btnCancel = document.getElementById('btnConfirmCancel');
        var btnOk = document.getElementById('btnConfirmOk');

        function closeModal() {
            if (modal) modal.remove();
        }

        btnCancel.addEventListener('click', closeModal);
        btnOk.addEventListener('click', function () {
            closeModal();
            onConfirm();
        });
    }

    function csrf() {
        var i = document.querySelector('input[name=__RequestVerificationToken]');
        return i ? i.value : '';
    }

    function esc(s) {
        if (!s) return '';
        var d = document.createElement('div');
        d.textContent = s;
        return d.innerHTML;
    }

    // ── تابع حذف اصلی با لاگ دقیق کنسول ────────────────────────────────
    window.deleteAttachment = function (attachmentId) {
        console.log('[DeleteAttachment] ۱. کلیک بر روی حذف ضمیمه ID:', attachmentId);

        showLocalConfirm('حذف ضمیمه', 'آیا از حذف این فایل ضمیمه اطمینان دارید؟ این عملیات قابل بازگشت نیست.', async function () {
            console.log('[DeleteAttachment] ۲. کاربر دکمه حذف را در مدال کلیک کرد.');

            var token = csrf();
            console.log('[DeleteAttachment] ۳. وضعیت CSRF Token:', token ? 'یافت شد' : '❌ یافت نشد!');

            var fd = new FormData();
            fd.append('attachmentId', attachmentId);
            fd.append('contractId', cid);
            fd.append('__RequestVerificationToken', token);

            try {
                console.log('[DeleteAttachment] ۴. ارسال درخواست POST به /Contracts/DeleteAttachment...');
                var response = await fetch('/Contracts/DeleteAttachment', {
                    method: 'POST',
                    headers: { 'X-Requested-With': 'XMLHttpRequest' },
                    body: fd
                });

                console.log('[DeleteAttachment] ۵. کد وضعیت HTTP دریافت شد:', response.status);

                if (!response.ok) {
                    console.error('[DeleteAttachment] ❌ خطا در پاسخ سرور. کد وضعیت:', response.status);
                    alert('خطایی در سرور رخ داد (کد ' + response.status + ')');
                    return;
                }

                var r = await response.json();
                console.log('[DeleteAttachment] ۶. پاسخ JSON سرور:', r);

                if (r.success) {
                    console.log('[DeleteAttachment] ۷. عملیات حذف در سرور موفق بود. در حال حذف سطر DOM...');
                    var row = document.querySelector('[data-attachment-id="' + attachmentId + '"]');
                    if (row) {
                        row.remove();
                        console.log('[DeleteAttachment] ۸. سطر با موفقیت از جدول پاک شد.');
                    } else {
                        console.warn('[DeleteAttachment] ⚠️ سطری با data-attachment-id="' + attachmentId + '" در DOM پیدا نشد!');
                    }

                    var currentBody = document.getElementById('contractAttachmentsBody');
                    if (currentBody && !currentBody.querySelector('tr')) {
                        console.log('[DeleteAttachment] ۹. تمام سطرها حذف شدند، نمایش وضعیت خالی...');
                        var tableWrapper = document.querySelector('#contractAttachmentsList .overflow-x-auto');
                        if (tableWrapper) tableWrapper.remove();

                        ae = document.getElementById('contractAttachmentsEmpty');
                        if (ae) {
                            ae.classList.remove('hidden');
                        } else {
                            var emptyHtml = '<div class="text-center py-6" id="contractAttachmentsEmpty">' +
                                '<div class="inline-flex items-center justify-center h-10 w-10 rounded-full bg-slate-100 dark:bg-slate-800 mb-2">' +
                                '<i class="bi bi-paperclip text-slate-400"></i></div>' +
                                '<p class="text-[11px] text-slate-400 dark:text-slate-500">هیچ ضمیمه‌ای وجود ندارد.</p></div>';
                            if (al) al.insertAdjacentHTML('beforeend', emptyHtml);
                        }
                    }
                } else {
                    console.error('[DeleteAttachment] ❌ خروجی سرور همراه با خطا بود:', r.message);
                    alert(r.message || 'خطا در حذف فایل');
                }
            } catch (err) {
                console.error('[DeleteAttachment] ❌ خطای شبکه یا اجرای اسکریپت:', err);
                alert('خطا در ارتباط با سرور');
            }
        });
    };

    // ── فرم مودال آپلود ──────────────────────────────────────────────
    var modalForm = document.querySelector('#uploadAttachmentModal form');
    if (modalForm) {
        modalForm.addEventListener('submit', function (e) {
            e.preventDefault();
            var fileInput = modalForm.querySelector('input[type="file"]');
            if (!fileInput || !fileInput.files || !fileInput.files.length) return;
            hf(fileInput.files);
            fileInput.value = '';
            var modal = modalForm.closest('.fixed');
            if (modal) {
                modal.classList.add('hidden');
                document.body.classList.remove('overflow-hidden');
            }
        });
    }

    if (!dz || !fi) return;

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
            '<button type="button" onclick="deleteAttachment(' + a.id + ')" class="rounded-lg border border-slate-200 bg-white dark:bg-slate-950 px-2 py-1 text-[9px] font-bold text-slate-600 hover:bg-rose-50 hover:text-rose-600 hover:border-rose-200 dark:border-slate-800 dark:text-slate-400 dark:hover:bg-rose-950/20 dark:hover:text-rose-400 transition cursor-pointer" title="حذف"><i class="bi bi-trash"></i></button>' +
            '</div></td></tr>';

        ab = document.getElementById('contractAttachmentsBody');
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
        ab = document.getElementById('contractAttachmentsBody');
    }

    function pd(e) { e.preventDefault(); e.stopPropagation(); }

    dz.addEventListener('click', function () { fi.click(); });
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

    // ── هندل کردن فرم‌های سابمیت‌شده حذف ──────────────────────────────
    document.querySelectorAll('#contractAttachmentsList form[action*="DeleteAttachment"]').forEach(function (form) {
        form.addEventListener('submit', function (e) {
            e.preventDefault();
            var attId = form.querySelector('input[name="attachmentId"]');
            if (attId) window.deleteAttachment(parseInt(attId.value));
        });
    });

})();
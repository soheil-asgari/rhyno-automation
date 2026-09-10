// ── Signature Pad Integration ────────────────────────────────────
function getSwal() {
    if (typeof Swal !== 'undefined') return Swal;
    return {
        fire: function (arg1, arg2, arg3) {
            var title = typeof arg1 === 'object' ? (arg1.title || '') : (arg1 || '');
            var text = typeof arg1 === 'object' ? (arg1.text || '') : (arg2 || '');
            var isConfirm = typeof arg1 === 'object' && arg1.showCancelButton;
            var msg = title ? (title + (text ? '\n' + text : '')) : text;
            if (isConfirm) {
                var ok = window.confirm(msg);
                return Promise.resolve({ isConfirmed: ok });
            } else {
                window.alert(msg);
                return Promise.resolve({ isConfirmed: true });
            }
        }
    };
}

function initSignaturePad() {
    var canvas = document.getElementById('sig-pad');
    if (!canvas || typeof SignaturePad === 'undefined') return;

    var pad = new SignaturePad(canvas, {
        backgroundColor: 'rgba(255, 255, 255, 0)',
        penColor: 'rgb(0, 0, 0)'
    });

    function resizeCanvas() {
        var padContainer = document.getElementById('sig-pad-container');
        if (padContainer && padContainer.classList.contains('hidden')) return;

        var rect = canvas.getBoundingClientRect();
        var width = canvas.offsetWidth || rect.width || (canvas.parentElement ? canvas.parentElement.offsetWidth : 600);
        var height = canvas.offsetHeight || rect.height || 220;
        if (width <= 0) width = 600;
        if (height <= 0) height = 220;

        var ratio = Math.max(window.devicePixelRatio || 1, 1);
        canvas.width = width * ratio;
        canvas.height = height * ratio;
        var ctx = canvas.getContext('2d');
        if (ctx) ctx.scale(ratio, ratio);
        pad.clear();
    }

    // Resize when the signature tab is shown
    var sigTabBtn = document.querySelector('[data-tab="signature"]');
    if (sigTabBtn) {
        sigTabBtn.addEventListener('click', function () {
            setTimeout(resizeCanvas, 50);
            setTimeout(resizeCanvas, 200);
        });
    }

    window.addEventListener('resize', function () {
        resizeCanvas();
    });

    // Re-sign button toggle
    var reSignBtn = document.getElementById('btn-re-sign');
    if (reSignBtn) {
        reSignBtn.addEventListener('click', function () {
            var existingContainer = document.getElementById('existing-signature-container');
            var padContainer = document.getElementById('sig-pad-container');
            if (existingContainer) existingContainer.classList.add('hidden');
            if (padContainer) padContainer.classList.remove('hidden');
            setTimeout(resizeCanvas, 50);
        });
    }

    // Initial resize if signature tab is visible on load
    var sigPanel = document.getElementById('tab-signature');
    if (sigPanel && !sigPanel.classList.contains('hidden')) {
        setTimeout(resizeCanvas, 100);
    }

    document.getElementById('sig-clear')?.addEventListener('click', function () { pad.clear(); });

    function getTrimmedSignatureDataUrl(srcCanvas) {
        var ctx = srcCanvas.getContext('2d');
        if (!ctx) return srcCanvas.toDataURL('image/png');
        var width = srcCanvas.width;
        var height = srcCanvas.height;
        var imgData = ctx.getImageData(0, 0, width, height);
        var data = imgData.data;

        var minX = width, minY = height, maxX = 0, maxY = 0;
        var found = false;

        for (var y = 0; y < height; y++) {
            for (var x = 0; x < width; x++) {
                var alpha = data[(y * width + x) * 4 + 3];
                if (alpha > 10) {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                    found = true;
                }
            }
        }

        if (!found) return srcCanvas.toDataURL('image/png');

        // Add small padding around the bounding box
        var padding = 10;
        minX = Math.max(0, minX - padding);
        minY = Math.max(0, minY - padding);
        maxX = Math.min(width, maxX + padding);
        maxY = Math.min(height, maxY + padding);

        var cropWidth = maxX - minX;
        var cropHeight = maxY - minY;
        if (cropWidth <= 0 || cropHeight <= 0) return srcCanvas.toDataURL('image/png');

        var cropCanvas = document.createElement('canvas');
        cropCanvas.width = cropWidth;
        cropCanvas.height = cropHeight;
        var cropCtx = cropCanvas.getContext('2d');
        if (!cropCtx) return srcCanvas.toDataURL('image/png');

        cropCtx.drawImage(srcCanvas, minX, minY, cropWidth, cropHeight, 0, 0, cropWidth, cropHeight);
        return cropCanvas.toDataURL('image/png');
    }

    document.getElementById('sig-contract-save')?.addEventListener('click', function () {
        var swal = getSwal();
        if (pad.isEmpty()) {
            swal.fire('خطا', 'لطفاً امضای خود را رسم کنید.', 'warning');
            return;
        }
        var dataUrl = getTrimmedSignatureDataUrl(canvas);
        var contractId = this.getAttribute('data-contract-id');
        if (!contractId) return;

        var selectedParty = document.querySelector('input[name="signerPartyChoice"]:checked')?.value || 'FirstParty';
        var signerNameInput = document.getElementById('signerFullNameInput');
        var signerName = signerNameInput ? signerNameInput.value.trim() : '';

        var partyLabel = selectedParty === 'SecondParty' ? 'طرف دوم (مشتری)' : 'طرف اول (شرکت)';

        swal.fire({
            title: 'ثبت امضای قرارداد',
            text: 'امضای شما به عنوان «' + partyLabel + '» ثبت خواهد شد.',
            icon: 'question',
            showCancelButton: true,
            confirmButtonText: 'بله، ثبت شود',
            cancelButtonText: 'انصراف'
        }).then(function (result) {
            if (!result.isConfirmed) return;
            var token = document.querySelector('input[name="__RequestVerificationToken"]')?.value ||
                        document.querySelector('meta[name="request-verification-token"]')?.getAttribute('content') || '';
            fetch('/Contracts/' + contractId + '/Sign', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                body: JSON.stringify({
                    signatureImageData: dataUrl,
                    signerParty: selectedParty,
                    signerName: signerName
                })
            }).then(function (r) { return r.json(); }).then(function (resp) {
                if (resp.success) {
                    swal.fire('موفقیت', 'امضا با موفقیت ثبت شد.', 'success').then(function () { location.reload(); });
                } else {
                    swal.fire('خطا', resp.message || 'خطا در ثبت امضا.', 'error');
                }
            }).catch(function () {
                swal.fire('خطا', 'ارتباط با سرور برقرار نشد.', 'error');
            });
        });
    });
}

// ── Signature Requests Tab Loader ────────────────────────────
function initSignatureRequestsTab() {
    var tabBtn = document.querySelector('[data-tab="signature"]');
    if (!tabBtn) return;
    tabBtn.addEventListener('click', function () {
        var container = document.getElementById('tab-signature');
        if (!container || container.dataset.loaded) return;
        container.dataset.loaded = 'true';
        var contractId = container.getAttribute('data-contract-id');
        if (!contractId) return;
        var targetListContainer = document.getElementById('signature-requests-list') || container;
        fetch('/Contracts/' + contractId + '/SignatureRequests')
            .then(function (r) { return r.json(); })
            .then(function (resp) {
                if (!resp.success) return;
                renderSignatureRequests(targetListContainer, resp.data);
            });
    });
}

function renderSignatureRequests(container, requests) {
    var html = '<div class="space-y-3">';
    requests.forEach(function (req) {
        html += '<div class="border border-slate-200 dark:border-slate-800 rounded-xl p-3.5 bg-slate-50/50 dark:bg-slate-950/40">';
        html += '<div class="flex items-center justify-between mb-2">';
        html += '<span class="font-bold text-xs text-slate-800 dark:text-slate-200">' + escHtml(req.title) + '</span>';
        html += '<span class="text-[10px] font-bold px-2 py-0.5 rounded-full bg-teal-100 text-teal-700 dark:bg-teal-900/40 dark:text-teal-300">' + escHtml(req.status) + '</span>';
        html += '</div>';
        html += '<div class="text-xs text-slate-500 space-y-1">';
        html += '<div><span class="font-bold">روش ارسال:</span> ' + escHtml(req.deliveryMethod) + ' | <span class="font-bold">ترتیب:</span> ' + req.signingOrder + '</div>';
        html += '<div class="font-mono"><span class="font-bold">تاریخ ایجاد:</span> ' + new Date(req.createdAt).toLocaleDateString('fa-IR') + '</div>';
        if (req.completedAt) html += '<div class="font-mono"><span class="font-bold">تاریخ تکمیل:</span> ' + new Date(req.completedAt).toLocaleDateString('fa-IR') + '</div>';
        html += '<div class="mt-2 font-bold text-slate-700 dark:text-slate-300">امضاکنندگان:</div>';
        html += '<ul class="space-y-1 mt-1">';
        req.signers.forEach(function (s) {
            var icon = s.result === 'Signed' ? 'bi-check-circle-fill text-emerald-500' : s.result === 'Declined' ? 'bi-x-circle-fill text-rose-500' : 'bi-clock text-amber-500';
            html += '<li class="text-xs flex items-center gap-1.5"><i class="bi ' + icon + '"></i>' + escHtml(s.userName) + ' (' + escHtml(s.signerType) + ') - ' + escHtml(s.result) + (s.signedAt ? ' در ' + new Date(s.signedAt).toLocaleDateString('fa-IR') : '') + '</li>';
        });
        html += '</ul></div></div>';
    });
    if (requests.length === 0) html += '<div class="text-center py-4 text-slate-400 text-xs">درخواست امضایی ثبت نشده است.</div>';
    html += '</div>';
    container.innerHTML = html;
}

function escHtml(str) {
    if (!str) return '';
    var div = document.createElement('div');
    div.appendChild(document.createTextNode(str));
    return div.innerHTML;
}

document.addEventListener('DOMContentLoaded', function () {
    'use strict';

    // ── Tab Switcher with Strict Isolation ──
    function initTabs() {
        var container = document.querySelector('[data-contract-tabs]');
        if (!container) return;
        var buttons = container.querySelectorAll('.tab-btn');
        var panels = container.querySelectorAll('.tab-panel');

        function activateTab(tabId) {
            // Strictly hide all panels first
            panels.forEach(function (panel) {
                panel.classList.add('hidden');
            });

            // Map target tab ID
            var targetId = 'tab-' + tabId;
            if (tabId === 'content') targetId = 'tab-body';
            if (tabId === 'activity') targetId = 'tab-history';

            var targetPanel = document.getElementById(targetId) || document.getElementById('tab-' + tabId);
            if (targetPanel) {
                targetPanel.classList.remove('hidden');
            }

            // Highlight active button and deactivate others
            buttons.forEach(function (btn) {
                var btnTab = btn.getAttribute('data-tab');
                var isMatch = (btnTab === tabId) ||
                              (btnTab === 'body' && tabId === 'content') ||
                              (btnTab === 'content' && tabId === 'body') ||
                              (btnTab === 'history' && tabId === 'activity') ||
                              (btnTab === 'activity' && tabId === 'history');

                if (isMatch) {
                    btn.className = 'tab-btn px-4 py-3 border-b-2 transition flex items-center gap-1.5 border-teal-500 text-teal-600 dark:text-teal-400 font-bold';
                } else {
                    btn.className = 'tab-btn px-4 py-3 border-b-2 transition flex items-center gap-1.5 border-transparent text-slate-500 dark:text-slate-400 hover:text-slate-700 dark:hover:text-slate-300';
                }
            });
        }

        buttons.forEach(function (btn) {
            btn.addEventListener('click', function (e) {
                e.preventDefault();
                var tabId = btn.getAttribute('data-tab');
                if (tabId) activateTab(tabId);
            });
        });
    }

    // ── Global Event Delegation for Modals via [data-modal-target] ──
    document.addEventListener('click', function (e) {
        var trigger = e.target.closest('[data-modal-target]');
        if (!trigger) return;
        e.preventDefault();
        var targetId = trigger.getAttribute('data-modal-target');
        var modal = document.getElementById(targetId);
        if (modal) {
            modal.classList.remove('hidden');
            if (typeof initJalaliPickers === 'function') {
                initJalaliPickers();
            }
        }
    });

    // Close modal via [data-close-modal] buttons
    document.addEventListener('click', function (e) {
        var closeBtn = e.target.closest('[data-close-modal]');
        if (closeBtn) {
            e.preventDefault();
            var modal = closeBtn.closest('.fixed.inset-0');
            if (modal) modal.classList.add('hidden');
            if (typeof closeJalaliPopups === 'function') closeJalaliPopups();
        }
    });

    // Close modal on backdrop click
    document.addEventListener('click', function (e) {
        if (e.target.closest && (e.target.closest('.vanilla-jalali-popup') || e.target.closest('.jalali-datepicker-wrapper'))) {
            return;
        }
        if (e.target.classList.contains('bg-black/50') && e.target.classList.contains('fixed')) {
            document.querySelectorAll('.fixed.inset-0.z-50').forEach(function (m) {
                m.classList.add('hidden');
            });
            if (typeof closeJalaliPopups === 'function') closeJalaliPopups();
        }
    });

    // Escape key closes open modals
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            document.querySelectorAll('.fixed.inset-0.z-50').forEach(function (el) {
                if (!el.classList.contains('hidden')) el.classList.add('hidden');
            });
            if (typeof closeJalaliPopups === 'function') closeJalaliPopups();
        }
    });

    // ── Jalali Datepicker Engine ──
    var PERSIAN_MONTHS = ['فروردین', 'اردیبهشت', 'خرداد', 'تیر', 'مرداد', 'شهریور', 'مهر', 'آبان', 'آذر', 'دی', 'بهمن', 'اسفند'];
    var PERSIAN_WEEKDAYS = ['ش', 'ی', 'د', 'س', 'چ', 'پ', 'ج'];

    function toPersianDigits(str) {
        return String(str).replace(/\d/g, function (d) { return '۰۱۲۳۴۵۶۷۸۹'[d]; });
    }

    function writeJalaliToHidden(input, jalali) {
        var wrapper = input.closest('.jalali-datepicker-wrapper');
        if (!wrapper) return;
        var hidden = wrapper.querySelector('input.jalali-date-hidden');
        if (!hidden) return;
        if (!jalali) {
            hidden.value = '';
            return;
        }
        try {
            var parts = jalali.trim().split('/');
            if (parts.length === 3) {
                var y = parseInt(parts[0], 10);
                var m = parseInt(parts[1], 10);
                var d = parseInt(parts[2], 10);
                if (!isNaN(y) && !isNaN(m) && !isNaN(d)) {
                    var pd = new persianDate([y, m, d]);
                    var g = pd.toDate();
                    hidden.value = g.getFullYear() + '-' +
                        String(g.getMonth() + 1).padStart(2, '0') + '-' +
                        String(g.getDate()).padStart(2, '0');
                }
            }
        } catch (e) {
            console.warn('writeJalaliToHidden error:', e);
        }
    }

    function createDatepicker(input) {
        var wrapper = input.closest('.jalali-datepicker-wrapper');
        if (!wrapper) {
            wrapper = document.createElement('div');
            wrapper.className = 'jalali-datepicker-wrapper relative';
            wrapper.style.cssText = 'position:relative;display:inline-block;width:100%';
            input.parentNode.insertBefore(wrapper, input);
            wrapper.appendChild(input);
        } else {
            wrapper.style.position = 'relative';
        }

        var popup = wrapper.querySelector('.vanilla-jalali-popup');
        if (!popup) {
            popup = document.createElement('div');
            popup.className = 'vanilla-jalali-popup';
            wrapper.appendChild(popup);
        }

        var viewYear = 0, viewMonth = 0;

        input._jalaliPicker = {
            get viewYear() { return viewYear; },
            set viewYear(v) { viewYear = v; },
            get viewMonth() { return viewMonth; },
            set viewMonth(v) { viewMonth = v; },
            render: function () { render(); }
        };

        function render() {
            var today = new persianDate();
            if (!viewYear) {
                if (input.value && input.value.indexOf('/') !== -1) {
                    var parts = input.value.split('/');
                    viewYear = parseInt(parts[0], 10) || today.year();
                    viewMonth = parseInt(parts[1], 10) || today.month();
                } else {
                    viewYear = today.year();
                    viewMonth = today.month();
                }
            }

            var daysInMonth = today.daysInMonth(viewYear, viewMonth);
            var firstWeekday = today.getFirstWeekDayOfMonth(viewYear, viewMonth);

            var html = '<div style="display:flex;align-items:center;justify-content:space-between;padding:8px 10px;border-bottom:1px solid #f1f5f9;">';
            html += '<button type="button" class="vjp-nav-btn" data-nav="prev" aria-label="ماه قبل">&rsaquo;</button>';
            html += '<span style="font-size:12px;font-weight:700;color:#334155;">' + PERSIAN_MONTHS[viewMonth - 1] + ' ' + toPersianDigits(viewYear) + '</span>';
            html += '<button type="button" class="vjp-nav-btn" data-nav="next" aria-label="ماه بعد">&lsaquo;</button></div>';

            html += '<div style="display:grid;grid-template-columns:repeat(7,1fr);padding:4px 6px;border-bottom:1px solid #f1f5f9;">';
            PERSIAN_WEEKDAYS.forEach(function (w) { html += '<div style="text-align:center;font-size:10px;font-weight:700;color:#94a3b8;padding:2px 0;">' + w + '</div>'; });
            html += '</div>';

            html += '<div style="display:grid;grid-template-columns:repeat(7,1fr);padding:6px;">';
            for (var i = 0; i < firstWeekday; i++) html += '<div></div>';
            for (var day = 1; day <= daysInMonth; day++) {
                var isToday = (day === today.date() && viewMonth === today.month() && viewYear === today.year());
                html += '<button type="button" class="vjp-day-btn' + (isToday ? ' is-today' : '') + '" data-day="' + day + '">' + toPersianDigits(day) + '</button>';
            }
            html += '</div>';
            popup.innerHTML = html;
        }

        input.onfocus = function () {
            document.querySelectorAll('.vanilla-jalali-popup').forEach(function (p) { p.style.display = 'none'; });
            render();
            popup.style.display = 'block';
        };
        input.onclick = function (e) {
            e.preventDefault();
            document.querySelectorAll('.vanilla-jalali-popup').forEach(function (p) { p.style.display = 'none'; });
            render();
            popup.style.display = 'block';
        };

        input.addEventListener('change', function () {
            writeJalaliToHidden(input, input.value);
        });
        input.addEventListener('input', function () {
            writeJalaliToHidden(input, input.value);
        });
    }

    function initJalaliPickers() {
        if (typeof persianDate === 'undefined') return;
        document.querySelectorAll('.jalali-input, .jalali-date-input').forEach(function (input) {
            var wrapper = input.closest('.jalali-datepicker-wrapper');
            if (wrapper) {
                var hidden = wrapper.querySelector('input.jalali-date-hidden');
                if (hidden && hidden.value && !input.value) {
                    try {
                        var d = new Date(hidden.value);
                        if (!isNaN(d.getTime())) {
                            var jDate = new persianDate(d);
                            input.value = jDate.format('YYYY/MM/DD');
                        }
                    } catch (e) {}
                } else if (input.value && (!hidden || !hidden.value)) {
                    writeJalaliToHidden(input, input.value);
                }
            }
            if (input._jalaliPicker) return;
            createDatepicker(input);
        });
    }
    window.initJalaliPickers = initJalaliPickers;

    function closeJalaliPopups() {
        document.querySelectorAll('.vanilla-jalali-popup').forEach(function (p) { p.style.display = 'none'; });
    }
    window.closeJalaliPopups = closeJalaliPopups;

    // Datepicker event delegation
    document.addEventListener('click', function (e) {
        var navBtn = e.target.closest('.vjp-nav-btn');
        if (navBtn) {
            e.stopPropagation();
            var popup = navBtn.closest('.vanilla-jalali-popup');
            if (!popup) return;
            var wrapper = popup.closest('.jalali-datepicker-wrapper');
            var input = wrapper ? wrapper.querySelector('.jalali-input, .jalali-date-input') : null;
            if (!input) return;
            var picker = input._jalaliPicker;
            if (!picker) return;
            var isPrev = navBtn.getAttribute('data-nav') === 'prev';
            if (isPrev) {
                picker.viewMonth--;
                if (picker.viewMonth < 1) { picker.viewMonth = 12; picker.viewYear--; }
            } else {
                picker.viewMonth++;
                if (picker.viewMonth > 12) { picker.viewMonth = 1; picker.viewYear++; }
            }
            picker.render();
            return;
        }

        var dayBtn = e.target.closest('.vjp-day-btn');
        if (dayBtn) {
            e.stopPropagation();
            var popup = dayBtn.closest('.vanilla-jalali-popup');
            if (!popup) return;
            var wrapper = popup.closest('.jalali-datepicker-wrapper');
            var input = wrapper ? wrapper.querySelector('.jalali-input, .jalali-date-input') : null;
            if (!input) return;
            var picker = input._jalaliPicker;
            if (!picker) return;
            var day = parseInt(dayBtn.getAttribute('data-day'), 10);
            var selected = new persianDate([picker.viewYear, picker.viewMonth, day]);
            input.value = selected.format('YYYY/MM/DD');
            writeJalaliToHidden(input, input.value);
            input.dispatchEvent(new Event('input', { bubbles: true }));
            input.dispatchEvent(new Event('change', { bubbles: true }));
            popup.style.display = 'none';
            return;
        }
    }, true);

    initTabs();
    initSignaturePad();
    initSignatureRequestsTab();
    initJalaliPickers();
});
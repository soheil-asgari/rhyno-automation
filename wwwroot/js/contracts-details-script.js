// ── Signature Pad Integration ────────────────────────────────────
    function initSignaturePad() {
        var canvas = document.getElementById('sig-pad');
        if (!canvas || typeof SignaturePad === 'undefined') return;

        var pad = new SignaturePad(canvas, {
            backgroundColor: 'rgba(255, 255, 255, 0)',
            penColor: 'rgb(0, 0, 0)'
        });

        function resizeCanvas() {
            var ratio = Math.max(window.devicePixelRatio || 1, 1);
            canvas.width = canvas.offsetWidth * ratio;
            canvas.height = canvas.offsetHeight * ratio;
            canvas.getContext('2d').scale(ratio, ratio);
            pad.clear();
        }

        // Resize when the signature tab is shown
        var sigTabBtn = document.querySelector('[data-tab="signature"]');
        if (sigTabBtn) {
            sigTabBtn.addEventListener('click', function () {
                setTimeout(resizeCanvas, 100);
            });
        }

        document.getElementById('sig-clear')?.addEventListener('click', function () { pad.clear(); });

        document.getElementById('sig-contract-save')?.addEventListener('click', function () {
            if (pad.isEmpty()) {
                Swal.fire('خطا', 'لطفاً امضای خود را رسم کنید.', 'warning');
                return;
            }
            var dataUrl = pad.toDataURL();
            var contractId = this.getAttribute('data-contract-id');
            if (!contractId) return;

            Swal.fire({
                title: 'ثبت امضای قرارداد',
                text: 'امضای شما برای این قرارداد ثبت می‌شود.',
                icon: 'question',
                showCancelButton: true,
                confirmButtonText: 'بله، ثبت شود',
                cancelButtonText: 'انصراف'
            }).then(function (result) {
                if (!result.isConfirmed) return;
                fetch('/Contracts/' + contractId + '/Sign', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
                    },
                    body: JSON.stringify({ signatureImageData: dataUrl })
                }).then(function (r) { return r.json(); }).then(function (resp) {
                    if (resp.success) {
                        Swal.fire('موفقیت', 'امضا با موفقیت ثبت شد.', 'success').then(function () { location.reload(); });
                    } else {
                        Swal.fire('خطا', resp.message || 'خطا در ثبت امضا.', 'error');
                    }
                }).catch(function () {
                    Swal.fire('خطا', 'ارتباط با سرور برقرار نشد.', 'error');
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
            fetch('/Contracts/' + contractId + '/SignatureRequests')
                .then(function (r) { return r.json(); })
                .then(function (resp) {
                    if (!resp.success) return;
                    renderSignatureRequests(container, resp.data);
                });
        });
    }

    function renderSignatureRequests(container, requests) {
        var html = '<div class="space-y-4">';
        requests.forEach(function (req) {
            html += '<div class="border border-slate-200 dark:border-slate-700 rounded-lg p-3">';
            html += '<div class="flex items-center justify-between mb-2">';
            html += '<span class="font-medium text-sm">' + escHtml(req.title) + '</span>';
            html += '<span class="text-xs px-2 py-0.5 rounded-full bg-slate-100 dark:bg-slate-700">' + req.status + '</span>';
            html += '</div>';
            html += '<div class="text-[11px] text-slate-500 space-y-1">';
            html += '<div><span class="font-medium">نوع ارسال:</span> ' + req.deliveryMethod + ' | <span class="font-medium">ترتیب:</span> ' + req.signingOrder + '</div>';
            html += '<div><span class="font-medium">تاریخ:</span> ' + new Date(req.createdAt).toLocaleDateString('fa-IR') + '</div>';
            if (req.completedAt) html += '<div><span class="font-medium">تکمیل:</span> ' + new Date(req.completedAt).toLocaleDateString('fa-IR') + '</div>';
            html += '<div class="mt-2"><span class="font-medium">امضاکنندگان:</span></div>';
            html += '<ul class="list-disc list-inside">';
            req.signers.forEach(function (s) {
                var icon = s.result === 'Signed' ? 'bi-check-circle-fill text-green-600' : s.result === 'Declined' ? 'bi-x-circle-fill text-rose-500' : 'bi-clock text-slate-400';
                html += '<li class="text-xs"><i class="bi ' + icon + ' ml-1"></i>' + escHtml(s.userName) + ' (' + s.signerType + ') - ' + s.result + (s.signedAt ? ' در ' + new Date(s.signedAt).toLocaleDateString('fa-IR') : '') + '</li>';
            });
            html += '</ul></div></div>';
        });
        if (requests.length === 0) html += '<div class="text-center py-8 text-slate-400 text-sm">درخواست امضایی وجود ندارد.</div>';
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

    // ── Tab Switcher ──
    function initTabs() {
        var container = document.querySelector('[data-contract-tabs]');
        if (!container) return;
        var buttons = container.querySelectorAll('.tab-btn');
        var panels = container.querySelectorAll('.tab-panel');
        function activateTab(tabId) {
            buttons.forEach(function (btn) {
                var isActive = btn.getAttribute('data-tab') === tabId;
                btn.className = 'tab-btn px-4 py-2.5 border-b-2 transition ' +
                    (isActive ? 'border-teal-500 text-teal-600 dark:text-teal-400' : 'border-transparent text-slate-500 dark:text-slate-400 hover:text-slate-700 dark:hover:text-slate-300');
            });
            panels.forEach(function (panel) {
                panel.classList.toggle('hidden', panel.id !== 'tab-' + tabId);
            });
        }
        buttons.forEach(function (btn) {
            btn.addEventListener('click', function (e) { e.preventDefault(); if (btn.getAttribute('data-tab')) activateTab(btn.getAttribute('data-tab')); });
        });
    }

    // ── Modal helpers via Tailwind hidden class ──
    function setupModal(triggerId, modalId) {
        var btn = document.getElementById(triggerId);
        var modal = document.getElementById(modalId);
        if (!btn || !modal) return;
        btn.addEventListener('click', function (e) { e.preventDefault(); modal.classList.remove('hidden'); });
    }
    setupModal('btnNewPayment', 'addPaymentModal');
    setupModal('btnUploadAttachment', 'uploadAttachmentModal');

    // Close modal via data-close-modal
    document.querySelectorAll('[data-close-modal]').forEach(function (btn) {
        btn.addEventListener('click', function () {
            var modal = this.closest('.fixed.inset-0');
            if (modal) modal.classList.add('hidden');
        });
    });

    // ── Jalali Datepicker Engine (pure Vanilla, no jQuery) ──
    var PERSIAN_MONTHS = ['فروردین', 'اردیبهشت', 'خرداد', 'تیر', 'مرداد', 'شهریور', 'مهر', 'آبان', 'آذر', 'دی', 'بهمن', 'اسفند'];
    var PERSIAN_WEEKDAYS = ['ش', 'ی', 'د', 'س', 'چ', 'پ', 'ج'];

    function toPersianDigits(str) {
        return String(str).replace(/\d/g, function (d) { return '۰۱۲۳۴۵۶۷۸۹'[d]; });
    }

    function createDatepicker(input) {
        var wrapper = document.createElement('div');
        wrapper.className = 'jalali-datepicker-wrapper';
        wrapper.style.cssText = 'position:relative;display:inline-block;width:100%';
        input.parentNode.insertBefore(wrapper, input);
        wrapper.appendChild(input);

        var popup = document.createElement('div');
        popup.className = 'vanilla-jalali-popup';
        wrapper.appendChild(popup);

        var viewYear = 0, viewMonth = 0;

        // Expose state + render on the input element so the global delegation handler can drive navigation / selection
        input._jalaliPicker = {
            get viewYear() { return viewYear; },
            set viewYear(v) { viewYear = v; },
            get viewMonth() { return viewMonth; },
            set viewMonth(v) { viewMonth = v; },
            render: function () { render(); }
        };

        function render() {
            var today = new persianDate();
            if (!viewYear) viewYear = today.year();
            if (!viewMonth) viewMonth = today.month();

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

            // Uses event delegation on popup for nav buttons — no direct onclick bindings.
            // The global capture-phase listener (below) handles stopPropagation.
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

        document.addEventListener('click', function (e) {
            if (!wrapper.contains(e.target)) {
                popup.style.display = 'none';
            }
        });
    }

    function initJalaliPickers() {
        if (typeof persianDate === 'undefined') { return; }
        document.querySelectorAll('.jalali-input').forEach(function (input) {
            if (input.closest('.jalali-datepicker-wrapper')) return;
            createDatepicker(input);
        });
    }

    function closeJalaliPopups() {
        document.querySelectorAll('.vanilla-jalali-popup').forEach(function (p) { p.style.display = 'none'; });
    }

    // ── Event delegation: popup nav buttons & day selection ──
    // Uses a single listener on document (capture phase) to handle all popup interactions.
    document.addEventListener('click', function (e) {
        // Navigate months
        var navBtn = e.target.closest('.vjp-nav-btn');
        if (navBtn) {
            e.stopPropagation();
            var popup = navBtn.closest('.vanilla-jalali-popup');
            if (!popup) return;
            var wrapper = popup.closest('.jalali-datepicker-wrapper');
            var input = wrapper ? wrapper.querySelector('.jalali-input') : null;
            if (!input) return;
            // Find this picker's state (viewYear, viewMonth are closure-scoped in createDatepicker)
            // We re-use the fact that createDatepicker's render is called from input's onfocus/onclick.
            // Instead, we manually update the state via the picker's stored reference.
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

        // Select a day
        var dayBtn = e.target.closest('.vjp-day-btn');
        if (dayBtn) {
            e.stopPropagation();
            var popup = dayBtn.closest('.vanilla-jalali-popup');
            if (!popup) return;
            var wrapper = popup.closest('.jalali-datepicker-wrapper');
            var input = wrapper ? wrapper.querySelector('.jalali-input') : null;
            if (!input) return;
            var picker = input._jalaliPicker;
            if (!picker) return;
            var day = parseInt(dayBtn.getAttribute('data-day'), 10);
            var selected = new persianDate([picker.viewYear, picker.viewMonth, day]);
            input.value = selected.format('YYYY/MM/DD');
            // Dispatch input and change events so any form listeners detect the change
            var evt = new Event('input', { bubbles: true });
            input.dispatchEvent(evt);
            evt = new Event('change', { bubbles: true });
            input.dispatchEvent(evt);
            popup.style.display = 'none';
            return;
        }
    }, true);

    // ── Close modals on backdrop click (ignore datepicker clicks) ──
    document.addEventListener('click', function (e) {
        if (e.target.closest && (e.target.closest('.vanilla-jalali-popup') || e.target.closest('.jalali-datepicker-wrapper'))) {
            return;
        }
        if (e.target.classList.contains('bg-black/50') && e.target.classList.contains('fixed')) {
            document.querySelectorAll('.fixed.inset-0.z-50').forEach(function (m) {
                m.classList.add('hidden');
                closeJalaliPopups();
            });
        }
    });

    // Escape key to close modals
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            document.querySelectorAll('.fixed.inset-0.z-50').forEach(function (el) {
                if (!el.classList.contains('hidden')) el.classList.add('hidden');
            });
            closeJalaliPopups();
        }
    });

    // Event delegation for [data-modal-target] triggers
    document.addEventListener('click', function (e) {
        var trigger = e.target.closest('[data-modal-target]');
        if (!trigger) return;
        e.preventDefault();
        var modal = document.getElementById(trigger.getAttribute('data-modal-target'));
        if (modal) modal.classList.remove('hidden');
    });

    // ── Re-init jalali pickers when payment modal opens
    var paymentModal = document.getElementById('addPaymentModal');
    if (paymentModal) {
        var observer = new MutationObserver(function () {
            if (!paymentModal.classList.contains('hidden')) {
                initJalaliPickers();
            } else {
                closeJalaliPopups();
            }
        });
        observer.observe(paymentModal, { attributes: true, attributeFilter: ['class'] });
    }

    // ── Payment form: convert Jalali text inputs to Gregorian hidden fields ──
    // The server model (ContractPaymentCreateVM) expects DateTime (Gregorian),
    // so before submit we convert the Persian date strings to Gregorian and
    // store them into hidden fields that the model binder can parse.
    var paymentForm = document.querySelector('#addPaymentModal form');
    if (paymentForm) {
        // Remove name from original text inputs so they don't get posted as strings
        var paymentInput = document.getElementById('paymentDate');
        var dueInput = document.getElementById('paymentDueDate');
        if (paymentInput) paymentInput.removeAttribute('name');
        if (dueInput) dueInput.removeAttribute('name');

        // Create hidden fields for Gregorian dates (that the model binder expects)
        var paymentDateHidden = document.createElement('input');
        paymentDateHidden.type = 'hidden';
        paymentDateHidden.name = 'PaymentDate';
        paymentForm.appendChild(paymentDateHidden);

        var dueDateHidden = document.createElement('input');
        dueDateHidden.type = 'hidden';
        dueDateHidden.name = 'DueDate';
        paymentForm.appendChild(dueDateHidden);

        paymentForm.addEventListener('submit', function (e) {
            var paymentDateInput = document.getElementById('paymentDate');
            var dueDateInput = document.getElementById('paymentDueDate');

            // Convert PaymentDate (required)
            var paymentJalali = paymentDateInput ? paymentDateInput.value.trim() : '';
            if (paymentJalali) {
                try {
                    var parts = paymentJalali.split('/');
                    if (parts.length === 3) {
                        var pd = new persianDate([
                            parseInt(parts[0], 10),
                            parseInt(parts[1], 10),
                            parseInt(parts[2], 10)
                        ]);
                        var g = pd.toDate();
                        paymentDateHidden.value = g.getFullYear() + '-' +
                            String(g.getMonth() + 1).padStart(2, '0') + '-' +
                            String(g.getDate()).padStart(2, '0');
                    }
                } catch (ex) {
                    console.warn('PaymentDate conversion failed', ex);
                }
            }

            // Convert DueDate (optional)
            var dueJalali = dueDateInput ? dueDateInput.value.trim() : '';
            if (dueJalali) {
                try {
                    var parts = dueJalali.split('/');
                    if (parts.length === 3) {
                        var pd = new persianDate([
                            parseInt(parts[0], 10),
                            parseInt(parts[1], 10),
                            parseInt(parts[2], 10)
                        ]);
                        var g = pd.toDate();
                        dueDateHidden.value = g.getFullYear() + '-' +
                            String(g.getMonth() + 1).padStart(2, '0') + '-' +
                            String(g.getDate()).padStart(2, '0');
                    }
                } catch (ex) {
                    console.warn('DueDate conversion failed', ex);
                }
            }
        });
    }

    // ── Schedule Lazy-Load Retry Helper ──
    window.reloadPaymentSchedule = function () {
        var container = document.getElementById('paymentScheduleContainer');
        if (!container) return;
        delete container.dataset.loaded;
        container.innerHTML = '<div class="schedule-skeleton space-y-2 "><div class="h-4 bg-slate-200 dark:bg-slate-700 rounded w-1/3"></div><div class="h-8 bg-slate-200 dark:bg-slate-700 rounded w-full"></div><div class="h-8 bg-slate-200 dark:bg-slate-700 rounded w-full"></div><div class="h-8 bg-slate-200 dark:bg-slate-700 rounded w-full"></div><div class="h-8 bg-slate-200 dark:bg-slate-700 rounded w-2/3"></div></div>';
        var contractId = container.getAttribute('data-contract-id');
        if (!contractId) return;
        fetch('/Contracts/Schedule/' + contractId)
            .then(function (r) { return r.text(); })
            .then(function (html) { container.innerHTML = html; })
            .catch(function () {
                container.innerHTML = '<div class="text-center py-4 text-[11px] text-rose-500"><i class="bi bi-exclamation-triangle ml-1"></i>خطا در بارگذاری. <button onclick="reloadPaymentSchedule()" class="text-teal-600 underline mr-1">تلاش مجدد</button></div>';
            });
    };

    // ── Schedule Lazy-Load Retry Helper ──
    window.reloadPaymentSchedule = function () {
        var container = document.getElementById('paymentScheduleContainer');
        if (!container) return;
        delete container.dataset.loaded;
        container.innerHTML = '<div class="schedule-skeleton space-y-2 "><div class="h-4 bg-slate-200 dark:bg-slate-700 rounded w-1/3"></div><div class="h-8 bg-slate-200 dark:bg-slate-700 rounded w-full"></div><div class="h-8 bg-slate-200 dark:bg-slate-700 rounded w-full"></div><div class="h-8 bg-slate-200 dark:bg-slate-700 rounded w-full"></div><div class="h-8 bg-slate-200 dark:bg-slate-700 rounded w-2/3"></div></div>';
        var contractId = container.getAttribute('data-contract-id');
        if (!contractId) return;
        fetch('/Contracts/Schedule/' + contractId)
            .then(function (r) { return r.text(); })
            .then(function (html) { container.innerHTML = html; })
            .catch(function () {
                container.innerHTML = '<div class="text-center py-4 text-[11px] text-rose-500"><i class="bi bi-exclamation-triangle ml-1"></i>خطا در بارگذاری. <button onclick="reloadPaymentSchedule()" class="text-teal-600 underline mr-1">تلاش مجدد</button></div>';
            });
    };

    initTabs();
    initSignaturePad();

    // ── Document Requirements Lazy-Load ────────────────────
    function initDocumentRequirements() {
        var container = document.getElementById('documentRequirementsContainer');
        if (!container) return;
        var contractId = container.getAttribute('data-contract-id');
        if (!contractId) return;

        function loadRequirements() {
            container.innerHTML = '<div class="text-center py-6"><div class="schedule-skeleton space-y-2"><div class="h-4 bg-slate-200 dark:bg-slate-700 rounded w-1/3 mx-auto"></div><div class="h-8 bg-slate-200 dark:bg-slate-700 rounded w-full"></div></div></div>';
            fetch('/Contracts/' + contractId + '/Requirements')
                .then(function (r) { return r.text(); })
                .then(function (html) {
                    container.innerHTML = html;
                    bindRequirementUI();
                })
                .catch(function () {
                    container.innerHTML = '<div class="text-center py-4 text-[11px] text-rose-500"><i class="bi bi-exclamation-triangle ml-1"></i>خطا در بارگذاری. <button onclick="loadRequirements()" class="text-teal-600 underline mr-1">تلاش مجدد</button></div>';
                });
        }

        function bindRequirementUI() {
            // Seed defaults form
            document.querySelectorAll('.seed-requirements-form').forEach(function (f) {
                f.addEventListener('submit', function (e) {
                    e.preventDefault();
                    var btn = this.querySelector('button');
                    btn.disabled = true;
                    btn.innerHTML = '<i class="bi bi-hourglass ml-1"></i>در حال بارگذاری...';
                    fetch(this.action, {
                        method: 'POST',
                        body: new FormData(this),
                        headers: { 'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || '' }
                    }).then(function (r) { return r.json(); }).then(function (resp) {
                        if (resp.success) { loadRequirements(); }
                        else { alert(resp.message || 'خطا'); btn.disabled = false; btn.innerHTML = '<i class="bi bi-gear ml-1"></i>بارگذاری پیش‌فرض‌ها'; }
                    }).catch(function () { alert('خطا در ارتباط'); btn.disabled = false; btn.innerHTML = '<i class="bi bi-gear ml-1"></i>بارگذاری پیش‌فرض‌ها'; });
                });
            });

            // Upload for requirement buttons
            document.querySelectorAll('.upload-for-req-btn').forEach(function (btn) {
                btn.addEventListener('click', function () {
                    var reqId = this.getAttribute('data-req-id');
                    var reqName = this.getAttribute('data-req-name');
                    // Trigger the main upload button with a data attribute for the requirement
                    var uploadBtn = document.getElementById('btnUploadAttachment');
                    if (uploadBtn) {
                        uploadBtn.setAttribute('data-req-id', reqId);
                    }
                    // Open the upload modal
                    var modal = document.getElementById('uploadAttachmentModal');
                    if (modal) { modal.classList.remove('hidden'); }
                });
            });
        }

        // Load when the Attachments tab is shown
        var attachmentsTab = document.querySelector('[data-tab="attachments"]');
        if (attachmentsTab) {
            attachmentsTab.addEventListener('click', function () {
                if (!container.dataset.loaded) {
                    container.dataset.loaded = '1';
                    loadRequirements();
                }
            });
        }

        // Also load if Attachments tab is already active
        if (document.getElementById('tab-attachments') && !document.getElementById('tab-attachments').classList.contains('hidden')) {
            container.dataset.loaded = '1';
            loadRequirements();
        }
    }

    initDocumentRequirements();
    initSignatureRequestsTab();
    initJalaliPickers();
});
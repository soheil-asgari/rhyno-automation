(function () {
                'use strict';

                // ── Multi-Step Wizard Engine ───────────────────────────────
                var currentStep = 1;
                var totalSteps = 3;

                function updateWizardStep(step) {
                currentStep = step;

                // ۱. نمایش/مخفی‌سازی پنل‌ها
                document.querySelectorAll('[data-step-panel]').forEach(function (panel) {
                var panelStep = parseInt(panel.getAttribute('data-step-panel'), 10);
                if (panelStep === step) {
                panel.classList.add('active');
                panel.style.display = 'block';
                } else {
                panel.classList.remove('active');
                panel.style.display = 'none';
                }
                });

                // ۲. به‌روزرسانی درصد پرشدن خط اتصال بین گام‌ها
                var progressLine = document.getElementById('wizardProgressLine');
                if (progressLine) {
                var percentage = ((step - 1) / (totalSteps - 1)) * 100;
                progressLine.style.width = percentage + '%';
                }

                // ۳. به‌روزرسانی ظاهر دایره‌ها و لیبل‌ها
                document.querySelectorAll('.wizard-step-btn').forEach(function (btn) {
                var btnStep = parseInt(btn.getAttribute('data-step'), 10);
                var badge = btn.querySelector('.step-badge');
                var label = btn.querySelector('.step-label');

                if (!badge || !label) return;

                if (btnStep === step) {
                badge.className = 'step-badge w-10 h-10 rounded-full flex items-center justify-center text-xs font-bold ring-4 ring-white dark:ring-slate-900 bg-teal-600 text-white shadow-sm transition-all';
                label.className = 'step-label text-xs font-bold text-teal-600 dark:text-teal-400 mt-2.5 transition-colors';
                } else if (btnStep < step) {
                badge.className = 'step-badge w-10 h-10 rounded-full flex items-center justify-center text-xs font-bold ring-4 ring-white dark:ring-slate-900 bg-teal-50 dark:bg-teal-950 text-teal-600 dark:text-teal-400 border border-teal-200 dark:border-teal-800 transition-all';
                label.className = 'step-label text-xs font-semibold text-teal-600 dark:text-teal-400 mt-2.5 transition-colors';
                } else {
                badge.className = 'step-badge w-10 h-10 rounded-full flex items-center justify-center text-xs font-bold ring-4 ring-white dark:ring-slate-900 bg-slate-100 dark:bg-slate-800 text-slate-400 dark:text-slate-500 border border-slate-300 dark:border-slate-700 transition-all';
                label.className = 'step-label text-xs font-medium text-slate-400 dark:text-slate-500 mt-2.5 transition-colors';
                }
                });

                // ۴. مدیریت وضعیت نمایش دکمه‌های ناوبری پایین
                var prevBtn = document.getElementById('prevStepBtn');
                var nextBtn = document.getElementById('nextStepBtn');
                var submitBtn = document.getElementById('submitFormBtn');

                if (prevBtn) prevBtn.classList.toggle('hidden', step === 1);
                if (nextBtn) nextBtn.classList.toggle('hidden', step === totalSteps);
                if (submitBtn) submitBtn.classList.toggle('hidden', step !== totalSteps);

                window.scrollTo({ top: 0, behavior: 'smooth' });
                }

                // ── Jalali Date Picker Logic ──────────────────────────────
                var PERSIAN_MONTHS = ['فروردین', 'اردیبهشت', 'خرداد', 'تیر', 'مرداد', 'شهریور', 'مهر', 'آبان', 'آذر', 'دی', 'بهمن', 'اسفند'];
                var PERSIAN_WEEKDAYS = ['ش', 'ی', 'د', 'س', 'چ', 'پ', 'ج'];

                function toPersianDigits(str) {
                return String(str).replace(/\d/g, function (d) { return '۰۱۲۳۴۵۶۷۸۹'[d]; });
                }

                function writeJalaliToHidden(input, jalali) {
                var wrapper = input.closest('.jalali-datepicker-wrapper');
                var hidden = wrapper ? wrapper.querySelector('input.jalali-date-hidden') : null;
                if (hidden && jalali) {
                try {
                var parts = jalali.split('/');
                if (parts.length === 3) {
                var pd = new persianDate([parseInt(parts[0], 10), parseInt(parts[1], 10), parseInt(parts[2], 10)]);
                var g = pd.toDate();
                hidden.value = g.getFullYear() + '-' + String(g.getMonth() + 1).padStart(2, '0') + '-' + String(g.getDate()).padStart(2, '0');
                }
                } catch (e) { }
                }
                }

                function createDatepicker(input) {
                var wrapper = input.closest('.jalali-datepicker-wrapper') || input.parentElement;
                if (!wrapper) return;

                var popup = document.createElement('div');
                popup.className = 'vanilla-jalali-popup';
                wrapper.appendChild(popup);

                var viewYear = 0, viewMonth = 0;

                function render() {
                var today = new persianDate();
                if (!viewYear) viewYear = today.year();
                if (!viewMonth) viewMonth = today.month();

                var daysInMonth = today.daysInMonth(viewYear, viewMonth);
                var firstWeekday = today.getFirstWeekDayOfMonth(viewYear, viewMonth);

                var html = '<div style="display:flex;align-items:center;justify-content:space-between;padding:8px 10px;border-bottom:1px solid #f1f5f9;">';
                html += '<button type="button" class="vjp-nav-btn" data-nav="prev">&rsaquo;</button>';
                html += '<span style="font-size:12px;font-weight:700;color:#334155;">' + PERSIAN_MONTHS[viewMonth - 1] + ' ' + toPersianDigits(viewYear) + '</span>';
                html += '<button type="button" class="vjp-nav-btn" data-nav="next">&lsaquo;</button></div>';

                html += '<div style="display:grid;grid-template-columns:repeat(7,1fr);padding:4px 6px;border-bottom:1px solid #f1f5f9;">';
                PERSIAN_WEEKDAYS.forEach(function (w) { html += '<div style="text-align:center;font-size:10px;font-weight:700;color:#94a3b8;">' + w + '</div>'; });
                html += '</div><div style="display:grid;grid-template-columns:repeat(7,1fr);padding:6px;">';

                for (var i = 0; i < firstWeekday; i++) html += '<div></div>';
                for (var day = 1; day <= daysInMonth; day++) {
                var isToday = (day === today.date() && viewMonth === today.month() && viewYear === today.year());
                html += '<button type="button" class="vjp-day-btn' + (isToday ? ' is-today' : '') + '" data-day="' + day + '">' + toPersianDigits(day) + '</button>';
                }
                html += '</div>';
                popup.innerHTML = html;

                popup.querySelector('[data-nav="prev"]').onclick = function () { viewMonth--; if (viewMonth < 1) { viewMonth = 12; viewYear--; } render(); };
                popup.querySelector('[data-nav="next"]').onclick = function () { viewMonth++; if (viewMonth > 12) { viewMonth = 1; viewYear++; } render(); };

                popup.querySelectorAll('[data-day]').forEach(function (btn) {
                btn.onclick = function () {
                var day = parseInt(btn.getAttribute('data-day'), 10);
                var selected = new persianDate([viewYear, viewMonth, day]);
                var jalali = selected.format('YYYY/MM/DD');
                input.value = jalali;
                writeJalaliToHidden(input, jalali);
                popup.style.display = 'none';
                };
                });
                }

                input.onclick = function (e) { e.preventDefault(); render(); popup.style.display = 'block'; };
                document.addEventListener('click', function (e) { if (!wrapper.contains(e.target)) popup.style.display = 'none'; });
                }

                // ── WYSIWYG & Template Auto-Populate ───────────────────────
                var editor = document.getElementById('createContractEditor');
                var textarea = document.getElementById('ContractBodyHtml');

                window.syncContractEditor = function () {
                if (editor && textarea) textarea.value = editor.innerHTML;
                };

                if (editor && textarea) {
                if (textarea.value) editor.innerHTML = textarea.value;
                editor.addEventListener('input', window.syncContractEditor);
                }

                document.querySelectorAll('.wysiwyg-btn[data-cmd]').forEach(function (btn) {
                btn.addEventListener('click', function () {
                var cmd = btn.getAttribute('data-cmd');
                if (editor) editor.focus();
                document.execCommand(cmd, false, null);
                window.syncContractEditor();
                });
                });

                var templateSelect = document.getElementById('TemplateId');
                if (templateSelect && editor) {
                templateSelect.addEventListener('change', function () {
                var id = this.value;
                if (id) {
                fetch('/Contracts/PreviewTemplate?id=' + id + '&raw=true')
                .then(function (r) { return r.text(); })
                .then(function (html) {
                if (html && (!editor.innerHTML.trim() || confirm('متن فعلی جایگزین شود؟'))) {
                editor.innerHTML = html;
                window.syncContractEditor();
                }
                });
                }
                });
                }

                // WYSIWYG Global Helpers
                window.changeFontSize = function (size) { if (editor) { editor.focus(); document.execCommand('fontSize', false, size); window.syncContractEditor(); } };
                window.insertImage = function () { document.getElementById('imageUploadInput')?.click(); };
                window.handleImageUpload = function (e) {
                var file = e.target.files[0];
                if (file) {
                var reader = new FileReader();
                reader.onload = function (evt) {
                if (editor) { editor.focus(); document.execCommand('insertImage', false, evt.target.result); window.syncContractEditor(); }
                };
                reader.readAsDataURL(file);
                }
                };
                window.insertTable = function () {
                var r = prompt('تعداد ردیف:', '3'), c = prompt('تعداد ستون:', '3');
                if (r && c) {
                var html = '<table style="width:100%;border-collapse:collapse;margin:10px 0;">';
                for (var i = 0; i < parseInt(r, 10); i++) {
                html += '<tr>';
                for (var j = 0; j < parseInt(c, 10); j++) html += '<td style="border:1px solid #ddd;padding:6px;">&nbsp;</td>';
                html += '</tr>';
                }
                html += '</table>';
                if (editor) { editor.focus(); document.execCommand('insertHTML', false, html); window.syncContractEditor(); }
                }
                };

                // ── DOM Initialization ─────────────────────────────────────
                document.addEventListener('DOMContentLoaded', function () {
                // ۱. اجرای اولیه گام ۱
                updateWizardStep(1);

                // ۲. ایونت کلیک دکمه‌های بالایی ویزارد (شماره‌های ۱، ۲، ۳)
                document.querySelectorAll('.wizard-step-btn').forEach(function (btn) {
                btn.addEventListener('click', function () {
                var targetStep = parseInt(btn.getAttribute('data-step'), 10);
                if (targetStep) updateWizardStep(targetStep);
                });
                });

                // ۳. ایونت دکمه گام بعدی
                document.getElementById('nextStepBtn')?.addEventListener('click', function () {
                if (currentStep < totalSteps) updateWizardStep(currentStep + 1);
                });

                // ۴. ایونت دکمه گام قبلی
                document.getElementById('prevStepBtn')?.addEventListener('click', function () {
                if (currentStep > 1) updateWizardStep(currentStep - 1);
                });

                // ۵. راه اندازی تاریخ پرشین
                if (typeof persianDate !== 'undefined') {
                document.querySelectorAll('.jalali-date-input').forEach(createDatepicker);
                }

                // ۶. همگام‌سازی ادیتور هنگام ثبت فرم
                document.getElementById('contractCreateForm')?.addEventListener('submit', window.syncContractEditor);
                });
                })();

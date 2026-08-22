(function() {
            'use strict';

            // ════════════════════════════════════════════════════════════
            // Vanilla JS Jalali Datepicker
            // Uses the pure-JS persian-date library (NO jQuery).
            // ════════════════════════════════════════════════════════════
            var PERSIAN_MONTHS = ['فروردین', 'اردیبهشت', 'خرداد', 'تیر', 'مرداد', 'شهریور', 'مهر', 'آبان', 'آذر', 'دی', 'بهمن', 'اسفند'];
            var PERSIAN_WEEKDAYS = ['ش', 'ی', 'د', 'س', 'چ', 'پ', 'ج']; // Sat..Fri

            function toPersianDigits(str) {
                return String(str).replace(/\d/g, function(d) {
                    return '۰۱۲۳۴۵۶۷۸۹'[d];
                });
            }

            function createDatepicker(input) {
                var wrapper = input.closest('.jalali-datepicker-wrapper') || input.parentElement;
                if (!wrapper) return null;

                var popup = document.createElement('div');
                popup.className = 'vanilla-jalali-popup';
                wrapper.style.position = 'relative';
                wrapper.appendChild(popup);

                var viewYear = 0;
                var viewMonth = 0;

                function render() {
                    var today = new persianDate();
                    if (!viewYear) viewYear = today.year();
                    if (!viewMonth) viewMonth = today.month();

                    var daysInMonth = today.daysInMonth(viewYear, viewMonth);
                    var firstWeekday = today.getFirstWeekDayOfMonth(viewYear, viewMonth); // 0 = Saturday

                    var html = '';

                    // Header: prev / title / next
                    html += '<div style="display:flex;align-items:center;justify-content:space-between;padding:8px 10px;border-bottom:1px solid #f1f5f9;">';
                    html += '<button type="button" class="vjp-nav-btn" data-nav="prev" aria-label="ماه قبل">&rsaquo;</button>';
                    html += '<span style="font-size:12px;font-weight:700;color:#334155;">' + PERSIAN_MONTHS[viewMonth - 1] + ' ' + toPersianDigits(viewYear) + '</span>';
                    html += '<button type="button" class="vjp-nav-btn" data-nav="next" aria-label="ماه بعد">&lsaquo;</button>';
                    html += '</div>';

                    // Weekday header
                    html += '<div style="display:grid;grid-template-columns:repeat(7,1fr);padding:4px 6px;border-bottom:1px solid #f1f5f9;">';
                    PERSIAN_WEEKDAYS.forEach(function(w) {
                        html += '<div style="text-align:center;font-size:10px;font-weight:700;color:#94a3b8;padding:2px 0;">' + w + '</div>';
                    });
                    html += '</div>';

                    // Day grid
                    html += '<div style="display:grid;grid-template-columns:repeat(7,1fr);padding:6px;">';
                    for (var i = 0; i < firstWeekday; i++) {
                        html += '<div></div>';
                    }
                    for (var day = 1; day <= daysInMonth; day++) {
                        var isToday = (day === today.date() && viewMonth === today.month() && viewYear === today.year());
                        html += '<button type="button" class="vjp-day-btn' + (isToday ? ' is-today' : '') + '" data-day="' + day + '">' + toPersianDigits(day) + '</button>';
                    }
                    html += '</div>';

                    popup.innerHTML = html;

                    // Bind navigation with stopPropagation
                    popup.querySelector('[data-nav="prev"]').addEventListener('click', function(e) {
                        e.stopPropagation();
                        e.preventDefault();
                        viewMonth--;
                        if (viewMonth < 1) { viewMonth = 12; viewYear--; }
                        render();
                    });
                    popup.querySelector('[data-nav="next"]').addEventListener('click', function(e) {
                        e.stopPropagation();
                        e.preventDefault();
                        viewMonth++;
                        if (viewMonth > 12) { viewMonth = 1; viewYear++; }
                        render();
                    });

                    // Bind day selection
                    popup.querySelectorAll('[data-day]').forEach(function(btn) {
                        btn.addEventListener('click', function() {
                            var day = parseInt(btn.getAttribute('data-day'), 10);
                            var selected = new persianDate([viewYear, viewMonth, day]);
                            var jalali = selected.format('YYYY/MM/DD');
                            input.value = jalali;
                            writeJalaliToHidden(input, jalali);
                            popup.style.display = 'none';
                        });
                    });
                }

                function show() {
                    viewYear = 0;
                    viewMonth = 0;
                    render();
                    popup.style.display = 'block';
                }

                function hide() {
                    popup.style.display = 'none';
                }

                // Open on input click
                input.addEventListener('click', function(e) {
                    e.preventDefault();
                    e.stopPropagation();
                    show();
                });

                // Open on calendar icon click
                var icon = wrapper.querySelector('span.form-icon-left') || wrapper.querySelector('span.absolute') || wrapper.querySelector('.input-icon-right');
                if (icon) {
                    icon.addEventListener('click', function(e) {
                        e.preventDefault();
                        e.stopPropagation();
                        show();
                        input.focus();
                    });
                }

                // Close on outside click
                document.addEventListener('click', function(e) {
                    if (!wrapper.contains(e.target)) hide();
                });

                return { show: show, hide: hide };
            }

            // ── Jalali Date Picker Initialization ──────────────────────
            function writeJalaliToHidden(input, jalali) {
                var fieldName = input.getAttribute('data-jalali') || input.getAttribute('data-target');
                if (!fieldName) return;
                var hidden = input.closest('.jalali-datepicker-wrapper').querySelector('input.jalali-date-hidden');
                if (!hidden) hidden = document.querySelector('input[data-target="' + fieldName + '"]');
                if (hidden && jalali) {
                    try {
                        var parts = jalali.split('/');
                        if (parts.length === 3) {
                            var pd = new persianDate([parseInt(parts[0]), parseInt(parts[1]), parseInt(parts[2])]);
                            var g = pd.toDate();
                            var iso = g.getFullYear() + '-' +
                                String(g.getMonth() + 1).padStart(2, '0') + '-' +
                                String(g.getDate()).padStart(2, '0');
                            hidden.value = iso;
                        }
                    } catch (e) {}
                }
            }

            function initJalaliPickers() {
                if (typeof persianDate === 'undefined') {
                    console.warn('persian-date library not loaded — date inputs will accept manual text entry.');
                    return;
                }

                document.querySelectorAll('.jalali-date-input').forEach(function(input) {
                    var fieldName = input.getAttribute('data-jalali');
                    if (!fieldName) return;

                    var wrapper = input.closest('.jalali-datepicker-wrapper') || input.parentElement;
                    var hidden = wrapper.querySelector('input.jalali-date-hidden') ||
                        document.querySelector('input[data-target="' + fieldName + '"]');
                    var existingVal = (hidden && hidden.value) ? hidden.value : input.value;
                    if (existingVal) {
                        try {
                            var d = new Date(existingVal);
                            if (!isNaN(d.getTime())) {
                                var jDate = new persianDate(d);
                                input.value = jDate.format('YYYY/MM/DD');
                            }
                        } catch (e) {}
                    }

                    createDatepicker(input);
                });
            }

            // ── Jalali-to-Gregorian Conversion Before Submit ───────────
            function convertDatesBeforeSubmit() {
                var form = document.querySelector('form[asp-action="Edit"]');
                if (!form) return;

                form.addEventListener('submit', function() {
                    syncEditEditor();

                    document.querySelectorAll('.jalali-date-input').forEach(function(input) {
                        var fieldName = input.getAttribute('data-jalali');
                        if (!fieldName) return;

                        var jalaliText = input.value.trim();
                        if (jalaliText) {
                            try {
                                writeJalaliToHidden(input, jalaliText);
                            } catch (e) {
                                console.warn('Jalali date conversion failed for', fieldName, e);
                            }
                        }
                    });
                });
            }

            // ── WYSIWYG Editor ────────────────────────────────────────
            var editEditor = document.getElementById('editContractEditor');
            var editTextarea = document.getElementById('ContractBodyHtml');

            window.syncEditEditor = function() {
                if (editEditor && editTextarea) {
                    editTextarea.value = editEditor.innerHTML;
                }
            };

            if (editEditor && editTextarea && editTextarea.value) {
                editEditor.innerHTML = editTextarea.value;
            }

            if (editEditor) {
                editEditor.addEventListener('input', syncEditEditor);
            }

            document.querySelectorAll('.wysiwyg-btn[data-cmd]').forEach(function(btn) {
                btn.addEventListener('click', function() {
                    var cmd = btn.getAttribute('data-cmd');
                    if (editEditor) editEditor.focus();
                    document.execCommand(cmd, false, null);
                    syncEditEditor();
                });
            });

            // ── Template Auto-populate ────────────────────────────────
            var templateSelect = document.getElementById('TemplateId');
            if (templateSelect && editEditor) {
                templateSelect.addEventListener('change', function() {
                    var templateId = this.value;
                    if (templateId && templateId !== '') {
                        fetch('/Contracts/PreviewTemplate?id=' + templateId + '&raw=true')
                            .then(function(r) { return r.text(); })
                            .then(function(html) {
                                if (html && html.length > 0) {
                                    if (!editEditor.innerHTML.trim() || confirm('با انتخاب قالب جدید، متن فعلی جایگزین می‌شود. ادامه دهید؟')) {
                                        editEditor.innerHTML = html;
                                        syncEditEditor();
                                    }
                                }
                            })
                            .catch(function() {});
                    }
                });
            }

            // ── Initialize ────────────────────────────────────────────
            document.addEventListener('DOMContentLoaded', function() {
                initJalaliPickers();
                convertDatesBeforeSubmit();

                // Ensure clicking a calendar icon forwards focus to its input
                // so the Jalali datepicker opens reliably.
                document.querySelectorAll('.jalali-datepicker-wrapper .form-icon-left').forEach(function(icon) {
                    icon.addEventListener('click', function() {
                        var input = this.closest('.jalali-datepicker-wrapper').querySelector('input.jalali-date-input');
                        if (input) input.focus();
                    });
                });
            });
        })();

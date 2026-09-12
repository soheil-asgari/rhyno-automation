(function () {
    'use strict';

    // ════════════════════════════════════════════════════════════
    // Vanilla JS Jalali Datepicker
    // Uses the pure-JS persian-date library (NO jQuery).
    // ════════════════════════════════════════════════════════════
    var PERSIAN_MONTHS = ['فروردین', 'اردیبهشت', 'خرداد', 'تیر', 'مرداد', 'شهریور', 'مهر', 'آبان', 'آذر', 'دی', 'بهمن', 'اسفند'];
    var PERSIAN_WEEKDAYS = ['ش', 'ی', 'د', 'س', 'چ', 'پ', 'ج']; // Sat..Fri

    function toPersianDigits(str) {
        return String(str).replace(/\d/g, function (d) {
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
            PERSIAN_WEEKDAYS.forEach(function (w) {
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
            popup.querySelector('[data-nav="prev"]').addEventListener('click', function (e) {
                e.stopPropagation();
                e.preventDefault();
                viewMonth--;
                if (viewMonth < 1) { viewMonth = 12; viewYear--; }
                render();
            });
            popup.querySelector('[data-nav="next"]').addEventListener('click', function (e) {
                e.stopPropagation();
                e.preventDefault();
                viewMonth++;
                if (viewMonth > 12) { viewMonth = 1; viewYear++; }
                render();
            });

            // Bind day selection
            popup.querySelectorAll('[data-day]').forEach(function (btn) {
                btn.addEventListener('click', function () {
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
        input.addEventListener('click', function (e) {
            e.preventDefault();
            e.stopPropagation();
            show();
        });

        // Open on calendar icon click
        var icon = wrapper.querySelector('span.form-icon-left') || wrapper.querySelector('span.absolute') || wrapper.querySelector('.input-icon-right');
        if (icon) {
            icon.addEventListener('click', function (e) {
                e.preventDefault();
                e.stopPropagation();
                show();
                input.focus();
            });
        }

        // Close on outside click
        document.addEventListener('click', function (e) {
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
            } catch (e) { }
        }
    }

    function initJalaliPickers() {
        if (typeof persianDate === 'undefined') {
            console.warn('persian-date library not loaded — date inputs will accept manual text entry.');
            return;
        }

        document.querySelectorAll('.jalali-date-input').forEach(function (input) {
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
                } catch (e) { }
            }

            createDatepicker(input);
        });
    }

    // ── Jalali-to-Gregorian Conversion Before Submit ───────────
    function convertDatesBeforeSubmit() {
        var form = document.querySelector('form[asp-action="Edit"]');
        if (!form) return;

        form.addEventListener('submit', function () {
            syncEditEditor();

            document.querySelectorAll('.jalali-date-input').forEach(function (input) {
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

    window.syncEditEditor = function () {
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

    document.querySelectorAll('.wysiwyg-btn[data-cmd]').forEach(function (btn) {
        btn.addEventListener('click', function () {
            var cmd = btn.getAttribute('data-cmd');
            if (editEditor) editEditor.focus();
            document.execCommand(cmd, false, null);
            syncEditEditor();
        });
    });

    // ── Template Auto-populate ────────────────────────────────
    var templateSelect = document.getElementById('TemplateId');
    if (templateSelect && editEditor) {
        templateSelect.addEventListener('change', function () {
            var templateId = this.value;
            if (templateId && templateId !== '') {
                fetch('/Contracts/PreviewTemplate?id=' + templateId + '&raw=true')
                    .then(function (r) { return r.text(); })
                    .then(function (html) {
                        if (html && html.length > 0) {
                            if (!editEditor.innerHTML.trim() || confirm('با انتخاب قالب جدید، متن فعلی جایگزین می‌شود. ادامه دهید؟')) {
                                editEditor.innerHTML = html;
                                syncEditEditor();
                            }
                        }
                    })
                    .catch(function () { });
            }
        });
    }

    // ── Initialize ────────────────────────────────────────────
    document.addEventListener('DOMContentLoaded', function () {
        initJalaliPickers();
        convertDatesBeforeSubmit();

        // Render initial dynamic fields for selected contract type
        var contractTypeSelect = document.getElementById('ContractType');
        if (contractTypeSelect && contractTypeSelect.value) {
            renderDynamicFieldsForType(contractTypeSelect.value);
        }
    });
})();

// ── Dynamic Type-Specific Fields (EAV) ──────────────────────
function escapeHtml(str) {
    if (str === null || str === undefined) return '';
    return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
}

var allTypeFields = [];
try { var jsonEl = document.getElementById('allTypeFieldsJson'); if (jsonEl && jsonEl.value) allTypeFields = JSON.parse(jsonEl.value); } catch (e) { }

var activeFieldValues = {};
try {
    var valuesEl = document.getElementById('submittedFieldValuesJson');
    if (valuesEl && valuesEl.value && valuesEl.value !== '{}') {
        var parsed = JSON.parse(valuesEl.value);
        if (typeof parsed === 'object' && !Array.isArray(parsed)) activeFieldValues = parsed;
    }
} catch (e) { }

function updateActiveFieldValuesFromDOM() {
    document.querySelectorAll('#dynamicFieldsContainer .dynamic-field-input').forEach(function (input) {
        var key = input.getAttribute('data-field-key');
        if (key) {
            activeFieldValues[key] = input.value;
        }
    });
}

function renderDynamicFieldsForType(contractType) {
    updateActiveFieldValuesFromDOM();
    var container = document.getElementById('dynamicFieldsContainer');
    if (!container) return;
    var matching = allTypeFields.filter(function (f) { return f.ContractType === contractType; });
    if (matching.length === 0) { container.innerHTML = ''; return; }

    var sections = {};
    matching.forEach(function (f) {
        var section = f.SectionName || 'سایر اطلاعات';
        if (!sections[section]) sections[section] = [];
        sections[section].push(f);
    });

    var html = '<div class="erp-fieldset mt-3">';
    html += '<div class="flex items-center gap-2 mb-2 pb-1 border-b border-slate-100 dark:border-slate-800">';
    html += '<div class="w-6 h-6 rounded bg-teal-50 dark:bg-teal-950/30 flex items-center justify-center text-teal-600 dark:text-teal-400 text-[10px]"><i class="bi bi-list-check"></i></div>';
    html += '<div><h2>اطلاعات تخصصی قرارداد</h2><p class="form-section-desc">فیلدهای مرتبط با نوع قرارداد</p></div></div>';

    Object.keys(sections).forEach(function (sectionName) {
        var fields = sections[sectionName];
        html += '<div class="mb-3"><h3 class="text-[10px] font-bold text-slate-600 dark:text-slate-400 mb-1.5">' + escapeHtml(sectionName) + '</h3>';
        html += '<div class="grid grid-cols-2 md:grid-cols-3 xl:grid-cols-4 gap-2">';
        fields.forEach(function (field) {
            var savedValue = activeFieldValues[field.FieldKey] !== undefined ? activeFieldValues[field.FieldKey] : '';
            html += '<div class="dynamic-field" data-field-key="' + field.FieldKey + '">';
            html += '<label class="erp-label">' + escapeHtml(field.DisplayName) + (field.IsRequired ? ' <span class="text-red-500">*</span>' : '') + '</label>';
            html += '<div class="input-wrapper">';
            if (field.FieldType === 'Boolean') {
                html += '<select class="erp-input dynamic-field-input" data-field-key="' + field.FieldKey + '"><option value="">انتخاب کنید</option><option value="بله"' + (savedValue === 'بله' ? ' selected' : '') + '>بله</option><option value="خیر"' + (savedValue === 'خیر' ? ' selected' : '') + '>خیر</option></select>';
            } else if (field.FieldType === 'Select' && field.Options) {
                html += '<select class="erp-input dynamic-field-input" data-field-key="' + field.FieldKey + '"><option value="">انتخاب کنید</option>';
                field.Options.split(',').forEach(function (o) {
                    var trimmed = o.trim();
                    html += '<option value="' + escapeHtml(trimmed) + '"' + (savedValue === trimmed ? ' selected' : '') + '>' + escapeHtml(trimmed) + '</option>';
                });
                html += '</select>';
            } else if (field.FieldType === 'TextArea' || field.FieldType === 'Textarea') {
                html += '<textarea class="erp-input dynamic-field-input w-full min-h-[52px] text-[11px]" data-field-key="' + field.FieldKey + '" placeholder="' + escapeHtml(field.Placeholder || '') + '" rows="2">' + escapeHtml(savedValue) + '</textarea>';
            } else if (field.FieldType === 'Number' || field.FieldType === 'Decimal') {
                html += '<input type="text" class="erp-input dynamic-field-input" data-field-key="' + field.FieldKey + '" value="' + escapeHtml(savedValue) + '" placeholder="' + escapeHtml(field.Placeholder || '0') + '" dir="ltr" />';
            } else if (field.FieldType === 'Date') {
                html += '<input type="date" class="erp-input dynamic-field-input" data-field-key="' + field.FieldKey + '" value="' + escapeHtml(savedValue) + '" />';
            } else {
                html += '<input type="text" class="erp-input dynamic-field-input" data-field-key="' + field.FieldKey + '" value="' + escapeHtml(savedValue) + '" placeholder="' + escapeHtml(field.Placeholder || '') + '" />';
            }
            html += '</div></div>';
        });
        html += '</div></div>';
    });
    html += '</div>';
    container.innerHTML = html;
}

var contractTypeSelect = document.getElementById('ContractType');
if (contractTypeSelect) {
    contractTypeSelect.addEventListener('change', function () { renderDynamicFieldsForType(this.value); });
}

function collectDynamicFieldValues() {
    updateActiveFieldValuesFromDOM();
    // Remove any prior dynamic hidden inputs
    document.querySelectorAll('input.dynamic-field-hidden').forEach(function (h) { h.remove(); });
    // Create proper hidden inputs for ASP.NET Core Dictionary model binding
    document.querySelectorAll('.dynamic-field-input').forEach(function (input) {
        var key = input.getAttribute('data-field-key');
        if (key) {
            var hidden = document.createElement('input');
            hidden.type = 'hidden';
            hidden.name = 'FieldValues[' + key + ']';
            hidden.value = input.value || '';
            hidden.className = 'dynamic-field-hidden';
            input.closest('.dynamic-field')?.appendChild(hidden);
        }
    });
}

// ── Live Currency & Tax Recalculation ───────────────────────
function recalculateTaxes() {
    var totalAmountInput = document.getElementById('TotalAmount');
    var taxMethodSelect = document.getElementById('TaxMethod');
    var taxRateInput = document.getElementById('DefaultTaxRatePercent');
    var totalTaxInput = document.getElementById('TotalTaxAmount');
    var totalInclusiveInput = document.getElementById('TotalAmountInclusiveTax');

    if (!totalAmountInput || !taxMethodSelect || !totalTaxInput || !totalInclusiveInput) return;

    var total = parseFloat(totalAmountInput.value) || 0;
    var method = taxMethodSelect.value || 'Exclusive';
    var rate = taxRateInput ? (parseFloat(taxRateInput.value) || 0) : 0;

    var taxAmount = 0;
    var inclusiveAmount = total;

    if (method === 'Exclusive') {
        taxAmount = Math.round(total * (rate / 100) * 100) / 100;
        inclusiveAmount = total + taxAmount;
    } else if (method === 'Inclusive') {
        inclusiveAmount = total;
        if (rate > 0) {
            taxAmount = Math.round((total - (total / (1 + (rate / 100)))) * 100) / 100;
        } else {
            taxAmount = 0;
        }
    } else { // None
        taxAmount = 0;
        inclusiveAmount = total;
    }

    totalTaxInput.value = taxAmount.toLocaleString('en-US', { minimumFractionDigits: 0, maximumFractionDigits: 2 });
    totalInclusiveInput.value = inclusiveAmount.toLocaleString('en-US', { minimumFractionDigits: 0, maximumFractionDigits: 2 });
}

function handleCurrencyChange() {
    var currencySelect = document.getElementById('CurrencyCode');
    var exchangeContainer = document.getElementById('exchangeRateContainer');
    var exchangeInput = document.getElementById('ExchangeRate');
    if (!currencySelect || !exchangeContainer) return;

    if (currencySelect.value === 'IRR') {
        exchangeContainer.style.display = 'none';
        if (exchangeInput) exchangeInput.value = '1';
    } else {
        exchangeContainer.style.display = '';
        if (exchangeInput && (!exchangeInput.value || parseFloat(exchangeInput.value) <= 0)) {
            exchangeInput.value = '1';
        }
    }
}

function handleTaxMethodChange() {
    var taxMethodSelect = document.getElementById('TaxMethod');
    var taxRateContainer = document.getElementById('taxRateContainer');
    var taxRateInput = document.getElementById('DefaultTaxRatePercent');
    if (!taxMethodSelect || !taxRateContainer) return;

    if (taxMethodSelect.value === 'None') {
        taxRateContainer.style.display = 'none';
        if (taxRateInput) taxRateInput.value = '0';
    } else {
        taxRateContainer.style.display = '';
        if (taxRateInput && (!taxRateInput.value || parseFloat(taxRateInput.value) === 0)) {
            taxRateInput.value = '10';
        }
    }
    recalculateTaxes();
}

var totalAmountEl = document.getElementById('TotalAmount');
if (totalAmountEl) {
    totalAmountEl.addEventListener('input', recalculateTaxes);
}

var taxRateEl = document.getElementById('DefaultTaxRatePercent');
if (taxRateEl) {
    taxRateEl.addEventListener('input', recalculateTaxes);
}

var taxMethodEl = document.getElementById('TaxMethod');
if (taxMethodEl) {
    taxMethodEl.addEventListener('change', handleTaxMethodChange);
}

var currencyEl = document.getElementById('CurrencyCode');
if (currencyEl) {
    currencyEl.addEventListener('change', handleCurrencyChange);
}

// Initial calculation and state
handleCurrencyChange();
if (taxMethodEl && taxMethodEl.value === 'None') {
    var trc = document.getElementById('taxRateContainer');
    if (trc) trc.style.display = 'none';
}
recalculateTaxes();

var editFormElement = document.getElementById('contractEditForm') || document.querySelector('form[action*="Edit"]') || document.querySelector('form');
if (editFormElement) {
    editFormElement.addEventListener('submit', function () {
        collectDynamicFieldValues();
        if (typeof window.syncEditEditor === 'function') window.syncEditEditor();
    });
}



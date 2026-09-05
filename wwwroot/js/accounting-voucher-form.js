/**
 * مدیریت کامل فرم سند حسابداری (نسخه تعاملی و عملیاتی)
 */
var VoucherForm = (function () {
    var copiedRowData = null;

    // تبدیل اعداد فارسی به انگلیسی
    function toEnglishDigits(str) {
        if (!str) return '';
        return String(str)
            .replace(/[۰-۹]/g, function (d) { return '۰۱۲۳۴۵۶۷۸۹'.indexOf(d); })
            .replace(/[٠-٩]/g, function (d) { return '٠١٢٣٤٥٦٧٨٩'.indexOf(d); });
    }

    // جداکننده ارقام
    function formatCurrency(value) {
        var clean = toEnglishDigits(value).replace(/[^\d]/g, '');
        if (!clean) return '';
        return clean.replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    }

    function parseNumeric(val) {
        var clean = toEnglishDigits(val).replace(/,/g, '');
        var num = parseFloat(clean);
        return isNaN(num) ? 0 : num;
    }

    // نمایش پیام هشدار شیک به کاربر
    function showToast(message, type) {
        var existing = document.querySelector('.rk-toast');
        if (existing) existing.remove();

        var toast = document.createElement('div');
        toast.className = 'rk-toast ' + (type || 'info');
        toast.innerHTML = '<span>' + message + '</span>';
        document.body.appendChild(toast);

        setTimeout(function () { toast.classList.add('show'); }, 10);
        setTimeout(function () {
            toast.classList.remove('show');
            setTimeout(function () { toast.remove(); }, 300);
        }, 3500);
    }

    function init() {
        renderInitialRows(1); // دقیقاً ۱ سطر در هنگام باز شدن سند جدید
        bindEvents();
        calculateTotals();
    }

    function renderInitialRows(count) {
        var tbody = document.getElementById('tblVoucherItemsBody');
        if (!tbody) return;
        tbody.innerHTML = '';
        for (var i = 1; i <= count; i++) {
            tbody.appendChild(createRow(i));
        }
    }

    // ساخت سطر به همراه شماره ردیف در اولین ستون
    function createRow(index, data) {
        var tr = document.createElement('tr');
        tr.setAttribute('data-row-index', index);
        if (index === 1) tr.classList.add('selected');

        tr.innerHTML =
            '<td class="row-index-cell">' + index + '</td>' +
            '<td class="col-center"><input type="text" class="col-center bg-disabled" value="موقت" readonly /></td>' +
            '<td><input type="text" class="txt-acc-code col-center font-num" placeholder="کد معین" /></td>' +
            '<td><input type="text" class="txt-level4 col-center font-num" placeholder="سطح ۴" /></td>' +
            '<td><input type="text" class="txt-level5 col-center font-num" placeholder="سطح ۵" /></td>' +
            '<td><input type="text" class="txt-level6 col-center font-num" placeholder="سطح ۶" /></td>' +
            '<td><input type="text" class="txt-desc" placeholder="شرح آرتیکل..." /></td>' +
            '<td><input type="text" class="txt-debit col-left font-num" placeholder="0" /></td>' +
            '<td><input type="text" class="txt-credit col-left font-num" placeholder="0" /></td>' +
            '<td><input type="text" class="txt-track-no col-center font-num" /></td>' +
            '<td><input type="text" class="txt-track-date col-center font-num" /></td>' +
            '<td><input type="text" class="txt-desc2" /></td>';

        if (data) {
            var inputs = tr.querySelectorAll('input');
            data.forEach(function (val, idx) {
                if (inputs[idx]) inputs[idx].value = val;
            });
        }

        // انتخاب ردیف با کلیک
        tr.addEventListener('click', function () {
            document.querySelectorAll('#tblVoucherItemsBody tr').forEach(function (r) {
                r.classList.remove('selected');
            });
            tr.classList.add('selected');
        });

        // مدیریت مبالغ بدهکار و بستانکار
        var debitInp = tr.querySelector('.txt-debit');
        var creditInp = tr.querySelector('.txt-credit');

        [debitInp, creditInp].forEach(function (inp) {
            inp.addEventListener('input', function (e) {
                var pos = e.target.selectionStart;
                var oldLen = e.target.value.length;
                e.target.value = formatCurrency(e.target.value);
                var newLen = e.target.value.length;
                pos = pos + (newLen - oldLen);
                e.target.setSelectionRange(pos, pos);
                calculateTotals();
            });

            // فشردن Enter برای ایجاد سطر جدید سریع
            inp.addEventListener('keydown', function (e) {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    document.getElementById('btnAddRow').click();
                }
            });
        });

        return tr;
    }

    function reIndexRows() {
        var rows = document.querySelectorAll('#tblVoucherItemsBody tr');
        rows.forEach(function (tr, idx) {
            tr.setAttribute('data-row-index', idx + 1);
            var indexCell = tr.querySelector('.row-index-cell');
            if (indexCell) indexCell.textContent = (idx + 1);
        });
    }

    function calculateTotals() {
        var totalDebit = 0;
        var totalCredit = 0;

        document.querySelectorAll('#tblVoucherItemsBody tr').forEach(function (tr) {
            totalDebit += parseNumeric(tr.querySelector('.txt-debit').value);
            totalCredit += parseNumeric(tr.querySelector('.txt-credit').value);
        });

        var diff = totalDebit - totalCredit;

        var txtTotalDebit = document.getElementById('txtTotalDebit');
        var txtTotalCredit = document.getElementById('txtTotalCredit');
        var txtRemDebit = document.getElementById('txtRemDebit');
        var txtRemCredit = document.getElementById('txtRemCredit');

        if (txtTotalDebit) txtTotalDebit.value = totalDebit > 0 ? totalDebit.toLocaleString('en-US') : '0';
        if (txtTotalCredit) txtTotalCredit.value = totalCredit > 0 ? totalCredit.toLocaleString('en-US') : '0';

        if (diff > 0) {
            if (txtRemDebit) txtRemDebit.value = Math.abs(diff).toLocaleString('en-US');
            if (txtRemCredit) txtRemCredit.value = '';
        } else if (diff < 0) {
            if (txtRemDebit) txtRemDebit.value = '';
            if (txtRemCredit) txtRemCredit.value = Math.abs(diff).toLocaleString('en-US');
        } else {
            if (txtRemDebit) txtRemDebit.value = '';
            if (txtRemCredit) txtRemCredit.value = '';
        }

        return { debit: totalDebit, credit: totalCredit, diff: diff };
    }

    // تقویم شمسی مستقل
    function openDatePickerModal() {
        var curDateInput = document.getElementById('txtVoucherDate');
        var curVal = curDateInput ? curDateInput.value.split('/') : ['1403', '01', '01'];

        var overlay = document.createElement('div');
        overlay.className = 'rk-modal-overlay';
        overlay.innerHTML =
            '<div class="rk-modal">' +
            '<div class="rk-modal-header">' +
            '<span>انتخاب تاریخ سند</span>' +
            '<button type="button" class="rk-btn" id="btnCloseCal">✕</button>' +
            '</div>' +
            '<div style="display:flex; gap:6px; margin-bottom:12px;">' +
            '<input type="number" id="calYear" class="rk-input col-center font-num" value="' + (curVal[0] || 1403) + '" />' +
            '<select id="calMonth" class="rk-input col-center font-num">' +
            '<option value="01">فروردین</option><option value="02">اردیبهشت</option><option value="03">خرداد</option>' +
            '<option value="04">تیر</option><option value="05">مرداد</option><option value="06">شهریور</option>' +
            '<option value="07">مهر</option><option value="08">آبان</option><option value="09">آذر</option>' +
            '<option value="10">دی</option><option value="11">بهمن</option><option value="12">اسفند</option>' +
            '</select>' +
            '<input type="number" id="calDay" min="1" max="31" class="rk-input col-center font-num" value="' + (curVal[2] || 1) + '" />' +
            '</div>' +
            '<div style="display:flex; justify-content:flex-end; gap:6px;">' +
            '<button type="button" class="rk-btn rk-btn-primary" id="btnApplyCal">انتخاب</button>' +
            '</div>' +
            '</div>';

        document.body.appendChild(overlay);

        var monthSelect = overlay.querySelector('#calMonth');
        if (monthSelect && curVal[1]) monthSelect.value = curVal[1];

        overlay.querySelector('#btnCloseCal').onclick = function () { overlay.remove(); };
        overlay.querySelector('#btnApplyCal').onclick = function () {
            var y = overlay.querySelector('#calYear').value;
            var m = overlay.querySelector('#calMonth').value;
            var d = overlay.querySelector('#calDay').value.padStart(2, '0');
            if (curDateInput) curDateInput.value = y + '/' + m + '/' + d;
            overlay.remove();
        };
    }

    // ماشین‌حساب تعاملی
    function openCalculatorModal() {
        var overlay = document.createElement('div');
        overlay.className = 'rk-modal-overlay';
        overlay.innerHTML =
            '<div class="rk-modal" style="width:240px;">' +
            '<div class="rk-modal-header"><span>ماشین حساب</span><button class="rk-btn" id="btnCalcClose">✕</button></div>' +
            '<input type="text" id="calcScreen" class="rk-input col-left font-num" style="height:36px; font-size:16px; margin-bottom:8px;" readonly value="0" />' +
            '<div style="display:grid; grid-template-columns:repeat(4, 1fr); gap:4px;" id="calcKeys">' +
            '<button class="rk-btn">7</button><button class="rk-btn">8</button><button class="rk-btn">9</button><button class="rk-btn">/</button>' +
            '<button class="rk-btn">4</button><button class="rk-btn">5</button><button class="rk-btn">6</button><button class="rk-btn">*</button>' +
            '<button class="rk-btn">1</button><button class="rk-btn">2</button><button class="rk-btn">3</button><button class="rk-btn">-</button>' +
            '<button class="rk-btn">C</button><button class="rk-btn">0</button><button class="rk-btn">=</button><button class="rk-btn">+</button>' +
            '</div>' +
            '</div>';

        document.body.appendChild(overlay);
        var screen = overlay.querySelector('#calcScreen');

        overlay.querySelector('#btnCalcClose').onclick = function () { overlay.remove(); };
        overlay.querySelector('#calcKeys').addEventListener('click', function (e) {
            if (e.target.tagName !== 'BUTTON') return;
            var key = e.target.textContent;
            if (key === 'C') screen.value = '0';
            else if (key === '=') {
                try { screen.value = eval(screen.value.replace(/,/g, '')); } catch (err) { screen.value = 'خطا'; }
            } else {
                screen.value = (screen.value === '0' || screen.value === 'خطا') ? key : screen.value + key;
            }
        });
    }

    function bindEvents() {
        // ۱. دکمه ذخیره سند با بررسی شرط تراز بودن
        var btnSave = document.getElementById('btnSaveVoucher');
        if (btnSave) {
            btnSave.addEventListener('click', function () {
                var totals = calculateTotals();
                if (totals.debit === 0 && totals.credit === 0) {
                    showToast('سند خالی است! لطفاً مبالغ آرتیکل‌ها را وارد کنید.', 'error');
                    return;
                }
                if (totals.diff !== 0) {
                    showToast('سند تراز نیست! اختلاف بدهکار و بستانکار: ' + Math.abs(totals.diff).toLocaleString('fa-IR') + ' ریال', 'error');
                    return;
                }
                showToast('سند با موفقیت ذخیره و تراز تأیید شد.', 'success');
            });
        }

        // ۲. ذخیره و ایجاد سند جدید
        var btnSaveAndNew = document.getElementById('btnSaveAndNew');
        if (btnSaveAndNew) {
            btnSaveAndNew.addEventListener('click', function () {
                var totals = calculateTotals();
                if (totals.diff !== 0) {
                    showToast('ابتدا باید سند جاری تراز شود!', 'error');
                    return;
                }
                showToast('سند ذخیره شد. سند جدید باز شد.', 'success');
                renderInitialRows(1);
                calculateTotals();
            });
        }

        // ۳. بازگشت تغییرات / خالی‌سازی سند
        var btnUndo = document.getElementById('btnUndo');
        if (btnUndo) {
            btnUndo.addEventListener('click', function () {
                if (confirm('آیا از بازنشانی کلیه اطلاعات سند مطمئن هستید؟')) {
                    renderInitialRows(1);
                    calculateTotals();
                    showToast('تغییرات لغو شد.', 'info');
                }
            });
        }

        // ۴. چاپ سند
        var btnPrint = document.getElementById('btnPrint');
        if (btnPrint) {
            btnPrint.addEventListener('click', function () {
                window.print();
            });
        }

        // ۵. ماشین حساب
        var btnCalc = document.getElementById('btnCalculator');
        if (btnCalc) {
            btnCalc.addEventListener('click', openCalculatorModal);
        }

        // ۶. آیکون تقویم
        var btnCal = document.getElementById('btnCalendarPicker');
        if (btnCal) {
            btnCal.addEventListener('click', openDatePickerModal);
        }

        // ۷. نشانه‌گذاری (Bookmark)
        var btnBookmark = document.getElementById('btnBookmark');
        if (btnBookmark) {
            btnBookmark.addEventListener('click', function () {
                this.classList.toggle('active-bookmark');
                var isActive = this.classList.contains('active-bookmark');
                showToast(isActive ? 'سند نشانه‌گذاری شد.' : 'نشانه‌گذاری حذف شد.', 'info');
            });
        }

        // ۸. راهنما
        var btnHelp = document.getElementById('btnHelp');
        if (btnHelp) {
            btnHelp.addEventListener('click', function () {
                alert('راهنمای کلیدها:\n• کلید Enter در ستون مبالغ: ایجاد خودکار سطر جدید\n• دکمه ذخیره: ملزم به تراز بودن کامل بدهکار و بستانکار است.');
            });
        }

        // ۹. افزودن سطر
        var btnAdd = document.getElementById('btnAddRow');
        if (btnAdd) {
            btnAdd.addEventListener('click', function () {
                var tbody = document.getElementById('tblVoucherItemsBody');
                var currentCount = tbody.querySelectorAll('tr').length;
                var newRow = createRow(currentCount + 1);
                tbody.appendChild(newRow);
                newRow.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
                newRow.querySelector('.txt-acc-code').focus();
            });
        }

        // ۱۰. حذف سطر
        var btnDel = document.getElementById('btnDeleteRow');
        if (btnDel) {
            btnDel.addEventListener('click', function () {
                var selected = document.querySelector('#tblVoucherItemsBody tr.selected');
                var tbody = document.getElementById('tblVoucherItemsBody');
                if (tbody.querySelectorAll('tr').length <= 1) {
                    showToast('سند باید حداقل شامل یک سطر باشد.', 'error');
                    return;
                }
                if (selected) {
                    var prev = selected.previousElementSibling || selected.nextElementSibling;
                    selected.remove();
                    reIndexRows();
                    if (prev) {
                        prev.classList.add('selected');
                    }
                    calculateTotals();
                }
            });
        }

        // ۱۱. کپی سطر انتخاب‌شده
        var btnCopy = document.getElementById('btnCopyRow');
        if (btnCopy) {
            btnCopy.addEventListener('click', function () {
                var selected = document.querySelector('#tblVoucherItemsBody tr.selected');
                if (!selected) return;
                copiedRowData = [];
                selected.querySelectorAll('input').forEach(function (inp) {
                    copiedRowData.push(inp.value);
                });
                showToast('اطلاعات سطر کپی شد.', 'info');
            });
        }

        // ۱۲. برش سطر (Cut)
        var btnCut = document.getElementById('btnCutRow');
        if (btnCut) {
            btnCut.addEventListener('click', function () {
                var selected = document.querySelector('#tblVoucherItemsBody tr.selected');
                var tbody = document.getElementById('tblVoucherItemsBody');
                if (tbody.querySelectorAll('tr').length <= 1) {
                    showToast('امکان برش تنها سطر موجود وجود ندارد.', 'error');
                    return;
                }
                if (selected) {
                    btnCopy.click();
                    selected.remove();
                    reIndexRows();
                    calculateTotals();
                }
            });
        }

        // ۱۳. انتقال سطر به بالا
        var btnMoveUp = document.getElementById('btnMoveUp');
        if (btnMoveUp) {
            btnMoveUp.addEventListener('click', function () {
                var selected = document.querySelector('#tblVoucherItemsBody tr.selected');
                if (selected && selected.previousElementSibling) {
                    selected.parentNode.insertBefore(selected, selected.previousElementSibling);
                    reIndexRows();
                }
            });
        }

        // ۱۴. انتقال سطر به پایین
        var btnMoveDown = document.getElementById('btnMoveDown');
        if (btnMoveDown) {
            btnMoveDown.addEventListener('click', function () {
                var selected = document.querySelector('#tblVoucherItemsBody tr.selected');
                if (selected && selected.nextElementSibling) {
                    selected.parentNode.insertBefore(selected.nextElementSibling, selected);
                    reIndexRows();
                }
            });
        }

        // ۱۵. جستجو در سطرها
        var btnSearch = document.getElementById('btnSearchRow');
        if (btnSearch) {
            btnSearch.addEventListener('click', function () {
                var q = prompt('متن یا مبلغ مورد نظر را وارد کنید:');
                if (!q) return;
                var found = false;
                document.querySelectorAll('#tblVoucherItemsBody tr').forEach(function (tr) {
                    var text = tr.innerText || '';
                    tr.querySelectorAll('input').forEach(function (inp) { text += ' ' + inp.value; });
                    if (text.indexOf(q) !== -1) {
                        tr.classList.add('selected');
                        tr.scrollIntoView({ behavior: 'smooth' });
                        found = true;
                    } else {
                        tr.classList.remove('selected');
                    }
                });
                if (!found) showToast('موردی یافت نشد.', 'info');
            });
        }

        // ۱۶. وضعیت مالیاتی
        var btnTax = document.getElementById('btnTaxStatus');
        if (btnTax) {
            btnTax.addEventListener('click', function () {
                var selected = document.querySelector('#tblVoucherItemsBody tr.selected');
                if (!selected) {
                    showToast('ابتدا یک سطر را انتخاب کنید.', 'error');
                    return;
                }
                var descInput = selected.querySelector('.txt-desc');
                if (descInput) {
                    descInput.value = (descInput.value + ' [مشمول مالیات]').trim();
                    showToast('وضعیت مالیاتی به سطر اضافه شد.', 'info');
                }
            });
        }
    }

    return {
        init: init
    };
})();
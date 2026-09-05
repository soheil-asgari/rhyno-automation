var VoucherForm = (function () {
    var copiedRowData = null;
    var subsidiariesCache = [];
    var floatingDetailsCache = {};
    var journalTypesCache = [];
    var fiscalYearsCache = [];

    // ---- Anti-forgery ----
    function getAntiForgeryToken() {
        var meta = document.querySelector('meta[name="request-verification-token"]');
        if (meta) return meta.content;
        var form = document.getElementById('rhGlobalAntiForgery');
        if (form) {
            var input = form.querySelector('input[name="__RequestVerificationToken"]');
            if (input) return input.value;
        }
        return '';
    }

    // ---- Utility ----
    function toEnglishDigits(str) {
        if (!str) return '';
        return String(str).replace(/[۰-۹]/g, function (d) { return '۰۱۲۳۴۵۶۷۸۹'.indexOf(d); })
            .replace(/[٠-٩]/g, function (d) { return '٠١٢٣٤٥٦٧٨٩'.indexOf(d); });
    }

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

    function showLoading(show) {
        var existing = document.querySelector('.rk-form-loading');
        if (show) {
            if (!existing) {
                var div = document.createElement('div');
                div.className = 'rk-form-loading';
                div.innerHTML = '<div style="position:fixed;inset:0;background:rgba(255,255,255,.6);z-index:9998;display:flex;align-items:center;justify-content:center;"><div style="background:#1e293b;color:#fff;padding:12px 28px;border-radius:8px;font-size:13px;">در حال ذخیره...</div></div>';
                document.body.appendChild(div);
            }
        } else {
            if (existing) existing.remove();
        }
    }

    // ---- Data Loading ----
    function loadSubsidiaries(q) {
        var url = '/api/vouchers/subsidiaries';
        if (q) url += '?q=' + encodeURIComponent(q);
        return fetch(url).then(function (r) { return r.json(); }).then(function (data) {
            subsidiariesCache = data;
            return data;
        });
    }

    function loadFloatingDetails(subsidiaryId, q) {
        var params = new URLSearchParams();
        if (subsidiaryId) params.append('subsidiaryId', subsidiaryId);
        if (q) params.append('q', q);
        return fetch('/api/vouchers/floating-details?' + params.toString()).then(function (r) { return r.json(); }).then(function (data) {
            floatingDetailsCache[subsidiaryId || 'all'] = data;
            return data;
        });
    }

    function loadJournalTypes() {
        return fetch('/api/vouchers/journal-types').then(function (r) { return r.json(); }).then(function (data) {
            journalTypesCache = data;
            return data;
        });
    }

    function loadFiscalYears() {
        return fetch('/api/vouchers/fiscal-years').then(function (r) { return r.json(); }).then(function (data) {
            fiscalYearsCache = data;
            return data;
        });
    }

    // ---- Autocomplete ----
    function setupAutocomplete(inputEl, sourceFn, displayFn, selectFn) {
        var listEl = null;
        var fetching = false;

        function removeList() {
            if (listEl) { listEl.remove(); listEl = null; }
        }

        function fetchAndShow(query) {
            if (fetching) return;
            fetching = true;
            var rect = inputEl.getBoundingClientRect();
            sourceFn(query).then(function (items) {
                fetching = false;
                removeList();
                if (!items || items.length === 0) return;
                listEl = document.createElement('div');
                listEl.className = 'acc-autocomplete-list show';
                listEl.style.position = 'fixed';
                listEl.style.top = (rect.bottom + 4) + 'px';
                listEl.style.left = Math.max(8, rect.left) + 'px';
                listEl.style.width = Math.max(320, rect.width) + 'px';
                listEl.setAttribute('tabindex', '-1');

                items.forEach(function (item) {
                    var div = document.createElement('div');
                    div.className = 'acc-autocomplete-item';
                    div.setAttribute('tabindex', '0');
                    div.innerHTML = displayFn(item);
                    div.addEventListener('mousedown', function (e) {
                        e.preventDefault();
                        selectFn(item);
                        removeList();
                    });
                    div.addEventListener('keydown', function (e) {
                        if (e.key === 'Enter') { e.preventDefault(); selectFn(item); removeList(); inputEl.focus(); }
                        if (e.key === 'ArrowDown') {
                            e.preventDefault();
                            var next = div.nextElementSibling;
                            if (next) { next.focus(); }
                        }
                        if (e.key === 'ArrowUp') {
                            e.preventDefault();
                            var prev = div.previousElementSibling;
                            if (prev) { prev.focus(); }
                            else { inputEl.focus(); }
                        }
                        if (e.key === 'Escape') { removeList(); inputEl.focus(); }
                    });
                    listEl.appendChild(div);
                });

                document.body.appendChild(listEl);
            }).catch(function () {
                fetching = false;
            });
        }

        // باز شدن خودکار لیست با فوکوس و کلیک
        inputEl.addEventListener('focus', function () {
            var val = inputEl.value.trim();
            fetchAndShow(val);
        });

        inputEl.addEventListener('click', function () {
            if (listEl) return;
            var val = inputEl.value.trim();
            fetchAndShow(val);
        });

        inputEl.addEventListener('input', function () {
            var val = inputEl.value.trim();
            fetchAndShow(val);
        });

        inputEl.addEventListener('blur', function (e) {
            setTimeout(function () {
                if (document.activeElement && listEl && (listEl === document.activeElement || listEl.contains(document.activeElement))) return;
                removeList();
            }, 200);
        });

        inputEl.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') { removeList(); return; }
            if (e.key === 'ArrowDown' && listEl) {
                e.preventDefault();
                var first = listEl.querySelector('.acc-autocomplete-item');
                if (first) first.focus();
                return;
            }
            if (e.key === 'Enter' && listEl) {
                e.preventDefault();
                var first = listEl.querySelector('.acc-autocomplete-item');
                if (first) first.click();
            }
        });
    }

    // ---- Row Management ----
    function createRow(index, data) {
        var tr = document.createElement('tr');
        tr.setAttribute('data-row-index', index);

        tr.innerHTML =
            '<td class="row-index-cell">' + index + '</td>' +
            '<td class="col-center" style="position:relative;">' +
            '  <input type="text" class="txt-acc-code col-center font-num" placeholder="جستجوی کد معین..." style="width:100%;" />' +
            '</td>' +
            '<td class="col-center" style="position:relative;">' +
            '  <input type="text" class="txt-level4 col-center font-num" placeholder="سطح ۴..." style="width:100%;" />' +
            '</td>' +
            '<td class="col-center"><input type="text" class="txt-level5 col-center font-num" placeholder="سطح ۵" style="width:100%;" /></td>' +
            '<td class="col-center"><input type="text" class="txt-level6 col-center font-num" placeholder="سطح ۶" style="width:100%;" /></td>' +
            '<td><input type="text" class="txt-desc" placeholder="شرح آرتیکل..." style="width:100%;" /></td>' +
            '<td class="col-left"><input type="text" class="txt-debit col-left font-num" placeholder="0" style="width:100%;direction:ltr;text-align:left;" /></td>' +
            '<td class="col-left"><input type="text" class="txt-credit col-left font-num" placeholder="0" style="width:100%;direction:ltr;text-align:left;" /></td>';

        // data-id برای ذخیره اطلاعات حساب
        tr.setAttribute('data-subsidiary-id', '');
        tr.setAttribute('data-floating-id', '');
        tr.setAttribute('data-acc-code', '');
        tr.setAttribute('data-acc-name', '');

        if (data) {
            var inputs = tr.querySelectorAll('input');
            if (data.code) { inputs[0].value = data.code; tr.setAttribute('data-acc-code', data.code); }
            if (data.name) tr.setAttribute('data-acc-name', data.name);
            if (data.subsidiaryId) tr.setAttribute('data-subsidiary-id', data.subsidiaryId);
            if (data.level4) inputs[1].value = data.level4;
            if (data.level5) inputs[2].value = data.level5;
            if (data.level6) inputs[3].value = data.level6;
            if (data.desc) inputs[4].value = data.desc;
            if (data.debit) inputs[5].value = data.debit;
            if (data.credit) inputs[6].value = data.credit;
        }

        // کلیک برای انتخاب ردیف
        tr.addEventListener('click', function () {
            document.querySelectorAll('#tblVoucherItemsBody tr').forEach(function (r) { r.classList.remove('selected'); });
            tr.classList.add('selected');
            updateAccountHierarchy();
        });

        // ---- Autocomplete برای کد معین ----
        var accInput = tr.querySelector('.txt-acc-code');
        var level4Input = tr.querySelector('.txt-level4');

        setupAutocomplete(accInput, function (q) {
            return loadSubsidiaries(q);
        }, function (item) {
            return '<span class="acc-autocomplete-code">' + item.code + '</span>' +
                '<span class="acc-autocomplete-name">' + item.name + '</span>' +
                '<span class="acc-autocomplete-path">' + item.groupName + ' › ' + item.generalName + '</span>';
        }, function (item) {
            accInput.value = item.code + ' - ' + item.name;
            tr.setAttribute('data-subsidiary-id', item.id);
            tr.setAttribute('data-acc-code', item.code);
            tr.setAttribute('data-acc-name', item.name);
            // بارگذاری سطح ۴ مربوطه
            loadFloatingDetails(item.id).then(function (floats) {
                if (floats && floats.length > 0) {
                    var firstFloat = floats[0];
                    level4Input.value = firstFloat.code + ' - ' + firstFloat.name;
                    tr.setAttribute('data-floating-id', firstFloat.id);
                }
            });
            updateAccountHierarchy();
        });

        // ---- Autocomplete برای سطح ۴ (تفصیلی شناور) ----
        setupAutocomplete(level4Input, function (q) {
            var subId = tr.getAttribute('data-subsidiary-id');
            return loadFloatingDetails(subId || null, q);
        }, function (item) {
            return '<span class="acc-autocomplete-code">' + item.code + '</span>' +
                '<span class="acc-autocomplete-name">' + item.name + '</span>' +
                '<span class="acc-autocomplete-path">' + item.type + '</span>';
        }, function (item) {
            level4Input.value = item.code + ' - ' + item.name;
            tr.setAttribute('data-floating-id', item.id);
            updateAccountHierarchy();
        });

        // مدیریت مبالغ
        var debitInp = tr.querySelector('.txt-debit');
        var creditInp = tr.querySelector('.txt-credit');

        [debitInp, creditInp].forEach(function (inp) {
            inp.addEventListener('input', function (e) {
                var pos = e.target.selectionStart;
                var oldLen = e.target.value.length;
                e.target.value = formatCurrency(e.target.value);
                var newLen = e.target.value.length;
                var adjust = newLen - oldLen;
                try { e.target.setSelectionRange(pos + adjust, pos + adjust); } catch (ex) {}
                calculateTotals();
            });

            inp.addEventListener('keydown', function (e) {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    // اگر مبلغ وارد شده، برو سطر بعد
                    var val = parseNumeric(inp.value);
                    if (val > 0) {
                        document.getElementById('btnAddRow').click();
                        var newRows = document.querySelectorAll('#tblVoucherItemsBody tr');
                        var lastRow = newRows[newRows.length - 1];
                        if (lastRow) {
                            var lastDebit = lastRow.querySelector('.txt-debit');
                            var lastCredit = lastRow.querySelector('.txt-credit');
                            if (lastDebit) lastDebit.focus();
                        }
                    }
                }
            });
        });

        if (index === 1) tr.classList.add('selected');
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
            if (txtRemDebit) { txtRemDebit.value = Math.abs(diff).toLocaleString('en-US'); txtRemDebit.style.color = '#16a34a'; }
            if (txtRemCredit) { txtRemCredit.value = ''; }
        } else if (diff < 0) {
            if (txtRemDebit) { txtRemDebit.value = ''; }
            if (txtRemCredit) { txtRemCredit.value = Math.abs(diff).toLocaleString('en-US'); txtRemCredit.style.color = '#dc2626'; }
        } else {
            if (txtRemDebit) { txtRemDebit.value = '✓ تراز'; txtRemDebit.style.color = '#16a34a'; }
            if (txtRemCredit) { txtRemCredit.value = '✓ تراز'; txtRemCredit.style.color = '#16a34a'; }
        }

        return { debit: totalDebit, credit: totalCredit, diff: diff, isBalanced: diff === 0 };
    }

    function updateAccountHierarchy() {
        var selected = document.querySelector('#tblVoucherItemsBody tr.selected');
        if (!selected) return;
        var accCode = selected.getAttribute('data-acc-code') || '';
        var accName = selected.getAttribute('data-acc-name') || '';
        var lblAccSub = document.getElementById('lblAccSubsidiary');
        var lblAccGen = document.getElementById('lblAccGeneral');
        var lblAccGrp = document.getElementById('lblAccGroup');
        var lblDetail4 = document.getElementById('lblDetail4');

        if (lblAccSub) lblAccSub.value = accCode ? accCode + ' - ' + accName : '-';

        // پیدا کردن اطلاعات گروه و کل از کش
        var sub = subsidiariesCache.find(function (s) {
            return s.code === accCode || s.id == selected.getAttribute('data-subsidiary-id');
        });
        if (sub) {
            if (lblAccGrp) lblAccGrp.value = sub.groupCode + ' - ' + sub.groupName;
            if (lblAccGen) lblAccGen.value = sub.generalCode + ' - ' + sub.generalName;
        }

        var level4Input = selected.querySelector('.txt-level4');
        if (lblDetail4) lblDetail4.value = level4Input ? level4Input.value : '-';
    }

    // ---- Save ----
    function saveVoucher() {
        var totals = calculateTotals();
        if (totals.debit === 0 && totals.credit === 0) {
            showToast('سند خالی است! لطفاً مبالغ را وارد کنید.', 'error');
            return;
        }
        if (!totals.isBalanced) {
            showToast('⚠ سند تراز نیست! اختلاف: ' + Math.abs(totals.diff).toLocaleString('fa-IR') + ' ریال', 'error');
            return;
        }

        // Validate at least 2 lines
        var rows = document.querySelectorAll('#tblVoucherItemsBody tr');
        var validLines = [];
        var hasIssue = false;

        rows.forEach(function (tr) {
            var debit = parseNumeric(tr.querySelector('.txt-debit').value);
            var credit = parseNumeric(tr.querySelector('.txt-credit').value);
            var subId = tr.getAttribute('data-subsidiary-id');
            if (!subId && (debit > 0 || credit > 0)) {
                hasIssue = true;
                showToast('لطفاً برای همه سطرها کد معین را انتخاب کنید.', 'error');
                return;
            }
            if (subId && (debit > 0 || credit > 0)) {
                var floatingId = tr.getAttribute('data-floating-id');
                validLines.push({
                    subsidiaryId: parseInt(subId),
                    floatingDetailId: floatingId || null,
                    debitAmount: debit,
                    creditAmount: credit,
                    narration: tr.querySelector('.txt-desc') ? tr.querySelector('.txt-desc').value : ''
                });
            }
        });

        if (hasIssue) return;

        if (validLines.length < 2) {
            showToast('حداقل دو خط (بدهکار و بستانکار) برای سند لازم است.', 'error');
            return;
        }

        var request = {
            voucherDate: document.getElementById('txtVoucherDate').value,
            description: document.getElementById('txtMainDescription').value,
            referenceNo: document.getElementById('txtRefNo').value,
            journalTypeId: parseInt(document.getElementById('drpVoucherType').value) || 1,
            lines: validLines
        };

        showLoading(true);

        var token = getAntiForgeryToken();
        var headers = { 'Content-Type': 'application/json' };
        if (token) headers['RequestVerificationToken'] = token;

        fetch('/api/vouchers/save', {
            method: 'POST',
            headers: headers,
            body: JSON.stringify(request)
        })
            .then(function (res) {
                if (!res.ok) {
                    return res.json().then(function (err) { throw new Error(err.message || 'خطا در ذخیره سند'); });
                }
                return res.json();
            })
            .then(function (data) {
                showLoading(false);
                showToast('✓ سند با موفقیت ذخیره شد. شماره: ' + (data.id || ''), 'success');
            })
            .catch(function (error) {
                showLoading(false);
                showToast('✗ ' + error.message, 'error');
            });
    }

    // ---- Init ----
    function init() {
        renderInitialRows(1);
        loadInitialData();
        bindEvents();
        calculateTotals();
    }

    function loadInitialData() {
        loadJournalTypes().then(function (types) {
            var drp = document.getElementById('drpVoucherType');
            if (drp && types) {
                drp.innerHTML = types.map(function (t) {
                    return '<option value="' + t.id + '">' + t.name + '</option>';
                }).join('');
            }
        });
        loadFiscalYears().then(function (years) {
            if (years && years.length > 0) {
                // set default date from fiscal year
                var fy = years[0];
                if (fy.startDate) {
                    var d = new Date(fy.startDate);
                    var year = d.getFullYear();
                    var month = String(d.getMonth() + 1).padStart(2, '0');
                    var day = String(d.getDate()).padStart(2, '0');
                    // Use current date in Persian roughly
                }
            }
        });
        loadSubsidiaries().then(function () {});
    }

    function renderInitialRows(count) {
        var tbody = document.getElementById('tblVoucherItemsBody');
        if (!tbody) return;
        tbody.innerHTML = '';
        for (var i = 1; i <= count; i++) {
            tbody.appendChild(createRow(i));
        }
    }

    function bindEvents() {
        // Save
        document.getElementById('btnSaveVoucher').addEventListener('click', saveVoucher);

        // Save & New
        document.getElementById('btnSaveAndNew').addEventListener('click', function () {
            var totals = calculateTotals();
            if (totals.diff !== 0) {
                showToast('ابتدا سند جاری را تراز کنید.', 'error');
                return;
            }
            saveVoucher();
            renderInitialRows(1);
            calculateTotals();
        });

        // Undo
        document.getElementById('btnUndo').addEventListener('click', function () {
            if (confirm('آیا از بازنشانی کلیه اطلاعات سند مطمئن هستید؟')) {
                renderInitialRows(1);
                calculateTotals();
                document.getElementById('txtMainDescription').value = '';
                document.getElementById('txtRefNo').value = '';
                document.getElementById('txtDailyNo').value = '';
                document.getElementById('txtSubNo').value = '';
                showToast('تغییرات لغو شد.', 'info');
            }
        });

        // Print
        document.getElementById('btnPrint').addEventListener('click', function () { window.print(); });

        // Calculator
        document.getElementById('btnCalculator').addEventListener('click', openCalculatorModal);

        // Calendar
        document.getElementById('btnCalendarPicker').addEventListener('click', openDatePickerModal);

        // Bookmark
        document.getElementById('btnBookmark').addEventListener('click', function () {
            this.classList.toggle('active-bookmark');
            showToast(this.classList.contains('active-bookmark') ? 'سند نشانه‌گذاری شد.' : 'نشانه‌گذاری حذف شد.', 'info');
        });

        // Help
        document.getElementById('btnHelp').addEventListener('click', function () {
            showToast('• Enter در مبالغ: سطر جدید • کد معین و سطح ۴ با جستجوی هوشمند', 'info');
        });

        // ---- Row Buttons ----
        document.getElementById('btnAddRow').addEventListener('click', function () {
            var tbody = document.getElementById('tblVoucherItemsBody');
            var currentCount = tbody.querySelectorAll('tr').length;
            var newRow = createRow(currentCount + 1);
            tbody.appendChild(newRow);
            newRow.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
            newRow.querySelector('.txt-acc-code').focus();
        });

        document.getElementById('btnDeleteRow').addEventListener('click', function () {
            var selected = document.querySelector('#tblVoucherItemsBody tr.selected');
            var tbody = document.getElementById('tblVoucherItemsBody');
            if (tbody.querySelectorAll('tr').length <= 1) {
                showToast('سند باید حداقل یک سطر داشته باشد.', 'error');
                return;
            }
            if (selected) {
                var prev = selected.previousElementSibling || selected.nextElementSibling;
                selected.remove();
                reIndexRows();
                if (prev) prev.classList.add('selected');
                calculateTotals();
                updateAccountHierarchy();
            }
        });

        document.getElementById('btnCopyRow').addEventListener('click', function () {
            var selected = document.querySelector('#tblVoucherItemsBody tr.selected');
            if (!selected) return;
            copiedRowData = {
                code: selected.getAttribute('data-acc-code'),
                name: selected.getAttribute('data-acc-name'),
                subsidiaryId: selected.getAttribute('data-subsidiary-id'),
                floatingId: selected.getAttribute('data-floating-id'),
                level4: selected.querySelector('.txt-level4').value,
                level5: selected.querySelector('.txt-level5').value,
                level6: selected.querySelector('.txt-level6').value,
                desc: selected.querySelector('.txt-desc').value
            };
            showToast('اطلاعات سطر کپی شد.', 'info');
        });

        document.getElementById('btnPasteRow').addEventListener('click', function () {
            if (!copiedRowData) { showToast('اول یک سطر را کپی کنید.', 'error'); return; }
            var selected = document.querySelector('#tblVoucherItemsBody tr.selected');
            if (!selected) return;
            selected.setAttribute('data-acc-code', copiedRowData.code || '');
            selected.setAttribute('data-acc-name', copiedRowData.name || '');
            selected.setAttribute('data-subsidiary-id', copiedRowData.subsidiaryId || '');
            selected.setAttribute('data-floating-id', copiedRowData.floatingId || '');
            var inputs = selected.querySelectorAll('input');
            if (inputs[0]) inputs[0].value = copiedRowData.code || '';
            if (inputs[1]) inputs[1].value = copiedRowData.level4 || '';
            if (inputs[2]) inputs[2].value = copiedRowData.level5 || '';
            if (inputs[3]) inputs[3].value = copiedRowData.level6 || '';
            if (inputs[4]) inputs[4].value = copiedRowData.desc || '';
            showToast('اطلاعات سطر چسبانده شد.', 'success');
            updateAccountHierarchy();
        });

        document.getElementById('btnCutRow').addEventListener('click', function () {
            var selected = document.querySelector('#tblVoucherItemsBody tr.selected');
            var tbody = document.getElementById('tblVoucherItemsBody');
            if (tbody.querySelectorAll('tr').length <= 1) {
                showToast('امکان برش تنها سطر موجود وجود ندارد.', 'error');
                return;
            }
            if (selected) {
                document.getElementById('btnCopyRow').click();
                selected.remove();
                reIndexRows();
                calculateTotals();
            }
        });

        document.getElementById('btnMoveUp').addEventListener('click', function () {
            var selected = document.querySelector('#tblVoucherItemsBody tr.selected');
            if (selected && selected.previousElementSibling) {
                selected.parentNode.insertBefore(selected, selected.previousElementSibling);
                reIndexRows();
            }
        });

        document.getElementById('btnMoveDown').addEventListener('click', function () {
            var selected = document.querySelector('#tblVoucherItemsBody tr.selected');
            if (selected && selected.nextElementSibling) {
                selected.parentNode.insertBefore(selected.nextElementSibling, selected);
                reIndexRows();
            }
        });

        document.getElementById('btnSearchRow').addEventListener('click', function () {
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

        document.getElementById('btnTaxStatus').addEventListener('click', function () {
            var selected = document.querySelector('#tblVoucherItemsBody tr.selected');
            if (!selected) { showToast('ابتدا یک سطر را انتخاب کنید.', 'error'); return; }
            var descInput = selected.querySelector('.txt-desc');
            if (descInput) {
                descInput.value = (descInput.value + ' [مشمول مالیات]').trim();
                showToast('وضعیت مالیاتی اضافه شد.', 'info');
            }
        });
    }

    // ---- Calendar Modal ----
    function openDatePickerModal() {
        var curDateInput = document.getElementById('txtVoucherDate');
        var curVal = curDateInput ? curDateInput.value.split('/') : ['1404', '01', '01'];

        var overlay = document.createElement('div');
        overlay.className = 'rk-modal-overlay';
        overlay.innerHTML =
            '<div class="rk-modal" style="width:280px;">' +
            '<div class="rk-modal-header"><span>انتخاب تاریخ سند</span><button type="button" class="rk-btn" id="btnCloseCal">✕</button></div>' +
            '<div style="display:flex;gap:6px;margin-bottom:12px;">' +
            '<input type="number" id="calYear" class="rk-input col-center font-num" value="' + (curVal[0] || 1404) + '" style="width:80px;" />' +
            '<select id="calMonth" class="rk-input col-center font-num" style="flex:1;">' +
            '<option value="01">فروردین</option><option value="02">اردیبهشت</option><option value="03">خرداد</option>' +
            '<option value="04">تیر</option><option value="05">مرداد</option><option value="06">شهریور</option>' +
            '<option value="07">مهر</option><option value="08">آبان</option><option value="09">آذر</option>' +
            '<option value="10">دی</option><option value="11">بهمن</option><option value="12">اسفند</option>' +
            '</select>' +
            '<input type="number" id="calDay" min="1" max="31" class="rk-input col-center font-num" value="' + (curVal[2] || 1) + '" style="width:70px;" />' +
            '</div>' +
            '<div style="display:flex;justify-content:flex-end;gap:6px;">' +
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

    // ---- Calculator Modal ----
    function openCalculatorModal() {
        var overlay = document.createElement('div');
        overlay.className = 'rk-modal-overlay';
        overlay.innerHTML =
            '<div class="rk-modal" style="width:240px;">' +
            '<div class="rk-modal-header"><span>ماشین حساب</span><button class="rk-btn" id="btnCalcClose">✕</button></div>' +
            '<input type="text" id="calcScreen" class="rk-input col-left font-num" style="height:38px;font-size:18px;margin-bottom:10px;text-align:left;direction:ltr;" readonly value="0" />' +
            '<div style="display:grid;grid-template-columns:repeat(4,1fr);gap:4px;" id="calcKeys">' +
            '<button class="rk-btn" style="height:36px;">7</button><button class="rk-btn" style="height:36px;">8</button><button class="rk-btn" style="height:36px;">9</button><button class="rk-btn" style="height:36px;background:#e2e8f0;">/</button>' +
            '<button class="rk-btn" style="height:36px;">4</button><button class="rk-btn" style="height:36px;">5</button><button class="rk-btn" style="height:36px;">6</button><button class="rk-btn" style="height:36px;background:#e2e8f0;">*</button>' +
            '<button class="rk-btn" style="height:36px;">1</button><button class="rk-btn" style="height:36px;">2</button><button class="rk-btn" style="height:36px;">3</button><button class="rk-btn" style="height:36px;background:#e2e8f0;">-</button>' +
            '<button class="rk-btn" style="height:36px;background:#fee2e2;color:#991b1b;">C</button><button class="rk-btn" style="height:36px;">0</button><button class="rk-btn" style="height:36px;background:#16a34a;color:#fff;">=</button><button class="rk-btn" style="height:36px;background:#e2e8f0;">+</button>' +
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
                var cur = screen.value;
                screen.value = (cur === '0' || cur === 'خطا') ? key : cur + key;
            }
        });
    }

    return { init: init };
})();

document.addEventListener('DOMContentLoaded', function () {
    if (window.VoucherForm) {
        window.VoucherForm.init();
    }
});

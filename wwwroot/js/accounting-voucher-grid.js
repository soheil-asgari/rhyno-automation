var VoucherGrid = (function () {
    var state = {
        page: 1,
        pageSize: 25,
        totalCount: 0,
        filters: {}
    };

    function init() {
        bindEvents();
        loadData();
    }

    function bindEvents() {
        var filterInputs = document.querySelectorAll('.rk-f-cell input');
        var timer = null;

        filterInputs.forEach(function (input) {
            input.addEventListener('input', function (e) {
                clearTimeout(timer);
                timer = setTimeout(function () {
                    var key = e.target.getAttribute('data-filter');
                    state.filters[key] = e.target.value.trim();
                    state.page = 1;
                    loadData();
                }, 350);
            });

            input.addEventListener('keydown', function (e) {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    clearTimeout(timer);
                    var key = e.target.getAttribute('data-filter');
                    state.filters[key] = e.target.value.trim();
                    state.page = 1;
                    loadData();
                }
            });
        });

        document.getElementById('btnClearFilters').addEventListener('click', function () {
            filterInputs.forEach(function (inp) { inp.value = ''; });
            state.filters = {};
            state.page = 1;
            loadData();
        });

        document.getElementById('btnNextPage').addEventListener('click', function () {
            var maxPage = Math.ceil(state.totalCount / state.pageSize) || 1;
            if (state.page < maxPage) { state.page++; loadData(); }
        });

        document.getElementById('btnPrevPage').addEventListener('click', function () {
            if (state.page > 1) { state.page--; loadData(); }
        });

        document.getElementById('btnFirstPage').addEventListener('click', function () {
            if (state.page !== 1) { state.page = 1; loadData(); }
        });

        document.getElementById('btnLastPage').addEventListener('click', function () {
            var maxPage = Math.ceil(state.totalCount / state.pageSize) || 1;
            if (state.page !== maxPage) { state.page = maxPage; loadData(); }
        });
    }

    function loadData() {
        var tbody = document.getElementById('voucherTableBody');
        if (tbody) {
            tbody.innerHTML = '<tr><td colspan="11" class="rk-empty-cell"><span style="display:inline-block;animation:pulse 1.2s infinite;">در حال دریافت اسناد...</span></td></tr>';
        }

        var params = new URLSearchParams();
        params.append('page', state.page);
        params.append('pageSize', state.pageSize);

        Object.keys(state.filters).forEach(function (k) {
            if (state.filters[k]) params.append(k, state.filters[k]);
        });

        fetch('/api/vouchers/list?' + params.toString())
            .then(function (res) {
                if (!res.ok) {
                    return res.json().then(function (err) {
                        throw new Error(err.detail || err.message || 'خطا در دریافت اطلاعات');
                    });
                }
                return res.json();
            })
            .then(function (data) {
                state.totalCount = data.totalCount || 0;
                renderTable(data.items || []);
                updatePager();
            })
            .catch(function (error) {
                if (tbody) {
                    tbody.innerHTML = '<tr><td colspan="11" class="rk-error-cell">⚠ ' + error.message + '</td></tr>';
                }
                updatePager();
            });
    }

    function renderTable(items) {
        var tbody = document.getElementById('voucherTableBody');
        if (!tbody) return;

        if (!items || items.length === 0) {
            tbody.innerHTML = '<tr><td colspan="11" class="rk-empty-cell">هیچ سندی یافت نشد.</td></tr>';
            return;
        }

        tbody.innerHTML = items.map(function (v) {
            var statusClass = 'status-' + (v.status || 'draft').toLowerCase();
            var statusText = getStatusText(v.status);
            return '<tr data-id="' + v.id + '">' +
                '<td class="col-center col-num">' + (v.voucherNo || '') + '</td>' +
                '<td class="col-center font-num">' + (v.voucherDate || '') + '</td>' +
                '<td class="col-center"><span class="' + statusClass + '">' + statusText + '</span></td>' +
                '<td class="col-center">' + (v.voucherType || '') + '</td>' +
                '<td title="' + escHtml(v.description) + '">' + escHtml(v.description) + '</td>' +
                '<td class="col-center col-num">' + (v.referenceNo || '') + '</td>' +
                '<td class="col-center col-num">' + (v.dailyNo || '') + '</td>' +
                '<td class="col-left font-num" style="font-weight:600;">' + formatNum(v.totalAmount || 0) + '</td>' +
                '<td>' + (v.createdBy || '') + '</td>' +
                '<td>' + (v.modifiedBy || '') + '</td>' +
                '<td>' + (v.checkedBy || '') + '</td>' +
                '</tr>';
        }).join('');

        tbody.querySelectorAll('tr').forEach(function (row) {
            row.addEventListener('dblclick', function () {
                var id = row.getAttribute('data-id');
                if (id) {
                    var tabManager = window.RhynoTabs || (window.parent && window.parent.RhynoTabs);
                    if (tabManager && typeof tabManager.open === 'function') {
                        tabManager.open('/Finance/Vouchers/Edit/' + id, 'ویرایش سند', 'bi-pencil');
                    }
                }
            });
            row.addEventListener('click', function () {
                tbody.querySelectorAll('tr').forEach(function (r) { r.classList.remove('selected'); });
                row.classList.add('selected');
                document.getElementById('btnEditVoucher').classList.remove('rk-disabled');
                document.getElementById('btnEditVoucher').disabled = false;
                document.getElementById('btnDeleteVoucher').classList.remove('rk-disabled');
                document.getElementById('btnDeleteVoucher').disabled = false;
            });
        });
    }

    function updatePager() {
        var countEl = document.getElementById('vTotalInfo');
        var pageEl = document.getElementById('vCurrentPage');
        if (countEl) countEl.textContent = state.totalCount.toLocaleString('fa-IR') + ' مورد';
        if (pageEl) pageEl.textContent = 'صفحه ' + state.page + ' از ' + (Math.ceil(state.totalCount / state.pageSize) || 1);
    }

    function getStatusText(s) {
        var map = { draft: 'موقت', reviewed: 'بررسی شده', approved: 'تأیید', permanent: 'قطعی', posted: 'ثبت شده', cancelled: 'لغو شده' };
        return map[(s || 'draft').toLowerCase()] || s;
    }

    function formatNum(n) { return Number(n).toLocaleString('fa-IR'); }

    function escHtml(s) {
        if (!s) return '';
        var d = document.createElement('div');
        d.textContent = s;
        return d.innerHTML;
    }

    return { init: init };
})();

var btnNew = document.getElementById('btnNewVoucher');
if (btnNew) {
    btnNew.addEventListener('click', function (e) {
        e.preventDefault();
        var tabManager = window.RhynoTabs || (window.parent && window.parent.RhynoTabs) || (window.top && window.top.RhynoTabs);
        if (tabManager && typeof tabManager.open === 'function') {
            tabManager.open('/Finance/Vouchers/Create', 'سند حسابداری جدید', 'bi-file-earmark-plus');
        } else {
            window.location.href = '/Finance/Vouchers/Create';
        }
    });
}

// اضافه کردن انیمیشن pulse به صورت CSS
var styleSheet = document.createElement('style');
styleSheet.textContent = '@keyframes pulse { 0%,100% { opacity: 1; } 50% { opacity: .4; } }';
document.head.appendChild(styleSheet);

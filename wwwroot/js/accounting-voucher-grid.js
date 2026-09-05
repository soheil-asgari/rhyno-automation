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
        });

        // دکمه حذف فیلترها
        var btnClear = document.getElementById('btnClearFilters');
        if (btnClear) {
            btnClear.addEventListener('click', function () {
                filterInputs.forEach(function (inp) { inp.value = ''; });
                state.filters = {};
                state.page = 1;
                loadData();
            });
        }

        // پیجینگ
        var btnNext = document.getElementById('btnNextPage');
        if (btnNext) {
            btnNext.addEventListener('click', function () {
                var maxPage = Math.ceil(state.totalCount / state.pageSize) || 1;
                if (state.page < maxPage) {
                    state.page++;
                    loadData();
                }
            });
        }

        var btnPrev = document.getElementById('btnPrevPage');
        if (btnPrev) {
            btnPrev.addEventListener('click', function () {
                if (state.page > 1) {
                    state.page--;
                    loadData();
                }
            });
        }

        var btnFirst = document.getElementById('btnFirstPage');
        if (btnFirst) {
            btnFirst.addEventListener('click', function () {
                if (state.page !== 1) {
                    state.page = 1;
                    loadData();
                }
            });
        }

        var btnLast = document.getElementById('btnLastPage');
        if (btnLast) {
            btnLast.addEventListener('click', function () {
                var maxPage = Math.ceil(state.totalCount / state.pageSize) || 1;
                if (state.page !== maxPage) {
                    state.page = maxPage;
                    loadData();
                }
            });
        }
    }

    function loadData() {
        var tbody = document.getElementById('voucherTableBody');
        if (tbody) {
            tbody.innerHTML = '<tr><td colspan="11" class="rk-empty-cell">در حال دریافت اسناد از سرور...</td></tr>';
        }

        var params = new URLSearchParams({
            page: state.page,
            pageSize: state.pageSize
        });

        Object.keys(state.filters).forEach(function (k) {
            if (state.filters[k]) params.append(k, state.filters[k]);
        });

        fetch('/api/vouchers/list?' + params.toString())
            .then(function (res) {
                if (!res.ok) {
                    return res.json().then(function (err) {
                        throw new Error(err.detail || err.message || "خطا در برقراری ارتباط با دیتابیس");
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
                    tbody.innerHTML = '<tr><td colspan="11" class="rk-error-cell">خطای دیتابیس: ' + error.message + '</td></tr>';
                }
                updatePager();
            });
    }

    function renderTable(items) {
        var tbody = document.getElementById('voucherTableBody');
        if (!tbody) return;

        if (!items || items.length === 0) {
            tbody.innerHTML = '<tr><td colspan="11" class="rk-empty-cell">هیچ سندی در این دوره مالی یافت نشد.</td></tr>';
            return;
        }

        tbody.innerHTML = items.map(function (v) {
            return '<tr data-id="' + v.id + '">' +
                '<td class="col-center col-num">' + (v.voucherNo || '') + '</td>' +
                '<td class="col-center col-num">' + (v.voucherDate || '') + '</td>' +
                '<td class="col-center">' + (v.status || '') + '</td>' +
                '<td class="col-center">' + (v.voucherType || '') + '</td>' +
                '<td title="' + (v.description || '') + '">' + (v.description || '') + '</td>' +
                '<td class="col-center col-num">' + (v.referenceNo || '') + '</td>' +
                '<td class="col-center col-num">' + (v.dailyNo || '') + '</td>' +
                '<td class="col-left col-num">' + Number(v.totalAmount || 0).toLocaleString('fa-IR') + '</td>' +
                '<td>' + (v.createdBy || '') + '</td>' +
                '<td>' + (v.modifiedBy || '') + '</td>' +
                '<td>' + (v.checkedBy || '') + '</td>' +
                '</tr>';
        }).join('');

        tbody.querySelectorAll('tr').forEach(function (row) {
            row.addEventListener('click', function () {
                tbody.querySelectorAll('tr').forEach(function (r) { r.classList.remove('selected'); });
                row.classList.add('selected');
            });
        });
    }

    function updatePager() {
        var countEl = document.getElementById('vTotalInfo');
        var pageEl = document.getElementById('vCurrentPage');
        if (countEl) countEl.textContent = state.totalCount + ' مورد یافت شد';
        if (pageEl) pageEl.textContent = 'صفحه ' + state.page;
    }

    return { init: init };
})();
// فعال‌سازی دکمه سند جدید
var btnNew = document.getElementById('btnNewVoucher');
if (btnNew) {
    btnNew.addEventListener('click', function (e) {
        e.preventDefault();
        // پیدا کردن شیء مدیریت تب‌ها در پنجره جاری یا والد (پوسته اصلی راینو)
        var tabManager = window.RhynoTabs || (window.parent && window.parent.RhynoTabs) || (window.top && window.top.RhynoTabs);
        if (tabManager && typeof tabManager.open === 'function') {
            tabManager.open('/Finance/Vouchers/Create', 'سند حسابداری جدید', 'bi-file-earmark-plus');
        } else {
            window.location.href = '/Finance/Vouchers/Create';
        }
    });
}
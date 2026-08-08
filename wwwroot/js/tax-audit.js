/* Tax Audit — live search */
(function () {
    'use strict';
    var searchInput = document.getElementById('auditSearch');
    var searchBtn = document.getElementById('auditSearchBtn');
    var tbody = document.getElementById('auditTableBody');
    if (!searchInput || !searchBtn || !tbody) return;

    var token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

    function escapeHtml(s) {
        if (!s) return '—';
        return String(s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    function doSearch() {
        var q = searchInput.value.trim();
        fetch('/TaxAudit/Search?search=' + encodeURIComponent(q), {
            headers: { 'RequestVerificationToken': token }
        })
            .then(function (r) { return r.json(); })
            .then(function (entries) {
                tbody.innerHTML = '';
                if (!entries || entries.length === 0) {
                    var tr = document.createElement('tr');
                    tr.innerHTML = '<td colspan="5" style="text-align:center;color:#888;">نتیجه‌ای یافت نشد.</td>';
                    tbody.appendChild(tr);
                    return;
                }
                entries.forEach(function (e) {
                    var tr = document.createElement('tr');
                    tr.innerHTML =
                        '<td>' + new Date(e.dateTime).toLocaleString('fa-IR') + '</td>' +
                        '<td>' + escapeHtml(e.userId) + '</td>' +
                        '<td><span class="audit-action">' + escapeHtml(e.action) + '</span></td>' +
                        '<td>' + escapeHtml(e.entityName) + '</td>' +
                        '<td>' + escapeHtml(e.details) + '</td>';
                    tbody.appendChild(tr);
                });
            })
            .catch(function () { /* best effort */ });
    }

    searchBtn.addEventListener('click', doSearch);
    searchInput.addEventListener('keydown', function (e) { if (e.key === 'Enter') doSearch(); });
})();
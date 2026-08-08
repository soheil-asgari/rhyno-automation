/* Intake Automation — bulk ZIP upload recent list */
(function () {
    'use strict';
    var tbody = document.querySelector('#iaTable tbody');
    if (!tbody) return;

    var token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

    function row(item) {
        var tr = document.createElement('tr');
        var status = item.status || 'Pending';
        var statusFa = { Pending: 'در انتظار', Processing: 'در حال پردازش', Completed: 'تکمیل', Failed: 'ناموفق' }[status] || status;
        tr.innerHTML =
            '<td>' + escapeHtml(item.fileName) + '</td>' +
            '<td>' + statusFa + '</td>' +
            '<td>' + (item.ocrConfidence != null ? item.ocrConfidence.toLocaleString('fa-IR') + '٪' : '—') + '</td>' +
            '<td>' + escapeHtml((item.errors && item.errors[0]) || '—') + '</td>';
        return tr;
    }

    function escapeHtml(s) {
        if (!s) return '—';
        return String(s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    fetch('/IntakeAutomation/List', {
        headers: { 'RequestVerificationToken': token }
    })
        .then(function (r) { return r.json(); })
        .then(function (data) {
            tbody.innerHTML = '';
            if (!data.success || !data.items || data.items.length === 0) {
                var tr = document.createElement('tr');
                tr.innerHTML = '<td colspan="4" style="text-align:center;color:#888;">فایلی در جلسه فعلی بارگذاری نشده است.</td>';
                tbody.appendChild(tr);
                return;
            }
            data.items.forEach(function (item) { tbody.appendChild(row(item)); });
        })
        .catch(function () { /* best effort */ });
})();
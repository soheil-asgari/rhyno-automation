document.getElementById('sourceInvoice').addEventListener('change', function () {
        var invoiceId = this.value;
        var tbody = document.getElementById('linesBody');
        tbody.innerHTML = '<tr><td colspan="5" class="px-2 py-6 text-center text-slate-400">در حال بارگذاری...</td></tr>';
        fetch('/Commercial/GetInvoiceLines?id=' + invoiceId)
            .then(function (r) { return r.json(); })
            .then(function (data) {
                if (!data.success) { tbody.innerHTML = '<tr><td colspan="5" class="px-2 py-6 text-center text-rose-500">' + data.message + '</td></tr>'; return; }
                if (data.items.length === 0) { tbody.innerHTML = '<tr><td colspan="5" class="px-2 py-6 text-center text-slate-400">فاکتور ردیفی ندارد.</td></tr>'; return; }
                tbody.innerHTML = '';
                data.items.forEach(function (it) {
                    var tr = document.createElement('tr');
                    tr.innerHTML =
                        '<td class="px-2 py-2"><input type="hidden" name="lines[][ItemName]" value="' + it.itemName + '" /><input type="hidden" name="lines[][ProductId]" value="' + (it.productId || '') + '" /><span class="font-semibold">' + it.itemName + '</span></td>' +
                        '<td class="px-2 py-2"><input name="lines[][Quantity]" type="number" step="0.001" min="0" value="' + it.quantity + '" class="h-7 w-20 rounded border border-slate-200/80 px-1 text-xs" /></td>' +
                        '<td class="px-2 py-2"><input name="lines[][UnitPrice]" type="number" step="0.01" min="0" value="' + it.unitPrice + '" class="h-7 w-24 rounded border border-slate-200/80 px-1 text-xs" /></td>' +
                        '<td class="px-2 py-2"><input name="lines[][LineVatAmount]" type="number" step="0.01" min="0" value="' + it.lineVatAmount + '" class="h-7 w-24 rounded border border-slate-200/80 px-1 text-xs" /></td>' +
                        '<td class="px-2 py-2"><input name="lines[][LineSubTotal]" type="number" step="0.01" min="0" value="' + it.lineSubTotal + '" class="h-7 w-24 rounded border border-slate-200/80 px-1 text-xs" /><input type="hidden" name="lines[][LineGrandTotal]" value="' + it.lineGrandTotal + '" /></td>';
                    tbody.appendChild(tr);
                });
            })
            .catch(function () { tbody.innerHTML = '<tr><td colspan="5" class="px-2 py-6 text-center text-rose-500">خطا در بارگذاری ردیف‌ها.</td></tr>'; });
    });

(() => {
        const body = document.getElementById("audit-table-body");
        const summary = document.getElementById("audit-summary");
        const detailPanel = document.getElementById("audit-detail-panel");
        const oldValues = document.getElementById("audit-old-values");
        const newValues = document.getElementById("audit-new-values");
        const detailMeta = document.getElementById("audit-detail-meta");
        const moduleSelect = document.getElementById("audit-module");
        const actionSelect = document.getElementById("audit-action");
        const tableSelect = document.getElementById("audit-table");
        const searchInput = document.getElementById("audit-search");
        const userSelect = document.getElementById("audit-user");
        const fromInput = document.getElementById("audit-from");
        const toInput = document.getElementById("audit-to");
        const sensitiveSelect = document.getElementById("audit-sensitive");
        const exportLink = document.getElementById("audit-export");
        const expandAllButton = document.getElementById("audit-expand-all");
        const collapseAllButton = document.getElementById("audit-collapse-all");
        const copyJsonButton = document.getElementById("audit-copy-json");
        const pageSize = 20;
        let page = 1;
        let totalPages = 1;
        let lastRows = [];
        let allRows = [];
        let selectedRowIndex = null;

        const escape = (value) => String(value ?? "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll("\"", "&quot;");

        const params = () => {
            const query = new URLSearchParams();
            if (moduleSelect.value) query.set("Module", moduleSelect.value);
            if (actionSelect.value) query.set("Action", actionSelect.value);
            if (tableSelect.value) query.set("TableName", tableSelect.value);
            if (userSelect.value) query.set("UserId", userSelect.value);
            if (fromInput.value) query.set("From", `${fromInput.value}T00:00:00+03:30`);
            if (toInput.value) query.set("To", `${toInput.value}T23:59:59+03:30`);
            if (sensitiveSelect.value) query.set("SensitiveOnly", sensitiveSelect.value);
            query.set("Page", page.toString());
            query.set("PageSize", pageSize.toString());
            return query;
        };

        const applyExportHref = () => {
            const query = new URLSearchParams();
            if (moduleSelect.value) query.set("Module", moduleSelect.value);
            if (actionSelect.value) query.set("Action", actionSelect.value);
            if (tableSelect.value) query.set("TableName", tableSelect.value);
            if (userSelect.value) query.set("UserId", userSelect.value);
            if (fromInput.value) query.set("From", `${fromInput.value}T00:00:00+03:30`);
            if (toInput.value) query.set("To", `${toInput.value}T23:59:59+03:30`);
            if (sensitiveSelect.value) query.set("SensitiveOnly", sensitiveSelect.value);
            exportLink.href = `/api/admin/audit-logs/export?${query.toString()}`;
        };

        const showDetails = (item) => {
            if (!item) return;
            detailPanel.style.display = "block";
            oldValues.textContent = formatJson(item.oldValues);
            newValues.textContent = formatJson(item.newValues);
            detailMeta.textContent = `${item.tableName} | ${item.module} | ${item.userDisplayName || item.userId || "-"} | ${new Date(item.dateTime).toLocaleString("fa-IR")}`;
        };

        const formatJson = (value) => {
            if (!value) return "ندارد";
            try {
                const parsed = JSON.parse(value);
                return JSON.stringify(parsed, null, 2);
            } catch {
                return value;
            }
        };

        const applyClientSearch = () => {
            const term = (searchInput.value || "").trim().toLowerCase();
            if (!term) {
                renderRows(allRows);
                return;
            }

            const filtered = allRows.filter(item => {
                const haystack = [
                    item.userDisplayName,
                    item.userId,
                    item.action,
                    item.tableName,
                    item.module,
                    item.affectedColumns,
                    item.oldValues,
                    item.newValues,
                    item.userIP
                ].filter(Boolean).join(" ").toLowerCase();
                return haystack.includes(term);
            });

            renderRows(filtered);
            summary.textContent = `${filtered.length} رکورد مطابق جستجو`;
        };

        const renderRows = (items) => {
            lastRows = items || [];
            if (!items.length) {
                body.innerHTML = `<tr><td colspan="7"><div class="empty-state"><i class="bi bi-inbox"></i><div>رکوردی پیدا نشد.</div></div></td></tr>`;
                return;
            }

            body.innerHTML = items.map((item, index) => {
                const actionBadgeClass = item.action === "Delete" ? "bg-red-100 text-red-700 dark:bg-red-500/15 dark:text-red-400" : item.action === "Update" ? "bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-400" : "bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-400";
                const sensitiveBadgeClass = item.isSensitive ? "bg-red-100 text-red-700 dark:bg-red-500/15 dark:text-red-400" : "bg-gray-100 text-gray-500 dark:bg-slate-700 dark:text-slate-400";

                return `
                <tr class="audit-row hover:bg-gray-50 dark:hover:bg-dark-elevated cursor-pointer transition" data-index="${index}">
                    <td class="px-4 py-3 whitespace-nowrap text-gray-700 dark:text-gray-300">${new Date(item.dateTime).toLocaleString("fa-IR")}</td>
                    <td class="px-4 py-3 whitespace-nowrap text-gray-900 dark:text-white font-medium">${escape(item.userDisplayName || item.userId || "-")}</td>
                    <td class="px-4 py-3 whitespace-nowrap"><span class="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-bold ${actionBadgeClass}">${escape(item.action)}</span></td>
                    <td class="px-4 py-3 whitespace-nowrap text-gray-600 dark:text-gray-400">${escape(item.module)}</td>
                    <td class="px-4 py-3 whitespace-nowrap text-gray-600 dark:text-gray-400">${escape(item.tableName)}</td>
                    <td class="px-4 py-3 whitespace-nowrap"><span class="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-bold ${sensitiveBadgeClass}">${item.isSensitive ? 'بله' : 'خیر'}</span></td>
                    <td class="px-4 py-3">
                        <button type="button" class="inline-flex items-center gap-1 px-3 py-1.5 text-xs font-bold rounded-lg text-teal-600 dark:text-teal-400 border border-teal-200 dark:border-teal-500/30 hover:bg-teal-50 dark:hover:bg-teal-500/10 transition audit-toggle mb-2">نمایش جزئیات</button>
                        <div class="text-xs text-gray-400 dark:text-gray-500 mt-1">${escape(item.affectedColumns || "-")}</div>
                        <div class="text-xs text-gray-500 dark:text-gray-400">${item.newValues ? "دارای داده جدید" : "بدون داده جدید"}</div>
                        <div class="audit-inline-detail mt-2" style="display:none;">
                            <div class="text-xs text-gray-400 dark:text-gray-500 mb-1">مقادیر قبلی</div>
                            <pre class="mb-2 audit-json">${escape(formatJson(item.oldValues))}</pre>
                            <div class="text-xs text-gray-400 dark:text-gray-500 mb-1">مقادیر جدید</div>
                            <pre class="mb-0 audit-json">${escape(formatJson(item.newValues))}</pre>
                        </div>
                    </td>
                </tr>
                `;
            }).join("");

            body.querySelectorAll(".audit-row").forEach(row => {
                const detail = row.querySelector(".audit-inline-detail");
                const toggle = row.querySelector(".audit-toggle");
                toggle?.addEventListener("click", (e) => {
                    e.stopPropagation();
                    const open = detail.style.display !== "none";
                    detail.style.display = open ? "none" : "block";
                });
                row.addEventListener("click", () => {
                    selectedRowIndex = Number(row.dataset.index);
                    showDetails(lastRows[selectedRowIndex]);
                });
            });
        };

        const loadOptions = async () => {
            const response = await fetch("/api/admin/audit-logs/filter-options", { credentials: "same-origin" });
            if (!response.ok) return;
            const data = await response.json();
            const fill = (select, values, valueKey = "value", labelKey = "label") => {
                values.forEach(v => {
                    const option = document.createElement("option");
                    option.value = v[valueKey] ?? v;
                    option.textContent = v[labelKey] ?? v;
                    select.appendChild(option);
                });
            };
            fill(moduleSelect, data.modules || []);
            fill(actionSelect, data.actions || []);
            fill(tableSelect, data.tableNames || []);
            fill(userSelect, data.users || []);
        };

        const loadData = async () => {
            body.innerHTML = `<tr><td colspan="7"><div class="empty-state"><i class="bi bi-hourglass-split"></i><div>در حال بارگذاری ...</div></div></td></tr>`;
            const response = await fetch(`/api/admin/audit-logs?${params().toString()}`, { credentials: "same-origin" });
            if (!response.ok) {
                body.innerHTML = `<tr><td colspan="7"><div class="empty-state"><i class="bi bi-exclamation-triangle"></i><div>خطا در دریافت داده‌ها</div></div></td></tr>`;
                return;
            }

            const data = await response.json();
            totalPages = data.totalPages || 1;
            allRows = data.items || [];
            renderRows(allRows);
            summary.textContent = `صفحه ${data.page} از ${data.totalPages} | ${data.totalCount} رکورد`;
            document.getElementById("audit-prev").disabled = page <= 1;
            document.getElementById("audit-next").disabled = page >= totalPages;
            applyExportHref();
        };

        document.getElementById("audit-refresh").addEventListener("click", (e) => {
            e.preventDefault();
            page = 1;
            loadData();
        });

        document.getElementById("audit-prev").addEventListener("click", () => {
            if (page > 1) {
                page--;
                loadData();
            }
        });

        document.getElementById("audit-next").addEventListener("click", () => {
            if (page < totalPages) {
                page++;
                loadData();
            }
        });

        expandAllButton.addEventListener("click", () => {
            document.querySelectorAll(".audit-inline-detail").forEach(detail => detail.style.display = "block");
        });

        collapseAllButton.addEventListener("click", () => {
            document.querySelectorAll(".audit-inline-detail").forEach(detail => detail.style.display = "none");
        });

        copyJsonButton.addEventListener("click", async () => {
            const item = selectedRowIndex != null ? lastRows[selectedRowIndex] : null;
            if (!item) return;
            const payload = {
                oldValues: item.oldValues ? (() => { try { return JSON.parse(item.oldValues); } catch { return item.oldValues; } })() : null,
                newValues: item.newValues ? (() => { try { return JSON.parse(item.newValues); } catch { return item.newValues; } })() : null,
                affectedColumns: item.affectedColumns,
                tableName: item.tableName,
                action: item.action
            };
            await navigator.clipboard.writeText(JSON.stringify(payload, null, 2));
            window.AppUI?.toast("کپی شد", "JSON ردیف انتخاب‌شده در کلیپ‌بورد قرار گرفت.", "success");
        });

        [moduleSelect, actionSelect, tableSelect, userSelect, fromInput, toInput, sensitiveSelect].forEach(el => {
            el.addEventListener("change", () => {
                page = 1;
                loadData();
            });
        });

        searchInput.addEventListener("input", applyClientSearch);

        Promise.all([loadOptions()]).then(() => loadData());
    })();

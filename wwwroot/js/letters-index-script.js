(() => {
            const preferenceEndpoint = "/table-preferences/letters.filters";
            const storageKey = "rhyno.letters.filters";
            const antiForgeryToken = document.querySelector('meta[name="request-verification-token"]')?.content || "";
            const savedContainer = document.getElementById("lettersSavedFilters");
            const saveButton = document.getElementById("lettersSaveFilter");
            const clearButton = document.getElementById("lettersClearSaved");
            const searchInput = document.getElementById("lettersSearch");
            const senderFilter = document.getElementById("letterSenderFilter");
            const receiverFilter = document.getElementById("letterReceiverFilter");
            const statusFilter = document.getElementById("letterStatusFilter");
            const dateFrom = document.getElementById("letterDateFrom");
            const dateTo = document.getElementById("letterDateTo");

            const readJson = (key, fallback) => {
                try { return JSON.parse(localStorage.getItem(key) || "null") || fallback; } catch { return fallback; }
            };
            const writeJson = (key, value) => localStorage.setItem(key, JSON.stringify(value));
            const currentFilter = () => ({
                search: searchInput?.value || "",
                sender: senderFilter?.value || "",
                receiver: receiverFilter?.value || "",
                status: statusFilter?.value || "",
                from: dateFrom?.value || "",
                to: dateTo?.value || ""
            });
            const loadSaved = async () => {
                try {
                    const res = await fetch(preferenceEndpoint, { credentials: "same-origin" });
                    const items = res.ok ? await res.json() : [];
                    const list = Array.isArray(items) ? items : [];
                    if (list.length) writeJson(storageKey, list);
                    return list.length ? list : readJson(storageKey, []);
                } catch { return readJson(storageKey, []); }
            };
            const saveSaved = async (items) => {
                writeJson(storageKey, items);
                try {
                    await fetch(preferenceEndpoint, {
                        method: "POST",
                        credentials: "same-origin",
                        headers: {
                            "Content-Type": "application/json",
                            ...(antiForgeryToken ? { RequestVerificationToken: antiForgeryToken } : {})
                        },
                        body: JSON.stringify(items)
                    });
                } catch { }
            };
            const renderSaved = () => {
                const items = readJson(storageKey, []);
                if (!savedContainer) return;
                savedContainer.innerHTML = "";
                if (!items.length) {
                    savedContainer.innerHTML = '<span class="text-[11px] text-slate-400 dark:text-slate-500">هنوز فیلتر ذخیره نشده است.</span>';
                    return;
                }
                items.slice(0, 8).forEach(item => {
                    const btn = document.createElement("button");
                    btn.type = "button";
                    btn.className = "px-3 py-1.5 border border-slate-200/80 dark:border-slate-700/60 rounded-xl text-[10px] font-semibold text-slate-600 dark:text-slate-400 bg-white dark:bg-slate-900 hover:bg-slate-50 dark:hover:bg-slate-800 transition-colors";
                    btn.textContent = item.name;
                    btn.addEventListener("click", () => {
                        if (searchInput) searchInput.value = item.search || "";
                        if (senderFilter) senderFilter.value = item.sender || "";
                        if (receiverFilter) receiverFilter.value = item.receiver || "";
                        if (statusFilter) statusFilter.value = item.status || "";
                        if (dateFrom) dateFrom.value = item.from || "";
                        if (dateTo) dateTo.value = item.to || "";
                        searchInput?.dispatchEvent(new Event("input", { bubbles: true }));
                    });
                    savedContainer.appendChild(btn);
                });
            };
            saveButton?.addEventListener("click", () => {
                const name = prompt("نام فیلتر را وارد کن:", "فیلتر ذخیره‌شده");
                if (!name) return;
                const items = readJson(storageKey, []);
                items.unshift({ name, ...currentFilter() });
                saveSaved(items.slice(0, 10)).finally(renderSaved);
            });
            clearButton?.addEventListener("click", () => {
                localStorage.removeItem(storageKey);
                renderSaved();
            });
            loadSaved().finally(renderSaved);
        })();

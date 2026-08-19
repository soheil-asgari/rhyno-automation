document.querySelectorAll(".btn-delete").forEach(btn => {
        btn.addEventListener("click", function () {
            const id = this.dataset.id;
            window.location.href = `/Leaves/Delete/${id}`;
        });
    });

    (() => {
        const preferenceEndpoint = "/table-preferences/leaves.filters";
        const storageKey = "rhyno.leaves.filters";
        const antiForgeryToken = document.querySelector('meta[name="request-verification-token"]')?.content || "";
        const savedContainer = document.getElementById("leavesSavedFilters");
        const saveButton = document.getElementById("leavesSaveFilter");
        const clearButton = document.getElementById("leavesClearSaved");
        const nameInput = document.getElementById("leavesSaveFilterNameInput");
        const status = new URLSearchParams(window.location.search).get("status") || "";

        const readJson = (key, fallback) => {
            try { return JSON.parse(localStorage.getItem(key) || "null") || fallback; } catch { return fallback; }
        };
        const writeJson = (key, value) => localStorage.setItem(key, JSON.stringify(value));
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
                savedContainer.innerHTML = '<span class="text-slate-400 dark:text-slate-500 text-xs">هنوز فیلتری ذخیره نشده است.</span>';
                return;
            }
            items.slice(0, 8).forEach(item => {
                const btn = document.createElement("button");
                btn.type = "button";
                btn.className = "px-3 py-1 rounded-full text-xs font-bold border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 dark:border-slate-800 dark:bg-slate-950 dark:hover:bg-slate-900 dark:text-slate-300 transition shadow-sm cursor-pointer";
                btn.textContent = item.name;
                btn.addEventListener("click", () => {
                    if (item.status) {
                        window.location.href = `/Leaves?status=${encodeURIComponent(item.status)}`;
                    } else {
                        window.location.href = "/Leaves";
                    }
                });
                savedContainer.appendChild(btn);
            });
        };
        saveButton?.addEventListener("click", () => {
            if (nameInput) {
                nameInput.style.display = "inline-block";
                nameInput.focus();
                const handleSave = () => {
                    const name = nameInput.value.trim();
                    if (!name) return;
                    const items = readJson(storageKey, []);
                    items.unshift({ name, status });
                    saveSaved(items.slice(0, 10)).finally(() => {
                        renderSaved();
                        nameInput.style.display = "none";
                        nameInput.value = "";
                    });
                    nameInput.removeEventListener("blur", handleSave);
                    nameInput.removeEventListener("keydown", handleKey);
                };
                const handleKey = (e) => {
                    if (e.key === "Enter") { handleSave(); }
                    if (e.key === "Escape") { nameInput.style.display = "none"; nameInput.value = ""; nameInput.removeEventListener("blur", handleSave); nameInput.removeEventListener("keydown", handleKey); }
                };
                nameInput.addEventListener("blur", handleSave);
                nameInput.addEventListener("keydown", handleKey);
            }
        });
        clearButton?.addEventListener("click", () => {
            localStorage.removeItem(storageKey);
            renderSaved();
        });
        loadSaved().finally(renderSaved);
    })();

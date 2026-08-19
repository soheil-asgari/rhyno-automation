(() => {
        // ─────────────────────────────────────────────────────────────
        // Constants & element refs
        // ─────────────────────────────────────────────────────────────
        const storageKey = "rhyno.humanCapital.filters";
        const recentKey = "rhyno.humanCapital.recent";
        const preferenceEndpoint = "/table-preferences/human-capital.filters";
        const form = document.getElementById("hc-filter-form");
        const savedContainer = document.getElementById("hc-saved-filters");
        const saveButton = document.getElementById("hc-save-filter");
        const clearButton = document.getElementById("hc-clear-filters");
        const instantSearch = document.getElementById("hc-instant-search");
        const rows = Array.from(document.querySelectorAll("#hc-table-body .hc-data-row"));
        const pageSizeSelect = document.getElementById("hc-page-size");
        const paginationInfo = document.getElementById("hc-pagination-info");
        const paginationControls = document.getElementById("hc-pagination-controls");
        const totalRows = rows.length;

        // ─────────────────────────────────────────────────────────────
        // Saved filters helpers (kept from original)
        // ─────────────────────────────────────────────────────────────
        const readJson = (key, fallback) => {
            try { return JSON.parse(localStorage.getItem(key) || "null") || fallback; }
            catch { return fallback; }
        };
        const writeJson = (key, value) => localStorage.setItem(key, JSON.stringify(value));

        const loadSavedFilters = async () => {
            try {
                const response = await fetch(preferenceEndpoint, { credentials: "same-origin" });
                if (!response.ok) return readJson(storageKey, []);
                const items = await response.json();
                const normalized = Array.isArray(items) ? items : [];
                if (normalized.length) writeJson(storageKey, normalized);
                return normalized.length ? normalized : readJson(storageKey, []);
            } catch {
                return readJson(storageKey, []);
            }
        };

        const saveFilters = async (items) => {
            writeJson(storageKey, items);
            try {
                await fetch(preferenceEndpoint, {
                    method: "POST",
                    credentials: "same-origin",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify(items)
                });
            } catch { }
        };

        const filterForm = document.getElementById("hc-filter-form");
        const currentFilter = () => {
            if (!filterForm) return {};
            return {
                searchTerm: filterForm.querySelector('[name="SearchTerm"]')?.value?.trim() || "",
                status: filterForm.querySelector('[name="Status"]')?.value || "",
                departmentId: filterForm.querySelector('[name="DepartmentId"]')?.value || "",
                employmentType: filterForm.querySelector('[name="EmploymentType"]')?.value || "",
                workflowView: filterForm.querySelector('[name="WorkflowView"]')?.value || "all"
            };
        };

        const filterLabel = (item) => {
            const parts = [];
            if (item.searchTerm) parts.push(`جستجو: ${item.searchTerm}`);
            if (item.status) parts.push(`وضعیت: ${item.status}`);
            if (item.departmentId) parts.push(`دپارتمان: ${item.departmentId}`);
            if (item.employmentType) parts.push(`همکاری: ${item.employmentType}`);
            if (item.workflowView && item.workflowView !== "all") parts.push(`نما: ${item.workflowView}`);
            return parts.length ? parts.join(" | ") : "فیلتر بدون شرط";
        };

        const renderSaved = () => {
            if (!savedContainer) return;
            const items = readJson(storageKey, []);
            savedContainer.innerHTML = "";
            if (!items.length) {
                savedContainer.innerHTML = '<span class="text-slate-400 text-[11px]">هنوز فیلتری ذخیره نشده است.</span>';
                return;
            }
            items.slice(0, 6).forEach(item => {
                const chip = document.createElement("button");
                chip.type = "button";
                chip.className = "px-3 py-1 rounded-full text-[11px] font-bold border border-black/10 bg-white hover:bg-slate-50 text-slate-700 shadow-sm cursor-pointer transition";
                chip.textContent = item.name;
                chip.title = filterLabel(item);
                chip.addEventListener("click", () => {
                    if (!filterForm) return;
                    filterForm.querySelector('[name="SearchTerm"]').value = item.searchTerm || "";
                    filterForm.querySelector('[name="Status"]').value = item.status || "";
                    filterForm.querySelector('[name="DepartmentId"]').value = item.departmentId || "";
                    filterForm.querySelector('[name="EmploymentType"]').value = item.employmentType || "";
                    filterForm.querySelector('[name="WorkflowView"]').value = item.workflowView || "all";
                    filterForm.submit();
                });
                savedContainer.appendChild(chip);
            });
        };

        saveButton?.addEventListener("click", () => {
            const next = currentFilter();
            const name = prompt("نام این فیلتر را وارد کنید:", "فیلتر ذخیره‌شده");
            if (!name) return;
            const items = readJson(storageKey, []);
            items.unshift({ name, ...next });
            saveFilters(items.slice(0, 8)).finally(renderSaved);
        });

        clearButton?.addEventListener("click", () => {
            if (!filterForm) return;
            filterForm.querySelector('[name="SearchTerm"]').value = "";
            filterForm.querySelector('[name="Status"]').value = "";
            filterForm.querySelector('[name="DepartmentId"]').value = "";
            filterForm.querySelector('[name="EmploymentType"]').value = "";
            filterForm.querySelector('[name="WorkflowView"]').value = "all";
            filterForm.submit();
        });

        // ─────────────────────────────────────────────────────────────
        // Client-side pagination + instant search (single renderer)
        // ─────────────────────────────────────────────────────────────
        let currentPage = 1;
        let instantTimer = null;
        const pageSize = () => parseInt(pageSizeSelect?.value || "10", 10);

        const filteredRows = () => {
            const q = (instantSearch?.value || "").trim().toLowerCase();
            if (!q) return rows;
            return rows.filter(row => (row.dataset.name || "").toLowerCase().includes(q));
        };

        const renderPagination = () => {
            const filtered = filteredRows();
            const size = pageSize();
            const pages = Math.max(1, Math.ceil(filtered.length / size));
            if (currentPage > pages) currentPage = pages;

            // Hide all rows, then reveal only the current page slice of the filtered set.
            rows.forEach(row => row.style.display = "none");
            const start = (currentPage - 1) * size;
            filtered.slice(start, start + size).forEach(row => row.style.display = "");

            const from = filtered.length === 0 ? 0 : start + 1;
            const to = Math.min(start + size, filtered.length);
            paginationInfo.textContent = `نمایش ${from} تا ${to} از ${filtered.length} رکورد`;

            paginationControls.innerHTML = "";
            const mkBtn = (label, page, opts = {}) => {
                const b = document.createElement("button");
                b.type = "button";
                b.textContent = label;
                b.className = "min-w-[28px] h-7 px-1.5 rounded-md text-[11px] font-bold transition " +
                    (opts.active
                        ? "bg-emerald-600 text-white shadow-sm"
                        : "border border-black/10 bg-white text-slate-600 hover:bg-slate-50");
                if (opts.disabled) {
                    b.disabled = true;
                    b.className += " opacity-40 cursor-not-allowed";
                } else {
                    b.addEventListener("click", () => { currentPage = page; renderPagination(); });
                }
                return b;
            };

            paginationControls.appendChild(mkBtn("‹", currentPage - 1, { disabled: currentPage <= 1 }));
            const startPage = Math.max(1, currentPage - 2);
            const endPage = Math.min(pages, startPage + 4);
            for (let p = startPage; p <= endPage; p++) {
                paginationControls.appendChild(mkBtn(p.toLocaleString("fa-IR"), p, { active: p === currentPage }));
            }
            paginationControls.appendChild(mkBtn("›", currentPage + 1, { disabled: currentPage >= pages }));
        };

        instantSearch?.addEventListener("input", () => {
            clearTimeout(instantTimer);
            instantTimer = setTimeout(() => { currentPage = 1; renderPagination(); }, 220);
        });

        pageSizeSelect?.addEventListener("change", () => { currentPage = 1; renderPagination(); });
        renderPagination();

        // ─────────────────────────────────────────────────────────────
        // Action dropdowns (Vanilla JS)
        // ─────────────────────────────────────────────────────────────
        const closeAllMenus = () => {
            document.querySelectorAll(".hc-action-menu").forEach(m => m.classList.add("hidden"));
            document.querySelectorAll(".hc-action-toggle").forEach(b => b.setAttribute("aria-expanded", "false"));
        };

        document.addEventListener("click", (e) => {
            const toggle = e.target.closest(".hc-action-toggle");
            if (toggle) {
                e.preventDefault();
                e.stopPropagation();
                const menu = toggle.closest(".hc-action-wrap")?.querySelector(".hc-action-menu");
                const isOpen = menu && !menu.classList.contains("hidden");
                closeAllMenus();
                if (menu && !isOpen) {
                    menu.classList.remove("hidden");
                    toggle.setAttribute("aria-expanded", "true");
                }
                return;
            }
            if (!e.target.closest(".hc-action-menu")) closeAllMenus();
        });

        document.addEventListener("keydown", (e) => { if (e.key === "Escape") closeAllMenus(); });

        // ─────────────────────────────────────────────────────────────
        // Tabs (Vanilla JS)
        // ─────────────────────────────────────────────────────────────
        const panelTable = document.getElementById("hc-panel-table");
        const panelStatus = document.getElementById("hc-panel-status");
        const panelSalary = document.getElementById("hc-panel-salary");
        const panelDirectives = document.getElementById("hc-panel-directives");
        const tabButtons = document.querySelectorAll(".hc-tab-btn");
        const setTabActive = (btn) => {
            tabButtons.forEach(b => {
                b.classList.remove("active", "bg-emerald-600", "text-white", "shadow-sm");
                b.classList.add("text-slate-600");
            });
            btn.classList.add("active", "bg-emerald-600", "text-white", "shadow-sm");
            btn.classList.remove("text-slate-600");
        };
        const showPanel = (target) => {
            [panelTable, panelStatus, panelSalary, panelDirectives].forEach(p => {
                if (p) p.classList.add("hidden");
            });
            if (target) target.classList.remove("hidden");
        };
        tabButtons.forEach(btn => {
            btn.addEventListener("click", () => {
                setTabActive(btn);
                const tab = btn.getAttribute("data-hc-tab");
                if (tab === "status") showPanel(panelStatus);
                else if (tab === "salary") showPanel(panelSalary);
                else if (tab === "directives") showPanel(panelDirectives);
                else showPanel(panelTable);
            });
        });

        // Track visits (recent items persisted in localStorage)
        document.querySelectorAll(".hc-track-visit").forEach(link => {
            link.addEventListener("click", () => {
                const items = readJson(recentKey, []).filter(x => x.id !== link.dataset.id);
                items.unshift({ id: link.dataset.id, name: link.dataset.name, url: link.dataset.url, when: new Date().toLocaleString("fa-IR") });
                writeJson(recentKey, items.slice(0, 8));
            });
        });

        // ─────────────────────────────────────────────────────────────
        // Export (Excel) — server-side download via /HumanCapital/ExportExcel
        // The anchor href already points to the endpoint; no JS interception needed.
        // ─────────────────────────────────────────────────────────────
        const exportBtn = document.getElementById("hc-export-btn");
        exportBtn?.addEventListener("click", (e) => {
            // Allow the default anchor navigation to trigger the file download.
            // No preventDefault() — the browser will download the .xlsx file.
        });

        // ─────────────────────────────────────────────────────────────
        // Init
        // ─────────────────────────────────────────────────────────────
        loadSavedFilters().finally(renderSaved);
    })();

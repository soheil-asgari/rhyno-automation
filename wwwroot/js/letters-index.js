(function () {
    'use strict';

    const search = document.getElementById('lettersSearch');
    const searchClear = document.getElementById('lettersSearchClear');
    const senderFilter = document.getElementById('letterSenderFilter');
    const receiverFilter = document.getElementById('letterReceiverFilter');
    const statusFilter = document.getElementById('letterStatusFilter');
    const dateFrom = document.getElementById('letterDateFrom');
    const dateTo = document.getElementById('letterDateTo');
    const container = document.querySelector('.lts-table-scroll');
    const filterButtons = document.querySelectorAll('[data-letter-filter]');
    const clearFilters = document.getElementById('clearLetterFilters');
    const emptyStateHost = document.getElementById('lettersEmptyState');
    const pageInfo = document.getElementById('pageInfo');
    const pagerControls = document.getElementById('pagerControls');
    const pageNumbers = document.getElementById('pageNumbers');
    const pageSizeSelect = document.getElementById('pageSizeSelect');
    const pagePrev = document.getElementById('pagePrev');
    const pageNext = document.getElementById('pageNext');
    const activeFilterBar = document.getElementById('activeFilterBadges');
    const activeFilterBadgeList = document.getElementById('activeFilterBadgeList');
    const selectAll = document.getElementById('selectAllLetters');
    const bulkArchiveBtn = document.getElementById('bulkArchiveBtn');
    const bulkCountLabel = document.getElementById('bulkCountLabel');
    const refreshBtn = document.getElementById('lettersRefreshBtn');
    const aiDrawer = document.getElementById('letterAiPanel');
    const aiDrawerToggle = document.getElementById('aiDrawerToggle');
    const aiDrawerBody = document.getElementById('aiDrawerBody');
    const tableInfo = document.getElementById('tableInfo');
    const persistKey = 'rhyno.letters.filters';
    const recentKey = 'rhyno.letters.recent';
    const pinnedKey = 'rhyno.letters.pinned';

    if (!container) return;

    const rows = Array.from(container.querySelectorAll('.lts-row'));
    let activeFilter = 'all';
    let pageSize = parseInt(pageSizeSelect?.value || '25', 10);
    let currentPage = 1;

    const faDigits = (n) => Number(n).toLocaleString('fa-IR');
    const normalize = (str) => (str || '').toString().trim().toLowerCase();

    // ─── Count-up ───
    document.querySelectorAll('[data-count-to]').forEach(el => {
        const target = parseInt(el.dataset.countTo, 10) || 0;
        let current = 0;
        const step = Math.max(1, Math.ceil(target / 20));
        const timer = setInterval(() => {
            current += step;
            if (current >= target) { current = target; clearInterval(timer); }
            el.textContent = faDigits(current);
        }, 25);
    });

    // ─── Keyboard shortcut: Ctrl/Cmd + K → focus search ───
    document.addEventListener('keydown', (e) => {
        if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
            e.preventDefault();
            search?.focus();
            search?.select();
        }
        if (e.key === 'Escape') {
            document.querySelectorAll('.lts-dropdown.open').forEach(d => d.classList.remove('open'));
        }
    });

    // ─── Search clear ───
    const syncSearchClear = () => {
        if (!searchClear) return;
        searchClear.hidden = !search?.value;
    };
    search?.addEventListener('input', () => { syncSearchClear(); applyFilters(); });
    searchClear?.addEventListener('click', () => {
        if (search) { search.value = ''; search.focus(); }
        syncSearchClear();
        applyFilters();
    });

    // ─── Refresh button ───
    refreshBtn?.addEventListener('click', () => {
        refreshBtn.classList.add('is-loading');
        setTimeout(() => window.location.reload(), 450);
    });

    // ─── AI drawer toggle ───
    const aiTrigger = document.querySelector('[data-ai-trigger]');
    aiTrigger?.addEventListener('click', (e) => {
        if (e.target.closest('.lts-ai-chip')) return;
        if (e.target.closest('#aiDrawerToggle')) return;
        aiDrawer?.classList.toggle('collapsed');
    });
    aiDrawerToggle?.addEventListener('click', (e) => {
        e.stopPropagation();
        aiDrawer?.classList.toggle('collapsed');
    });

    // ─── Dropdown filters (custom popovers synced to hidden selects) ───
    const dropdowns = document.querySelectorAll('.lts-dropdown');
    dropdowns.forEach(dropdown => {
        const trigger = dropdown.querySelector('[data-dropdown-trigger]');
        const menu = dropdown.querySelector('[data-dropdown-menu]');
        const valueEl = dropdown.querySelector('[data-dropdown-value]');
        const hiddenSelect = dropdown.querySelector('select');

        const setValue = (raw, label) => {
            if (hiddenSelect) hiddenSelect.value = raw || '';
            if (valueEl) valueEl.textContent = label || 'همه';
            menu?.querySelectorAll('.lts-dropdown-opt').forEach(opt => {
                opt.classList.toggle('active', opt.dataset.value === (raw || ''));
            });
            if (dropdown.dataset.dropdown === 'date' && raw === 'clear') {
                if (dateFrom) dateFrom.value = '';
                if (dateTo) dateTo.value = '';
            }
            applyFilters();
        };

        trigger?.addEventListener('click', (e) => {
            e.stopPropagation();
            const wasOpen = dropdown.classList.contains('open');
            dropdowns.forEach(d => d.classList.remove('open'));
            if (!wasOpen) dropdown.classList.add('open');
        });

        menu?.addEventListener('click', (e) => {
            const opt = e.target.closest('.lts-dropdown-opt');
            if (!opt) return;
            const val = opt.dataset.value || '';
            if (dropdown.dataset.dropdown === 'date') {
                const preset = opt.dataset.preset;
                if (preset === 'today') { setDateRange('today'); setValue('clear', 'امروز'); }
                else if (preset === '7days') { setDateRange('7days'); setValue('clear', '۷ روز اخیر'); }
                else if (preset === 'month') { setDateRange('month'); setValue('clear', 'این ماه'); }
                else if (preset === 'clear') { setValue('clear', 'همه زمان‌ها'); }
                else { setValue(val, opt.textContent); }
            } else {
                setValue(val, opt.textContent.trim());
            }
            dropdown.classList.remove('open');
        });
    });

    const setDateRange = (mode) => {
        const now = new Date();
        const iso = (d) => d.toISOString().split('T')[0];
        let from = '';
        let to = iso(now);
        if (mode === 'today') { from = to; }
        else if (mode === '7days') { const f = new Date(now); f.setDate(f.getDate() - 7); from = iso(f); }
        else if (mode === 'month') { from = iso(new Date(now.getFullYear(), now.getMonth(), 1)); }
        if (dateFrom) dateFrom.value = from;
        if (dateTo) dateTo.value = to;
    };

    document.getElementById('dateRangeApply')?.addEventListener('click', () => {
        applyFilters();
        dropdowns.forEach(d => { if (d.dataset.dropdown === 'date') d.classList.remove('open'); });
    });

    // Close dropdowns on outside click
    document.addEventListener('click', (e) => {
        if (!e.target.closest('.lts-dropdown')) {
            dropdowns.forEach(d => d.classList.remove('open'));
        }
    });

    // ─── Restore filters from localStorage ───
    const restoreFilters = () => {
        try {
            const raw = localStorage.getItem(persistKey);
            if (!raw) return;
            const state = JSON.parse(raw);
            if (search) search.value = state.search || '';
            if (senderFilter) senderFilter.value = state.sender || '';
            if (receiverFilter) receiverFilter.value = state.receiver || '';
            if (statusFilter) statusFilter.value = state.status || '';
            if (dateFrom) dateFrom.value = state.from || '';
            if (dateTo) dateTo.value = state.to || '';
            if (state.activeFilter) activeFilter = state.activeFilter;
            filterButtons.forEach(btn => btn.classList.toggle('active', (btn.dataset.letterFilter || 'all') === activeFilter));
            syncDropdownLabels();
            syncSearchClear();
        } catch { }
    };

    const syncDropdownLabels = () => {
        dropdowns.forEach(d => {
            const hiddenSelect = d.querySelector('select');
            const valueEl = d.querySelector('[data-dropdown-value]');
            if (!hiddenSelect || !valueEl) return;
            const raw = hiddenSelect.value || '';
            if (d.dataset.dropdown === 'date') {
                valueEl.textContent = raw ? 'بازه دلخواه' : 'همه زمان‌ها';
                return;
            }
            const opt = d.querySelector(`.lts-dropdown-opt[data-value="${raw}"]`);
            valueEl.textContent = opt ? opt.textContent.trim() : 'همه';
            d.querySelectorAll('.lts-dropdown-opt').forEach(o => o.classList.toggle('active', o.dataset.value === raw));
        });
    };

    const saveFilters = () => {
        localStorage.setItem(persistKey, JSON.stringify({
            search: search?.value || '',
            sender: senderFilter?.value || '',
            receiver: receiverFilter?.value || '',
            status: statusFilter?.value || '',
            from: dateFrom?.value || '',
            to: dateTo?.value || '',
            activeFilter
        }));
    };

    const getPinned = () => {
        try { return JSON.parse(localStorage.getItem(pinnedKey) || '[]'); } catch { return []; }
    };
    const savePinned = (ids) => localStorage.setItem(pinnedKey, JSON.stringify(ids.slice(0, 20)));

    const pushRecent = (id, title) => {
        try {
            const items = JSON.parse(localStorage.getItem(recentKey) || '[]').filter(x => x.id !== id);
            items.unshift({ id, title, at: new Date().toISOString() });
            localStorage.setItem(recentKey, JSON.stringify(items.slice(0, 8)));
        } catch { }
    };

    // ─── Filtering ───
    const matchesDate = (row) => {
        if (!dateFrom?.value && !dateTo?.value) return true;
        const raw = row.dataset.sentAt;
        if (!raw) return true;
        const date = new Date(raw);
        if (Number.isNaN(date.getTime())) return true;
        if (dateFrom?.value && date < new Date(dateFrom.value)) return false;
        if (dateTo?.value) {
            const to = new Date(dateTo.value);
            to.setHours(23, 59, 59, 999);
            if (date > to) return false;
        }
        return true;
    };

    const itemMatchesFilter = (row, filter) => {
        const unread = row.dataset.unread === '1';
        const sent = row.dataset.sent === '1';
        const received = row.dataset.received === '1';
        const norm = row.dataset.normalizedStatus || '';
        if (filter === 'unread') return unread;
        if (filter === 'sent') return sent;
        if (filter === 'received') return received;
        if (filter === 'draft') return norm === 'Draft';
        return true;
    };

    const buildActiveBadges = () => {
        if (!activeFilterBar || !activeFilterBadgeList) return;
        const badges = [];
        const addBadge = (label, clearKey) => {
            const b = document.createElement('span');
            b.className = 'lts-filter-badge';
            b.innerHTML = `<span>${label}</span><button type="button" title="حذف فیلتر" data-clear="${clearKey}"><i class="bi bi-x"></i></button>`;
            badges.push(b);
        };
        if (search?.value) addBadge(`جستجو: ${search.value}`, 'search');
        if (senderFilter?.value) addBadge(`فرستنده: ${senderFilter.value}`, 'sender');
        if (receiverFilter?.value) addBadge(`گیرنده: ${receiverFilter.value}`, 'receiver');
        if (statusFilter?.value) addBadge(`وضعیت: ${statusFilter.options[statusFilter.selectedIndex]?.text}`, 'status');
        if (dateFrom?.value || dateTo?.value) addBadge('بازه تاریخ', 'date');

        activeFilterBadgeList.innerHTML = '';
        badges.forEach(b => activeFilterBadgeList.appendChild(b));
        activeFilterBar.hidden = badges.length === 0;

        activeFilterBadgeList.querySelectorAll('[data-clear]').forEach(btn => {
            btn.addEventListener('click', () => {
                const key = btn.dataset.clear;
                if (key === 'search' && search) search.value = '';
                if (key === 'sender' && senderFilter) senderFilter.value = '';
                if (key === 'receiver' && receiverFilter) receiverFilter.value = '';
                if (key === 'status' && statusFilter) statusFilter.value = '';
                if (key === 'date') { if (dateFrom) dateFrom.value = ''; if (dateTo) dateTo.value = ''; }
                syncDropdownLabels();
                syncSearchClear();
                applyFilters();
            });
        });
    };

    const ensureEmptyFallback = () => {
        const table = document.getElementById('lettersTable');
        if (!table) return;
        let block = document.querySelector('.letters-empty');
        if (block) return;
        block = document.createElement('div');
        block.className = 'letters-empty flex-col items-center justify-center gap-3 py-14 text-center';
        block.style.display = 'none';
        block.innerHTML =
            '<div class="w-16 h-16 flex items-center justify-center rounded-2xl bg-indigo-50 dark:bg-indigo-950/40 text-indigo-300 dark:text-indigo-600 text-2xl">' +
            '<i class="bi bi-search"></i>' +
            '</div>' +
            '<div>' +
            '<h4 class="m-0 text-sm font-black text-slate-800 dark:text-slate-200">نامه‌ای پیدا نشد</h4>' +
            '<p class="m-0 text-[11px] text-slate-400 mt-1">فیلترها را تغییر دهید یا جستجو را ساده‌تر کنید.</p>' +
            '</div>' +
            '<button type="button" id="lettersEmptyReset" class="mt-1 flex items-center gap-1.5 px-4 h-9 rounded-lg bg-gradient-to-l from-indigo-600 to-violet-600 hover:from-indigo-700 hover:to-violet-600 text-white text-xs font-bold transition-all shadow-md shadow-indigo-500/30">' +
            '<i class="bi bi-arrow-counterclockwise"></i>' +
            '<span>حذف همه فیلترها</span>' +
            '</button>';
        block.querySelector('#lettersEmptyReset')?.addEventListener('click', () => clearFilters?.click());
        table.parentElement.appendChild(block);
    };

    // ─── Pagination ───
    const renderPagination = (filtered) => {
        const total = filtered.length;
        const pages = Math.max(1, Math.ceil(total / pageSize));
        if (currentPage > pages) currentPage = pages;

        const start = total === 0 ? 0 : (currentPage - 1) * pageSize + 1;
        const end = Math.min(total, currentPage * pageSize);

        if (pageInfo) pageInfo.textContent = `نمایش ${faDigits(start)} تا ${faDigits(end)} از ${faDigits(total)} نامه`;
        if (tableInfo) tableInfo.textContent = `${faDigits(total)} نامه در کارتابل شما`;

        if (!pagerControls) return;
        pagerControls.style.display = total > pageSize ? '' : 'none';
        if (!pageNumbers || !pagePrev || !pageNext) return;

        pagePrev.disabled = currentPage <= 1;
        pageNext.disabled = currentPage >= pages;

        pageNumbers.innerHTML = '';
        const addPageBtn = (p, label) => {
            const btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'lts-page-btn' + (p === currentPage ? ' active' : '');
            btn.textContent = label ?? faDigits(p);
            btn.addEventListener('click', () => { currentPage = p; renderRows(filtered); });
            pageNumbers.appendChild(btn);
        };

        const maxBtns = 5;
        let from = Math.max(1, currentPage - Math.floor(maxBtns / 2));
        let to = Math.min(pages, from + maxBtns - 1);
        from = Math.max(1, to - maxBtns + 1);
        if (from > 1) addPageBtn(1);
        if (from > 2) {
            const dots = document.createElement('span');
            dots.className = 'px-1 text-[10px] text-slate-400';
            dots.textContent = '…';
            pageNumbers.appendChild(dots);
        }
        for (let p = from; p <= to; p++) addPageBtn(p);
        if (to < pages - 1) {
            const dots = document.createElement('span');
            dots.className = 'px-1 text-[10px] text-slate-400';
            dots.textContent = '…';
            pageNumbers.appendChild(dots);
        }
        if (to < pages) addPageBtn(pages);
    };

    const renderRows = (filtered) => {
        rows.forEach(row => { row.style.display = 'none'; });
        const pageRows = filtered.slice((currentPage - 1) * pageSize, currentPage * pageSize);
        pageRows.forEach(row => { row.style.display = ''; });

        const visibleInPage = pageRows.length;
        const table = document.getElementById('lettersTable');
        const emptyBlock = document.querySelector('.letters-empty');
        const allHidden = filtered.length === 0 && rows.length > 0;

        if (emptyBlock) emptyBlock.style.display = allHidden ? 'flex' : 'none';
        if (table) table.style.display = allHidden ? 'none' : '';

        if (emptyStateHost) {
            if (visibleInPage === 0) {
                emptyStateHost.classList.remove('d-none');
                if (window.AppUI) {
                    window.AppUI.empty(emptyStateHost, 'نامه‌ای پیدا نشد', 'فیلترها را تغییر دهید یا جستجو را ساده‌تر کنید.');
                } else {
                    emptyStateHost.innerHTML =
                        '<div class="p-8 text-center text-xs text-slate-400">نامه‌ای پیدا نشد — فیلترها را تغییر دهید.</div>';
                }
            } else {
                emptyStateHost.classList.add('d-none');
                emptyStateHost.innerHTML = '';
            }
        }
        updateBulkState();
    };

    const applyFilters = () => {
        const q = normalize(search?.value);
        const sender = normalize(senderFilter?.value);
        const receiver = normalize(receiverFilter?.value);
        const status = normalize(statusFilter?.value);

        const filtered = rows.filter(row => {
            const content = normalize(row.innerText);
            const senderText = normalize(row.dataset.sender);
            const receiverText = normalize(row.dataset.receiver);
            const statusText = normalize(row.dataset.status);
            const matchSearch = !q || content.includes(q);
            const matchSender = !sender || senderText.includes(sender);
            const matchReceiver = !receiver || receiverText.includes(receiver);
            const matchStatus = !status || statusText === status;
            const matchFilter = itemMatchesFilter(row, activeFilter);
            const matchDate = matchesDate(row);
            return matchSearch && matchSender && matchReceiver && matchStatus && matchFilter && matchDate;
        });

        currentPage = 1;
        renderRows(filtered);
        renderPagination(filtered);
        buildActiveBadges();
        syncDropdownLabels();
        syncSearchClear();
        saveFilters();
    };

    pageSizeSelect?.addEventListener('change', () => {
        pageSize = parseInt(pageSizeSelect.value, 10);
        applyFilters();
    });
    pagePrev?.addEventListener('click', () => {
        if (currentPage > 1) { currentPage--; applyPaged(); }
    });
    pageNext?.addEventListener('click', () => {
        const total = visibleFilteredCount();
        if (currentPage < Math.ceil(total / pageSize)) { currentPage++; applyPaged(); }
    });

    let lastFiltered = [];
    const visibleFilteredCount = () => {
        if (lastFiltered.length) return lastFiltered.length;
        const q = normalize(search?.value);
        const sender = normalize(senderFilter?.value);
        const receiver = normalize(receiverFilter?.value);
        const status = normalize(statusFilter?.value);
        lastFiltered = rows.filter(row => {
            const content = normalize(row.innerText);
            const matchSearch = !q || content.includes(q);
            const matchSender = !sender || normalize(row.dataset.sender).includes(sender);
            const matchReceiver = !receiver || normalize(row.dataset.receiver).includes(receiver);
            const matchStatus = !status || normalize(row.dataset.status) === status;
            const matchFilter = itemMatchesFilter(row, activeFilter);
            const matchDate = matchesDate(row);
            return matchSearch && matchSender && matchReceiver && matchStatus && matchFilter && matchDate;
        });
        return lastFiltered.length;
    };
    const applyPaged = () => {
        lastFiltered = [];
        const total = visibleFilteredCount();
        const pages = Math.max(1, Math.ceil(total / pageSize));
        if (currentPage > pages) currentPage = pages;
        renderRows(lastFiltered);
        renderPagination(lastFiltered);
    };

    // ─── Selection / bulk actions ───
    const updateBulkState = () => {
        const checked = document.querySelectorAll('.letter-check:checked').length;
        if (bulkCountLabel) bulkCountLabel.textContent = checked > 0 ? `بایگانی (${faDigits(checked)})` : 'بایگانی';
        if (bulkArchiveBtn) bulkArchiveBtn.disabled = checked === 0;
        if (selectAll) {
            const visibleChecks = Array.from(document.querySelectorAll('.letter-check')).filter(c => {
                const row = c.closest('.lts-row');
                return row && row.style.display !== 'none';
            });
            selectAll.checked = visibleChecks.length > 0 && visibleChecks.every(c => c.checked);
            selectAll.indeterminate = visibleChecks.some(c => c.checked) && !selectAll.checked;
        }
    };

    document.querySelectorAll('.letter-check').forEach(cb => cb.addEventListener('change', updateBulkState));
    selectAll?.addEventListener('change', () => {
        document.querySelectorAll('.letter-check').forEach(cb => {
            const row = cb.closest('.lts-row');
            if (row && row.style.display !== 'none') cb.checked = selectAll.checked;
        });
        updateBulkState();
    });

    bulkArchiveBtn?.addEventListener('click', () => {
        const checked = Array.from(document.querySelectorAll('.letter-check:checked'));
        if (!checked.length) return;
        const ids = checked.map(c => Number(c.dataset.id || 0)).filter(Boolean);
        if (window.AppUI && window.AppUI.confirm) {
            window.AppUI.confirm(`بایگانی ${faDigits(ids.length)} نامه انتخاب‌شده؟`, 'این عملیات قابل بازگشت نیست.', () => submitBulkArchive(ids));
        } else if (window.confirm(`بایگانی ${ids.length} نامه انتخاب‌شده؟`)) {
            submitBulkArchive(ids);
        }
    });

    const submitBulkArchive = (ids) => {
        const token = document.querySelector('meta[name="request-verification-token"]')?.content || '';
        fetch('/Letters/BulkArchive', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                ...(token ? { RequestVerificationToken: token } : {})
            },
            body: JSON.stringify({ ids })
        }).then(res => {
            if (res.ok) window.location.reload();
            else if (window.AppUI) window.AppUI.toast('خطا', 'بایگانی نامه‌ها انجام نشد.', 'danger');
        }).catch(() => {
            if (window.AppUI) window.AppUI.toast('خطا', 'اتصال به سرور برقرار نشد.', 'danger');
        });
    };

    // ─── Saved filters module hooks (letters-index-script.js) ───
    const dispatchFilterChanged = () => {
        document.querySelectorAll('[data-filter-change]').forEach(el => el.dispatchEvent(new Event('change', { bubbles: true })));
    };
    const origApply = applyFilters;
    window.__lettersApplyFilters = origApply;

    // ─── Init ───
    restoreFilters();
    ensureEmptyFallback();

    const pinned = getPinned();
    document.querySelectorAll('.pin-letter').forEach(btn => {
        const id = Number(btn.dataset.letterId || '0');
        btn.classList.toggle('active', pinned.includes(id));
    });

    [search, senderFilter, receiverFilter, statusFilter, dateFrom, dateTo].forEach(input => {
        input?.addEventListener('input', applyFilters);
        input?.addEventListener('change', applyFilters);
    });

    filterButtons.forEach(button => {
        button.addEventListener('click', function () {
            activeFilter = this.dataset.letterFilter || 'all';
            filterButtons.forEach(item => item.classList.toggle('active', (item.dataset.letterFilter || 'all') === activeFilter));
            applyFilters();
        });
    });

    clearFilters?.addEventListener('click', () => {
        [search, senderFilter, receiverFilter, statusFilter, dateFrom, dateTo].forEach(input => { if (input) input.value = ''; });
        activeFilter = 'all';
        filterButtons.forEach(item => item.classList.toggle('active', (item.dataset.letterFilter || 'all') === 'all'));
        syncDropdownLabels();
        syncSearchClear();
        localStorage.removeItem(persistKey);
        applyFilters();
    });

    document.addEventListener('click', function (e) {
        const moreBtn = e.target.closest('.more');
        if (moreBtn) {
            e.preventDefault();
            const dropdown = moreBtn.parentElement.querySelector('.lts-more-dropdown');
            if (dropdown) {
                dropdown.classList.toggle('open');
                document.addEventListener('click', function closeDropdown(ev) {
                    if (!ev.target.closest('.lts-more-menu')) {
                        dropdown.classList.remove('open');
                        document.removeEventListener('click', closeDropdown);
                    }
                }, { once: false });
            }
            return;
        }
        const pinBtn = e.target.closest('.pin-letter');
        if (pinBtn) {
            e.preventDefault();
            const id = Number(pinBtn.dataset.letterId || '0');
            if (!id) return;
            const pinned = getPinned();
            const next = pinned.includes(id) ? pinned.filter(x => x !== id) : [...pinned, id];
            savePinned(next);
            pinBtn.classList.toggle('active', next.includes(id));
            if (window.AppUI) window.AppUI.toast('ذخیره شد', next.includes(id) ? 'به علاقه‌مندی‌ها اضافه شد.' : 'از علاقه‌مندی‌ها حذف شد.', 'primary');
            return;
        }
        const archiveForm = e.target.closest('.archive-form');
        if (archiveForm) {
            e.preventDefault();
            const msg = archiveForm.dataset.confirm || 'این نامه بایگانی شود؟';
            if (window.confirm(msg)) archiveForm.submit();
            return;
        }
    });

    document.querySelectorAll('.lts-row').forEach(card => {
        const id = Number(card.dataset.letterId || '0');
        const title = card.querySelector('.lts-subject-link')?.textContent || '';
        if (id && title) {
            card.addEventListener('click', (event) => {
                if (event.target.closest('button, a, form, input')) return;
                pushRecent(id, title);
            }, { passive: true });
        }
    });

    applyFilters();
})();
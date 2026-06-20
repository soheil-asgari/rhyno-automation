(function () {
    const search = document.getElementById('lettersSearch');
    const senderFilter = document.getElementById('letterSenderFilter');
    const receiverFilter = document.getElementById('letterReceiverFilter');
    const statusFilter = document.getElementById('letterStatusFilter');
    const dateFrom = document.getElementById('letterDateFrom');
    const dateTo = document.getElementById('letterDateTo');
    const container = document.querySelector('.letters-container');
    const filterButtons = document.querySelectorAll('[data-letter-filter]');
    const clearFilters = document.getElementById('clearLetterFilters');
    const emptyStateHost = document.getElementById('lettersEmptyState');
    const recentKey = 'rhyno.letters.recent';
    const pinnedKey = 'rhyno.letters.pinned';

    if (!container) return;

    const rows = Array.from(container.querySelectorAll('.letter-row-card'));
    let activeFilter = 'all';

    const normalize = (str) => (str || '').toString().trim().toLowerCase();
    const persistKey = 'rhyno.letters.filters';

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
        } catch { }
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
        if (filter === 'unread') return unread;
        if (filter === 'sent') return sent;
        if (filter === 'received') return received;
        return true;
    };

    const applyFilters = () => {
        const q = normalize(search?.value);
        const sender = normalize(senderFilter?.value);
        const receiver = normalize(receiverFilter?.value);
        const status = normalize(statusFilter?.value);

        rows.forEach(row => {
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
            row.style.display = matchSearch && matchSender && matchReceiver && matchStatus && matchFilter && matchDate ? '' : 'none';
        });

        const visibleCount = rows.filter(row => row.style.display !== 'none').length;
        if (emptyStateHost) {
            if (visibleCount === 0) {
                emptyStateHost.classList.remove('d-none');
                if (window.AppUI) {
                    window.AppUI.empty(emptyStateHost, 'نامه‌ای پیدا نشد', 'فیلترها را تغییر بده یا جستجو را ساده‌تر کن.');
                }
            } else {
                emptyStateHost.classList.add('d-none');
                emptyStateHost.innerHTML = '';
            }
        }

        saveFilters();
    };

    restoreFilters();

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
            filterButtons.forEach(item => item.classList.remove('active'));
            this.classList.add('active');
            applyFilters();
        });
    });

    clearFilters?.addEventListener('click', () => {
        [search, senderFilter, receiverFilter, statusFilter, dateFrom, dateTo].forEach(input => { if (input) input.value = ''; });
        activeFilter = 'all';
        filterButtons.forEach(item => item.classList.toggle('active', (item.dataset.letterFilter || 'all') === 'all'));
        localStorage.removeItem(persistKey);
        applyFilters();
    });

    document.addEventListener('click', function (e) {
        const printBtn = e.target.closest('.print');
        if (printBtn) { e.preventDefault(); window.print(); return; }
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
        const moreBtn = e.target.closest('.more');
        if (moreBtn) {
            e.preventDefault();
            if (window.Swal) {
                Swal.fire({ icon: 'info', title: 'عملیات', text: 'این بخش در حال توسعه است.', confirmButtonText: 'تایید' });
            }
        }
    });

    document.querySelectorAll('.letter-row-card').forEach(card => {
        const id = Number(card.dataset.letterId || '0');
        const title = card.querySelector('.letter-title')?.textContent || '';
        if (id && title) {
            card.addEventListener('click', (event) => {
                if (event.target.closest('button, a, form')) return;
                pushRecent(id, title);
            }, { passive: true });
        }
    });

    applyFilters();
})();

(function () {
    const search = document.getElementById('lettersSearch');
    const container = document.querySelector('.letters-container');
    const filterButtons = document.querySelectorAll('[data-letter-filter]');

    if (!container) {
        return;
    }

    const rows = Array.from(container.querySelectorAll('.letter-row-card'));
    let activeFilter = 'all';

    function normalize(str) {
        return (str || '').toString().trim().toLowerCase();
    }

    function itemMatchesFilter(row, filter) {
        const unread = row.dataset.unread === '1';
        const sent = row.dataset.sent === '1';
        const received = row.dataset.received === '1';

        if (filter === 'unread') return unread;
        if (filter === 'sent') return sent;
        if (filter === 'received') return received;

        return true;
    }

    function applyFilters() {
        const q = normalize(search?.value);

        rows.forEach(row => {
            const content = normalize(row.innerText);
            const matchSearch = !q || content.includes(q);
            const matchFilter = itemMatchesFilter(row, activeFilter);
            row.style.display = matchSearch && matchFilter ? '' : 'none';
        });
    }

    if (search) {
        search.addEventListener('input', applyFilters);
    }

    filterButtons.forEach(button => {
        button.addEventListener('click', function () {
            activeFilter = this.dataset.letterFilter || 'all';

            filterButtons.forEach(item => item.classList.remove('active'));
            this.classList.add('active');

            applyFilters();
        });
    });

    document.addEventListener('click', function (e) {
        const printBtn = e.target.closest('.print');
        if (printBtn) {
            e.preventDefault();
            window.print();
            return;
        }

        const moreBtn = e.target.closest('.more');
        if (moreBtn) {
            e.preventDefault();
            if (window.Swal) {
                Swal.fire({
                    icon: 'info',
                    title: 'عملیات',
                    text: 'این بخش در حال توسعه است.',
                    confirmButtonText: 'تایید'
                });
            } else {
                console.log('این بخش در حال توسعه است.');
            }
        }
    });

    applyFilters();
})();

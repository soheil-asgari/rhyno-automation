(function () {
    const search = document.getElementById('lettersSearch');
    const clearBtn = document.getElementById('lettersSearchClear');
    const tbody = document.getElementById('lettersTbody');
    const emptySearch = document.getElementById('lettersEmptySearch');
    const countEl = document.getElementById('lettersCount');

    if (!search || !tbody) return;

    const rows = Array.from(tbody.querySelectorAll('.letter-row'));

    function normalize(str) {
        return (str || '')
            .toString()
            .trim()
            .toLowerCase();
    }

    function applyFilter() {
        const q = normalize(search.value);
        let visible = 0;

        rows.forEach(r => {
            const hay = normalize(
                (r.dataset.title || '') + ' ' +
                (r.dataset.sender || '') + ' ' +
                (r.dataset.receiver || '') + ' ' +
                (r.dataset.date || '')
            );

            const show = !q || hay.includes(q);
            r.style.display = show ? '' : 'none';
            if (show) visible++;
        });

        // count + empty state
        if (countEl) countEl.textContent = visible;

        if (q && visible === 0) {
            emptySearch?.classList.remove('d-none');
        } else {
            emptySearch?.classList.add('d-none');
        }

        // clear button
        if (q) clearBtn?.classList.remove('d-none');
        else clearBtn?.classList.add('d-none');
    }

    search.addEventListener('input', applyFilter);
    clearBtn?.addEventListener('click', function () {
        search.value = '';
        search.focus();
        applyFilter();
    });

    applyFilter();
})();
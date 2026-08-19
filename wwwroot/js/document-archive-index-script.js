(function () {
            // Quick client-side search on the table
            var searchInput = document.getElementById('archiveSearch');
            if (!searchInput) return;

            searchInput.addEventListener('input', function () {
                var query = (this.value || '').trim().toLowerCase();
                var rows = document.querySelectorAll('#archiveBody .archive-row-card');
                var visible = 0;
                rows.forEach(function (row) {
                    var title = (row.getAttribute('data-title') || '').toLowerCase();
                    var filename = (row.getAttribute('data-filename') || '').toLowerCase();
                    var match = !query || title.includes(query) || filename.includes(query);
                    row.style.display = match ? '' : 'none';
                    if (match) visible++;
                });
            });
        })();

let currentLevel = 1;

function loadReviewData(level, btnElement) {
    currentLevel = level;

    // مدیریت کلاس Active در سایدبار
    document.querySelectorAll('.sg-sidebar button').forEach(b => b.classList.remove('active'));
    btnElement.classList.add('active');

    fetchData();
}

function applyFilter() {
    fetchData();
}

function fetchData() {
    const fromYear = document.getElementById('fromFiscalYear').value;
    const toYear = document.getElementById('toFiscalYear').value;

    fetch(`/api/AccountReview/data?level=${currentLevel}&fromFiscalYear=${fromYear}&toFiscalYear=${toYear}`)
        .then(response => response.json())
        .then(data => {
            const tbody = document.querySelector('#voucherTable tbody');
            tbody.innerHTML = '';

            if (data.length === 0) {
                tbody.innerHTML = '<tr><td colspan="5" style="text-align: center;">سطری برای نمایش وجود ندارد.</td></tr>';
                return;
            }

            data.forEach(item => {
                const row = document.createElement('tr');
                row.innerHTML = `
                    <td>${item.voucherItemID || ''}</td>
                    <td>${item.description || ''}</td>
                    <td>${item.debit || 0}</td>
                    <td>${item.credit || 0}</td>
                    <td>0</td>
                `;
                tbody.appendChild(row);
            });
        })
        .catch(error => console.error('Error loading review data:', error));
}
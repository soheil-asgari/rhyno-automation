(() => {
            const dataEl = document.getElementById('finDashboardData');
            const initial = dataEl ? JSON.parse(dataEl.textContent) : {};
            const context = document.getElementById('finHubChart');
            if (!context || typeof Chart === 'undefined') return;

            const labels = initial.labels || [];
            const revenue = initial.revenue || [];
            const costs = initial.costs || [];
            const profit = initial.profit || [];
            let chart;

            function buildDatasets(areaMode) {
                return [
                    { label: 'درآمد', data: revenue, borderColor: '#10b981', backgroundColor: areaMode ? 'rgba(16,185,129,0.18)' : '#10b981', fill: areaMode, tension: 0.28 },
                    { label: 'هزینه', data: costs, borderColor: '#ef4444', backgroundColor: areaMode ? 'rgba(239,68,68,0.18)' : '#ef4444', fill: areaMode, tension: 0.28 },
                    { label: 'سود یا زیان', data: profit, borderColor: '#3b82f6', backgroundColor: areaMode ? 'rgba(59,130,246,0.18)' : '#3b82f6', fill: areaMode, tension: 0.28 }
                ];
            }

            function setActiveButton(mode) {
                document.querySelectorAll('.chart-mode').forEach(button => {
                    button.classList.toggle('is-active', button.getAttribute('data-mode') === mode);
                });
            }

            function renderChart(mode) {
                const areaMode = mode === 'area';
                const chartType = areaMode ? 'line' : mode;
                if (chart) chart.destroy();

                chart = new Chart(context, {
                    type: chartType,
                    data: {
                        labels,
                        datasets: buildDatasets(areaMode)
                    },
                    options: {
                        responsive: true,
                        maintainAspectRatio: false,
                        resizeDelay: 120,
                        plugins: {
                            legend: {
                                position: 'bottom'
                            }
                        },
                        scales: {
                            y: {
                                ticks: {
                                    callback: (value) => Number(value).toLocaleString('fa-IR')
                                }
                            }
                        }
                    }
                });

                setActiveButton(mode);
            }

            document.querySelectorAll('.chart-mode').forEach(button => {
                button.addEventListener('click', () => renderChart(button.getAttribute('data-mode') || 'bar'));
            });

            renderChart('bar');
        })();

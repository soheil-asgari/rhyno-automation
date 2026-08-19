(() => {
            const dataEl = document.getElementById('reportingHubData');
            const initial = dataEl ? JSON.parse(dataEl.textContent) : {};
            const labels = initial.labels || [];
            const revenueSeries = initial.revenue || [];
            const costSeries = initial.costs || [];
            const payrollSeries = initial.payroll || [];
            const stockMovementSeries = initial.stock || [];

            const context = document.getElementById('reportingHubChart');
            let chart;

            function datasets(areaMode) {
                return [
                    {
                        label: 'سود و زیان (درآمد)',
                        data: revenueSeries,
                        borderColor: '#10b981',
                        backgroundColor: areaMode ? 'rgba(16,185,129,0.2)' : '#10b981',
                        fill: areaMode,
                        tension: .3
                    },
                    {
                        label: 'سود و زیان (هزینه)',
                        data: costSeries,
                        borderColor: '#ef4444',
                        backgroundColor: areaMode ? 'rgba(239,68,68,0.2)' : '#ef4444',
                        fill: areaMode,
                        tension: .3
                    },
                    {
                        label: 'هزینه‌های حقوق',
                        data: payrollSeries,
                        borderColor: '#f59e0b',
                        backgroundColor: areaMode ? 'rgba(245,158,11,0.2)' : '#f59e0b',
                        fill: areaMode,
                        tension: .3
                    },
                    {
                        label: 'حجم گردش انبار',
                        data: stockMovementSeries,
                        borderColor: '#3b82f6',
                        backgroundColor: areaMode ? 'rgba(59,130,246,0.2)' : '#3b82f6',
                        fill: areaMode,
                        tension: .3,
                        yAxisID: 'yStock'
                    }
                ];
            }

            function draw(type) {
                const isArea = type === 'area';
                const baseType = isArea ? 'line' : type;
                if (chart) chart.destroy();

                chart = new Chart(context, {
                    type: baseType,
                    data: { labels, datasets: datasets(isArea) },
                    options: {
                        responsive: true,
                        maintainAspectRatio: false,
                        plugins: { legend: { position: 'bottom' } },
                        scales: {
                            y: {
                                position: 'left',
                                ticks: { callback: (val) => Number(val).toLocaleString('fa-IR') }
                            },
                            yStock: {
                                position: 'right',
                                grid: { drawOnChartArea: false },
                                ticks: { callback: (val) => Number(val).toLocaleString('fa-IR') }
                            }
                        }
                    }
                });
            }

            document.querySelectorAll('.chart-type-btn').forEach(button => {
                button.addEventListener('click', () => draw(button.getAttribute('data-chart-type') || 'bar'));
            });

            draw('bar');
        })();

document.addEventListener('DOMContentLoaded', () => {
            const overviewData = (() => {
                const el = document.getElementById('overviewChartData');
                return el ? JSON.parse(el.textContent) : {};
            })();
            const isDark = document.documentElement.classList.contains('dark') || document.documentElement.getAttribute('data-theme') === 'dark';
            const textColor = isDark ? '#94a3b8' : '#64748b';
            const gridColor = isDark ? '#334155' : '#e2e8f0';

            // 1. Sales vs Purchases Chart (Bar Chart)
            const ctxSales = document.getElementById('salesPurchasesChart')?.getContext('2d');
            if (ctxSales) {
                new Chart(ctxSales, {
                    type: 'bar',
                    data: {
                        labels: ['فروش', 'خرید'],
                        datasets: [{
                            label: 'مبلغ کل فاکتورها (ریال)',
                            data: [overviewData.totalSales || 0, overviewData.totalPurchases || 0],
                            backgroundColor: [
                                'rgba(16, 185, 129, 0.75)', // emerald
                                'rgba(239, 68, 68, 0.75)'   // rose
                            ],
                            borderColor: [
                                '#10b981',
                                '#ef4444'
                            ],
                            borderWidth: 1.5,
                            borderRadius: 6
                        }]
                    },
                    options: {
                        responsive: true,
                        maintainAspectRatio: false,
                        plugins: {
                            legend: {
                                display: false
                            }
                        },
                        scales: {
                            y: {
                                ticks: {
                                    color: textColor,
                                    font: { family: 'iranyekan, Vazir, Tahoma' }
                                },
                                grid: { color: gridColor }
                            },
                            x: {
                                ticks: {
                                    color: textColor,
                                    font: { family: 'iranyekan, Vazir, Tahoma', weight: 'bold' }
                                },
                                grid: { display: false }
                            }
                        }
                    }
                });
            }

            // 2. Pending Workflows Chart (Doughnut Chart)
            const ctxWorkflows = document.getElementById('workflowsChart')?.getContext('2d');
            if (ctxWorkflows) {
                const modules = overviewData.pendingModules || [];
                const counts = overviewData.pendingCounts || [];
                
                // Translate module names for UI display
                const translatedLabels = modules.map(m => {
                    if (m === 'Leave') return 'درخواست مرخصی';
                    if (m === 'Invoice') return 'فاکتور مالی';
                    if (m === 'Letter') return 'مکاتبات اداری';
                    return m;
                });

                new Chart(ctxWorkflows, {
                    type: 'doughnut',
                    data: {
                        labels: translatedLabels,
                        datasets: [{
                            data: counts,
                            backgroundColor: [
                                'rgba(20, 184, 166, 0.8)', // teal
                                'rgba(59, 130, 246, 0.8)', // blue
                                'rgba(245, 158, 11, 0.8)', // amber
                                'rgba(139, 92, 246, 0.8)'  // violet
                            ],
                            borderWidth: 0
                        }]
                    },
                    options: {
                        responsive: true,
                        maintainAspectRatio: false,
                        plugins: {
                            legend: {
                                position: 'bottom',
                                labels: {
                                    color: textColor,
                                    font: { family: 'iranyekan, Vazir, Tahoma', size: 11 }
                                }
                            }
                        }
                    });
            }
        });

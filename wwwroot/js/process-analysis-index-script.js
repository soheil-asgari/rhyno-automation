(() => {
            const pageElement = document.querySelector('.تحلیل-صفحه');
            if (!pageElement) return;

            const filterForm = document.getElementById('فرمفیلتر');
            const loadingStatus = document.getElementById('وضعیتبارگذاری');
            const dataUrl = pageElement.dataset.نشانیداده;
            const excelExportLink = document.getElementById('پیوندخروجیاکسل');
            const charts = {};

            const elements = {
                cycleTimeAverage: document.getElementById('خلاصهمیانگینچرخه'),
                sampleCount: document.getElementById('خلاصهنمونهها'),
                pendingCount: document.getElementById('خلاصهدرانتظار'),
                sensitiveCount: document.getElementById('خلاصهرویدادحساس'),
                cycleTimeTableBody: document.getElementById('جدولزمانچرخه'),
                combinedTableBody: document.getElementById('جدولترکیبی')
            };

            function escapeHtml(value) {
                return `${value ?? ''}`
                    .replace(/&/g, '&amp;')
                    .replace(/</g, '&lt;')
                    .replace(/>/g, '&gt;')
                    .replace(/"/g, '&quot;')
                    .replace(/'/g, '&#39;');
            }

            function formatNumber(value) {
                return new Intl.NumberFormat('fa-IR', { maximumFractionDigits: 2 }).format(value ?? 0);
            }

            function buildChartConfig(chartData, type) {
                return {
                    type: type,
                    data: {
                        labels: chartData.labels,
                        datasets: chartData.series.map(series => ({
                            label: series.label,
                            data: series.values,
                            backgroundColor: type === 'line' ? 'rgba(0,0,0,0)' : series.color,
                            borderColor: series.color,
                            borderWidth: 2,
                            tension: .3,
                            fill: false
                        }))
                    },
                    options: {
                        responsive: true,
                        maintainAspectRatio: false,
                        plugins: {
                            legend: {
                                labels: {
                                    font: { family: 'Vazir' }
                                }
                            }
                        },
                        scales: type === 'doughnut' ? {} : {
                            x: { ticks: { font: { family: 'Vazir' } } },
                            y: { ticks: { font: { family: 'Vazir' } } }
                        }
                    }
                };
            }

            function renderChart(canvasId, chartData, type) {
                if (charts[canvasId]) {
                    charts[canvasId].destroy();
                }

                const ctx = document.getElementById(canvasId).getContext('2d');
                charts[canvasId] = new Chart(ctx, buildChartConfig(chartData, type));
            }

            function renderTables(report) {
                elements.cycleTimeTableBody.innerHTML = report.cycleTime.items.map(item => `
                    <tr>
                        <td>${escapeHtml(item.processName || item.نامفرآیند || '')}</td>
                        <td>${formatNumber(item.count || item.تعداد)}</td>
                        <td>${formatNumber(item.averageHours || item.میانگینساعت)}</td>
                        <td>${formatNumber(item.medianHours || item.میانهساعت)}</td>
                        <td>${formatNumber(item.maxHours || item.بیشینهساعت)}</td>
                    </tr>`).join('');

                const bottleneckRows = report.bottlenecks.items.map(item => `
                    <tr>
                        <td>گلوگاه</td>
                        <td>${escapeHtml(item.processName || item.نامفرآیند || '')} - ${escapeHtml(item.stepName || item.ناممرحله || '')}</td>
                        <td>${formatNumber(item.count || item.تعداد)}</td>
                        <td>${formatNumber(item.averageStopHours || item.میانگینساعتتوقف)}</td>
                        <td>${formatNumber(item.maxStopHours || item.بیشینهساعتتوقف)}</td>
                    </tr>`);

                const pendingRows = report.approvalDuration.items.map(item => `
                    <tr>
                        <td>در انتظار تایید</td>
                        <td>${escapeHtml(item.processName || item.نامفرآیند || '')}</td>
                        <td>${formatNumber(item.pendingCount || item.تعداددرانتظار)}</td>
                        <td>${formatNumber(item.averageWaitHours || item.میانگینساعتانتظار)}</td>
                        <td>${formatNumber(item.maxWaitHours || item.بیشینهساعتانتظار)}</td>
                    </tr>`);

                elements.combinedTableBody.innerHTML = [...bottleneckRows, ...pendingRows].join('');
            }

            function renderReport(report) {
                elements.cycleTimeAverage.textContent = formatNumber(report.cycleTime.averageHours);
                elements.sampleCount.textContent = formatNumber(report.cycleTime.sampleCount);
                elements.pendingCount.textContent = formatNumber(report.approvalDuration.totalCount);
                elements.sensitiveCount.textContent = formatNumber(report.auditEvents.sensitiveCount);

                renderChart('نمودارزمانچرخه', report.cycleTime.trendChart, 'line');
                renderChart('نمودارگلوگاه', report.bottlenecks.bottleneckChart, 'bar');
                renderChart('نمودارماندگاری', report.approvalDuration.distributionChart, 'doughnut');
                renderChart('نمودارممیزی', report.auditEvents.eventChart, 'bar');
                renderTables(report);
            }

            async function loadReport() {
                loadingStatus.textContent = 'در حال بارگذاری...';
                const formData = new FormData(filterForm);
                const queryParams = new URLSearchParams();
                for (const [key, value] of formData.entries()) {
                    if (value) queryParams.set(key, value.toString());
                }

                excelExportLink.href = `${(dataUrl || '').replace('/ChartData', '/ExportExcel')}?${queryParams.toString()}`;

                const response = await fetch(`${dataUrl}?${queryParams.toString()}`, {
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });

                if (!response.ok) {
                    loadingStatus.textContent = 'دریافت داده ناموفق بود';
                    return;
                }

                const report = await response.json();
                renderReport(report);
                loadingStatus.textContent = 'آخرین به‌روزرسانی انجام شد';
            }

            filterForm.addEventListener('submit', async event => {
                event.preventDefault();
                await loadReport();
            });

            const today = new Date();
            const fromDate = new Date();
            fromDate.setDate(today.getDate() - 30);
            document.getElementById('تاتاریخ').value = today.toISOString().slice(0, 10);
            document.getElementById('ازتاریخ').value = fromDate.toISOString().slice(0, 10);

            loadReport();
        })();

(() => {
    const root = document.querySelector('.management-dashboard');
    if (!root || !window.Chart) {
        return;
    }

    const els = {
        token: root.querySelector('input[name="__RequestVerificationToken"]'),
        dbForm: document.getElementById('mdDbForm'),
        connectionId: document.getElementById('mdConnectionId'),
        connectionName: document.getElementById('mdConnectionName'),
        provider: document.getElementById('mdProvider'),
        host: document.getElementById('mdHost'),
        port: document.getElementById('mdPort'),
        database: document.getElementById('mdDatabase'),
        username: document.getElementById('mdUsername'),
        password: document.getElementById('mdPassword'),
        trustCertificate: document.getElementById('mdTrustCertificate'),
        testDbBtn: document.getElementById('mdTestDbBtn'),
        saveConnectionBtn: document.getElementById('mdSaveConnectionBtn'),
        newConnectionBtn: document.getElementById('mdNewConnectionBtn'),
        dbResult: document.getElementById('mdDbResult'),
        savedCount: document.getElementById('mdSavedCount'),
        savedConnectionsList: document.getElementById('mdSavedConnectionsList'),
        connectionLabel: document.getElementById('mdConnectionLabel'),
        heroStatus: root.querySelector('.md-hero-status'),
        chat: document.getElementById('mdChat'),
        reportForm: document.getElementById('mdReportForm'),
        prompt: document.getElementById('mdPrompt'),
        generateBtn: document.getElementById('mdGenerateBtn'),
        chartButtons: root.querySelectorAll('.md-chart-tabs button'),
        chartModeLabel: document.getElementById('mdChartModeLabel'),
        numberFormat: document.getElementById('mdNumberFormat'),
        zoomChartBtn: document.getElementById('mdZoomChartBtn'),
        printReportBtn: document.getElementById('mdPrintReportBtn'),
        reportTitle: document.getElementById('mdReportTitle'),
        reportSummary: document.getElementById('mdReportSummary'),
        insights: document.getElementById('mdInsights'),
        chart: document.getElementById('mdReportChart'),
        chartModal: document.getElementById('mdChartModal'),
        closeChartModalBtn: document.getElementById('mdCloseChartModalBtn'),
        zoomChart: document.getElementById('mdZoomChart'),
        sqlDebug: document.getElementById('mdSqlDebug'),
        generatedSql: document.getElementById('mdGeneratedSql'),
        copySqlBtn: document.getElementById('mdCopySqlBtn')
    };

    let activeChartType = 'bar';
    let chart;
    let zoomChart;
    let connectedDatabase = null;
    let savedConnections = [];
    let currentReport = null;

    const chartLabels = {
        bar: 'نمودار ستونی',
        line: 'نمودار خطی',
        doughnut: 'نمودار حلقه‌ای',
        radar: 'نمودار راداری'
    };

    const palette = [
        'rgba(79, 70, 229, .78)',
        'rgba(37, 99, 235, .72)',
        'rgba(14, 165, 233, .72)',
        'rgba(16, 185, 129, .72)',
        'rgba(245, 158, 11, .72)',
        'rgba(239, 68, 68, .72)',
        'rgba(124, 58, 237, .72)',
        'rgba(236, 72, 153, .72)',
        'rgba(20, 184, 166, .72)',
        'rgba(132, 204, 22, .72)',
        'rgba(249, 115, 22, .72)',
        'rgba(100, 116, 139, .72)'
    ];

    const escapeHtml = (value) => String(value ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');

    const formatNumber = (value) => {
        const numeric = Number(value || 0);
        const mode = els.numberFormat?.value || 'standard';

        if (mode === 'compact') {
            return new Intl.NumberFormat('fa-IR', {
                notation: 'compact',
                maximumFractionDigits: 1
            }).format(numeric);
        }

        if (mode === 'currency') {
            return `${new Intl.NumberFormat('fa-IR', { maximumFractionDigits: 0 }).format(numeric)} ریال`;
        }

        return new Intl.NumberFormat('fa-IR', { maximumFractionDigits: 0 }).format(numeric);
    };

    const setBusy = (button, isBusy, busyText) => {
        if (!button) {
            return;
        }

        if (isBusy) {
            button.dataset.originalHtml = button.innerHTML;
            button.disabled = true;
            button.innerHTML = `<span class="spinner-border spinner-border-sm"></span><span>${busyText}</span>`;
            return;
        }

        button.disabled = false;
        if (button.dataset.originalHtml) {
            button.innerHTML = button.dataset.originalHtml;
            delete button.dataset.originalHtml;
        }
    };

    const getConnectionPayload = () => ({
        id: els.connectionId.value ? Number(els.connectionId.value) : null,
        name: els.connectionName.value.trim() || null,
        provider: els.provider.value,
        host: els.host.value.trim(),
        port: els.port.value ? Number(els.port.value) : null,
        databaseName: els.database.value.trim() || null,
        username: els.username.value.trim() || null,
        password: els.password.value || null,
        trustServerCertificate: els.trustCertificate.checked
    });

    const syncProviderDefaults = () => {
        const provider = els.provider.value;
        const portMap = {
            SqlServer: 1433,
            PostgreSql: 5432,
            MySql: 3306,
            Sqlite: 0
        };

        els.port.value = portMap[provider] || '';
        els.port.disabled = provider === 'Sqlite';
        els.host.placeholder = provider === 'Sqlite'
            ? 'C:\\data\\reports.db'
            : 'localhost یا 192.168.1.20';
        els.database.placeholder = provider === 'Sqlite'
            ? 'مسیر فایل دیتابیس'
            : 'OfficeAutoDb';
    };

    const setDbResult = (message, type = 'muted') => {
        els.dbResult.className = `md-result md-result-${type}`;
        els.dbResult.textContent = message;
    };

    const addMessage = (type, text) => {
        const item = document.createElement('div');
        item.className = `md-message ${type}`;
        item.innerHTML = `
            <div class="md-avatar"><i class="bi ${type === 'user' ? 'bi-person' : 'bi-robot'}"></i></div>
            <div class="md-bubble">${escapeHtml(text)}</div>
        `;
        els.chat.appendChild(item);
        els.chat.scrollTop = els.chat.scrollHeight;
    };

    const renderInsights = (items) => {
        els.insights.innerHTML = '';
        (items || []).forEach((item) => {
            const node = document.createElement('div');
            node.className = 'md-insight';
            node.innerHTML = `
                <i class="bi bi-lightning-charge"></i>
                <button type="button" class="md-insight-action" data-prompt="${escapeHtml(item)}">
                    ${escapeHtml(item)}
                </button>
            `;
            els.insights.appendChild(node);
        });
    };

    const syncChartButtons = () => {
        els.chartButtons.forEach((button) => {
            button.classList.toggle('active', button.dataset.chartType === activeChartType);
        });
        els.chartModeLabel.textContent = chartLabels[activeChartType] || 'نمودار';
    };

    const buildChartConfig = (report, type) => {
        const labels = report.labels || [];
        const values = report.values || [];

        return {
            type,
            data: {
                labels,
                datasets: [{
                    label: report.title || 'گزارش مدیریتی',
                    data: values,
                    borderWidth: 2,
                    tension: .35,
                    fill: type === 'line',
                    backgroundColor: palette,
                    borderColor: type === 'line'
                        ? 'rgba(79, 70, 229, 1)'
                        : 'rgba(79, 70, 229, .9)'
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        display: type === 'doughnut' || type === 'radar',
                        position: 'bottom'
                    },
                    tooltip: {
                        callbacks: {
                            label: (context) => {
                                const value = context.parsed?.y ?? context.parsed?.r ?? context.parsed ?? context.raw;
                                return `${context.dataset.label}: ${formatNumber(value)}`;
                            }
                        }
                    }
                },
                scales: type === 'doughnut'
                    ? {}
                    : type === 'radar'
                        ? {
                            r: {
                                beginAtZero: true,
                                grid: { color: 'rgba(148, 163, 184, .18)' },
                                ticks: { callback: (value) => formatNumber(value) }
                            }
                        }
                        : {
                        y: {
                            beginAtZero: true,
                            grid: { color: 'rgba(148, 163, 184, .18)' },
                            ticks: { callback: (value) => formatNumber(value) }
                        },
                        x: { grid: { display: false } }
                        }
            }
        };
    };

    const renderChart = (report) => {
        if (chart) {
            chart.destroy();
        }

        chart = new Chart(els.chart.getContext('2d'), buildChartConfig(report, activeChartType));
    };

    const renderZoomChart = () => {
        if (!currentReport || !els.zoomChart) {
            return;
        }

        if (zoomChart) {
            zoomChart.destroy();
        }

        zoomChart = new Chart(els.zoomChart.getContext('2d'), buildChartConfig(currentReport, activeChartType));
    };

    const showGeneratedSql = (sql) => {
        if (!els.sqlDebug || !els.generatedSql) {
            return;
        }

        if (!sql) {
            els.generatedSql.textContent = '';
            els.sqlDebug.classList.add('d-none');
            return;
        }

        els.generatedSql.textContent = sql;
        els.sqlDebug.classList.remove('d-none');
    };

    const applyReport = (report) => {
        currentReport = { ...report };
        activeChartType = report.chartType || activeChartType;
        currentReport.chartType = activeChartType;
        syncChartButtons();
        els.reportTitle.textContent = report.title || 'گزارش مدیریتی';
        els.reportSummary.textContent = report.summary || '';
        renderInsights(report.insights);
        renderChart(currentReport);
        showGeneratedSql(report.generatedSql || report.GeneratedSql || '');
    };

    const fetchJson = async (url, payload) => {
        const response = await fetch(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': els.token?.value || '',
                'X-Requested-With': 'XMLHttpRequest'
            },
            body: JSON.stringify(payload)
        });

        const data = await response.json().catch(() => ({}));
        if (!response.ok) {
            throw new Error(data.message || 'درخواست ناموفق بود.');
        }

        return data;
    };

    const fetchJsonGet = async (url) => {
        const response = await fetch(url, {
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });

        const data = await response.json().catch(() => ({}));
        if (!response.ok) {
            throw new Error(data.message || 'دریافت اطلاعات ناموفق بود.');
        }

        return data;
    };

    const buildSavedConnectionPayload = (item) => ({
        id: item.id,
        name: item.name,
        provider: item.provider,
        host: item.host,
        port: item.port,
        databaseName: item.databaseName,
        username: item.username,
        password: null,
        trustServerCertificate: item.trustServerCertificate
    });

    const fillConnectionForm = (item, markSelected = true) => {
        els.connectionId.value = item.id || '';
        els.connectionName.value = item.name || '';
        els.provider.value = item.provider || 'SqlServer';
        syncProviderDefaults();
        els.host.value = item.host || '';
        els.port.value = item.port || '';
        els.database.value = item.databaseName || '';
        els.username.value = item.username || '';
        els.password.value = '';
        els.trustCertificate.checked = item.trustServerCertificate !== false;

        if (markSelected) {
            connectedDatabase = {
                provider: item.provider,
                databaseName: item.databaseName,
                endpoint: item.endpoint,
                connection: buildSavedConnectionPayload(item)
            };
            els.heroStatus.classList.add('connected');
            els.connectionLabel.textContent = `${item.provider} - ${item.endpoint}`;
            setDbResult('اتصال ذخیره‌شده انتخاب شد. برای اطمینان می‌توانید تست اتصال را اجرا کنید.', 'success');
        }
    };

    const resetConnectionForm = () => {
        els.connectionId.value = '';
        els.connectionName.value = '';
        els.provider.value = 'SqlServer';
        syncProviderDefaults();
        els.host.value = 'localhost';
        els.database.value = '';
        els.username.value = '';
        els.password.value = '';
        els.trustCertificate.checked = true;
        connectedDatabase = null;
        els.heroStatus.classList.remove('connected');
        els.connectionLabel.textContent = 'دیتابیس انتخاب نشده';
        setDbResult('اطلاعات اتصال را وارد کنید یا یکی از اتصال‌های ذخیره‌شده را انتخاب کنید.', 'muted');
    };

    const renderSavedConnections = (items) => {
        savedConnections = items || [];
        els.savedCount.textContent = String(savedConnections.length);
        els.savedConnectionsList.innerHTML = '';

        if (savedConnections.length === 0) {
            els.savedConnectionsList.innerHTML = '<div class="md-saved-empty">هنوز اتصالی ذخیره نشده است.</div>';
            return;
        }

        savedConnections.forEach((item) => {
            const node = document.createElement('div');
            node.className = 'md-saved-item';
            node.dataset.id = item.id;
            node.innerHTML = `
                <div class="md-saved-info">
                    <div class="md-saved-title">${escapeHtml(item.name || 'بدون نام')}</div>
                    <div class="md-saved-meta">${escapeHtml(`${item.provider} - ${item.endpoint}${item.databaseName ? ` / ${item.databaseName}` : ''}`)}</div>
                </div>
                <div class="md-saved-actions">
                    <button type="button" class="btn btn-sm btn-outline-primary md-load-connection" title="انتخاب"><i class="bi bi-check2"></i></button>
                    <button type="button" class="btn btn-sm btn-outline-secondary md-edit-connection" title="ویرایش"><i class="bi bi-pencil"></i></button>
                    <button type="button" class="btn btn-sm btn-outline-danger md-delete-connection" title="حذف"><i class="bi bi-trash"></i></button>
                </div>
            `;
            els.savedConnectionsList.appendChild(node);
        });
    };

    const loadSavedConnections = async () => {
        try {
            const result = await fetchJsonGet(root.dataset.connectionsUrl);
            renderSavedConnections(result.items || []);
        } catch (error) {
            renderSavedConnections([]);
            setDbResult(error.message || 'دریافت اتصال‌های ذخیره‌شده ناموفق بود.', 'error');
        }
    };

    const saveConnection = async () => {
        const payload = getConnectionPayload();
        if (!payload.host) {
            setDbResult('آدرس سرور یا مسیر دیتابیس را وارد کنید.', 'error');
            return;
        }

        setBusy(els.saveConnectionBtn, true, 'در حال ذخیره...');
        try {
            const result = await fetchJson(root.dataset.saveConnectionUrl, payload);
            if (!result.success || !result.item) {
                throw new Error(result.message || 'ذخیره اتصال ناموفق بود.');
            }

            fillConnectionForm(result.item, true);
            await loadSavedConnections();
            setDbResult(result.message || 'اتصال ذخیره شد.', 'success');
        } catch (error) {
            setDbResult(error.message || 'ذخیره اتصال ناموفق بود.', 'error');
        } finally {
            setBusy(els.saveConnectionBtn, false);
        }
    };

    const deleteConnection = async (id) => {
        if (!window.confirm('این اتصال حذف شود؟')) {
            return;
        }

        try {
            const result = await fetchJson(root.dataset.deleteConnectionUrl, { id });
            await loadSavedConnections();
            if (Number(els.connectionId.value) === Number(id)) {
                resetConnectionForm();
            }
            setDbResult(result.message || 'اتصال حذف شد.', 'success');
        } catch (error) {
            setDbResult(error.message || 'حذف اتصال ناموفق بود.', 'error');
        }
    };

    const submitReportPrompt = (prompt) => {
        els.prompt.value = prompt;
        els.reportForm.requestSubmit();
    };

    els.provider.addEventListener('change', syncProviderDefaults);
    els.saveConnectionBtn.addEventListener('click', saveConnection);
    els.newConnectionBtn.addEventListener('click', resetConnectionForm);

    els.savedConnectionsList.addEventListener('click', (event) => {
        const button = event.target.closest('button');
        const row = event.target.closest('.md-saved-item');
        if (!button || !row) {
            return;
        }

        const id = Number(row.dataset.id);
        const item = savedConnections.find((connection) => connection.id === id);
        if (!item) {
            return;
        }

        if (button.classList.contains('md-delete-connection')) {
            deleteConnection(id);
            return;
        }

        fillConnectionForm(item, true);
    });

    els.insights.addEventListener('click', (event) => {
        const button = event.target.closest('.md-insight-action');
        if (!button) {
            return;
        }

        const tip = button.dataset.prompt || button.textContent.trim();
        submitReportPrompt(`بر اساس این پیشنهاد یک گزارش مدیریتی بساز: ${tip}`);
    });

    els.dbForm.addEventListener('submit', async (event) => {
        event.preventDefault();
        const payload = getConnectionPayload();

        if (!payload.host) {
            setDbResult('آدرس سرور یا مسیر دیتابیس را وارد کنید.', 'error');
            return;
        }

        setBusy(els.testDbBtn, true, 'در حال تست...');
        setDbResult('در حال بررسی اتصال...', 'muted');

        try {
            const result = await fetchJson(root.dataset.testDbUrl, payload);
            if (!result.success) {
                throw new Error(result.detail || result.message);
            }

            connectedDatabase = {
                provider: result.provider,
                databaseName: payload.databaseName,
                endpoint: result.endpoint,
                connection: payload
            };

            els.heroStatus.classList.add('connected');
            els.connectionLabel.textContent = `${result.provider} - ${result.endpoint}`;
            setDbResult(`${result.message} زمان پاسخ: ${result.latencyMs}ms`, 'success');
        } catch (error) {
            connectedDatabase = null;
            els.heroStatus.classList.remove('connected');
            els.connectionLabel.textContent = 'اتصال برقرار نشد';
            setDbResult(error.message || 'اتصال برقرار نشد.', 'error');
        } finally {
            setBusy(els.testDbBtn, false);
        }
    });

    els.chartButtons.forEach((button) => {
        button.addEventListener('click', () => {
            activeChartType = button.dataset.chartType || 'bar';
            if (currentReport) {
                currentReport.chartType = activeChartType;
                renderChart(currentReport);
                renderZoomChart();
            }
            syncChartButtons();
        });
    });

    els.numberFormat?.addEventListener('change', () => {
        if (currentReport) {
            renderChart(currentReport);
            renderZoomChart();
        }
    });

    els.zoomChartBtn?.addEventListener('click', () => {
        if (!currentReport) {
            return;
        }

        els.chartModal.classList.remove('d-none');
        renderZoomChart();
    });

    els.closeChartModalBtn?.addEventListener('click', () => {
        els.chartModal.classList.add('d-none');
        if (zoomChart) {
            zoomChart.destroy();
            zoomChart = null;
        }
    });

    els.chartModal?.addEventListener('click', (event) => {
        if (event.target === els.chartModal) {
            els.closeChartModalBtn.click();
        }
    });

    els.printReportBtn?.addEventListener('click', () => {
        window.print();
    });

    els.copySqlBtn?.addEventListener('click', async () => {
        const sql = els.generatedSql?.textContent || '';
        if (!sql) {
            return;
        }

        await navigator.clipboard?.writeText(sql);
    });

    els.reportForm.addEventListener('submit', async (event) => {
        event.preventDefault();
        const prompt = els.prompt.value.trim();

        if (!prompt) {
            addMessage('ai', 'لطفا درخواست گزارش را بنویسید.');
            return;
        }

        addMessage('user', prompt);
        setBusy(els.generateBtn, true, 'در حال ساخت...');

        try {
            const payload = {
                prompt,
                chartType: activeChartType,
                provider: connectedDatabase?.provider || null,
                databaseName: connectedDatabase?.databaseName || null,
                connection: connectedDatabase?.connection || null
            };

            const result = await fetchJson(root.dataset.reportUrl, payload);
            if (!result.success || !result.report) {
                throw new Error(result.message || 'ساخت گزارش ناموفق بود.');
            }

            applyReport(result.report);
            addMessage('ai', result.report.isFromDatabase
                ? 'گزارش از دیتابیس خوانده شد. روی پیشنهادهای گزارش هم می‌توانید کلیک کنید تا گزارش بعدی ساخته شود.'
                : 'گزارش آماده شد. برای خروجی دقیق‌تر، اتصال دیتابیس را انتخاب و تست کنید.');
            els.prompt.value = '';
        } catch (error) {
            addMessage('ai', error.message || 'ساخت گزارش ناموفق بود. دوباره تلاش کنید.');
        } finally {
            setBusy(els.generateBtn, false);
        }
    });

    syncProviderDefaults();
    loadSavedConnections();
    applyReport({
        title: 'نمونه پیش‌نمایش مدیریتی',
        summary: 'برای شروع، اتصال دیتابیس را تست کنید و سپس یک درخواست گزارش بنویسید. نمودار نمونه زیر فقط برای نمایش حالت‌های بصری است.',
        labels: ['فروردین', 'اردیبهشت', 'خرداد', 'تیر', 'مرداد', 'شهریور', 'مهر', 'آبان', 'آذر', 'دی', 'بهمن', 'اسفند'],
        values: [72, 58, 64, 81, 49, 55, 69, 74, 68, 83, 79, 91],
        insights: [
            'هزینه ماه‌های پیک را با مرکز هزینه و نوع سند مقایسه کن.',
            'روند ماهانه هزینه را با بودجه مصوب همان دوره مقایسه کن.',
            'برای ماه‌هایی که رشد غیرعادی دارند گزارش جزئی اسناد بساز.'
        ],
        chartType: 'bar'
    });
})();

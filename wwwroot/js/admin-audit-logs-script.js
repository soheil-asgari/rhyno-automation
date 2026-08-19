(() => {
            const apiUrl = '/api/admin/audit-logs';
            const optionsUrl = '/api/admin/audit-logs/filter-options';
            const state = {
                page: 1,
                pageSize: 20,
                totalCount: 0,
                totalPages: 0,
                items: [],
                options: {
                    users: [],
                    actions: [],
                    tableNames: [],
                    modules: []
                }
            };

            const els = {
                form: document.getElementById('auditFiltersForm'),
                tableBody: document.getElementById('auditLogsTableBody'),
                loading: document.getElementById('auditLoading'),
                pagination: document.getElementById('auditPagination'),
                paginationInfo: document.getElementById('paginationInfo'),
                gridSummary: document.getElementById('gridSummary'),
                sensitiveWarningBox: document.getElementById('sensitiveWarningBox'),
                filterOptionsStatus: document.getElementById('filterOptionsStatus'),
                activeFilterBadges: document.getElementById('activeFilterBadges'),
                metricTotal: document.getElementById('metricTotal'),
                metricCreate: document.getElementById('metricCreate'),
                metricUpdate: document.getElementById('metricUpdate'),
                metricDelete: document.getElementById('metricDelete'),
                userId: document.getElementById('filterUserId'),
                tableName: document.getElementById('filterTableName'),
                module: document.getElementById('filterModule'),
                action: document.getElementById('filterAction'),
                sensitiveOnly: document.getElementById('filterSensitiveOnly'),
                from: document.getElementById('filterFrom'),
                to: document.getElementById('filterTo'),
                pageSize: document.getElementById('pageSizeSelect'),
                clear: document.getElementById('clearFiltersBtn'),
                refresh: document.getElementById('refreshAuditLogsBtn'),
                export: document.getElementById('exportAuditLogsBtn'),
                modal: document.getElementById('auditDiffModal'),
                modalSubtitle: document.getElementById('auditDiffSubtitle'),
                modalMeta: document.getElementById('auditDiffMeta'),
                modalContent: document.getElementById('auditDiffContent')
            };

            const modal = els.modal ? new bootstrap.Modal(els.modal) : null;
            const persianNumberFormatter = new Intl.NumberFormat('fa-IR');

            const getValue = (obj, ...keys) => {
                if (!obj) {
                    return undefined;
                }

                for (const key of keys) {
                    if (Object.prototype.hasOwnProperty.call(obj, key)) {
                        return obj[key];
                    }
                }

                return undefined;
            };

            const normalizeItems = (payload) => getValue(payload, 'items', 'Items') || [];
            const normalizeText = (value) => value === null || value === undefined || value === '' ? '-' : String(value);
            const toFaNumber = (value) => persianNumberFormatter.format(Number(value) || 0);
            const escapeHtml = (value) => normalizeText(value)
                .replace(/&/g, '&amp;')
                .replace(/</g, '&lt;')
                .replace(/>/g, '&gt;')
                .replace(/"/g, '&quot;')
                .replace(/'/g, '&#039;');

            const sortForCompare = (value) => {
                if (Array.isArray(value)) {
                    return value.map(sortForCompare);
                }

                if (value && typeof value === 'object') {
                    return Object.keys(value).sort().reduce((result, key) => {
                        result[key] = sortForCompare(value[key]);
                        return result;
                    }, {});
                }

                return value;
            };

            const stableStringify = (value) => {
                if (value === null || value === undefined || value === '') {
                    return '';
                }

                if (typeof value === 'object') {
                    try {
                        return JSON.stringify(sortForCompare(value), null, 2);
                    } catch {
                        return String(value);
                    }
                }

                return String(value);
            };

            const formatJsonValue = (value) => {
                if (value === null || value === undefined || value === '') {
                    return '<span class="text-muted">بدون مقدار</span>';
                }

                if (typeof value === 'object') {
                    return escapeHtml(JSON.stringify(value, null, 2));
                }

                return escapeHtml(value);
            };

            const parseJson = (jsonText) => {
                if (!jsonText || typeof jsonText !== 'string') {
                    return {};
                }

                try {
                    const parsed = JSON.parse(jsonText);
                    return parsed && typeof parsed === 'object' ? parsed : {};
                } catch {
                    return {};
                }
            };

            const formatDate = (value, includeTime = true) => {
                if (!value) {
                    return '-';
                }

                const date = new Date(value);
                if (Number.isNaN(date.getTime())) {
                    return '-';
                }

                const dateText = date.toLocaleDateString('fa-IR', {
                    year: 'numeric',
                    month: '2-digit',
                    day: '2-digit'
                });

                if (!includeTime) {
                    return dateText;
                }

                const timeText = date.toLocaleTimeString('fa-IR', {
                    hour: '2-digit',
                    minute: '2-digit',
                    second: '2-digit',
                    hour12: false
                });

                return `${dateText}، ${timeText}`;
            };

            const actionInfo = (action) => {
                const normalized = String(action || '').toLowerCase();
                if (normalized === 'create' || normalized === 'insert') {
                    return { label: 'ثبت جدید', badgeClass: 'bg-success' };
                }

                if (normalized === 'update' || normalized === 'edit') {
                    return { label: 'ویرایش', badgeClass: 'bg-warning text-dark' };
                }

                if (normalized === 'delete') {
                    return { label: 'حذف', badgeClass: 'bg-danger' };
                }

                return { label: normalizeText(action), badgeClass: 'bg-secondary' };
            };

            const buildQuery = (page = state.page, pageSize = state.pageSize) => {
                const params = new URLSearchParams();
                params.set('page', String(page));
                params.set('pageSize', String(pageSize));

                if (els.userId.value) {
                    params.set('userId', els.userId.value);
                }
                if (els.action.value) {
                    params.set('action', els.action.value);
                }
                if (els.tableName.value) {
                    params.set('tableName', els.tableName.value);
                }
                if (els.module.value) {
                    params.set('module', els.module.value);
                }
                if (els.sensitiveOnly.checked) {
                    params.set('sensitiveOnly', 'true');
                }
                if (els.from.value) {
                    params.set('from', new Date(`${els.from.value}T00:00:00`).toISOString());
                }
                if (els.to.value) {
                    params.set('to', new Date(`${els.to.value}T23:59:59`).toISOString());
                }

                return params;
            };

            const setLoading = (isLoading) => {
                els.loading.classList.toggle('d-none', !isLoading);
                els.loading.classList.toggle('d-flex', isLoading);
            };

            const fetchJson = async (url) => {
                const response = await fetch(url, {
                    method: 'GET',
                    headers: {
                        'Accept': 'application/json'
                    }
                });

                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }

                return response.json();
            };

            const fillSelect = (select, items, placeholder, mapItem) => {
                select.innerHTML = `<option value="">${placeholder}</option>`;
                items.forEach((item) => {
                    const mapped = mapItem(item);
                    const option = document.createElement('option');
                    option.value = mapped.value;
                    option.textContent = mapped.label;
                    select.appendChild(option);
                });
            };

            const loadFilterOptions = async () => {
                try {
                    const payload = await fetchJson(optionsUrl);
                    state.options.users = getValue(payload, 'users', 'Users') || [];
                    state.options.actions = getValue(payload, 'actions', 'Actions') || [];
                    state.options.tableNames = getValue(payload, 'tableNames', 'TableNames') || [];
                    state.options.modules = getValue(payload, 'modules', 'Modules') || [];

                    fillSelect(els.userId, state.options.users, 'همه کاربران', (item) => ({
                        value: getValue(item, 'value', 'Value') || '',
                        label: getValue(item, 'label', 'Label') || ''
                    }));
                    fillSelect(els.tableName, state.options.tableNames, 'همه بخش‌ها', (item) => ({
                        value: item,
                        label: item
                    }));
                    fillSelect(els.module, state.options.modules, 'همه ماژول‌ها', (item) => ({
                        value: item,
                        label: item
                    }));
                    fillSelect(els.action, state.options.actions, 'همه عملیات', (item) => ({
                        value: item,
                        label: actionInfo(item).label
                    }));

                    els.filterOptionsStatus.textContent = 'گزینه‌ها آماده است';
                    els.filterOptionsStatus.className = 'badge bg-success-subtle text-success';
                } catch (error) {
                    els.filterOptionsStatus.textContent = 'خطا در دریافت گزینه‌ها';
                    els.filterOptionsStatus.className = 'badge bg-danger-subtle text-danger';
                    console.error('Audit filter options failed:', error);
                }
            };

            const renderRows = () => {
                if (!state.items.length) {
                    els.tableBody.innerHTML = `
                        <tr>
                            <td colspan="7">
                                <div class="audit-empty text-center">
                                    <i class="bi bi-inbox fs-2 d-block mb-2 text-muted"></i>
                                    رکوردی مطابق فیلترهای انتخاب‌شده یافت نشد.
                                </div>
                            </td>
                        </tr>`;
                    return;
                }

                els.tableBody.innerHTML = state.items.map((item, index) => {
                    const action = getValue(item, 'action', 'Action');
                    const actionBadge = actionInfo(action);
                    const userDisplayName = getValue(item, 'userDisplayName', 'UserDisplayName') || getValue(item, 'userId', 'UserId');
                    const tableName = getValue(item, 'tableName', 'TableName');
                    const moduleName = getValue(item, 'module', 'Module');
                    const dateTime = getValue(item, 'dateTime', 'DateTime');
                    const affectedColumns = getValue(item, 'affectedColumns', 'AffectedColumns');
                    const userIp = getValue(item, 'userIP', 'UserIP', 'userIp', 'UserIp');
                    const isSensitive = getValue(item, 'isSensitive', 'IsSensitive');

                    return `
                        <tr class="audit-row" data-index="${index}" title="برای مشاهده جزئیات تغییرات کلیک کنید">
                            <td class="text-nowrap">
                                <div class="fw-bold">${escapeHtml(formatDate(dateTime))}</div>
                                <div class="small text-muted text-ltr">${escapeHtml(dateTime)}</div>
                            </td>
                            <td>
                                <span class="fw-bold">${escapeHtml(userDisplayName)}</span>
                            </td>
                            <td>
                                <span class="badge ${actionBadge.badgeClass} px-3 py-2">${escapeHtml(actionBadge.label)}</span>
                                ${isSensitive ? '<span class="badge bg-danger-subtle text-danger ms-1">حساس</span>' : ''}
                            </td>
                            <td>
                                <span class="badge bg-light text-dark border">${escapeHtml(tableName)}</span>
                                <div class="small text-muted mt-1">${escapeHtml(moduleName)}</div>
                            </td>
                            <td class="text-ltr text-muted">${escapeHtml(userIp)}</td>
                            <td>${escapeHtml(affectedColumns)}</td>
                            <td class="text-center">
                                <button type="button" class="btn btn-outline-primary btn-sm audit-detail-btn" data-index="${index}">
                                    <i class="bi bi-eye ms-1"></i>
                                    مشاهده
                                </button>
                            </td>
                        </tr>`;
                }).join('');
            };

            const renderPagination = () => {
                const page = state.page;
                const totalPages = state.totalPages;
                const totalCount = state.totalCount;

                if (!totalCount) {
                    els.paginationInfo.textContent = '';
                    els.pagination.innerHTML = '';
                    return;
                }

                const from = ((page - 1) * state.pageSize) + 1;
                const to = Math.min(page * state.pageSize, totalCount);
                els.paginationInfo.textContent = `نمایش ${toFaNumber(from)} تا ${toFaNumber(to)} از ${toFaNumber(totalCount)} رکورد`;

                const pages = new Set([1, totalPages, page - 2, page - 1, page, page + 1, page + 2]);
                const validPages = [...pages].filter((item) => item >= 1 && item <= totalPages).sort((a, b) => a - b);
                const html = [];

                html.push(pageButton('قبلی', page - 1, page <= 1));
                let previous = 0;
                validPages.forEach((pageNumber) => {
                    if (previous && pageNumber - previous > 1) {
                        html.push('<li class="page-item disabled"><span class="page-link">...</span></li>');
                    }
                    html.push(pageButton(toFaNumber(pageNumber), pageNumber, false, pageNumber === page));
                    previous = pageNumber;
                });
                html.push(pageButton('بعدی', page + 1, page >= totalPages));

                els.pagination.innerHTML = html.join('');
            };

            const pageButton = (label, page, disabled = false, active = false) => `
                <li class="page-item ${disabled ? 'disabled' : ''} ${active ? 'active' : ''}">
                    <button type="button" class="page-link" data-page="${page}" ${disabled ? 'disabled' : ''}>${label}</button>
                </li>`;

            const renderFilterBadges = () => {
                const badges = [];
                if (els.userId.value) {
                    badges.push(`کاربر: ${els.userId.options[els.userId.selectedIndex]?.text || els.userId.value}`);
                }
                if (els.tableName.value) {
                    badges.push(`بخش: ${els.tableName.value}`);
                }
                if (els.action.value) {
                    badges.push(`عملیات: ${actionInfo(els.action.value).label}`);
                }
                if (els.module.value) {
                    badges.push(`ماژول: ${els.module.value}`);
                }
                if (els.sensitiveOnly.checked) {
                    badges.push('فقط حساس');
                }
                if (els.from.value) {
                    badges.push(`از: ${formatDate(els.from.value, false)}`);
                }
                if (els.to.value) {
                    badges.push(`تا: ${formatDate(els.to.value, false)}`);
                }

                els.activeFilterBadges.innerHTML = badges.map((item) => `<span class="badge bg-primary-subtle text-primary">${escapeHtml(item)}</span>`).join('');
            };

            const renderSensitiveWarning = () => {
                const hasSensitive = state.items.some((item) => getValue(item, 'isSensitive', 'IsSensitive'));
                els.sensitiveWarningBox.classList.toggle('d-none', !hasSensitive);
            };

            const renderMetrics = async () => {
                els.metricTotal.textContent = toFaNumber(state.totalCount);

                const currentAction = els.action.value;
                if (currentAction) {
                    els.metricCreate.textContent = actionInfo(currentAction).label === 'ثبت جدید' ? toFaNumber(state.totalCount) : '۰';
                    els.metricUpdate.textContent = actionInfo(currentAction).label === 'ویرایش' ? toFaNumber(state.totalCount) : '۰';
                    els.metricDelete.textContent = actionInfo(currentAction).label === 'حذف' ? toFaNumber(state.totalCount) : '۰';
                    return;
                }

                const countByAction = async (action) => {
                    const params = buildQuery(1, 1);
                    params.set('action', action);
                    const payload = await fetchJson(`${apiUrl}?${params.toString()}`);
                    return getValue(payload, 'totalCount', 'TotalCount') || 0;
                };

                try {
                    const [created, inserted, updated, deleted] = await Promise.all([
                        countByAction('Create'),
                        countByAction('Insert'),
                        countByAction('Update'),
                        countByAction('Delete')
                    ]);
                    els.metricCreate.textContent = toFaNumber(created + inserted);
                    els.metricUpdate.textContent = toFaNumber(updated);
                    els.metricDelete.textContent = toFaNumber(deleted);
                } catch (error) {
                    console.error('Audit metrics failed:', error);
                }
            };

            const loadAuditLogs = async (page = 1) => {
                state.page = page;
                state.pageSize = Number(els.pageSize.value) || 20;
                setLoading(true);
                els.gridSummary.textContent = 'در حال بارگذاری...';

                try {
                    const payload = await fetchJson(`${apiUrl}?${buildQuery(state.page, state.pageSize).toString()}`);
                    state.items = normalizeItems(payload);
                    state.page = getValue(payload, 'page', 'Page') || state.page;
                    state.pageSize = getValue(payload, 'pageSize', 'PageSize') || state.pageSize;
                    state.totalCount = getValue(payload, 'totalCount', 'TotalCount') || 0;
                    state.totalPages = getValue(payload, 'totalPages', 'TotalPages') || Math.ceil(state.totalCount / state.pageSize) || 0;

                    renderRows();
                    renderPagination();
                    renderFilterBadges();
                    renderSensitiveWarning();
                    els.gridSummary.textContent = `${toFaNumber(state.totalCount)} رکورد در نتیجه فیلتر فعلی`;
                    await renderMetrics();
                } catch (error) {
                    console.error('Audit logs failed:', error);
                    els.tableBody.innerHTML = `
                        <tr>
                            <td colspan="7">
                                <div class="audit-empty text-center text-danger">
                                    <i class="bi bi-exclamation-triangle fs-2 d-block mb-2"></i>
                                    خطا در دریافت گزارش فعالیت‌ها.
                                </div>
                            </td>
                        </tr>`;
                    els.gridSummary.textContent = 'خطا در بارگذاری';
                    els.pagination.innerHTML = '';
                    els.paginationInfo.textContent = '';
                    els.sensitiveWarningBox.classList.add('d-none');
                } finally {
                    setLoading(false);
                }
            };

            const buildDiffRows = (oldValues, newValues) => {
                const keys = [...new Set([...Object.keys(oldValues), ...Object.keys(newValues)])].sort((a, b) => a.localeCompare(b));
                if (!keys.length) {
                    return '<div class="alert alert-light border mb-0">برای این رویداد مقدار قدیم یا جدیدی ثبت نشده است.</div>';
                }

                const rows = keys.map((key) => {
                    const oldValue = oldValues[key];
                    const newValue = newValues[key];
                    const oldText = stableStringify(oldValue);
                    const newText = stableStringify(newValue);
                    const changed = oldText !== newText;
                    const statusClass = oldValue === undefined ? 'diff-added' : newValue === undefined ? 'diff-removed' : changed ? 'diff-changed' : '';
                    const statusLabel = oldValue === undefined ? 'افزوده شده' : newValue === undefined ? 'حذف شده' : changed ? 'تغییر کرده' : 'بدون تغییر';
                    const statusBadge = changed || oldValue === undefined || newValue === undefined
                        ? `<span class="badge bg-primary-subtle text-primary">${statusLabel}</span>`
                        : `<span class="badge bg-secondary-subtle text-secondary">${statusLabel}</span>`;

                    return `
                        <tr class="${statusClass}">
                            <th>${escapeHtml(key)}</th>
                            <td class="diff-value">${formatJsonValue(oldValue)}</td>
                            <td class="diff-value">${formatJsonValue(newValue)}</td>
                            <td class="text-nowrap">${statusBadge}</td>
                        </tr>`;
                }).join('');

                return `
                    <div class="table-responsive">
                        <table class="table table-bordered align-middle diff-table">
                            <thead class="table-light">
                                <tr>
                                    <th>ویژگی</th>
                                    <th>مقدار قبلی</th>
                                    <th>مقدار جدید</th>
                                    <th>وضعیت</th>
                                </tr>
                            </thead>
                            <tbody>${rows}</tbody>
                        </table>
                    </div>`;
            };

            const showDetails = (index) => {
                const item = state.items[index];
                if (!item || !modal) {
                    return;
                }

                const oldValues = parseJson(getValue(item, 'oldValues', 'OldValues'));
                const newValues = parseJson(getValue(item, 'newValues', 'NewValues'));
                const action = getValue(item, 'action', 'Action');
                const tableName = getValue(item, 'tableName', 'TableName');
                const userDisplayName = getValue(item, 'userDisplayName', 'UserDisplayName') || getValue(item, 'userId', 'UserId');
                const userIp = getValue(item, 'userIP', 'UserIP', 'userIp', 'UserIp');
                const dateTime = getValue(item, 'dateTime', 'DateTime');
                const affectedColumns = getValue(item, 'affectedColumns', 'AffectedColumns');
                const actionBadge = actionInfo(action);

                els.modalSubtitle.textContent = `${normalizeText(tableName)} | ${formatDate(dateTime)}`;
                els.modalMeta.innerHTML = `
                    <div class="col-12 col-md-3">
                        <div class="p-3 rounded border bg-light h-100">
                            <div class="small text-muted mb-1">کاربر</div>
                            <div class="fw-bold">${escapeHtml(userDisplayName)}</div>
                        </div>
                    </div>
                    <div class="col-12 col-md-3">
                        <div class="p-3 rounded border bg-light h-100">
                            <div class="small text-muted mb-1">عملیات</div>
                            <span class="badge ${actionBadge.badgeClass} px-3 py-2">${escapeHtml(actionBadge.label)}</span>
                        </div>
                    </div>
                    <div class="col-12 col-md-3">
                        <div class="p-3 rounded border bg-light h-100">
                            <div class="small text-muted mb-1">آدرس IP</div>
                            <div class="fw-bold text-ltr">${escapeHtml(userIp)}</div>
                        </div>
                    </div>
                    <div class="col-12 col-md-3">
                        <div class="p-3 rounded border bg-light h-100">
                            <div class="small text-muted mb-1">ستون‌ها</div>
                            <div class="fw-bold">${escapeHtml(affectedColumns)}</div>
                        </div>
                    </div>`;
                els.modalContent.innerHTML = buildDiffRows(oldValues, newValues);
                modal.show();
            };

            const exportCsv = async () => {
                els.export.disabled = true;
                const originalText = els.export.innerHTML;
                els.export.innerHTML = '<span class="spinner-border spinner-border-sm ms-1" aria-hidden="true"></span>در حال آماده‌سازی';

                try {
                    const response = await fetch(`${apiUrl}/export?${buildQuery(1, 5000).toString()}`, { method: 'GET' });
                    if (!response.ok) {
                        throw new Error(`HTTP ${response.status}`);
                    }
                    const blob = await response.blob();
                    const url = URL.createObjectURL(blob);
                    const link = document.createElement('a');
                    link.href = url;
                    link.download = `audit-logs-${new Date().toISOString().slice(0, 10)}.csv`;
                    document.body.appendChild(link);
                    link.click();
                    document.body.removeChild(link);
                    URL.revokeObjectURL(url);
                } catch (error) {
                    console.error('Audit CSV export failed:', error);
                    alert('خطا در تولید خروجی اکسل. لطفا دوباره تلاش کنید.');
                } finally {
                    els.export.disabled = false;
                    els.export.innerHTML = originalText;
                }
            };

            els.form.addEventListener('submit', (event) => {
                event.preventDefault();
                loadAuditLogs(1);
            });

            els.clear.addEventListener('click', () => {
                els.form.reset();
                els.pageSize.value = '20';
                loadAuditLogs(1);
            });

            els.refresh.addEventListener('click', () => loadAuditLogs(state.page));
            els.export.addEventListener('click', exportCsv);
            els.pageSize.addEventListener('change', () => loadAuditLogs(1));

            els.pagination.addEventListener('click', (event) => {
                const button = event.target.closest('[data-page]');
                if (!button || button.disabled) {
                    return;
                }

                const nextPage = Number(button.dataset.page);
                if (Number.isFinite(nextPage)) {
                    loadAuditLogs(nextPage);
                }
            });

            els.tableBody.addEventListener('click', (event) => {
                const target = event.target.closest('[data-index]');
                if (!target) {
                    return;
                }

                showDetails(Number(target.dataset.index));
            });

            const initAuditDashboard = async () => {
                await loadFilterOptions();
                await loadAuditLogs(1);
            };

            if (document.readyState === 'loading') {
                document.addEventListener('DOMContentLoaded', initAuditDashboard, { once: true });
            } else {
                initAuditDashboard();
            }
        })();

(async () => {
            const workspace = document.querySelector('.inbox-workspace');
            if (!workspace) return;

            const items = JSON.parse(workspace.dataset.items || '[]');
            const assignees = JSON.parse(workspace.dataset.assignees || '[]');
            const groupsHost = document.getElementById('inboxGroupsHost');
            const searchInput = document.getElementById('inboxSearchInput');
            const groupBySelect = document.getElementById('groupBySelect');
            const moduleFilterSelect = document.getElementById('moduleFilterSelect');
            const statusFilterSelect = document.getElementById('statusFilterSelect');
            const readFilterSelect = document.getElementById('readFilterSelect');
            const actionFilterSelect = document.getElementById('actionFilterSelect');
            const savedViewsList = document.getElementById('savedViewsList');
            const saveCurrentViewBtn = document.getElementById('saveCurrentViewBtn');
            const selectAll = document.getElementById('inboxSelectAll');
            const selectionMeta = document.getElementById('selectionMeta');
            const actionSelect = document.getElementById('bulkActionSelect');
            const assigneeSelect = document.getElementById('bulkAssigneeSelect');
            const noteInput = document.getElementById('bulkNoteInput');

            const detailEmptyState = document.getElementById('detailEmptyState');
            const detailCard = document.getElementById('detailCard');
            const detailEyebrow = document.getElementById('detailEyebrow');
            const detailTitle = document.getElementById('detailTitle');
            const detailSummary = document.getElementById('detailSummary');
            const detailModule = document.getElementById('detailModule');
            const detailStatus = document.getElementById('detailStatus');
            const detailSla = document.getElementById('detailSla');
            const detailCreatedAt = document.getElementById('detailCreatedAt');
            const detailChips = document.getElementById('detailChips');
            const detailOpenLink = document.getElementById('detailOpenLink');

            const quickApproveForm = document.getElementById('quickApproveForm');
            const quickRejectForm = document.getElementById('quickRejectForm');
            const quickCommentForm = document.getElementById('quickCommentForm');
            const quickDelegateForm = document.getElementById('quickDelegateForm');

            const antiForgeryToken = document.querySelector('#bulkActionForm input[name="__RequestVerificationToken"]')?.value || '';
            const preferenceEndpoint = '/api/saved-views/WorkInbox';
            const storageKey = 'workInbox.savedViews.v2';
            const allowedIconPattern = /^bi-[a-z0-9-]+$/i;
            const allowedBadgePattern = /^(badge\s+)?text-bg-[a-z-]+$/i;
            const allowedPriorityPattern = /^inbox-priority-(high|normal)$/i;
            const state = {
                search: '',
                groupBy: 'none',
                module: '',
                status: '',
                read: '',
                action: '',
                selectedIds: new Set(),
                activeItemId: null
            };

            function uniqueValues(field) {
                return [...new Set(items.map(item => item[field]).filter(Boolean))].sort((a, b) => a.localeCompare(b));
            }

            function fillSelect(select, values) {
                values.forEach(value => {
                    const option = document.createElement('option');
                    option.value = value;
                    option.textContent = value;
                    select.appendChild(option);
                });
            }

            fillSelect(moduleFilterSelect, uniqueValues('module'));
            fillSelect(statusFilterSelect, uniqueValues('status'));

            function getSavedViews() {
                try {
                    return JSON.parse(localStorage.getItem(storageKey) || '[]');
                } catch {
                    return [];
                }
            }

            function setSavedViews(views) {
                localStorage.setItem(storageKey, JSON.stringify(views));
            }

            async function loadSavedViews() {
                try {
                    const response = await fetch(preferenceEndpoint, { credentials: 'same-origin' });
                    const payload = response.ok ? await response.json() : [];
                    const views = Array.isArray(payload)
                        ? payload.map(item => {
                            try {
                                return { name: item.name, state: JSON.parse(item.filterQueryJson || '{}') };
                            } catch {
                                return null;
                            }
                        }).filter(Boolean)
                        : [];

                    if (views.length > 0) {
                        setSavedViews(views);
                        return views;
                    }
                } catch {
                }

                return getSavedViews();
            }

            async function persistSavedViews(views) {
                setSavedViews(views);
                try {
                    await Promise.all(views.map(view => fetch(preferenceEndpoint, {
                        method: 'POST',
                        credentials: 'same-origin',
                        headers: {
                            'Content-Type': 'application/json',
                            ...(antiForgeryToken ? { RequestVerificationToken: antiForgeryToken } : {})
                        },
                        body: JSON.stringify({
                            name: view.name,
                            targetGridId: 'WorkInbox',
                            filterQueryJson: JSON.stringify(view.state),
                            columnLayoutJson: '[]'
                        })
                    })));
                } catch {
                }
            }

            function sanitizeIcon(icon) {
                return allowedIconPattern.test(icon || '') ? icon : 'bi-circle';
            }

            function sanitizeBadgeClass(className) {
                return allowedBadgePattern.test(className || '') ? className : 'text-bg-secondary';
            }

            function sanitizePriorityClass(className) {
                return allowedPriorityPattern.test(className || '') ? className : 'inbox-priority-normal';
            }

            function sanitizeUrl(url) {
                if (!url || typeof url !== 'string') return '#';
                return /^(\/|https?:\/\/)/i.test(url) ? url : '#';
            }

            function escapeHtml(value) {
                return `${value ?? ''}`
                    .replaceAll('&', '&')
                    .replaceAll('<', '<')
                    .replaceAll('>', '>')
                    .replaceAll('"', '"')
                    .replaceAll("'", '&#39;');
            }

            function renderSavedViews(views = getSavedViews()) {
                savedViewsList.replaceChildren();

                if (views.length === 0) {
                    const empty = document.createElement('span');
                    empty.className = 'saved-view-empty text-[11px] text-slate-400 dark:text-slate-500';
                    empty.textContent = 'هیچ نمای ذخیره‌شده‌ای وجود ندارد.';
                    savedViewsList.appendChild(empty);
                    return;
                }

                views.forEach(view => {
                    const chip = document.createElement('div');
                    chip.className = 'saved-view-chip inline-flex items-center gap-1';

                    const apply = document.createElement('button');
                    apply.type = 'button';
                    apply.className = 'px-3 py-1.5 border border-slate-200/80 dark:border-slate-700/60 rounded-xl text-[10px] font-semibold text-slate-600 dark:text-slate-400 bg-white dark:bg-slate-900 hover:bg-slate-50 dark:hover:bg-slate-800 transition-colors';
                    apply.textContent = view.name;
                    apply.addEventListener('click', () => {
                        Object.assign(state, view.state);
                        syncControls();
                        renderInbox();
                    });

                    const remove = document.createElement('button');
                    remove.type = 'button';
                    remove.className = 'text-rose-500 hover:text-rose-700 dark:text-rose-400 dark:hover:text-rose-300 ml-1';
                    const removeIcon = document.createElement('i');
                    removeIcon.className = 'bi bi-x-lg text-[10px]';
                    remove.appendChild(removeIcon);
                    remove.addEventListener('click', async () => {
                        const nextViews = getSavedViews().filter(item => item.name !== view.name);
                        setSavedViews(nextViews);
                        try {
                            await fetch(`${preferenceEndpoint}/${encodeURIComponent(view.name)}`, {
                                method: 'DELETE',
                                credentials: 'same-origin',
                                headers: antiForgeryToken ? { RequestVerificationToken: antiForgeryToken } : {}
                            });
                        } catch {
                        }
                        renderSavedViews(nextViews);
                    });

                    chip.append(apply, remove);
                    savedViewsList.appendChild(chip);
                });
            }

            function syncControls() {
                searchInput.value = state.search;
                groupBySelect.value = state.groupBy;
                moduleFilterSelect.value = state.module;
                statusFilterSelect.value = state.status;
                readFilterSelect.value = state.read;
                actionFilterSelect.value = state.action;
            }

            function passesFilters(item) {
                const searchText = `${item.searchText || ''}`.toLowerCase();
                if (state.search && !searchText.includes(state.search.toLowerCase())) return false;
                if (state.module && item.module !== state.module) return false;
                if (state.status && item.status !== state.status) return false;
                if (state.read === 'read' && !item.isRead) return false;
                if (state.read === 'unread' && item.isRead) return false;
                if (state.action === 'required' && !item.requiresAction) return false;
                if (state.action === 'high' && item.priority !== 'High') return false;
                return true;
            }

            function getGroupName(item) {
                switch (state.groupBy) {
                    case 'module': return item.groupLabel || item.module;
                    case 'status': return item.status;
                    case 'sla': return item.slaState;
                    case 'priority': return item.priority;
                    default: return 'همه موارد';
                }
            }

            function updateSelectionMeta() {
                selectionMeta.textContent = `${state.selectedIds.size} مورد انتخاب شده`;
            }

            function updateBulkFieldRules() {
                const value = actionSelect.value || '';
                assigneeSelect.required = value === 'delegate';
                assigneeSelect.disabled = value !== 'delegate';
                noteInput.required = value === 'reject';
            }

            function renderDetail(item) {
                if (!item) {
                    detailEmptyState.classList.remove('hidden');
                    detailCard.classList.add('hidden');
                    return;
                }

                detailEmptyState.classList.add('hidden');
                detailCard.classList.remove('hidden');

                detailEyebrow.textContent = item.detailCaption || item.type;
                detailTitle.textContent = item.detailTitle || item.title;
                detailSummary.textContent = item.detailSummary || item.description;
                detailModule.textContent = item.module;
                detailStatus.textContent = item.status;
                detailSla.textContent = item.slaText || '-';
                detailCreatedAt.textContent = item.createdAtText;
                detailOpenLink.href = sanitizeUrl(item.url);

                detailChips.replaceChildren();
                [item.priority, item.requiresAction ? 'نیازمند اقدام' : '', !item.isRead ? 'خوانده‌نشده' : '', item.isOverdue ? 'نقض سطح خدمت' : '']
                    .filter(Boolean)
                    .forEach(label => {
                        const chip = document.createElement('span');
                        chip.className = 'inline-flex items-center rounded-full px-2 py-0.5 text-[9px] font-medium bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400 border border-slate-200/60 dark:border-slate-700/40';
                        chip.textContent = label;
                        detailChips.appendChild(chip);
                    });

                const caseTaskActionArea = document.getElementById('caseTaskActionArea');
                const isCaseTask = item.id.startsWith('case-task-');

                if (isCaseTask && caseTaskActionArea) {
                    caseTaskActionArea.classList.remove('hidden');
                    const btnProcessList = caseTaskActionArea.querySelectorAll('.btn-process-task');
                    btnProcessList.forEach(btn => {
                        const newBtn = btn.cloneNode(true);
                        btn.parentNode.replaceChild(newBtn, btn);

                        newBtn.addEventListener('click', async (e) => {
                            e.preventDefault();
                            const action = newBtn.dataset.action;
                            const taskIdStr = item.id.replace('case-task-', '');
                            const taskId = parseInt(taskIdStr);

                            const originalText = newBtn.innerHTML;
                            newBtn.disabled = true;
                            newBtn.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> در حال اقدام...';

                            try {
                                const response = await fetch('/WorkInbox/ProcessTask', {
                                    method: 'POST',
                                    headers: {
                                        'Content-Type': 'application/x-www-form-urlencoded',
                                        'RequestVerificationToken': antiForgeryToken
                                    },
                                    body: `taskId=${taskId}&triggerAction=${action}`
                                });

                                const result = await response.json();
                                if (result.success) {
                                    Rhyno.toast(result.message, 'success');
                                    const rowToRemove = document.querySelector(`[data-item-id="${item.id}"]`);
                                    if (rowToRemove) {
                                        rowToRemove.remove();
                                    }
                                    const idx = items.findIndex(i => i.id === item.id);
                                    if (idx > -1) {
                                        items.splice(idx, 1);
                                    }
                                    renderInbox();
                                } else {
                                    Rhyno.toast('خطا: ' + result.message, 'error');
                                }
                            } catch (err) {
                                Rhyno.toast('بروز خطا در برقراری ارتباط با سرور.', 'error');
                            } finally {
                                newBtn.disabled = false;
                                newBtn.innerHTML = originalText;
                            }
                        });
                    });

                    quickApproveForm.classList.add('hidden');
                    quickRejectForm.classList.add('hidden');
                    quickCommentForm.classList.add('hidden');
                    quickDelegateForm.classList.add('hidden');
                } else {
                    if (caseTaskActionArea) {
                        caseTaskActionArea.classList.add('hidden');
                    }
                    quickApproveForm.classList.toggle('hidden', !item.canInlineApprove || !item.documentType || !item.documentId || !item.stepNumber);
                    quickRejectForm.classList.toggle('hidden', !item.canInlineReject || !item.documentType || !item.documentId || !item.stepNumber);
                    quickCommentForm.classList.toggle('hidden', !item.canInlineComment || !item.documentType || !item.documentId);
                    quickDelegateForm.classList.toggle('hidden', !item.canInlineDelegate || !item.workflowStepId);
                }

                if (item.documentType) {
                    document.getElementById('quickApproveDocumentType').value = item.documentType;
                    document.getElementById('quickRejectDocumentType').value = item.documentType;
                    document.getElementById('quickCommentDocumentType').value = item.documentType;
                }
                if (item.documentId) {
                    document.getElementById('quickApproveDocumentId').value = item.documentId;
                    document.getElementById('quickRejectDocumentId').value = item.documentId;
                    document.getElementById('quickCommentDocumentId').value = item.documentId;
                }
                if (item.stepNumber) {
                    document.getElementById('quickApproveStepNumber').value = item.stepNumber;
                    document.getElementById('quickRejectStepNumber').value = item.stepNumber;
                }
                document.getElementById('quickCommentWorkflowStepId').value = item.workflowStepId || '';
                document.getElementById('quickDelegateWorkflowStepId').value = item.workflowStepId || '';
            }

            function renderInbox() {
                const filteredItems = items.filter(passesFilters);
                const groups = filteredItems.reduce((acc, item) => {
                    const key = getGroupName(item);
                    acc[key] ??= [];
                    acc[key].push(item);
                    return acc;
                }, {});

                groupsHost.replaceChildren();

                if (filteredItems.length === 0) {
                    groupsHost.innerHTML = '<div class="p-8 text-center"><div class="mx-auto mb-3 grid h-14 w-14 place-items-center rounded-2xl bg-slate-100/80 dark:bg-slate-800/60"><i class="bi bi-search text-xl text-slate-300 dark:text-slate-600"></i></div><p class="text-xs text-slate-500 dark:text-slate-400">نتیجه‌ای برای جستجوی شما پیدا نشد.</p></div>';
                    renderDetail(null);
                    updateSelectionMeta();
                    return;
                }

                Object.entries(groups).forEach(([groupName, groupItems]) => {
                    const section = document.createElement('section');
                    section.className = 'inbox-group';

                    const header = document.createElement('div');
                    header.className = 'flex items-center justify-between px-3 py-2 bg-slate-100/60 dark:bg-slate-950/40 rounded-xl text-xs font-black text-slate-700 dark:text-slate-300 mb-2';
                    header.innerHTML = `<span>${escapeHtml(groupName)}</span><span class="font-mono text-[10px] bg-slate-200/60 dark:bg-slate-800 px-2 py-0.5 rounded-full">${groupItems.length} مورد</span>`;

                    const body = document.createElement('div');
                    body.className = 'inbox-list flex flex-col divide-y divide-slate-100 dark:divide-slate-800';

                    groupItems.forEach(item => {
                        const row = document.createElement('article');
                        row.className = `flex items-center justify-between p-4 border-b border-slate-100/80 dark:border-slate-800/60 hover:bg-slate-50/70 dark:hover:bg-slate-800/40 transition-colors ${sanitizePriorityClass(item.priorityCssClass)}${state.activeItemId === item.id ? ' bg-indigo-50/50 dark:bg-indigo-950/20' : ''}`;
                        row.dataset.itemId = item.id;

                        const checked = state.selectedIds.has(item.id) ? 'checked' : '';
                        const unread = !item.isRead ? '<span class="inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-[9px] font-medium bg-rose-50 text-rose-700 dark:bg-rose-950/40 dark:text-rose-400 border border-rose-200/60"><span class="status-dot status-dot-pulse bg-rose-500"></span>خوانده‌نشده</span>' : '<span class="inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-[9px] font-medium bg-slate-100 text-slate-500 dark:bg-slate-800/40 dark:text-slate-400 border border-slate-200/60"><span class="status-dot bg-slate-400"></span>خوانده‌شده</span>';
                        const overdue = item.isOverdue ? '<span class="inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-[9px] font-medium bg-rose-50 text-rose-700 dark:bg-rose-950/40 dark:text-rose-400 border border-rose-200/60"><span class="status-dot status-dot-pulse bg-rose-500"></span>نقض سطح خدمت</span>' : '';
                        const actionFlag = item.requiresAction ? '<span class="inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-[9px] font-medium bg-amber-50 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400 border border-amber-200/60"><span class="status-dot status-dot-pulse bg-amber-500"></span>نیازمند اقدام</span>' : '';

                        row.innerHTML = `
                            <input class="form-check-input inbox-item-check ml-3 rounded border-slate-300 dark:border-slate-700 text-indigo-600 focus:ring-indigo-500/20" type="checkbox" name="SelectedIds" value="${item.id}" ${checked} />
                            <button type="button" class="inbox-item-surface flex-1 flex items-center justify-between text-right border-0 bg-transparent p-0 outline-none w-full">
                                <div class="flex items-center gap-3 w-full">
                                    <span class="inbox-item-icon grid h-9 w-9 place-items-center rounded-xl bg-slate-100 dark:bg-slate-800 text-slate-500 dark:text-slate-400"><i class="bi ${sanitizeIcon(item.icon)}"></i></span>
                                    <div class="inbox-item-body min-w-0 flex-1">
                                        <div class="inbox-title-row flex items-baseline justify-between">
                                            <strong class="text-xs font-black text-slate-900 dark:text-white">${escapeHtml(item.title)}</strong>
                                            <small class="text-[10px] text-slate-400 dark:text-slate-500 mr-2">${escapeHtml(item.createdAtText)}</small>
                                        </div>
                                        <div class="inbox-description text-[11px] text-slate-500 dark:text-slate-400 truncate mt-0.5">${escapeHtml(item.description)}</div>
                                        <div class="inbox-meta flex items-center gap-1.5 flex-wrap mt-1 text-[10px]">
                                            <em class="font-bold not-italic bg-slate-100 dark:bg-slate-800 px-1.5 py-0.5 rounded-lg text-slate-600 dark:text-slate-300">${escapeHtml(item.module)}</em>
                                            <span class="badge ${sanitizeBadgeClass(item.statusCssClass)}">${escapeHtml(item.status)}</span>
                                            <span class="inline-flex items-center rounded-full px-2 py-0.5 text-[9px] font-medium bg-slate-100 text-slate-500 dark:bg-slate-800/40 dark:text-slate-400 border border-slate-200/60 dark:border-slate-700/40">${escapeHtml(item.slaText)}</span>
                                            ${unread}
                                            ${overdue}
                                            ${actionFlag}
                                        </div>
                                    </div>
                                </div>
                            </button>
                            <a class="inbox-open-icon text-slate-400 hover:text-indigo-600 dark:text-slate-500 dark:hover:text-indigo-400 mr-3 transition-colors" href="${sanitizeUrl(item.url)}" aria-label="Open ${escapeHtml(item.title)}">
                                <i class="bi bi-arrow-left-short text-lg"></i>
                            </a>`;

                        const checkbox = row.querySelector('.inbox-item-check');
                        checkbox.addEventListener('change', () => {
                            if (checkbox.checked) state.selectedIds.add(item.id);
                            else state.selectedIds.delete(item.id);
                            updateSelectionMeta();
                        });

                        row.querySelector('.inbox-item-surface').addEventListener('click', () => {
                            state.activeItemId = item.id;
                            renderInbox();
                            renderDetail(item);
                        });

                        body.appendChild(row);
                    });

                    section.append(header, body);
                    groupsHost.appendChild(section);
                });

                const activeItem = filteredItems.find(item => item.id === state.activeItemId) || filteredItems[0];
                state.activeItemId = activeItem?.id || null;
                renderDetail(activeItem || null);
                updateSelectionMeta();
            }

            selectAll.addEventListener('change', () => {
                const currentRows = groupsHost.querySelectorAll('.inbox-item-check');
                currentRows.forEach(check => {
                    check.checked = selectAll.checked;
                    if (selectAll.checked) state.selectedIds.add(check.value);
                    else state.selectedIds.delete(check.value);
                });
                updateSelectionMeta();
            });

            searchInput.addEventListener('input', () => { state.search = searchInput.value.trim(); renderInbox(); });
            groupBySelect.addEventListener('change', () => { state.groupBy = groupBySelect.value; renderInbox(); });
            moduleFilterSelect.addEventListener('change', () => { state.module = moduleFilterSelect.value; renderInbox(); });
            statusFilterSelect.addEventListener('change', () => { state.status = statusFilterSelect.value; renderInbox(); });
            readFilterSelect.addEventListener('change', () => { state.read = readFilterSelect.value; renderInbox(); });
            actionFilterSelect.addEventListener('change', () => { state.action = actionFilterSelect.value; renderInbox(); });
            actionSelect.addEventListener('change', updateBulkFieldRules);

            saveCurrentViewBtn.addEventListener('click', async () => {
                const name = prompt('نام نمای ذخیره‌شده را وارد کنید');
                if (!name) return;
                const views = getSavedViews().filter(item => item.name !== name);
                views.push({ name, state: { ...state, selectedIds: [] } });
                await persistSavedViews(views);
                renderSavedViews(views);
            });

            syncControls();
            updateBulkFieldRules();
            renderSavedViews(await loadSavedViews());
            renderInbox();
        })();

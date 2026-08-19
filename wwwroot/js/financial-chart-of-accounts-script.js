(function () {
        'use strict';
        const API_BASE = '/api/v1/finance/chart-of-accounts';
        let treeData = [];
        let flatData = [];
        let currentView = 'tree';
        let selectedNode = null;
        let expandedNodes = new Set();

        // ── Load data ─────────────────────────────────────────────
        async function loadTree(search) {
            const url = search ? `${API_BASE}/tree?search=${encodeURIComponent(search)}` : `${API_BASE}/tree`;
            const res = await fetch(url);
            if (!res.ok) return;
            treeData = await res.json();
            renderTree();
        }

        async function loadFlat(search) {
            const url = search ? `${API_BASE}/flat?search=${encodeURIComponent(search)}` : `${API_BASE}/flat`;
            const res = await fetch(url);
            if (!res.ok) return;
            flatData = await res.json();
            renderList();
        }

        // ── Tree view ─────────────────────────────────────────────
        function renderTree() {
            const container = document.getElementById('coaTreeView');
            if (!treeData.length) {
                container.innerHTML = '<div style="padding:20px;text-align:center;color:#aaa;">حسابی یافت نشد</div>';
                return;
            }
            container.innerHTML = treeData.map(node => renderTreeNode(node, 0)).join('');
        }

        function renderTreeNode(node, depth) {
            const hasChildren = node.children && node.children.length > 0;
            const isExpanded = expandedNodes.has(`${node.level}-${node.id}`);
            const badgeClass = `coa-badge-${node.level.toLowerCase()}`;
            const levelLabel = { Group: 'گروه', General: 'کل', Subsidiary: 'معین', Detailed: 'تفصیلی' }[node.level] || node.level;
            const toggleClass = hasChildren ? (isExpanded ? 'expanded' : 'collapsed') : 'leaf';
            const selectedClass = selectedNode && selectedNode.id === node.id && selectedNode.level === node.level ? ' selected' : '';
            const inactiveClass = !node.isActive ? ' inactive' : '';

            let html = `<div class="coa-tree-node">`;
            html += `<div class="coa-tree-row${selectedClass}${inactiveClass}" style="padding-right:${depth * 18}px" onclick="selectNode(${JSON.stringify(node).replace(/"/g, '"')})" ondblclick="toggleExpand('${node.level}-${node.id}')">`;
            html += `<span class="coa-tree-toggle ${toggleClass}" onclick="event.stopPropagation();toggleExpand('${node.level}-${node.id}')"></span>`;
            html += `<span class="coa-tree-code">${escapeHtml(node.code)}</span>`;
            html += `<span class="coa-tree-name">${escapeHtml(node.name)}</span>`;
            html += `<span class="coa-tree-badge ${badgeClass}">${levelLabel}</span>`;
            html += `</div>`;

            if (hasChildren && isExpanded) {
                html += `<div class="coa-tree-children">`;
                for (const child of node.children) {
                    html += renderTreeNode(child, depth + 1);
                }
                html += `</div>`;
            }

            html += `</div>`;
            return html;
        }

        // ── List view ─────────────────────────────────────────────
        function renderList() {
            const container = document.getElementById('coaListView');
            if (!flatData.length) {
                container.innerHTML = '<div style="padding:20px;text-align:center;color:#aaa;">حسابی یافت نشد</div>';
                return;
            }

            let html = `<table class="coa-list-table"><thead><tr>`;
            html += `<th>کد</th><th>نام</th><th>گروه</th><th>کل</th><th>ماهیت</th><th>وضعیت</th><th>کلید سیستم</th>`;
            html += `</tr></thead><tbody>`;
            for (const a of flatData) {
                const selectedClass = selectedNode && selectedNode.id === a.id && selectedNode.level === 'Subsidiary' ? ' selected' : '';
                const natureClass = a.nature === 'Debit' ? 'coa-nature-debit' : a.nature === 'Credit' ? 'coa-nature-credit' : '';
                html += `<tr class="${selectedClass}" onclick="selectFlatNode(${JSON.stringify(a).replace(/"/g, '"')})">`;
                html += `<td style="font-family:monospace;font-weight:700">${escapeHtml(a.code)}</td>`;
                html += `<td>${escapeHtml(a.name)}</td>`;
                html += `<td>${escapeHtml(a.accountGroup)}</td>`;
                html += `<td>${escapeHtml(a.generalAccount)}</td>`;
                html += `<td class="${natureClass}">${a.nature}</td>`;
                html += `<td>${a.isActive ? '✅' : '❌'}</td>`;
                html += `<td style="font-size:10px;color:#888">${escapeHtml(a.systemKey || '')}</td>`;
                html += `</tr>`;
            }
            html += `</tbody></table>`;
            container.innerHTML = html;
        }

        // ── Selection ──────────────────────────────────────────────
        window.selectNode = function (node) {
            selectedNode = node;
            document.getElementById('btnEditAccount').style.display = '';
            document.getElementById('btnAddChild').style.display = node.level !== 'Detailed' ? '' : 'none';
            document.getElementById('detailTitle').textContent = `${node.name} (${node.code})`;
            renderDetail(node);
            renderTree();
        };

        window.selectFlatNode = function (a) {
            selectedNode = { id: a.id, code: a.code, name: a.name, nature: a.nature, isActive: a.isActive, level: 'Subsidiary', systemKey: a.systemKey, isTemporary: a.isTemporary, allowsFloatingDetail: a.allowsFloatingDetail };
            document.getElementById('btnEditAccount').style.display = '';
            document.getElementById('btnAddChild').style.display = '';
            document.getElementById('detailTitle').textContent = `${a.name} (${a.code})`;
            renderFlatDetail(a);
            renderList();
        };

        function renderDetail(node) {
            const body = document.getElementById('detailBody');
            const natureFa = { Debit: 'بدهکار', Credit: 'بستانکار', NoControl: 'بدون کنترل' }[node.nature] || node.nature;
            const levelFa = { Group: 'گروه', General: 'کل', Subsidiary: 'معین', Detailed: 'تفصیلی' }[node.level] || node.level;

            let html = `<div class="coa-meta-grid">`;
            html += `<span class="coa-meta-label">سطح:</span><span class="coa-meta-value">${levelFa}</span>`;
            html += `<span class="coa-meta-label">کد:</span><span class="coa-meta-value" style="font-family:monospace">${escapeHtml(node.code)}</span>`;
            html += `<span class="coa-meta-label">نام:</span><span class="coa-meta-value">${escapeHtml(node.name)}</span>`;
            html += `<span class="coa-meta-label">ماهیت:</span><span class="coa-meta-value">${natureFa}</span>`;
            html += `<span class="coa-meta-label">وضعیت:</span><span class="coa-meta-value">${node.isActive ? 'فعال' : 'غیرفعال'}</span>`;
            if (node.systemKey) html += `<span class="coa-meta-label">کلید سیستم:</span><span class="coa-meta-value">${escapeHtml(node.systemKey)}</span>`;
            if (node.isTemporary !== undefined) html += `<span class="coa-meta-label">موقت:</span><span class="coa-meta-value">${node.isTemporary ? 'بله' : 'خیر'}</span>`;
            if (node.allowsFloatingDetail !== undefined) html += `<span class="coa-meta-label">تفصیلی شناور:</span><span class="coa-meta-value">${node.allowsFloatingDetail ? 'بله' : 'خیر'}</span>`;
            if (node.children) html += `<span class="coa-meta-label">تعداد زیرمجموعه:</span><span class="coa-meta-value">${node.children.length}</span>`;
            html += `</div>`;

            if (node.level === 'Subsidiary') {
                html += `<div style="margin-top:8px"><strong style="font-size:11px">فعالیت اخیر حساب</strong></div>`;
                html += `<div id="coaActivityArea" style="margin-top:4px"><div style="color:#aaa;font-size:10px">در حال بارگذاری...</div></div>`;
                body.innerHTML = html;
                loadActivity(node.id);
            } else {
                body.innerHTML = html;
            }
        }

        function renderFlatDetail(a) {
            const body = document.getElementById('detailBody');
            const natureFa = { Debit: 'بدهکار', Credit: 'بستانکار', NoControl: 'بدون کنترل' }[a.nature] || a.nature;

            let html = `<div class="coa-meta-grid">`;
            html += `<span class="coa-meta-label">سطح:</span><span class="coa-meta-value">معین</span>`;
            html += `<span class="coa-meta-label">کد:</span><span class="coa-meta-value" style="font-family:monospace">${escapeHtml(a.code)}</span>`;
            html += `<span class="coa-meta-label">نام:</span><span class="coa-meta-value">${escapeHtml(a.name)}</span>`;
            html += `<span class="coa-meta-label">ماهیت:</span><span class="coa-meta-value">${natureFa}</span>`;
            html += `<span class="coa-meta-label">گروه:</span><span class="coa-meta-value">${escapeHtml(a.accountGroup)}</span>`;
            html += `<span class="coa-meta-label">کل:</span><span class="coa-meta-value">${escapeHtml(a.generalAccount)}</span>`;
            html += `<span class="coa-meta-label">وضعیت:</span><span class="coa-meta-value">${a.isActive ? 'فعال' : 'غیرفعال'}</span>`;
            if (a.systemKey) html += `<span class="coa-meta-label">کلید سیستم:</span><span class="coa-meta-value">${escapeHtml(a.systemKey)}</span>`;
            html += `</div>`;
            html += `<div style="margin-top:8px"><strong style="font-size:11px">فعالیت اخیر حساب</strong></div>`;
            html += `<div id="coaActivityArea" style="margin-top:4px"><div style="color:#aaa;font-size:10px">در حال بارگذاری...</div></div>`;
            body.innerHTML = html;
            loadActivity(a.id);
        }

        async function loadActivity(subsidiaryAccountId) {
            try {
                const res = await fetch(`${API_BASE}/activity/${subsidiaryAccountId}?limit=15`);
                if (!res.ok) return;
                const data = await res.json();
                const area = document.getElementById('coaActivityArea');
                if (!area) return;
                if (!data.length) {
                    area.innerHTML = '<div style="color:#aaa;font-size:10px">فعالیتی ثبت نشده</div>';
                    return;
                }
                let html = `<table class="coa-activity-table"><thead><tr><th>تاریخ</th><th>شرح</th><th>بدهکار</th><th>بستانکار</th><th>وضعیت</th></tr></thead><tbody>`;
                for (const row of data) {
                    const dt = new Date(row.date).toLocaleDateString('fa-IR');
                    html += `<tr><td>${dt}</td><td>${escapeHtml(row.description || '')}</td><td style="color:#18af08">${row.debit.toLocaleString()}</td><td style="color:#ef404d">${row.credit.toLocaleString()}</td><td>${escapeHtml(row.status)}</td></tr>`;
                }
                html += `</tbody></table>`;
                area.innerHTML = html;
            } catch (e) { /* silent */ }
        }

        // ── View toggle ───────────────────────────────────────────
        window.setView = function (view) {
            currentView = view;
            document.getElementById('btnTreeView').classList.toggle('active', view === 'tree');
            document.getElementById('btnListView').classList.toggle('active', view === 'list');
            document.getElementById('coaTreeView').style.display = view === 'tree' ? '' : 'none';
            document.getElementById('coaListView').style.display = view === 'list' ? '' : 'none';
            if (view === 'list' && flatData.length === 0) loadFlat();
        };

        // ── Expand / Collapse ─────────────────────────────────────
        window.toggleExpand = function (key) {
            if (expandedNodes.has(key)) expandedNodes.delete(key);
            else expandedNodes.add(key);
            renderTree();
        };

        // ── Search ────────────────────────────────────────────────
        let searchTimer;
        document.getElementById('coaSearch').addEventListener('input', function () {
            clearTimeout(searchTimer);
            const val = this.value.trim();
            searchTimer = setTimeout(() => {
                if (currentView === 'tree') loadTree(val || undefined);
                else loadFlat(val || undefined);
            }, 300);
        });

        // ── Modal ─────────────────────────────────────────────────
        window.openAddModal = function (parentId, level) {
            document.getElementById('modalAccountId').value = '';
            document.getElementById('modalLevel').value = level || 'group';
            document.getElementById('modalParentId').value = parentId || '';
            document.getElementById('modalCode').value = '';
            document.getElementById('modalName').value = '';
            document.getElementById('modalNature').value = '';
            document.getElementById('modalIsActive').value = 'true';
            document.getElementById('modalIsTemporary').value = 'false';
            document.getElementById('modalAllowsFloating').value = 'true';
            document.getElementById('modalCodeError').textContent = '';
            document.getElementById('modalTitle').textContent = level === 'group' ? 'افزودن گروه حساب' : level === 'general' ? 'افزودن حساب کل' : level === 'subsidiary' ? 'افزودن حساب معین' : 'افزودن حساب تفصیلی';
            document.getElementById('modalCodeGroup').style.display = '';
            document.getElementById('modalActiveGroup').style.display = 'none';
            document.getElementById('modalSubFields').style.display = level === 'subsidiary' ? '' : 'none';
            document.getElementById('accountModal').classList.add('open');
        };

        window.openAddChildModal = function () {
            if (!selectedNode) return;
            const childLevel = { Group: 'general', General: 'subsidiary', Subsidiary: 'detailed' }[selectedNode.level];
            if (!childLevel) return;
            openAddModal(selectedNode.id, childLevel);
        };

        window.openEditModal = function () {
            if (!selectedNode) return;
            document.getElementById('modalAccountId').value = selectedNode.id;
            document.getElementById('modalLevel').value = selectedNode.level.toLowerCase();
            document.getElementById('modalCode').value = selectedNode.code || '';
            document.getElementById('modalName').value = selectedNode.name || '';
            document.getElementById('modalNature').value = selectedNode.nature || '';
            document.getElementById('modalIsActive').value = selectedNode.isActive ? 'true' : 'false';
            document.getElementById('modalIsTemporary').value = selectedNode.isTemporary ? 'true' : 'false';
            document.getElementById('modalAllowsFloating').value = selectedNode.allowsFloatingDetail !== false ? 'true' : 'false';
            document.getElementById('modalCodeError').textContent = '';
            document.getElementById('modalTitle').textContent = 'ویرایش حساب';
            document.getElementById('modalCodeGroup').style.display = selectedNode.level === 'Detailed' ? 'none' : '';
            document.getElementById('modalActiveGroup').style.display = '';
            document.getElementById('modalSubFields').style.display = selectedNode.level === 'Subsidiary' ? '' : 'none';
            document.getElementById('accountModal').classList.add('open');
        };

        window.closeModal = function () {
            document.getElementById('accountModal').classList.remove('open');
        };

        window.submitAccount = async function () {
            const id = document.getElementById('modalAccountId').value;
            const level = document.getElementById('modalLevel').value;
            const code = document.getElementById('modalCode').value.trim();
            const name = document.getElementById('modalName').value.trim();
            const nature = document.getElementById('modalNature').value;
            const isActive = document.getElementById('modalIsActive').value === 'true';
            const isTemporary = document.getElementById('modalIsTemporary').value === 'true';
            const allowsFloating = document.getElementById('modalAllowsFloating').value === 'true';
            const parentId = document.getElementById('modalParentId').value;

            document.getElementById('modalCodeError').textContent = '';

            if (!name) { alert('نام حساب الزامی است.'); return; }
            if (!id && level !== 'detailed' && !code) { alert('کد حساب الزامی است.'); return; }

            try {
                let res;
                if (id) {
                    res = await fetch(`${API_BASE}/${id}`, {
                        method: 'PUT',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ code: code || undefined, name, nature: nature || undefined, isActive, isTemporary, allowsFloatingDetail: allowsFloating })
                    });
                } else {
                    res = await fetch(API_BASE, {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ code, name, nature: nature || undefined, level, parentId: parentId ? parseInt(parentId) : undefined, isTemporary, allowsFloatingDetail: allowsFloating })
                    });
                }

                const result = await res.json();
                if (!res.ok || !result.success) {
                    document.getElementById('modalCodeError').textContent = result.message || 'خطا در ذخیره‌سازی';
                    return;
                }

                closeModal();
                selectedNode = null;
                document.getElementById('detailBody').innerHTML = '<div class="coa-detail-empty">یک حساب را از لیست انتخاب کنید</div>';
                document.getElementById('btnEditAccount').style.display = 'none';
                document.getElementById('btnAddChild').style.display = 'none';
                document.getElementById('detailTitle').textContent = 'جزئیات حساب';
                expandedNodes.clear();
                await loadTree();
                if (currentView === 'list') await loadFlat();
            } catch (e) {
                alert('خطا در ارتباط با سرور: ' + e.message);
            }
        };

        // ── Helpers ────────────────────────────────────────────────
        function escapeHtml(text) {
            if (!text) return '';
            const d = document.createElement('div');
            d.textContent = text;
            return d.innerHTML;
        }

        // ── Init ──────────────────────────────────────────────────
        // Expand all groups by default
        expandedNodes.add('Group-1');
        expandedNodes.add('Group-2');
        expandedNodes.add('Group-3');
        expandedNodes.add('Group-4');
        expandedNodes.add('Group-5');
        loadTree();
    })();

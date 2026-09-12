/**
 * Rhyno Contracts — Visual Diff Viewer for Contract Revisions
 * Compares two snapshots or version vs. current live state using jsdiff (word-level)
 */
(function () {
    'use strict';

    var modal = document.getElementById('compareVersionsModal');
    if (!modal) return;

    var selectA = document.getElementById('diffVersionA');
    var selectB = document.getElementById('diffVersionB');
    var btnSideBySide = document.getElementById('btnDiffSideBySide');
    var btnInline = document.getElementById('btnDiffInline');
    var btnRefresh = document.getElementById('btnRefreshDiff');
    var btnClose = document.getElementById('compareModalClose');
    var btnCloseFooter = document.getElementById('compareModalCloseBtn');

    var loadingEl = document.getElementById('diffLoading');
    var emptyStateEl = document.getElementById('diffEmptyState');
    var sideBySideContainer = document.getElementById('diffSideBySideContainer');
    var inlineContainer = document.getElementById('diffInlineContainer');

    var contentA = document.getElementById('contentVersionA');
    var contentB = document.getElementById('contentVersionB');
    var contentInline = document.getElementById('contentInline');

    var headerA = document.getElementById('headerVersionA');
    var headerB = document.getElementById('headerVersionB');
    var metaA = document.getElementById('metaVersionA');
    var metaB = document.getElementById('metaVersionB');

    var statAdded = document.getElementById('statWordsAdded');
    var statRemoved = document.getElementById('statWordsRemoved');

    var currentContractId = null;
    var cachedSnapshotsList = [];
    var versionCache = {}; // versionId -> version data
    var viewMode = 'sideBySide'; // 'sideBySide' or 'inline'

    // Clean html to plain text blocks preserving paragraph breaks
    function extractTextContent(htmlOrText) {
        if (!htmlOrText) return '';
        // If it looks like plain text without tags, normalize line breaks
        if (!/<[a-z][\s\S]*>/i.test(htmlOrText)) {
            return htmlOrText.trim();
        }
        var temp = document.createElement('div');
        // Replace block tags with newline placeholders to preserve paragraph structure
        var formatted = htmlOrText
            .replace(/<\/(p|div|tr|h[1-6]|li)>/gi, '\n')
            .replace(/<br\s*[\/]?>/gi, '\n')
            .replace(/&nbsp;/gi, ' ');
        temp.innerHTML = formatted;
        var text = temp.textContent || temp.innerText || '';
        // Normalize consecutive blank lines to double newlines
        return text.replace(/\r\n/g, '\n').replace(/\n{3,}/g, '\n\n').trim();
    }

    function escapeHtml(str) {
        if (!str) return '';
        return str
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;');
    }

    function openModal() {
        modal.classList.remove('hidden');
        modal.classList.add('flex');
        document.body.style.overflow = 'hidden';
    }

    function closeModal() {
        modal.classList.add('hidden');
        modal.classList.remove('flex');
        document.body.style.overflow = '';
    }

    if (btnClose) btnClose.addEventListener('click', closeModal);
    if (btnCloseFooter) btnCloseFooter.addEventListener('click', closeModal);
    modal.addEventListener('click', function (e) {
        if (e.target === modal) closeModal();
    });

    // View mode switching
    function setViewMode(mode) {
        viewMode = mode;
        if (mode === 'sideBySide') {
            btnSideBySide.classList.add('bg-white', 'dark:bg-slate-900', 'text-teal-600', 'dark:text-teal-400', 'shadow-xs');
            btnSideBySide.classList.remove('text-slate-700', 'dark:text-slate-300');

            btnInline.classList.remove('bg-white', 'dark:bg-slate-900', 'text-teal-600', 'dark:text-teal-400', 'shadow-xs');
            btnInline.classList.add('text-slate-700', 'dark:text-slate-300');

            sideBySideContainer.classList.remove('hidden');
            inlineContainer.classList.add('hidden');
        } else {
            btnInline.classList.add('bg-white', 'dark:bg-slate-900', 'text-teal-600', 'dark:text-teal-400', 'shadow-xs');
            btnInline.classList.remove('text-slate-700', 'dark:text-slate-300');

            btnSideBySide.classList.remove('bg-white', 'dark:bg-slate-900', 'text-teal-600', 'dark:text-teal-400', 'shadow-xs');
            btnSideBySide.classList.add('text-slate-700', 'dark:text-slate-300');

            inlineContainer.classList.remove('hidden');
            sideBySideContainer.classList.add('hidden');
        }
    }

    if (btnSideBySide) btnSideBySide.addEventListener('click', function () { setViewMode('sideBySide'); });
    if (btnInline) btnInline.addEventListener('click', function () { setViewMode('inline'); });

    // Load available snapshots for the contract
    function loadSnapshotsList(contractId, defaultA, defaultB) {
        currentContractId = contractId;
        selectA.innerHTML = '<option value="">در حال بارگذاری لیست نسخه‌ها...</option>';
        selectB.innerHTML = '<option value="">در حال بارگذاری لیست نسخه‌ها...</option>';

        fetch('/Contracts/' + contractId + '/VersionSnapshots')
            .then(function (res) { return res.json(); })
            .then(function (res) {
                if (!res.success || !res.data) {
                    selectA.innerHTML = '<option value="">خطا در دریافت نسخه‌ها</option>';
                    selectB.innerHTML = '<option value="">خطا در دریافت نسخه‌ها</option>';
                    return;
                }
                cachedSnapshotsList = res.data;
                populateDropdowns(defaultA, defaultB);
            })
            .catch(function () {
                selectA.innerHTML = '<option value="">خطا در ارتباط با سرور</option>';
                selectB.innerHTML = '<option value="">خطا در ارتباط با سرور</option>';
            });
    }

    function populateDropdowns(defaultA, defaultB) {
        var optionsHtmlA = '';
        var optionsHtmlB = '';

        cachedSnapshotsList.forEach(function (item) {
            optionsHtmlA += '<option value="' + item.id + '">' + escapeHtml(item.label) + '</option>';
            optionsHtmlB += '<option value="' + item.id + '">' + escapeHtml(item.label) + '</option>';
        });

        selectA.innerHTML = optionsHtmlA;
        selectB.innerHTML = optionsHtmlB;

        // Auto-select sensible defaults:
        // defaultB: current live (0) or second argument
        // defaultA: the most recent stored snapshot or first argument
        if (defaultB !== undefined && defaultB !== null) {
            selectB.value = defaultB;
        } else {
            selectB.value = 0; // live current
        }

        if (defaultA !== undefined && defaultA !== null) {
            selectA.value = defaultA;
        } else {
            // Pick first non-zero item if available
            var firstStored = cachedSnapshotsList.find(function (x) { return x.id !== 0; });
            if (firstStored) {
                selectA.value = firstStored.id;
            } else {
                selectA.value = 0;
            }
        }

        performDiff();
    }

    function fetchVersionContent(versionId) {
        if (versionCache[versionId]) {
            return Promise.resolve(versionCache[versionId]);
        }
        return fetch('/Contracts/' + currentContractId + '/VersionContent?versionId=' + versionId)
            .then(function (res) { return res.json(); })
            .then(function (res) {
                if (res.success) {
                    versionCache[versionId] = res;
                    return res;
                }
                throw new Error(res.message || 'خطا در بارگذاری محتوا');
            });
    }

    function performDiff() {
        var valA = selectA.value;
        var valB = selectB.value;

        if (valA === '' || valB === '') {
            emptyStateEl.classList.remove('hidden');
            sideBySideContainer.classList.add('hidden');
            inlineContainer.classList.add('hidden');
            statAdded.innerText = '+0 کلمه افزوده';
            statRemoved.innerText = '-0 کلمه حذف شده';
            return;
        }

        emptyStateEl.classList.add('hidden');
        loadingEl.classList.remove('hidden');

        Promise.all([fetchVersionContent(valA), fetchVersionContent(valB)])
            .then(function (results) {
                loadingEl.classList.add('hidden');
                var vA = results[0];
                var vB = results[1];

                renderDiff(vA, vB);
            })
            .catch(function (err) {
                loadingEl.classList.add('hidden');
                alert(err.message || 'خطا در محاسبه تفاوت نسخه‌ها');
            });
    }

    function countWords(str) {
        if (!str) return 0;
        var words = str.trim().split(/\s+/);
        return words.filter(function (w) { return w.length > 0; }).length;
    }

    function renderDiff(vA, vB) {
        var textA = extractTextContent(vA.bodyHtml || vA.bodyText || '');
        var textB = extractTextContent(vB.bodyHtml || vB.bodyText || '');

        // Update headers and metadata
        if (headerA) headerA.innerHTML = '<i class="bi bi-file-earmark ml-1 text-slate-400"></i>' + escapeHtml(vA.title || 'نسخه اول');
        if (headerB) headerB.innerHTML = '<i class="bi bi-file-earmark-text ml-1 text-teal-500"></i>' + escapeHtml(vB.title || 'نسخه دوم');

        var metaStrA = (vA.versionId === 0 ? 'نسخه جاری' : 'v' + vA.versionLabel) + ' | ' + (vA.startDatePersian || '-') + (vA.totalAmount ? ' | ' + Number(vA.totalAmount).toLocaleString('fa-IR') + ' تومان' : '');
        var metaStrB = (vB.versionId === 0 ? 'نسخه جاری' : 'v' + vB.versionLabel) + ' | ' + (vB.startDatePersian || '-') + (vB.totalAmount ? ' | ' + Number(vB.totalAmount).toLocaleString('fa-IR') + ' تومان' : '');

        if (metaA) metaA.innerText = metaStrA;
        if (metaB) metaB.innerText = metaStrB;

        // Compute diff using jsdiff (Diff.diffWordsWithSpace)
        var diffChunks = [];
        if (window.Diff && typeof window.Diff.diffWordsWithSpace === 'function') {
            diffChunks = window.Diff.diffWordsWithSpace(textA, textB);
        } else if (window.Diff && typeof window.Diff.diffWords === 'function') {
            diffChunks = window.Diff.diffWords(textA, textB);
        } else {
            // Fallback if Diff is missing
            fallbackSimpleDiff(textA, textB);
            return;
        }

        var wordsAdded = 0;
        var wordsRemoved = 0;

        var inlineHtml = '';
        var sideA_Html = '';
        var sideB_Html = '';

        diffChunks.forEach(function (part) {
            var escaped = escapeHtml(part.value);

            if (part.added) {
                wordsAdded += countWords(part.value);
                // Highlight additions in green
                var insSpan = '<ins class="diff-ins bg-emerald-100 text-emerald-800 dark:bg-emerald-950/50 dark:text-emerald-300 px-1 py-0.5 rounded font-semibold no-underline">' + escaped + '</ins>';
                inlineHtml += insSpan;
                sideB_Html += insSpan;
            } else if (part.removed) {
                wordsRemoved += countWords(part.value);
                // Highlight deletions in red strikethrough
                var delSpan = '<del class="diff-del bg-rose-100 text-rose-800 dark:bg-rose-950/50 dark:text-rose-300 px-1 py-0.5 rounded font-semibold line-through">' + escaped + '</del>';
                inlineHtml += delSpan;
                sideA_Html += delSpan;
            } else {
                inlineHtml += escaped;
                sideA_Html += escaped;
                sideB_Html += escaped;
            }
        });

        if (statAdded) statAdded.innerText = '+' + wordsAdded + ' کلمه افزوده';
        if (statRemoved) statRemoved.innerText = '-' + wordsRemoved + ' کلمه حذف شده';

        if (contentInline) {
            contentInline.innerHTML = inlineHtml || '<span class="text-slate-400">هیچ متنی در این نسخه ثبت نشده است.</span>';
        }
        if (contentA) {
            contentA.innerHTML = sideA_Html || '<span class="text-slate-400">متن خالی است.</span>';
        }
        if (contentB) {
            contentB.innerHTML = sideB_Html || '<span class="text-slate-400">متن خالی است.</span>';
        }

        setViewMode(viewMode);
    }

    function fallbackSimpleDiff(textA, textB) {
        if (contentInline) contentInline.innerText = textB;
        if (contentA) contentA.innerText = textA;
        if (contentB) contentB.innerText = textB;
        setViewMode(viewMode);
    }

    if (selectA) selectA.addEventListener('change', performDiff);
    if (selectB) selectB.addEventListener('change', performDiff);
    if (btnRefresh) btnRefresh.addEventListener('click', performDiff);

    // Global launcher function accessible from tabs and buttons
    window.openCompareVersions = function (contractId, versionAId, versionBId) {
        openModal();
        loadSnapshotsList(contractId, versionAId, versionBId);
    };

})();

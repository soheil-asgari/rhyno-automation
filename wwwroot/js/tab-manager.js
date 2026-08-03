/**
 * ─────────────────────────────────────────────────────────────────
 *  Rhyno ERP — Persistent Shell Tab Manager
 *  Multi-Tab Enterprise Shell (Rahkaran-inspired)
 *
 *  Implements:
 *    • Centralized tab store (localStorage-persisted)
 *    • Tab bar rendering below the main header
 *    • iframe-based tab content (state retention — Option B)
 *    • Loading spinner / progress bar during tab loads
 *    • Fixed non-closeable default tab (میز کار)
 *    • Deep-link seeding: opening /Some/Page directly opens it in a tab
 *    • Hides the static server-rendered body once the shell is active
 *
 *  Vanilla JS — no external dependencies.
 * ─────────────────────────────────────────────────────────────────
 */
(function () {
    'use strict';

    var STORAGE_KEY = 'rhyno.tabs.v1';
    var DEFAULT_TAB = {
        id: 'home',
        title: 'میز کار / Rhyno Dashboard',
        url: '/',
        icon: 'bi-speedometer2',
        fixed: true
    };

    // ── State ─────────────────────────────────────────────────────
    var tabs = [];
    var activeTabId = null;
    var initialized = false;

    // ── DOM refs (resolved lazily) ────────────────────────────────
    var elTabBar = null;
    var elWorkspace = null;

    // ── Utils ─────────────────────────────────────────────────────
    function normalizeUrl(url) {
        if (!url) return url;
        // Strip a trailing _tab param if re-opening an already-normalized URL
        var sep = url.indexOf('?') >= 0 ? '&' : '?';
        var u = new URL(url, window.location.origin);
        u.searchParams.delete('_tab');
        var clean = u.pathname + u.search + u.hash;
        return clean;
    }

    function buildTabUrl(url) {
        var clean = normalizeUrl(url);
        var sep = clean.indexOf('?') >= 0 ? '&' : '?';
        return clean + sep + '_tab=1';
    }

    function makeId(url) {
        var clean = normalizeUrl(url);
        return clean.split('#')[0].replace(/\/+$/, '') || '/';
    }

    function escapeHtml(str) {
        // NOTE: \x26 = '&' — written as hex escapes so HTML-entity auto-formatters
        // do not decode the entity literals in the source file.
        return String(str == null ? '' : str)
            .replace(/&/g, '\x26amp;')
            .replace(/</g, '\x26lt;')
            .replace(/>/g, '\x26gt;')
            .replace(/"/g, '\x26quot;')
            .replace(/'/g, '\x26#39;');
    }

    function faNum(n) {
        try {
            return Number(n || 0).toLocaleString('fa-IR');
        } catch (e) {
            return n;
        }
    }

    function currentPath() {
        return normalizeUrl(window.location.pathname + window.location.search + window.location.hash) || '/';
    }

    function isShellPage() {
        // Pages that must NOT boot the shell:
        //  - tab-content iframes (_tab=1 → _TabLayout has no tab containers)
        //  - login / identity pages (Layout = null)
        var q = window.location.search || '';
        if (q.indexOf('_tab=') >= 0) return false;
        return !!document.getElementById('rhynoTabBar') && !!document.getElementById('rhynoWorkspace');
    }

    function persist() {
        try {
            var data = {
                activeTabId: activeTabId,
                tabs: tabs.map(function (t) {
                    return { id: t.id, title: t.title, url: t.url, icon: t.icon, fixed: !!t.fixed };
                })
            };
            localStorage.setItem(STORAGE_KEY, JSON.stringify(data));
        } catch (e) { /* private mode — ignore */ }
    }

    // ── Store actions ─────────────────────────────────────────────
    function openTab(url, title, icon, opts) {
        opts = opts || {};
        url = normalizeUrl(url);
        if (!url) return;

        var id = opts.id || makeId(url);
        var existing = tabs.find(function (t) { return t.id === id; });

        if (existing) {
            setActive(existing.id);
            return existing;
        }

        var tab = {
            id: id,
            title: title || id,
            url: url,
            icon: icon || 'bi-file-earmark',
            fixed: !!opts.fixed,
            isActive: false
        };
        tabs.push(tab);
        setActive(tab.id);
        return tab;
    }

    function setActive(id) {
        if (activeTabId === id) {
            render();
            return;
        }
        activeTabId = id;
        tabs.forEach(function (t) { t.isActive = (t.id === id); });
        persist();
        render();
    }

    function closeTab(id) {
        var idx = tabs.findIndex(function (t) { return t.id === id; });
        if (idx < 0) return;
        if (tabs[idx].fixed) return; // default tab cannot be closed

        var wasActive = tabs[idx].isActive;
        tabs.splice(idx, 1);

        if (wasActive) {
            // Activate the neighbour (prefer the right/previous in RTL order = idx-1)
            var next = tabs[idx - 1] || tabs[idx] || null;
            if (next) {
                activeTabId = next.id;
                tabs.forEach(function (t) { t.isActive = (t.id === next.id); });
            } else {
                // No tabs left — restore default
                tabs = [Object.assign({}, DEFAULT_TAB, { isActive: true })];
                activeTabId = tabs[0].id;
            }
        }

        persist();
        render();
    }

    // ── Rendering ─────────────────────────────────────────────────
    function render() {
        if (!initialized) return;
        if (!elTabBar) elTabBar = document.getElementById('rhynoTabBar');
        if (!elWorkspace) elWorkspace = document.getElementById('rhynoWorkspace');
        if (!elTabBar || !elWorkspace) return;

        renderTabBar();
        renderWorkspace();
        syncSidebarActiveState();
    }

    function renderTabBar() {
        if (tabs.length === 0) {
            elTabBar.parentElement.style.display = 'none';
            return;
        }
        elTabBar.parentElement.style.display = '';

        var html = '';
        tabs.forEach(function (tab, index) {
            var isActive = tab.id === activeTabId;
            var closeBtn = tab.fixed
                ? ''
                : '<button type="button" class="rhyno-tab-close" data-tab-close="' + escapeHtml(tab.id) + '" title="بستن تب" aria-label="بستن تب">' +
                  '<svg viewBox="0 0 12 12" width="10" height="10" fill="none"><path d="M3 3L9 9M9 3L3 9" stroke="currentColor" stroke-width="1.4" stroke-linecap="round"/></svg>' +
                  '</button>';

            html += '<div class="rhyno-tab ' + (isActive ? 'active' : '') + (tab.fixed ? ' fixed' : '') + '" ' +
                    'data-tab-id="' + escapeHtml(tab.id) + '" data-tab-index="' + index + '" role="tab" aria-selected="' + (isActive ? 'true' : 'false') + '">' +
                    '<i class="bi ' + escapeHtml(tab.icon) + ' rhyno-tab-icon"></i>' +
                    '<span class="rhyno-tab-title">' + escapeHtml(tab.title) + '</span>' +
                    closeBtn +
                    '</div>';
        });

        elTabBar.innerHTML = html;
    }

    function renderWorkspace() {
        // Removes iframes for closed tabs; keeps DOM node per open tab (state retention)
        var existingFrames = {};
        [].slice.call(elWorkspace.querySelectorAll('iframe.rhyno-tab-frame')).forEach(function (frame) {
            existingFrames[frame.getAttribute('data-tab-id')] = frame;
        });

        var usedIds = {};
        tabs.forEach(function (tab) {
            usedIds[tab.id] = true;

            var frame = existingFrames[tab.id];
            if (!frame) {
                frame = document.createElement('iframe');
                frame.className = 'rhyno-tab-frame';
                frame.setAttribute('data-tab-id', tab.id);
                frame.setAttribute('data-tab-url', tab.url);
                frame.setAttribute('frameborder', '0');
                frame.setAttribute('allowfullscreen', 'true');
                elWorkspace.appendChild(frame);

                // Loading state
                var loadingEl = document.createElement('div');
                loadingEl.className = 'rhyno-tab-loading';
                loadingEl.innerHTML = '<div class="rhyno-spinner"></div><span>در حال بارگذاری...</span>';
                frame.parentNode.insertBefore(loadingEl, frame);

                frame.addEventListener('load', function () {
                    if (loadingEl && loadingEl.parentNode) loadingEl.parentNode.removeChild(loadingEl);
                    frame.classList.remove('rhyno-tab-loading-frame');
                });

                frame.src = buildTabUrl(tab.url);
                frame.classList.add('rhyno-tab-loading-frame');
            }

            // Visibility: only the active tab is displayed
            var isActive = tab.id === activeTabId;
            frame.style.display = isActive ? '' : 'none';
            frame.classList.toggle('rhyno-tab-frame-active', isActive);
        });

        // Remove frames whose tab no longer exists
        Object.keys(existingFrames).forEach(function (id) {
            if (!usedIds[id]) {
                var frame = existingFrames[id];
                var loading = frame.previousElementSibling;
                if (loading && loading.classList && loading.classList.contains('rhyno-tab-loading')) {
                    loading.parentNode.removeChild(loading);
                }
                frame.parentNode.removeChild(frame);
            }
        });
    }

    function syncSidebarActiveState() {
        // Highlight the sidebar item matching the active tab URL
        var activeTab = tabs.find(function (t) { return t.id === activeTabId; });
        document.querySelectorAll('#appSidebar a.menu-item, .rhyno-sidebar a.menu-item').forEach(function (a) {
            var href = a.getAttribute('href') || '';
            var tabUrl = activeTab ? (activeTab.url || '') : '';
            if (!tabUrl) return;
            var cleanHref = normalizeUrl(href);
            var isMatch = cleanHref === tabUrl ||
                          (tabUrl !== '/' && cleanHref.indexOf(tabUrl) === 0);
            a.classList.toggle('active', isMatch);
        });
    }

    // ── Event wiring ──────────────────────────────────────────────
    function wireEvents() {
        // Sidebar menu item clicks → open tab
        document.addEventListener('click', function (e) {
            var link = e.target.closest('a.menu-item[href]');
            if (!link) return;
            var href = link.getAttribute('href');
            if (!href || href === '#' || href.indexOf('javascript:') === 0) return;
            // Only intercept same-origin, non-download, no-target links
            if (link.hasAttribute('download') || link.hasAttribute('target')) return;

            e.preventDefault();
            openTabFromLink(link);
        });

        // Tab bar: activate / close
        var barHost = document.getElementById('rhynoTabBarHost');
        if (barHost) {
            barHost.addEventListener('click', function (e) {
                var closeBtn = e.target.closest('[data-tab-close]');
                if (closeBtn) {
                    e.stopPropagation();
                    closeTab(closeBtn.getAttribute('data-tab-close'));
                    return;
                }

                var tabEl = e.target.closest('.rhyno-tab[data-tab-id]');
                if (tabEl) {
                    setActive(tabEl.getAttribute('data-tab-id'));
                }
            });
        }
    }

    function openTabFromLink(link) {
        var href = link.getAttribute('href');
        var title = '';
        var icon = '';

        var titleEl = link.querySelector('.link-text, .tab-link-text, span');
        if (titleEl) title = titleEl.textContent.trim();
        if (!title) title = link.textContent.trim();

        var iconEl = link.querySelector('i.bi, .menu-icon');
        if (iconEl) {
            var cls = (iconEl.className || '').split(/\s+/).filter(function (c) { return c.indexOf('bi-') === 0; });
            icon = cls[0] || 'bi-file-earmark';
        }

        openTab(href, title, icon);
    }

    // ── Init ──────────────────────────────────────────────────────
    function seedFromCurrentUrl() {
        // If the user deep-links to a real page (not just "/"), open it as the
        // active tab alongside the fixed default tab.
        var path = currentPath();
        if (!path || path === '/' ) return;

        var defaultTab = tabs.find(function (t) { return t.fixed; });
        var alreadyOpen = tabs.some(function (t) { return t.id === makeId(path); });
        if (alreadyOpen) return;

        // Try to grab a nice title/icon from the sidebar, falling back to path
        var title = path;
        var icon = 'bi-file-earmark';
        document.querySelectorAll('#appSidebar a.menu-item[href]').forEach(function (a) {
            var cleanHref = normalizeUrl(a.getAttribute('href') || '');
            if (cleanHref === path || (path !== '/' && cleanHref.indexOf(path) === 0)) {
                var t = a.querySelector('span, .link-text');
                if (t && t.textContent.trim()) title = t.textContent.trim();
                var i = a.querySelector('i.bi, .menu-icon');
                if (i) {
                    var cls = (i.className || '').split(/\s+/).filter(function (c) { return c.indexOf('bi-') === 0; });
                    if (cls[0]) icon = cls[0];
                }
            }
        });

        // Home path always maps to the fixed default tab
        if (path === DEFAULT_TAB.url || makeId(path) === DEFAULT_TAB.id) {
            activeTabId = defaultTab ? defaultTab.id : DEFAULT_TAB.id;
            return;
        }

        tabs.push({
            id: makeId(path),
            title: title,
            url: path,
            icon: icon,
            fixed: false
        });
        activeTabId = makeId(path);
        tabs.forEach(function (t) { t.isActive = (t.id === activeTabId); });
    }

    function restoreOrInit() {
        var restored = null;
        try { restored = JSON.parse(localStorage.getItem(STORAGE_KEY)); } catch (e) { restored = null; }

        if (restored && Array.isArray(restored.tabs) && restored.tabs.length > 0) {
            // Always ensure the default tab exists first
            var hasDefault = restored.tabs.some(function (t) { return t.fixed; });
            if (!hasDefault) {
                restored.tabs.unshift(Object.assign({}, DEFAULT_TAB));
            }
            tabs = restored.tabs;
            activeTabId = restored.activeTabId;
            if (!tabs.some(function (t) { return t.id === activeTabId; })) {
                activeTabId = tabs[0].id;
            }
            tabs.forEach(function (t) { t.isActive = (t.id === activeTabId); });

            // Deep-link seeding: the restored session already has tabs, but if the
            // current URL is a distinct page not yet open, activate it too.
            seedFromCurrentUrl();
        } else {
            tabs = [Object.assign({}, DEFAULT_TAB, { isActive: true })];
            activeTabId = DEFAULT_TAB.id;
            // First visit (or cleared storage): if deep-linked, open that page in a tab.
            seedFromCurrentUrl();
            // If seeding changed the active tab, keep the default as a background tab.
            if (activeTabId !== DEFAULT_TAB.id) {
                tabs[0].isActive = false;
            }
        }

        persist();
    }

    function init() {
        if (initialized) return;
        if (!isShellPage()) return; // login pages & tab-content iframes skip

        elTabBar = document.getElementById('rhynoTabBar');
        elWorkspace = document.getElementById('rhynoWorkspace');
        if (!elTabBar || !elWorkspace) return;

        // Activate the persistent shell: hides the static @RenderBody() region
        document.body.classList.add('rhyno-shell-active');

        restoreOrInit();
        initialized = true;
        wireEvents();
        render();
    }

    // ── Public API ────────────────────────────────────────────────
    window.RhynoTabs = {
        init: init,
        open: openTab,
        activate: setActive,
        close: closeTab,
        getTabs: function () { return tabs.slice(); },
        getActiveTabId: function () { return activeTabId; }
    };

    // Auto-init when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
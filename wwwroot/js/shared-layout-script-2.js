// ── Rhyno Core Utilities ──────────────────────────────────
                                                                                                                                                    window.Rhyno = (() => {
                                                                                                                                                    const toggleDropdown = (btn) => {
                                                                                                                                                    const container = btn.closest('.rhyno-dropdown-trigger');
                                                                                                                                                    if (!container) return;
                                                                                                                                                    const dropdown = container.querySelector('.rhyno-dropdown');
                                                                                                                                                    if (!dropdown) return;
                                                                                                                                                    const isOpen = dropdown.classList.contains('open');
                                                                                                                                                    closeAllDropdowns();
                                                                                                                                                    if (!isOpen) {
                                                                                                                                                    dropdown.classList.add('open');
                                                                                                                                                    btn.setAttribute('aria-expanded', 'true');
                                                                                                                                                    }
                                                                                                                                                    };

                                                                                                                                                    const closeAllDropdowns = () => {
                                                                                                                                                    document.querySelectorAll('.rhyno-dropdown.open').forEach(d => {
                                                                                                                                                    d.classList.remove('open');
                                                                                                                                                    d.closest('.rhyno-dropdown-trigger')?.querySelector('button')?.setAttribute('aria-expanded', 'false');
                                                                                                                                                    });
                                                                                                                                                    };

                                                                                                                                                    const showModal = (id) => {
                                                                                                                                                    const el = document.getElementById(id);
                                                                                                                                                    if (!el) return;
                                                                                                                                                    el.style.display = 'flex';
                                                                                                                                                    el.classList.add('open');
                                                                                                                                                    document.body.style.overflow = 'hidden';
                                                                                                                                                    if (window.RhynoModalFocus) {
                                                                                                                                                    RhynoModalFocus.open(el, { trigger: document.activeElement });
                                                                                                                                                    }
                                                                                                                                                    };

                                                                                                                                                    const hideModal = (id) => {
                                                                                                                                                    const el = document.getElementById(id);
                                                                                                                                                    if (!el) return;
                                                                                                                                                    el.style.display = 'none';
                                                                                                                                                    el.classList.remove('open');
                                                                                                                                                    document.body.style.overflow = '';
                                                                                                                                                    if (window.RhynoModalFocus) {
                                                                                                                                                    RhynoModalFocus.close(el);
                                                                                                                                                    }
                                                                                                                                                    };

                                                                                                                                                    let _confirmCallback = null;
                                                                                                                                                    const confirm = (message, callback) => {
                                                                                                                                                    document.getElementById('appConfirmMessage').textContent = message || 'آیا مطمئن هستید؟';
                                                                                                                                                    _confirmCallback = callback;
                                                                                                                                                    showModal('appConfirmDialog');
                                                                                                                                                    };

                                                                                                                                                    const toast = (message, type = 'info') => {
                                                                                                                                                    const colors = { success: '#10b981', error: '#e11d48', warning: '#f59e0b', info: '#2b7cb3' };
                                                                                                                                                    const icons = { success: 'bi-check-circle', error: 'bi-x-circle', warning: 'bi-exclamation-triangle', info: 'bi-info-circle' };
                                                                                                                                                    const toast = document.createElement('div');
                                                                                                                                                    toast.style.cssText = `position:fixed;bottom:20px;left:20px;z-index:9999;display:flex;align-items:center;gap:8px;background:${colors[type] || colors.info};color:#fff;padding:10px 16px;border-radius:10px;font-size:13px;font-weight:700;box-shadow:0 8px 24px rgba(0,0,0,0.2);transform:translateY(16px);opacity:0;transition:all 0.3s ease;`;
                                                                                                                                                    toast.innerHTML = `<i class="bi ${icons[type] || icons.info}"></i> ${message}`;
                                                                                                                                                    document.body.appendChild(toast);
                                                                                                                                                    requestAnimationFrame(() => { toast.style.transform = 'translateY(0)'; toast.style.opacity = '1'; });
                                                                                                                                                    setTimeout(() => {
                                                                                                                                                    toast.style.transform = 'translateY(16px)'; toast.style.opacity = '0';
                                                                                                                                                    setTimeout(() => toast.remove(), 300);
                                                                                                                                                    }, 3500);
                                                                                                                                                    };

                                                                                                                                                    // Sidebar toggle
                                                                                                                                                    const toggleSidebar = () => {
                                                                                                                                                    const sidebar = document.getElementById('appSidebar');
                                                                                                                                                    const overlay = document.getElementById('sidebarOverlay');
                                                                                                                                                    if (!sidebar) return;
                                                                                                                                                    if (window.innerWidth <= 768) {
                                                                                                                                                    sidebar.classList.toggle('mobile-open');
                                                                                                                                                    if (overlay) overlay.classList.toggle('active');
                                                                                                                                                    } else {
                                                                                                                                                    sidebar.classList.toggle('collapsed');
                                                                                                                                                    }
                                                                                                                                                    };

                                                                                                                                                    const closeSidebar = () => {
                                                                                                                                                    const sidebar = document.getElementById('appSidebar');
                                                                                                                                                    const overlay = document.getElementById('sidebarOverlay');
                                                                                                                                                    if (sidebar) sidebar.classList.remove('mobile-open');
                                                                                                                                                    if (overlay) overlay.classList.remove('active');
                                                                                                                                                    };

                                                                                                                                                    // Module accordion toggle
                                                                                                                                                    const toggleModule = (btn) => {
                                                                                                                                                    const module = btn.closest('.sidebar-module');
                                                                                                                                                    const children = module?.querySelector('.module-children');
                                                                                                                                                    if (!children) return;
                                                                                                                                                    const isOpen = children.style.maxHeight && children.style.maxHeight !== '0px';
                                                                                                                                                    if (isOpen) {
                                                                                                                                                    children.style.maxHeight = '0px';
                                                                                                                                                    btn.classList.remove('expanded');
                                                                                                                                                    } else {
                                                                                                                                                    children.style.maxHeight = children.scrollHeight + 'px';
                                                                                                                                                    btn.classList.add('expanded');
                                                                                                                                                    }
                                                                                                                                                    };

                                                                                                                                                    return { toggleDropdown, closeAllDropdowns, showModal, hideModal, confirm, toast, toggleSidebar, closeSidebar, toggleModule };
                                                                                                                                                    })();

                                                                                                                                                    // ── Clock & Persian Date ────────────────────────────────
                                                                                                                                                    (() => {
                                                                                                                                                    const dateElem = document.getElementById('headerDate');
                                                                                                                                                    if (!dateElem) return;
                                                                                                                                                    const update = () => {
                                                                                                                                                    const now = new Date();
                                                                                                                                                    try {
                                                                                                                                                    dateElem.textContent = new Intl.DateTimeFormat('fa-IR-u-ca-persian', { year: 'numeric', month: '2-digit', day: '2-digit' }).format(now);
                                                                                                                                                    } catch (e) {
                                                                                                                                                    dateElem.textContent = now.toLocaleDateString('fa-IR');
                                                                                                                                                    }
                                                                                                                                                    };
                                                                                                                                                    update();
                                                                                                                                                    setInterval(update, 60000);
                                                                                                                                                    })();

                                                                                                                                                    // ── Sidebar Search ──────────────────────────────────────
                                                                                                                                                    (() => {
                                                                                                                                                    const input = document.getElementById('sidebarMenuSearch');
                                                                                                                                                    if (!input) return;
                                                                                                                                                    input.addEventListener('input', () => {
                                                                                                                                                    const query = input.value.trim().toLowerCase();
                                                                                                                                                    const items = document.querySelectorAll('#appSidebar .menu-item');
                                                                                                                                                    const modules = document.querySelectorAll('#appSidebar .sidebar-module');
                                                                                                                                                    items.forEach(item => {
                                                                                                                                                    const text = (item.textContent || '').toLowerCase();
                                                                                                                                                    item.style.display = (!query || text.includes(query)) ? '' : 'none';
                                                                                                                                                    });
                                                                                                                                                    modules.forEach(mod => {
                                                                                                                                                    const visibleItems = Array.from(mod.querySelectorAll('.menu-item')).some(i => i.style.display !== 'none');
                                                                                                                                                    const headerMatch = query && (mod.querySelector('.module-toggle span')?.textContent || '').toLowerCase().includes(query);
                                                                                                                                                    mod.style.display = (visibleItems || headerMatch || !query) ? '' : 'none';
                                                                                                                                                    const children = mod.querySelector('.module-children');
                                                                                                                                                    const btn = mod.querySelector('.module-toggle');
                                                                                                                                                    if (query && (visibleItems || headerMatch)) {
                                                                                                                                                    children.style.maxHeight = children.scrollHeight + 'px';
                                                                                                                                                    btn.classList.add('expanded');
                                                                                                                                                    } else if (!query) {
                                                                                                                                                    // keep server-rendered state untouched when query cleared
                                                                                                                                                    }
                                                                                                                                                    });
                                                                                                                                                    });
                                                                                                                                                    })();

                                                                                                                                                    // ── Event Delegation ────────────────────────────────────
                                                                                                                                                    document.addEventListener('click', (e) => {
                                                                                                                                                    if (!e.target.closest('.rhyno-dropdown-trigger')) Rhyno.closeAllDropdowns();
                                                                                                                                                    const overlay = document.getElementById('sidebarOverlay');
                                                                                                                                                    if (e.target === overlay) Rhyno.closeSidebar();
                                                                                                                                                    });

                                                                                                                                                    document.addEventListener('keydown', (e) => {
                                                                                                                                                    if (e.key === 'Escape') {
                                                                                                                                                    Rhyno.closeAllDropdowns();
                                                                                                                                                    Rhyno.hideModal('appConfirmDialog');
                                                                                                                                                    Rhyno.closeSidebar();
                                                                                                                                                    }
                                                                                                                                                    });

                                                                                                                                                    // ── Sidebar Toggle Button ───────────────────────────────
                                                                                                                                                    document.getElementById('btnToggleSidebar')?.addEventListener('click', (e) => {
                                                                                                                                                    e.preventDefault();
                                                                                                                                                    Rhyno.toggleSidebar();
                                                                                                                                                    });

                                                                                                                                                    // ── Confirm Dialog Bind ─────────────────────────────────
                                                                                                                                                    document.getElementById('appConfirmOk')?.addEventListener('click', () => {
                                                                                                                                                    if (window.Rhyno._confirmCallback) {
                                                                                                                                                    window.Rhyno._confirmCallback();
                                                                                                                                                    window.Rhyno._confirmCallback = null;
                                                                                                                                                    }
                                                                                                                                                    Rhyno.hideModal('appConfirmDialog');
                                                                                                                                                    });

                                                                                                                                                    // ── SignalR ────────────────────────────────────────────
                                                                                                                                                    (function () {
                                                                                                                                                    if (typeof signalR === 'undefined') return;
                                                                                                                                                    const connection = new signalR.HubConnectionBuilder()
                                                                                                                                                    .withUrl("/automationHub")
                                                                                                                                                    .withAutomaticReconnect()
                                                                                                                                                    .build();
                                                                                                                                                    connection.on("ReceiveNewTaskNotification", function (data) {
                                                                                                                                                    Rhyno.toast(`[کار جدید] ${data.message}`, 'info');
                                                                                                                                                    });
                    connection.start().catch(err => console.error("SignalR Error:", err));
                                                                                                                                                    })();

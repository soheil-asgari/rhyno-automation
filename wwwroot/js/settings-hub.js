(() => {
    const TAB_STORAGE_KEY = 'rhyno.settings.activeTab';
    const THEME_STORAGE_KEY = 'rhyno.settings.previewTheme';
    const GLOBAL_THEME_STORAGE_KEY = 'rhyno.theme.preference';

    const settingsPage = document.querySelector('.settings-page');
    const tabButtons = document.querySelectorAll('#settings-tab [data-bs-toggle="pill"]');
    const themeSelect = document.querySelector('select[name="Ui.ThemePreference"]');
    const htmlRoot = document.documentElement;

    if (!settingsPage) {
        return;
    }

    const clearPreviewTheme = () => {
        settingsPage.classList.remove('theme-preview-light', 'theme-preview-dark');
    };

    const applyPreviewTheme = (themeValue) => {
        clearPreviewTheme();

        const normalized = (themeValue || 'System').toLowerCase();

        if (normalized === 'light') {
            settingsPage.classList.add('theme-preview-light');
        }

        if (normalized === 'dark') {
            settingsPage.classList.add('theme-preview-dark');
        }
    };

    const applyGlobalThemePreview = (themeValue) => {
        const normalized = (themeValue || 'System').toLowerCase();
        htmlRoot.classList.remove('theme-light', 'theme-dark');
        htmlRoot.setAttribute('data-theme', normalized);

        if (normalized === 'light') {
            htmlRoot.classList.add('theme-light');
            return;
        }

        if (normalized === 'dark') {
            htmlRoot.classList.add('theme-dark');
            return;
        }

        const prefersDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
        htmlRoot.classList.add(prefersDark ? 'theme-dark' : 'theme-light');
    };

    const getInitialThemeValue = () => {
        const storedTheme = localStorage.getItem(THEME_STORAGE_KEY);
        if (storedTheme && ['light', 'dark', 'system'].includes(storedTheme.toLowerCase())) {
            return storedTheme;
        }

        return themeSelect ? (themeSelect.value || 'System') : 'System';
    };

    const activateStoredTab = () => {
        const storedSelector = localStorage.getItem(TAB_STORAGE_KEY);
        if (!storedSelector) {
            return;
        }

        const tabButton = document.querySelector(`#settings-tab [data-bs-target="${storedSelector}"]`);
        if (!tabButton || typeof bootstrap === 'undefined' || !bootstrap.Tab) {
            return;
        }

        bootstrap.Tab.getOrCreateInstance(tabButton).show();
    };

    tabButtons.forEach((button) => {
        button.addEventListener('shown.bs.tab', (event) => {
            const selected = event.target?.getAttribute('data-bs-target');
            if (selected) {
                localStorage.setItem(TAB_STORAGE_KEY, selected);
            }
        });
    });

    if (themeSelect) {
        themeSelect.addEventListener('change', () => {
            const selectedTheme = themeSelect.value || 'System';
            applyPreviewTheme(selectedTheme);
            applyGlobalThemePreview(selectedTheme);
            localStorage.setItem(GLOBAL_THEME_STORAGE_KEY, selectedTheme);
            localStorage.setItem(THEME_STORAGE_KEY, selectedTheme);
        });

        const hostingForm = themeSelect.closest('form');
        if (hostingForm) {
            hostingForm.addEventListener('submit', () => {
                localStorage.removeItem(THEME_STORAGE_KEY);
                localStorage.removeItem(GLOBAL_THEME_STORAGE_KEY);
            });
        }
    }

    activateStoredTab();
    const initialTheme = getInitialThemeValue();
    applyPreviewTheme(initialTheme);
    applyGlobalThemePreview(initialTheme);

    const signatureTabButton = document.querySelector('#settings-tab [data-bs-target="#signature-admin-pane"]');
    const signatureCanvas = document.getElementById('signature-canvas');
    const signatureSaveButton = document.getElementById('signature-save-btn');
    const signatureClearButton = document.getElementById('signature-clear-btn');
    const signatureUploadInput = document.getElementById('signature-upload-input');

    if (signatureTabButton && signatureCanvas && typeof SignaturePad !== 'undefined') {
        const signaturePad = new SignaturePad(signatureCanvas, {
            backgroundColor: 'rgba(255, 255, 255, 0)',
            penColor: 'rgb(0, 0, 0)'
        });

        const resizeSignatureCanvas = () => {
            const ratio = Math.max(window.devicePixelRatio || 1, 1);
            const hadContent = !signaturePad.isEmpty();
            const cachedData = hadContent ? signaturePad.toData() : null;
            signatureCanvas.width = signatureCanvas.offsetWidth * ratio;
            signatureCanvas.height = signatureCanvas.offsetHeight * ratio;
            signatureCanvas.getContext('2d').scale(ratio, ratio);
            signaturePad.clear();

            if (cachedData) {
                signaturePad.fromData(cachedData);
            }
        };

        signatureTabButton.addEventListener('shown.bs.tab', () => {
            resizeSignatureCanvas();
        });

        window.addEventListener('resize', resizeSignatureCanvas);
        resizeSignatureCanvas();

        signatureClearButton?.addEventListener('click', () => {
            signaturePad.clear();
        });

        signatureUploadInput?.addEventListener('change', (event) => {
            const input = event.target;
            const file = input && input.files ? input.files[0] : null;
            if (!file) {
                return;
            }

            const reader = new FileReader();
            reader.onload = (readEvent) => {
                const dataUrl = readEvent.target?.result;
                if (typeof dataUrl === 'string') {
                    signaturePad.clear();
                    signaturePad.fromDataURL(dataUrl);
                }
            };
            reader.readAsDataURL(file);
        });

        signatureSaveButton?.addEventListener('click', async () => {
            if (signaturePad.isEmpty()) {
                if (window.Swal) {
                    Swal.fire('خطا', 'امضا خالی است. ابتدا امضا را ترسیم کنید.', 'warning');
                }
                return;
            }

            try {
                const response = await fetch('/Settings/SaveSignature', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ imageData: signaturePad.toDataURL('image/png') })
                });

                if (!response.ok) {
                    throw new Error('Signature save failed');
                }

                const payload = await response.json();
                if (!payload?.success) {
                    throw new Error(payload?.message || 'Signature save failed');
                }

                if (window.Swal) {
                    await Swal.fire('موفق', 'امضای کاربر با موفقیت ذخیره شد.', 'success');
                }

                window.location.reload();
            } catch (error) {
                if (window.Swal) {
                    Swal.fire('خطا', 'ذخیره امضا با خطا مواجه شد.', 'error');
                }
            }
        });
    }

    requestAnimationFrame(() => {
        requestAnimationFrame(() => {
            settingsPage.classList.add('theme-preview-animated');
        });
    });
})();

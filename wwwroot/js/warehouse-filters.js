(function () {
    const forms = document.querySelectorAll('[data-warehouse-filter-form]');
    if (!forms.length) return;

    const normalize = (value) => (value ?? '').toString().trim();
    const safeJsonParse = (raw, fallback) => {
        try { return raw ? JSON.parse(raw) : fallback; } catch { return fallback; }
    };

    const readField = (field) => {
        if (!field || !field.dataset.filterKey) return null;
        const key = field.dataset.filterKey;
        if (field.type === 'checkbox') return [key, field.checked];
        return [key, normalize(field.value)];
    };

    const writeField = (field, value) => {
        if (!field || !field.dataset.filterKey) return;
        if (field.type === 'checkbox') {
            field.checked = Boolean(value);
            return;
        }

        field.value = value ?? '';
    };

    forms.forEach((form) => {
        const key = form.dataset.filtersKey || form.id || window.location.pathname;
        const stateKey = `rhyno.warehouse.filters.${key}`;
        const presetsKey = `rhyno.warehouse.presets.${key}`;
        const saveButtonId = form.dataset.saveButtonId;
        const clearButtonId = form.dataset.clearButtonId;
        const containerId = form.dataset.presetsContainerId;

        const fields = Array.from(form.querySelectorAll('[data-filter-key]'));
        const saveButton = saveButtonId ? document.getElementById(saveButtonId) : null;
        const clearButton = clearButtonId ? document.getElementById(clearButtonId) : null;
        const presetsContainer = containerId ? document.getElementById(containerId) : null;

        const loadState = () => safeJsonParse(localStorage.getItem(stateKey), {});
        const saveState = (state) => localStorage.setItem(stateKey, JSON.stringify(state));
        const loadPresets = () => safeJsonParse(localStorage.getItem(presetsKey), []);
        const savePresets = (items) => localStorage.setItem(presetsKey, JSON.stringify(items.slice(0, 12)));

        const collectState = () => {
            const state = {};
            fields.forEach((field) => {
                const entry = readField(field);
                if (entry) state[entry[0]] = entry[1];
            });
            return state;
        };

        const applyState = (state) => {
            fields.forEach((field) => {
                const keyName = field.dataset.filterKey;
                writeField(field, state?.[keyName]);
            });
        };

        const renderPresets = () => {
            if (!presetsContainer) return;
            const presets = loadPresets();
            presetsContainer.innerHTML = '';

            if (!presets.length) {
                presetsContainer.innerHTML = '<span class="text-muted small">هنوز فیلتری ذخیره نشده است.</span>';
                return;
            }

            presets.forEach((preset, index) => {
                const wrapper = document.createElement('div');
                wrapper.className = 'd-inline-flex align-items-center gap-1 rounded-pill border px-2 py-1 bg-body-tertiary';
                wrapper.innerHTML = `
                    <button type="button" class="btn btn-link btn-sm p-0 text-decoration-none warehouse-filter-preset" data-index="${index}">${preset.name}</button>
                    <button type="button" class="btn btn-link btn-sm p-0 text-muted warehouse-filter-preset-remove" data-index="${index}" aria-label="حذف">×</button>
                `;
                presetsContainer.appendChild(wrapper);
            });
        };

        const applySavedState = () => {
            const state = loadState();
            applyState(state);
        };

        const persistCurrentState = () => saveState(collectState());

        applySavedState();
        renderPresets();

        fields.forEach((field) => {
            field.addEventListener('input', persistCurrentState);
            field.addEventListener('change', persistCurrentState);
        });

        saveButton?.addEventListener('click', () => {
            const name = normalize(window.prompt('نام فیلتر ذخیره‌شده را وارد کن:'));
            if (!name) return;

            const presets = loadPresets();
            const state = collectState();
            const existing = presets.findIndex(item => item.name === name);
            const entry = { name, state, savedAt: new Date().toISOString() };
            if (existing >= 0) presets[existing] = entry;
            else presets.unshift(entry);
            savePresets(presets);
            renderPresets();
            if (window.AppUI?.toast) window.AppUI.toast('فیلتر ذخیره شد', name, 'success');
        });

        clearButton?.addEventListener('click', () => {
            fields.forEach((field) => {
                if (field.type === 'checkbox') field.checked = false;
                else field.value = '';
            });
            localStorage.removeItem(stateKey);
            persistCurrentState();
            if (window.AppUI?.toast) window.AppUI.toast('فیلترها پاک شد', 'فقط وضعیت فعلی پاک شد، presetها باقی ماندند.', 'secondary');
        });

        presetsContainer?.addEventListener('click', (event) => {
            const presetBtn = event.target.closest('.warehouse-filter-preset');
            const removeBtn = event.target.closest('.warehouse-filter-preset-remove');
            const presets = loadPresets();

            if (presetBtn) {
                const index = Number(presetBtn.dataset.index || '-1');
                const preset = presets[index];
                if (!preset) return;
                applyState(preset.state);
                persistCurrentState();
                if (window.AppUI?.toast) window.AppUI.toast('فیلتر اعمال شد', preset.name, 'primary');
                return;
            }

            if (removeBtn) {
                const index = Number(removeBtn.dataset.index || '-1');
                if (index < 0 || index >= presets.length) return;
                presets.splice(index, 1);
                savePresets(presets);
                renderPresets();
            }
        });
    });
})();

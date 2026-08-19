(() => {
            'use strict';

            // ════════════════════════════════════════════════════════════
            // Defensive Jalali Date Picker Initialization
            // Uses jQuery plugin API (persian-datepicker is a jQuery plugin).
            // Wrapped in try-catch so form handlers NEVER crash.
            // ════════════════════════════════════════════════════════════
            const initJalaliPickers = () => {
                const pickers = document.querySelectorAll('.jalali-date-input');
                if (!pickers.length) return;

                // Check if jQuery and the persianDatepicker plugin are available
                if (typeof jQuery === 'undefined' || typeof jQuery.fn.persianDatepicker === 'undefined') {
                    console.warn('[HC] persianDatepicker jQuery plugin not available — date inputs will accept manual text entry.');
                    return;
                }

                pickers.forEach((input) => {
                    try {
                        // Convert any existing ISO value (yyyy-MM-dd) to Jalali for display
                        const existing = input.value;
                        if (existing && typeof persianDate !== 'undefined') {
                            try {
                                const d = new Date(existing);
                                if (!isNaN(d.getTime())) {
                                    input.value = new persianDate(d).format('YYYY/MM/DD');
                                }
                            } catch (convErr) { /* leave as-is */ }
                        }

                        // Initialize via jQuery plugin API (NOT `new persianDatepicker(...)`)
                        const $input = jQuery(input);
                        $input.persianDatepicker({
                            format: 'YYYY/MM/DD',
                            autoClose: true,
                            observer: true,
                            initialValue: false
                        });

                        // Store instance reference for later access
                        input.persianDatepickerInstance = $input.data('datepicker');

                        // Attach click handler to the calendar icon
                        const wrapper = input.closest('.jalali-datepicker-wrapper');
                        if (wrapper) {
                            const icon = wrapper.querySelector('span.absolute');
                            if (icon) {
                                icon.addEventListener('click', (e) => {
                                    e.preventDefault();
                                    e.stopPropagation();
                                    const inst = $input.data('datepicker');
                                    if (inst && typeof inst.show === 'function') inst.show();
                                });
                            }
                        }
                    } catch (err) {
                        console.warn('[HC] Failed to initialize datepicker for', input.name, err);
                    }
                });
            };

            // ════════════════════════════════════════════════════════════
            // Convert displayed Jalali dates to Gregorian ISO before submit
            // ════════════════════════════════════════════════════════════
            const convertJalaliDates = () => {
                const form = document.getElementById('humanCapitalCreateForm');
                if (!form) return;

                form.querySelectorAll('.jalali-date-input').forEach((input) => {
                    const jalaliText = (input.value || '').trim();
                    if (!jalaliText) return;

                    // Already ISO/gregorian?
                    if (/^\d{4}-\d{2}-\d{2}$/.test(jalaliText)) return;

                    const parts = jalaliText.split('/');
                    if (parts.length !== 3) return;

                    const jYear = parseInt(parts[0], 10);
                    const jMonth = parseInt(parts[1], 10);
                    const jDay = parseInt(parts[2], 10);
                    if (isNaN(jYear) || isNaN(jMonth) || isNaN(jDay)) return;

                    // Use persianDate library if available, otherwise use server-side PersianCalendar
                    if (typeof persianDate !== 'undefined') {
                        try {
                            const gregDate = new persianDate([jYear, jMonth, jDay]).toDate();
                            const iso = gregDate.getFullYear() + '-' +
                                String(gregDate.getMonth() + 1).padStart(2, '0') + '-' +
                                String(gregDate.getDate()).padStart(2, '0');
                            input.value = iso;
                            return;
                        } catch (err) {
                            console.warn('[HC] Jalali conversion via persianDate failed for', input.name, err);
                        }
                    }

                    // Fallback: manual Jalali-to-Gregorian conversion (algorithmic)
                    try {
                        const gDate = jalaliToGregorian(jYear, jMonth, jDay);
                        const iso = gDate[0] + '-' + String(gDate[1]).padStart(2, '0') + '-' + String(gDate[2]).padStart(2, '0');
                        input.value = iso;
                    } catch (fallbackErr) {
                        console.warn('[HC] Fallback Jalali conversion failed for', input.name, fallbackErr);
                    }
                });
            };

            // ════════════════════════════════════════════════════════════
            // Fallback Jalali → Gregorian converter (pure JS, no library)
            // Based on the algorithmic Persian calendar conversion.
            // ════════════════════════════════════════════════════════════
            function jalaliToGregorian(jy, jm, jd) {
                jy = parseInt(jy, 10); jm = parseInt(jm, 10); jd = parseInt(jd, 10);
                var gy = (jy <= 979) ? 621 : 1600;
                jy -= (jy <= 979) ? 0 : 979;
                var days = (365 * jy) + (parseInt(jy / 33) * 8) + parseInt(((jy % 33) + 3) / 4)
                    + 78 + jd + ((jm < 7) ? (jm - 1) * 31 : ((jm - 7) * 30) + 186);
                gy += 400 * parseInt(days / 146097);
                days %= 146097;
                if (days > 36524) {
                    gy += 100 * parseInt(--days / 36524);
                    days %= 36524;
                    if (days >= 365) days++;
                }
                gy += 4 * parseInt(days / 1461);
                days %= 1461;
                if (days > 365) {
                    gy += parseInt((days - 1) / 365);
                    days = (days - 1) % 365;
                }
                var gd = days + 1;
                var sal_a = [0, 31, ((gy % 4 === 0 && gy % 100 !== 0) || (gy % 400 === 0)) ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
                var gm = 0;
                for (gm = 0; gm < 13 && gd > sal_a[gm]; gm++) gd -= sal_a[gm];
                return [gy, gm, gd];
            }

            // ════════════════════════════════════════════════════════════
            // Form reference & elements
            // ════════════════════════════════════════════════════════════
            const form = document.getElementById('humanCapitalCreateForm');
            if (!form) return;

            const submitBtn = document.getElementById('hcCreateSubmitBtn');
            const spinner = document.getElementById('hcCreateSpinner');
            const btnLabel = document.getElementById('hcCreateBtnLabel');
            const errorBanner = document.getElementById('hcCreateErrorBanner');
            const originalLabel = btnLabel ? btnLabel.textContent : '';
            const originalClasses = submitBtn ? submitBtn.className : '';

            // ── Self-contained toast helper ──
            const colors = { success: '#18af08', error: '#ef404d', warning: '#f59e0b', info: '#118AB2' };
            const icons = { success: 'bi-check-circle', error: 'bi-x-circle', warning: 'bi-exclamation-triangle', info: 'bi-info-circle' };
            const toast = (message, type = 'info') => {
                const el = document.createElement('div');
                el.style.cssText = 'position:fixed;bottom:20px;right:20px;z-index:99999;display:flex;align-items:center;gap:8px;direction:rtl;background:' + (colors[type] || colors.info) + ';color:#fff;padding:10px 16px;border-radius:10px;font-size:13px;font-weight:700;box-shadow:0 8px 24px rgba(0,0,0,0.2);opacity:0;transform:translateY(16px);transition:all 0.3s ease;';
                el.innerHTML = '<i class="bi ' + (icons[type] || icons.info) + '"></i><span>' + message + '</span>';
                document.body.appendChild(el);
                requestAnimationFrame(() => { el.style.opacity = '1'; el.style.transform = 'translateY(0)'; });
                setTimeout(() => {
                    el.style.opacity = '0'; el.style.transform = 'translateY(16px)';
                    setTimeout(() => el.remove(), 300);
                }, 5000);
            };

            // ── Anti-forgery token resolution ──
            const getToken = () => {
                const input = document.querySelector('input[name="__RequestVerificationToken"]');
                if (input && input.value) return input.value;
                const meta = document.querySelector('meta[name="request-verification-token"]');
                return meta ? meta.getAttribute('content') : '';
            };

            // ── Loading state (prevent double submit) ──
            const setLoading = (loading) => {
                if (!submitBtn) return;
                if (loading) {
                    submitBtn.disabled = true;
                    submitBtn.classList.add('opacity-60', 'cursor-not-allowed');
                    if (spinner) spinner.classList.remove('hidden');
                    if (btnLabel) btnLabel.textContent = 'در حال ثبت...';
                } else {
                    submitBtn.disabled = false;
                    submitBtn.className = originalClasses;
                    if (spinner) spinner.classList.add('hidden');
                    if (btnLabel) btnLabel.textContent = originalLabel;
                }
            };

            const showBanner = (message) => {
                if (!errorBanner) { toast(message, 'error'); return; }
                errorBanner.textContent = message;
                errorBanner.classList.remove('hidden');
                errorBanner.scrollIntoView({ behavior: 'smooth', block: 'center' });
            };

            const hideBanner = () => {
                if (!errorBanner) return;
                errorBanner.classList.add('hidden');
                errorBanner.textContent = '';
            };

            const clearFieldError = (fieldName) => {
                const span = document.querySelector('[data-valmsg-for="' + CSS.escape(fieldName) + '"]');
                if (span) { span.textContent = ''; span.classList.remove('text-rose-500'); }
            };

            const applyFieldError = (fieldName, message) => {
                const span = document.querySelector('[data-valmsg-for="' + CSS.escape(fieldName) + '"]');
                if (span) {
                    span.textContent = message;
                    span.classList.add('text-rose-500');
                }
            };

            const resetFields = () => form.querySelectorAll('[data-valmsg-for]').forEach(s => { s.textContent = ''; });

            // ── Success navigation (works inside tab iframe & full page) ──
            const navigateTo = (url) => {
                if (!url) { toast('پرونده با موفقیت ثبت شد.', 'success'); return; }
                try {
                    const fromIframe = window.self !== window.top;
                    const parentTabs = fromIframe ? window.parent : window;
                    if (parentTabs && parentTabs.RhynoTabs && typeof parentTabs.RhynoTabs.open === 'function') {
                        parentTabs.RhynoTabs.open(url, 'پرونده پرسنلی', 'bi-person-badge');
                        return;
                    }
                } catch (e) { /* cross-origin — fall through */ }
                const sep = url.indexOf('?') >= 0 ? '&' : '?';
                window.location.href = url + sep + '_tab=1';
            };

            // ════════════════════════════════════════════════════════════
            // Initialize date pickers (defensive — will NOT throw)
            // ════════════════════════════════════════════════════════════
            try {
                initJalaliPickers();
            } catch (initErr) {
                console.error('[HC] Datepicker initialization error (non-fatal):', initErr);
            }

            // ════════════════════════════════════════════════════════════
            // Form submission handler — ALWAYS attaches regardless of
            // datepicker status. The form will always be submittable.
            // ════════════════════════════════════════════════════════════
            form.addEventListener('submit', (e) => {
                e.preventDefault();

                hideBanner();
                resetFields();

                // Native HTML5 validation
                if (!form.checkValidity()) {
                    form.reportValidity();
                    return;
                }

                const token = getToken();
                if (!token) {
                    showBanner('توکن امنیتی (Anti-Forgery) یافت نشد. صفحه را مجدداً بارگذاری کنید.');
                    return;
                }

                // Convert Jalali dates to Gregorian ISO before building FormData
                try {
                    convertJalaliDates();
                } catch (convErr) {
                    console.warn('[HC] Date conversion error (non-fatal):', convErr);
                }

                setLoading(true);

                fetch(form.action, {
                    method: 'POST',
                    credentials: 'same-origin',
                    headers: {
                        'Accept': 'application/json, text/javascript',
                        'X-Requested-With': 'XMLHttpRequest',
                        'RequestVerificationToken': token
                    },
                    body: new FormData(form)
                })
                    .then(async (response) => {
                        if (response.ok) {
                            const data = await response.json().catch(() => ({}));
                            toast('پرونده پرسنلی با موفقیت ثبت شد.', 'success');
                            setTimeout(() => navigateTo(data.redirectUrl), 600);
                            return;
                        }

                        // 400 / validation failure — try to parse JSON errors
                        let payload = null;
                        try { payload = await response.json(); } catch (e) { payload = null; }

                        if (payload && payload.errors && typeof payload.errors === 'object') {
                            const entries = Object.entries(payload.errors);
                            const messages = [];
                            let hasInline = false;
                            entries.forEach(([field, msgs]) => {
                                const list = Array.isArray(msgs) ? msgs : [msgs];
                                list.forEach(m => {
                                    if (field && field !== '') {
                                        applyFieldError(field, m);
                                        hasInline = true;
                                    } else {
                                        messages.push(m);
                                    }
                                });
                            });
                            if (messages.length) {
                                showBanner(messages.join(' | '));
                            } else if (hasInline) {
                                toast('مشکلی در اطلاعات وارد شده وجود دارد. لطفاً خطاهای قرمز را بررسی کنید.', 'warning');
                            }
                            const firstField = entries.find(([field]) => field && field !== '');
                            if (firstField) {
                                const input = form.querySelector('[name="' + CSS.escape(firstField[0]) + '"]');
                                if (input) input.focus();
                            }
                            return;
                        }

                        if (payload && typeof payload === 'object' && payload.message) {
                            let serverMessage = String(payload.message);
                            if (payload.detail != null && String(payload.detail).trim() !== '') {
                                serverMessage = serverMessage + ' — ' + String(payload.detail);
                            }
                            showBanner(serverMessage);
                            return;
                        }

                        showBanner('خطا در ذخیره‌سازی. لطفاً دوباره تلاش کنید یا صفحه را بازنشانی کنید.');
                    })
                    .catch((err) => {
                        console.error('[HC] Create submit error:', err);
                        showBanner('ارتباط با سرور برقرار نشد. لطفاً اتصال اینترنت خود را بررسی و دوباره تلاش کنید.');
                    })
                    .finally(() => {
                        setLoading(false);
                    });
            });
        })();

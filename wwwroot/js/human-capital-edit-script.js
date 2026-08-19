(() => {
            'use strict';
            const form = document.querySelector('form[asp-action="Edit"]');
            if (!form) return;

            // ── Jalali (Persian) Date Picker Initialization ──
            const initJalaliPickers = () => {
                const pickers = document.querySelectorAll('.jalali-date-input');
                if (!pickers.length || typeof persianDatepicker === 'undefined') return;

                pickers.forEach((input) => {
                    const existing = input.value;
                    if (existing) {
                        try {
                            const d = new Date(existing);
                            if (!isNaN(d.getTime()) && typeof persianDate !== 'undefined') {
                                input.value = new persianDate(d).format('YYYY/MM/DD');
                            }
                        } catch (err) { /* leave as-is */ }
                    }

                    const picker = new persianDatepicker(input, {
                        format: 'YYYY/MM/DD',
                        autoClose: true,
                        observer: true,
                        initialValue: true,
                        onSelect: function () { /* display value already bound */ }
                    });
                    input.persianDatepickerInstance = picker;

                    const wrapper = input.closest('.jalali-datepicker-wrapper');
                    if (wrapper) {
                        const icon = wrapper.querySelector('span.absolute');
                        if (icon) {
                            icon.addEventListener('click', (e) => {
                                e.preventDefault();
                                e.stopPropagation();
                                if (picker.show) picker.show();
                            });
                        }
                    }
                });
            };

            // ── Convert displayed Jalali dates to Gregorian ISO (yyyy-MM-dd) before submit ──
            const convertJalaliDates = () => {
                form.querySelectorAll('.jalali-date-input').forEach((input) => {
                    const jalaliText = (input.value || '').trim();
                    if (!jalaliText) return;

                    if (/^\d{4}-\d{2}-\d{2}$/.test(jalaliText)) return;

                    const parts = jalaliText.split('/');
                    if (parts.length !== 3) return;

                    const jYear = parseInt(parts[0], 10);
                    const jMonth = parseInt(parts[1], 10);
                    const jDay = parseInt(parts[2], 10);
                    if (isNaN(jYear) || isNaN(jMonth) || isNaN(jDay)) return;

                    try {
                        const gregDate = new persianDate([jYear, jMonth, jDay]).toDate();
                        const iso = gregDate.getFullYear() + '-' +
                            String(gregDate.getMonth() + 1).padStart(2, '0') + '-' +
                            String(gregDate.getDate()).padStart(2, '0');
                        input.value = iso;
                    } catch (err) {
                        console.warn('Jalali conversion failed for', input.name, err);
                    }
                });
            };

            initJalaliPickers();

            form.addEventListener('submit', () => {
                convertJalaliDates();
            });
        })();

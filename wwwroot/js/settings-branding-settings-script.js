(function() {
            var feedbackEl = document.getElementById('saveFeedback');
            var resetDesktopBtn = document.getElementById('resetDesktopBgBtn');
            var desktopBgInput = document.getElementById('desktopBackgroundInput');
            var resetLoginBtn = document.getElementById('resetBackgroundBtn');
            var loginBgInput = document.getElementById('loginBackgroundInput');
            var form = document.querySelector('.branding-settings .settings-form');

            // ── Reset Desktop Background to Default (gradient) ──
            // Clearing the field makes the default sky-blue gradient from
            // rahkaran-theme.css show through — never a white/blank screen.
            if (resetDesktopBtn && desktopBgInput) {
                resetDesktopBtn.addEventListener('click', function() {
                    desktopBgInput.value = '';
                    if (feedbackEl) {
                        feedbackEl.textContent = '✅ پس‌زمینه به گرادیان پیش‌فرض بازگردانی شد. برای اعمال، دکمه ذخیره را بزنید.';
                        feedbackEl.className = 'text-[11px] font-bold text-emerald-700';
                        feedbackEl.classList.remove('hidden');
                        setTimeout(function() { feedbackEl.classList.add('hidden'); }, 5000);
                    }
                });
            }

            // ── Clear Login Background (optional) ──
            if (resetLoginBtn && loginBgInput) {
                resetLoginBtn.addEventListener('click', function() {
                    loginBgInput.value = '';
                    if (feedbackEl) {
                        feedbackEl.textContent = '✅ تصویر پس‌زمینهٔ ورود پاک شد. برای اعمال، دکمه ذخیره را بزنید.';
                        feedbackEl.className = 'text-[11px] font-bold text-emerald-700';
                        feedbackEl.classList.remove('hidden');
                        setTimeout(function() { feedbackEl.classList.add('hidden'); }, 5000);
                    }
                });
            }

            // ── Save Settings Feedback ──
            if (form && feedbackEl) {
                form.addEventListener('submit', function(e) {
                    var submitBtn = document.getElementById('saveBrandingBtn');
                    if (submitBtn) {
                        submitBtn.disabled = true;
                        submitBtn.innerHTML = '<i class="bi bi-arrow-repeat text-[11px] animate-spin"></i> در حال ذخیره...';
                    }
                    feedbackEl.textContent = '⏳ در حال ذخیره‌سازی تنظیمات...';
                    feedbackEl.className = 'text-[11px] font-bold text-amber-700';
                    feedbackEl.classList.remove('hidden');
                    // The form will submit normally; the page reload will show the result
                });
            }
        })();

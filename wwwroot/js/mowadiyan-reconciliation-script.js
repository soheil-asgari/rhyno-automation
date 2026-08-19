(function () {
        var urls = (function () {
            var el = document.getElementById('mowUrls');
            return el ? JSON.parse(el.textContent) : {};
        })();
        window.mowOpenExportModal = function () {
            var modal = document.getElementById('mowExportModal');
            var formBody = document.getElementById('mowExportFormBody');
            var progressBody = document.getElementById('mowExportProgressBody');
            var startBtn = document.getElementById('btnStartExport');
            var cancelBtn = document.getElementById('btnCancelExport');
            var otpInput = document.getElementById('mowOtpCode');
            window.mowAlreadyLoggedIn = false;
            window.mowSessionToken = '';
            closeProgressState();
            if (formBody) formBody.style.display = '';
            if (progressBody) progressBody.style.display = 'none';
            if (startBtn) startBtn.disabled = true;
            if (cancelBtn) cancelBtn.style.display = '';
            if (otpInput) { otpInput.disabled = true; otpInput.value = ''; }
            if (modal) modal.style.display = 'flex';

            // Immediately trigger the portal login challenge (submits username/password so the
            // Tax Portal sends the SMS OTP) before asking the user for their OTP code.
            mowInitiateLogin();
        };

        window.mowCloseExportModal = function (refresh) {
            var modal = document.getElementById('mowExportModal');
            if (modal) modal.style.display = 'none';
            if (refresh && typeof window.mowReloadReconciliation === 'function') {
                window.mowReloadReconciliation();
            }
        };

        window.closeProgressState = function () {
            document.querySelectorAll('.mow-progress-item').forEach(function (el) {
                el.classList.remove('active', 'done', 'fail');
            });
            var msg = document.getElementById('mowProgressMessage');
            var cta = document.getElementById('mowProgressCta');
            if (msg) { msg.textContent = ''; msg.classList.remove('mow-msg-error'); }
            if (cta) cta.style.display = 'none';
        };

        window.setStepState = function (role, state) {
            document.querySelectorAll('.mow-progress-item[data-role]').forEach(function (el) {
                if (el.getAttribute('data-role') === role) {
                    el.classList.remove('active', 'done', 'fail');
                    if (state) el.classList.add(state);
                } else if (el.classList.contains('active') && state === 'done') {
                    el.classList.remove('active', 'fail');
                    el.classList.add('done');
                }
            });
        };

        window.mowSetStepActive = function (stage) { setStepState('p-' + stage, 'active'); };
        window.mowSetStepDone = function (stage) { setStepState('p-' + stage, 'done'); };

        window.mowInitiateLogin = function () {
            var status = document.getElementById('mowOtpStatus');
            var otpInput = document.getElementById('mowOtpCode');
            var startBtn = document.getElementById('btnStartExport');
            var sendBtn = document.getElementById('btnSendOtp');
            var token = document.querySelector('#frmPortalExport input[name="__RequestVerificationToken"]');
            var afToken = token ? token.value : '';
            var url = urls.initiateLogin || '';

            if (otpInput) otpInput.disabled = true;
            if (startBtn) startBtn.disabled = true;
            if (sendBtn) sendBtn.disabled = true;
            if (status) {
                status.className = 'mow-otp-status mow-otp-status-pending';
                status.innerHTML = '<span class="mow-spinner"></span> در حال ارسال اطلاعات ورود و درخواست کد پیامکی از سامانه مودیان...';
            }

            fetch(url, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': afToken
                }
            })
            .then(function (r) { return r.json(); })
            .then(function (data) {
                if (!data.ok) {
                    if (status) {
                        status.className = 'mow-otp-status mow-otp-status-error';
                        status.textContent = data.message || 'درخواست کد پیامک ناموفق بود.';
                    }
                    if (sendBtn) sendBtn.disabled = false;
                    if (otpInput) otpInput.disabled = true;
                    if (startBtn) startBtn.disabled = true;
                    return;
                }

                if (status) {
                    var msg = data.message || 'کد پیامک با موفقیت به شماره شما ارسال شد.';
                    if (data.maskedPhone) msg += ' (' + data.maskedPhone + ')';
                    status.className = 'mow-otp-status mow-otp-status-success';
                    status.textContent = msg;
                }

                window.mowAlreadyLoggedIn = !!data.alreadyLoggedIn;
                window.mowSessionToken = data.challengeToken || '';
                if (window.mowAlreadyLoggedIn) {
                    // Session already active — auto-advance directly to the download
                    // (دریافت فاکتورها) step without asking for an SMS OTP code.
                    if (otpInput) {
                        otpInput.disabled = true;
                        otpInput.value = '';
                        otpInput.placeholder = 'نیازی به کد — نشست فعال است';
                    }
                } else {
                    if (otpInput) otpInput.disabled = false;
                }
                if (startBtn) startBtn.disabled = false;
                if (sendBtn) sendBtn.disabled = false;
                if (!window.mowAlreadyLoggedIn && otpInput) otpInput.focus();
            })
            .catch(function (err) {
                if (status) {
                    status.className = 'mow-otp-status mow-otp-status-error';
                    status.textContent = err.message || 'خطا در ارتباط با سرور برای دریافت کد پیامک.';
                }
                if (sendBtn) sendBtn.disabled = false;
                if (otpInput) otpInput.disabled = true;
                if (startBtn) startBtn.disabled = true;
            });
        };

        window.mowStartExport = function () {
            var startBtn = document.getElementById('btnStartExport');
            var cancelBtn = document.getElementById('btnCancelExport');
            var year = Number(document.getElementById('mowTaxYear').value);
            var period = Number(document.getElementById('mowTaxPeriod').value);
            var otp = (document.getElementById('mowOtpCode').value || '').trim();
            var alreadyLoggedIn = !!window.mowAlreadyLoggedIn;

            if (!alreadyLoggedIn && !otp) { alert('لطفاً کد تأیید پیامکی (OTP) را وارد کنید.'); return; }
            if (startBtn) startBtn.disabled = true;
            if (cancelBtn) cancelBtn.style.display = 'none';

            document.getElementById('mowExportFormBody').style.display = 'none';
            document.getElementById('mowExportProgressBody').style.display = '';
            closeProgressState();

            var token = document.querySelector('#frmPortalExport input[name="__RequestVerificationToken"]');
            var afToken = token ? token.value : '';
            var url = urls.createExport || '';

            fetch(url, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': afToken
                },
                body: JSON.stringify({ taxYear: year, taxPeriod: period, otpCode: otp, alreadyLoggedIn: alreadyLoggedIn, sessionToken: window.mowSessionToken || '' })
            })
            .then(function (r) { return r.json(); })
            .then(function (data) {
                if (!data.ok) { mowFailProgress(data.message || 'خطا در شروع عملیات'); return; }
                mowPollExport(data.jobId);
            })
            .catch(function (err) { mowFailProgress(err.message || 'خطا در ارتباط با سرور'); });
        };
window.mowPollExport = function (jobId) {
            var url = (urls.exportProgress || '') + '?jobId=' + encodeURIComponent(jobId);
            var poll = window.setInterval(function () {
                fetch(url, { headers: { 'Accept': 'application/json' } })
                .then(function (r) { return r.json(); })
                .then(function (p) {
                    if (!p.ok) { window.clearInterval(poll); mowFailProgress(p.message || 'وضعیت نامشخص است.'); return; }

                    var order = ['Authenticating', 'Submitting', 'Producing', 'Importing', 'Completed'];
                    var idx = order.indexOf(p.stageText);
                    order.forEach(function (s, i) {
                        if (i < idx) mowSetStepDone(s);
                        else if (i === idx) mowSetStepActive(s);
                    });

                    var msg = document.getElementById('mowProgressMessage');
                    if (msg) {
                        msg.textContent = p.message + (p.pollElapsedSeconds ? ' (' + p.pollElapsedSeconds + ' ثانیه)' : '');
                    }

                    if (p.isFailed) {
                        window.clearInterval(poll);
                        document.querySelectorAll('.mow-progress-item.active').forEach(function (el) { el.classList.add('fail'); });
                        if (msg) { msg.classList.add('mow-msg-error'); msg.textContent = p.error || p.message; }
                        showProgressCta();
                        return;
                    }

                    if (p.isComplete) {
                        window.clearInterval(poll);
                        mowSetStepDone('Completed');
                        showProgressCta();
                    }
                })
                .catch(function (err) {
                    window.clearInterval(poll);
                    mowFailProgress(err.message || 'خطا در دریافت وضعیت');
                });
            }, 2000);
        };

        window.showProgressCta = function () {
            var startBtn = document.getElementById('btnStartExport');
            var cta = document.getElementById('mowProgressCta');
            if (startBtn) startBtn.disabled = true;
            if (cta) cta.style.display = '';
        };

        window.mowFailProgress = function (message) {
            var msg = document.getElementById('mowProgressMessage');
            if (msg) { msg.classList.add('mow-msg-error'); msg.textContent = message || 'عملیات ناموفق بود.'; }
            showProgressCta();
        };

        window.mowReloadReconciliation = function () {
            var url = (urls.reconciliation || '') + (window.location.search || '');
            window.location.href = url;
        };
    })();

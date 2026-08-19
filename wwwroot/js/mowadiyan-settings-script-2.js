(function () {
                        'use strict';

                        function mowToast(message, type) {
                            type = type || 'success';
                            var colors = { success: '#18af08', error: '#ef404d', info: '#4d99d0', warning: '#f08046' };
                            var existing = document.querySelector('.mow-toast');
                            if (existing) existing.remove();
                            var t = document.createElement('div');
                            t.className = 'mow-toast';
                            t.style.cssText = 'position:fixed;bottom:20px;left:20px;z-index:100000;direction:rtl;display:flex;' +
                                'align-items:center;gap:8px;background:' + (colors[type] || colors.info) + ';color:#fff;' +
                                'padding:10px 16px;border-radius:4px;font-size:12px;font-weight:700;' +
                                'box-shadow:0 8px 24px rgba(0,0,0,0.25);opacity:0;transition:opacity .25s;';
                            t.textContent = message;
                            document.body.appendChild(t);
                            requestAnimationFrame(function () { t.style.opacity = '1'; });
                            window.setTimeout(function () {
                                t.style.opacity = '0';
                                window.setTimeout(function () { t.remove(); }, 300);
                            }, 3500);
                        }

                        function setSaving(form, saving) {
                            var btn = form.querySelector('button[type="submit"], .mow-btn-save');
                            if (!btn) return;
                            if (saving) {
                                btn.setAttribute('data-orig-html', btn.innerHTML);
                                btn.innerHTML = '<i class="bi bi-hourglass-split"></i> در حال ذخیره...';
                            } else {
                                var orig = btn.getAttribute('data-orig-html');
                                if (orig) { btn.innerHTML = orig; btn.removeAttribute('data-orig-html'); }
                            }
                            btn.disabled = saving;
                        }

                        function bindMowSettingsForm(root) {
                            var form = root.querySelector ? root.querySelector('#mowadiyan-settings-form') || root.querySelector('form[method="post"]') : null;
                            if (!form || form.getAttribute('data-mow-bound') === '1') return;
                            form.setAttribute('data-mow-bound', '1');

                            form.addEventListener('submit', function (e) {
                                e.preventDefault();
                                e.stopImmediatePropagation(); // ensure tab-manager never sees a navigation

                                setSaving(form, true);
                                var data = new FormData(form);
                                fetch(form.getAttribute('action') || window.location.href, {
                                    method: 'POST',
                                    headers: {
                                        'X-Requested-With': 'XMLHttpRequest',
                                        'Accept': 'application/json'
                                    },
                                    credentials: 'same-origin',
                                    body: data
                                }).then(function (res) {
                                    var ct = res.headers.get('content-type') || '';
                                    if (ct.indexOf('application/json') >= 0) {
                                        return res.json().then(function (payload) {
                                            if (payload && payload.success) {
                                                mowToast(payload.message || 'تنظیمات با موفقیت ذخیره شد', 'success');
                                            } else {
                                                mowToast((payload && payload.message) || 'خطا در ذخیره\u200cسازی تنظیمات', 'error');
                                            }
                                        });
                                    }
                                    // Not a JSON response → server-side validation error rendered as HTML.
                                    // Swap in the fresh form so validation messages are shown, then rebind.
                                    return res.text().then(function (html) {
                                        var wrapper = document.createElement('div');
                                        wrapper.innerHTML = html;
                                        var newRoot = wrapper.querySelector('.mow-root') || wrapper;
                                        var host = form.closest('.mow-root') || document.body;
                                        host.outerHTML = newRoot.outerHTML;
                                        bindMowSettingsForm(document);
                                    });
                                }).catch(function () {
                                    mowToast('خطا در برقراری ارتباط با سرور', 'error');
                                }).finally(function () {
                                    setSaving(form, false);
                                });
                            });
                        }

                        if (document.readyState === 'loading') {
                            document.addEventListener('DOMContentLoaded', function () { bindMowSettingsForm(document); });
                        } else {
                            bindMowSettingsForm(document);
                        }
                    })();

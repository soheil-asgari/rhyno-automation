(() => {
                    const toast = document.getElementById('permissionToast');

                    const showToast = (message, isSuccess = true) => {
                        if (!toast) return;

                        if (toast._timer) clearTimeout(toast._timer);

                        toast.textContent = message;
                        toast.className = [
                            'fixed bottom-5 left-1/2 -translate-x-1/2 z-50 px-4 py-2.5 rounded-xl border shadow-lg text-xs font-bold transition-all duration-200',
                            isSuccess
                                ? 'bg-emerald-50 border-emerald-300 text-emerald-700 dark:bg-emerald-950/60 dark:border-emerald-700 dark:text-emerald-300'
                                : 'bg-rose-50 border-rose-300 text-rose-700 dark:bg-rose-950/60 dark:border-rose-700 dark:text-rose-300'
                        ].join(' ');

                        toast._timer = setTimeout(() => {
                            toast.classList.add('hidden');
                        }, 2000);
                    };

                    const paintButton = (button, isAllowed) => {
                        button.dataset.isAllowed = isAllowed ? 'true' : 'false';
                        button.innerHTML = isAllowed
                            ? '<i class="bi bi-check-lg me-1"></i>فعال'
                            : '<i class="bi bi-x me-1"></i>غیر';

                        const on = 'bg-emerald-500 border-emerald-600 text-white hover:bg-emerald-600';
                        const off = 'bg-slate-100 dark:bg-slate-800 border-slate-200 dark:border-slate-700 text-slate-500 dark:text-slate-400 hover:bg-slate-200 dark:hover:bg-slate-700';

                        on.split(' ').forEach(c => button.classList.toggle(c, isAllowed));
                        off.split(' ').forEach(c => button.classList.toggle(c, !isAllowed));
                    };

                    const container = document.querySelector('[data-toggle-url]');
                    if (!container) return;

                    container.addEventListener('click', async (e) => {
                        const button = e.target.closest('.permission-toggle-btn');
                        if (!button) return;

                        e.preventDefault();
                        const toggleUrl = container.dataset.toggleUrl;
                        const antiForgeryToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
                        const previousState = button.dataset.isAllowed === 'true';
                        const nextState = !previousState;

                        button.disabled = true;
                        paintButton(button, nextState);

                        const payload = new URLSearchParams();
                        payload.append('RoleId', button.dataset.roleId);
                        payload.append('PermissionKey', button.dataset.permissionKey);
                        payload.append('IsAllowed', String(nextState));

                        try {
                            const response = await fetch(toggleUrl, {
                                method: 'POST',
                                headers: {
                                    'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8',
                                    'RequestVerificationToken': antiForgeryToken,
                                    'X-Requested-With': 'XMLHttpRequest'
                                },
                                body: payload.toString()
                            });

                            if (!response.ok) throw new Error('save_failed');

                            const result = await response.json();
                            if (!result?.success) throw new Error('invalid_result');

                            showToast(result.message || 'دسترسی با موفقیت ذخیره شد.', true);
                        } catch {
                            paintButton(button, previousState);
                            showToast('ذخیره تغییرات ناموفق بود. دوباره تلاش کنید.', false);
                        } finally {
                            button.disabled = false;
                        }
                    });
                })();

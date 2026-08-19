(() => {
            const roleField = document.getElementById('RoleField');
            const roleButtons = document.querySelectorAll('.role-card-btn');
            const preview = document.getElementById('rolePermissionPreview');
            const mapEl = document.getElementById('rolePermissionData');
            const map = mapEl ? JSON.parse(mapEl.textContent) : {};

            const render = () => {
                if (!preview || !roleField) return;
                const permissions = map[roleField.value] || [];
                preview.innerHTML = permissions.length
                    ? permissions.map(p => `<span class="inline-flex items-center px-2 py-0.5 rounded-full text-[9px] font-bold bg-teal-50 text-teal-750 dark:bg-teal-950/30 dark:text-teal-400 border border-teal-200/30">${p}</span>`).join('')
                    : '<span class="text-slate-400 dark:text-slate-550 text-xs">نقشی انتخاب نشده است.</span>';
            };

            roleButtons.forEach(btn => {
                btn.addEventListener('click', () => {
                    roleField.value = btn.dataset.roleValue;
                    
                    // Reset styling on all buttons
                    roleButtons.forEach(b => {
                        b.classList.remove('bg-teal-50/70', 'border-teal-500', 'dark:bg-teal-950/40', 'dark:border-teal-500', 'ring-2', 'ring-teal-500/10');
                        b.classList.add('bg-white', 'dark:bg-slate-950', 'border-slate-200', 'dark:border-slate-800');
                    });
                    
                    // Add active styling
                    btn.classList.remove('bg-white', 'dark:bg-slate-950', 'border-slate-200', 'dark:border-slate-800');
                    btn.classList.add('bg-teal-50/70', 'border-teal-500', 'dark:bg-teal-955/40', 'dark:border-teal-500', 'ring-2', 'ring-teal-500/10');
                    
                    render();
                });
            });

            render();
        })();

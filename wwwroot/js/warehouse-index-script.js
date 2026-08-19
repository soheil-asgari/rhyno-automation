(() => {
            document.querySelectorAll('[data-trend-width]').forEach((bar) => {
                const value = Number(String(bar.getAttribute('data-trend-width')).replace(',', '.'));
                if (Number.isFinite(value)) {
                    bar.style.setProperty('--trend-width', `${Math.max(0, Math.min(100, value))}%`);
                }
            });
        })();

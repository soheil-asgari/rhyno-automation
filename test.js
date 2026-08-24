async function runDeferredRevenueBackfill() {
    try {
        const response = await fetch('/Contracts/BackfillDeferredRevenue', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                // اگر سیستم شما از Antiforgery Token استفاده می‌کند، آن را اینجا اضافه کنید:
                // 'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value
            }
        });

        if (!response.ok) {
            throw new Error(`خطا در اجرای بک‌فیل: ${response.statusText}`);
        }

        const result = await response.json();
        console.log('بک‌فیل با موفقیت انجام شد:', result);
    } catch (error) {
        console.error('خطا:', error);
    }
}

// فراخوانی تابع
runDeferredRevenueBackfill();
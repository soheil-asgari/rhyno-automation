$(document).ready(function () {
    // ۱. تابع اصلی برای فعال‌سازی تقویم
    function initCalendar() {
        const calendarOptions = {
            format: 'YYYY/MM/DD',
            autoClose: true,
            initialValue: false,
            calendar: {
                persian: {
                    showHint: true,
                    locale: 'fa'
                }
            },
            onSelect: function (unix) {
                // اصلاح اصلی: تبدیل خروجی به اعداد انگلیسی برای سرور
                // .toLocale('en') باعث می‌شود عدد "۱۴۰۴" به "1404" تبدیل شود
                const latinDate = new persianDate(unix).toLocale('en').format('YYYY-MM-DD');

                if (this.model.inputElement.id === 'start_date_picker') {
                    $('#StartDate').val(latinDate);
                } else {
                    $('#EndDate').val(latinDate);
                }
                calculateDuration();
            }
        };

        // فعال‌سازی روی هر دو اینپوت
        $('#start_date_picker, #end_date_picker').persianDatepicker(calendarOptions);
    }

    // ۲. اجرای اولیه
    initCalendar();

    // ۳. حل مشکل باز نشدن (اجبار به باز شدن با کلیک روی باکس یا آیکون)
    $(document).on('click', '.pdate, .input-group-text', function (e) {
        e.preventDefault();
        const $group = $(this).closest('.input-group');
        const $input = $group.find('.pdate');
        $input.focus();
    });

    // ۴. تابع محاسبه مدت زمان با منطق میلادی
    function calculateDuration() {
        const startVal = $('#StartDate').val();
        const endVal = $('#EndDate').val();

        if (startVal && endVal) {
            const start = new Date(startVal);
            const end = new Date(endVal);

            // بررسی معتبر بودن بازه زمانی
            if (end >= start) {
                const diffTime = Math.abs(end - start);
                const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24)) + 1;

                $('#duration_box').removeClass('d-none').hide().fadeIn();
                $('#days_count').text(diffDays);
            } else {
                $('#duration_box').addClass('d-none');
                // پاک کردن مقدار پایان اگر اشتباه بود
                $('#EndDate').val('');
                $('#end_date_picker').val('');
            }
        }
    }
});
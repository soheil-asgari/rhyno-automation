// _SignatureHandler.js

$(document).ready(function () {
    const canvas = document.getElementById('sig-pad');
    if (!canvas) return;

    const signaturePad = new SignaturePad(canvas, {
        backgroundColor: 'rgba(255, 255, 255, 0)',
        penColor: 'rgb(0, 0, 0)'
    });

    // مهم: تابع برای تنظیم سایز دقیق بوم (حل مشکل دیده نشدن خط)
    function resizeCanvas() {
        const ratio = Math.max(window.devicePixelRatio || 1, 1);
        canvas.width = canvas.offsetWidth * ratio;
        canvas.height = canvas.offsetHeight * ratio;
        canvas.getContext("2d").scale(ratio, ratio);
        signaturePad.clear(); // با تغییر سایز بوم پاک می‌شود
    }

    // اجرای تنظیم سایز هنگام باز شدن تب امضا
    $('button[data-bs-target="#tab-signature"]').on('shown.bs.tab', function () {
        resizeCanvas();
    });

    // --- قابلیت آپلود فایل امضا ---
    $('#sig-upload').on('change', function (event) {
        const file = event.target.files[0];
        if (file) {
            const reader = new FileReader();
            reader.onload = function (e) {
                signaturePad.fromDataURL(e.target.result, {
                    width: canvas.offsetWidth,
                    height: canvas.offsetHeight
                });
            };
            reader.readAsDataURL(file);

            Swal.fire({
                toast: true,
                position: 'top-end',
                icon: 'info',
                title: 'فایل بارگذاری شد. می‌توانید آن را ویرایش کنید یا ذخیره بزنید.',
                showConfirmButton: false,
                timer: 4000
            });
        }
    });

    // پاکسازی
    $('#sig-clear').on('click', () => signaturePad.clear());

    // ذخیره نهایی (کشیده شده یا آپلود شده)
    $('#sig-save').on('click', function () {
        if (signaturePad.isEmpty()) {
            Swal.fire('خطا', 'امضا خالی است!', 'warning');
            return;
        }

        const dataURL = signaturePad.toDataURL(); // تصویر به صورت Base64

        Swal.fire({
            title: 'ثبت نهایی؟',
            text: "تصویر فعلی به عنوان امضای رسمی شما ثبت می‌شود.",
            icon: 'question',
            showCancelButton: true,
            confirmButtonText: 'بله، ثبت شود'
        }).then((result) => {
            if (result.isConfirmed) {
                // ارسال به سرور
                fetch('/Settings/SaveSignature', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ imageData: dataURL })
                }).then(res => {
                    if (res.ok) {
                        Swal.fire('موفقیت', 'امضا با موفقیت تغییر یافت.', 'success').then(() => location.reload());
                    }
                });
            }
        });
    });
});
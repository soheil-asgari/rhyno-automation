document.addEventListener("DOMContentLoaded", function () {

    // دریافت اطلاعات کاربران از متغیر گلوبال (که در فایل Razor تعریف شد)
    const allUsers = window.allUsers || [];

    const receiverSelect = document.getElementById('receiverSelect');
    const subjectInput = document.getElementById('subjectInput');

    // تغییر خودکار القاب و نام گیرنده
    if (receiverSelect) {
        receiverSelect.addEventListener('change', function () {
            const selectedId = this.value;
            const prefixElement = document.getElementById('gender-prefix');
            const receiverDisplayElement = document.getElementById('display-receiver');

            if (!selectedId) {
                if (prefixElement) prefixElement.innerText = "جناب آقای / سرکار خانم";
                if (receiverDisplayElement) receiverDisplayElement.innerText = "....................";
                return;
            }

            const user = allUsers.find(u => u.Id == selectedId);
            if (user) {
                if (receiverDisplayElement) receiverDisplayElement.innerText = user.FullName;

                if (prefixElement) {
                    const gender = String(user.Gender).trim();
                    if (gender === "Male") prefixElement.innerText = "جناب آقای";
                    else if (gender === "Female") prefixElement.innerText = "سرکار خانم";
                    else if (gender === "Department") prefixElement.innerText = "واحد محترم";
                    else prefixElement.innerText = "جناب آقای / سرکار خانم";
                }
            }
        });
    }

    // موضوع زنده
    if (subjectInput) {
        subjectInput.addEventListener('input', function () {
            const displaySubject = document.getElementById('display-subject');
            if (displaySubject) {
                displaySubject.innerText = this.value || "....................";
            }
        });
    }
});

// ارسال فرم (این تابع می‌تواند توسط یک دکمه onclick صدا زده شود)
function submitLetter() {
    const recElement = document.getElementById('receiverSelect');
    const subElement = document.getElementById('subjectInput');

    if (!recElement || !subElement) {
        console.error("المان‌های فرم یافت نشدند.");
        return;
    }

    const rec = recElement.value;
    const sub = subElement.value;

    if (!rec || !sub) {
        alert("لطفاً گیرنده و موضوع نامه را مشخص کنید.");
        return;
    }

    // پر کردن فیلدهای پنهان برای ارسال
    document.getElementById('hTitle').value = sub;
    // فرض بر این است که myEditor یک متغیر سراسری مربوط به ویرایشگر متن (مثل CKEditor) است
    if (typeof myEditor !== 'undefined') {
        document.getElementById('hContent').value = myEditor.getData();
    }
    document.getElementById('hReceiver').value = rec;

    // ارسال فرم
    document.getElementById('finalForm').submit();
}

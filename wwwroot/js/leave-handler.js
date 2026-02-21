// leave-handler.js
$(document).ready(function () {
    // فعالسازی جدول با قابلیت جستجو و صفحه بندی فارسی
    $('#leave-table').DataTable({
        language: { url: '//cdn.datatables.net/plug-ins/1.13.4/i18n/fa.json' },
        dom: 'rtip',
        pageLength: 10,
        responsive: true
    });
});

// تاییدیه حذف با SweetAlert2
function confirmDelete(id) {
    Swal.fire({
        title: 'آیا از حذف مطمئن هستید؟',
        text: "این درخواست مرخصی به کلی پاک خواهد شد.",
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: '#d33',
        cancelButtonColor: '#3085d6',
        confirmButtonText: 'بله، حذف شود',
        cancelButtonText: 'انصراف'
    }).then((result) => {
        if (result.isConfirmed) {
            // در اینجا می‌توانید یک فرم مخفی را Submit کنید یا از Fetch استفاده کنید
            window.location.href = "/Leave/Delete/" + id;
        }
    });
}
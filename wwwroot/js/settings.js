$(document).ready(function () {
    const btnAddUser = document.getElementById('btn-manual-add-user');
    if (btnAddUser) {
        btnAddUser.addEventListener('click', function () {
            const modalEl = document.getElementById('modal-add-user');
            const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
            modal.show();
        });
    }
    // تنظیمات عمومی SweetAlert
    const Toast = Swal.mixin({
        toast: true,
        position: 'top-end',
        showConfirmButton: false,
        timer: 3000,
        timerProgressBar: true,
        didOpen: (toast) => {
            toast.addEventListener('mouseenter', Swal.stopTimer)
            toast.addEventListener('mouseleave', Swal.resumeTimer)
        }
    });

    // ۱. تابع مدیریت جدول (DataTables) - اصلاح شده برای حذف اسکرول داخلی
    function getTableInstance() {
        if ($.fn.DataTable.isDataTable('#users-table')) {
            return $('#users-table').DataTable();
        } else {
            return $('#users-table').DataTable({
                language: {
                    url: '//cdn.datatables.net/plug-ins/1.13.4/i18n/fa.json'
                },
                responsive: true,
                autoWidth: false,
                paging: true,
                pageLength: 10,
                dom: 'rtip',
                // این تنظیمات را به این شکل اصلاح کنید:
                searching: true,
                info: true,
                // scrollY و scrollX را کلاً حذف کنید یا روی مقادیر پیش‌فرض بگذارید
            });
        }
    }

    getTableInstance();

            // ۲. لود لیست کاربران با Fetch و SweetAlert
    const btnLoad = document.getElementById('btn-load-users');
    if (btnLoad) {
        btnLoad.addEventListener('click', function () {
            $('#users-list-body').html('<tr><td colspan="6" class="text-center p-5"><div class="spinner-border text-primary"></div></td></tr>');

            fetch('/Settings/GetAllUsers')
                .then(res => res.json())
                .then(users => {
                    const table = getTableInstance();
                    table.clear();

                    // داخل حلقه کاربران در متد fetch
                    // داخل حلقه کاربران در متد fetch
                    users.forEach((u, index) => {
                        table.row.add([
                            index + 1,
                            // استفاده از کلاس text-dark برای جلوگیری از بنفش شدن
                            `<span class="fw-bold text-dark">${u.fullName || '---'}</span>`,
                            `<span class="badge bg-light text-dark border">${u.jobTitle || 'شخصی'}</span>`,
                            // اصلاح نمایش محل خدمت (تطبیق با مدل ارسالی سرور)
                            `<span><i class="bi bi-geo-alt text-danger me-1"></i>${u.serviceLocation || u.location || 'نامشخص'}</span>`,
                            // نام کاربری را با رنگ تیره (Slate) نمایش می‌دهیم تا زشت نباشد
                            `<span class="user-username" title="${u.userName || u.email}">${u.userName || u.email}</span>`,
                            `<div class="d-flex justify-content-center gap-2">
            <button class="btn-action btn-edit" onclick="editUser('${u.id}')">
                <i class="bi bi-pencil-square"></i> <span class="btn-text">ویرایش</span>
            </button>
            <button class="btn-action btn-delete" onclick="deleteUser('${u.id}')">
                <i class="bi bi-trash3"></i> <span class="btn-text">حذف</span>
            </button>
        </div>`
                        ]);
                    });
                    table.draw();
                    //table.columns.adjust().responsive.recalc();
                    Toast.fire({ icon: 'success', title: 'لیست کاربران بروزرسانی شد' });
                })
                .catch(err => {
                    Swal.fire('خطا', 'دریافت اطلاعات کاربران با خطا مواجه شد.', 'error');
                });
        });
    }
    const canvas = document.getElementById('sig-pad');
    if (canvas) {
        const signaturePad = new SignaturePad(canvas, {
            backgroundColor: 'rgba(255, 255, 255, 0)',
            penColor: 'rgb(0, 0, 0)'
        });

        function resizeCanvas() {
            const ratio = Math.max(window.devicePixelRatio || 1, 1);
            const internalClear = signaturePad.isEmpty(); // حفظ وضعیت خالی بودن
            const data = signaturePad.toData(); // ذخیره موقت ترسیمات

            canvas.width = canvas.offsetWidth * ratio;
            canvas.height = canvas.offsetHeight * ratio;
            canvas.getContext("2d").scale(ratio, ratio);

            signaturePad.clear();
            if (!internalClear) signaturePad.fromData(data); // بازگرداندن امضا بعد از تغییر سایز
        }

        // اجرای تغییر سایز هنگام باز شدن تب امضا
        const sigTabBtn = document.querySelector('button[data-bs-target="#tab-signature"], button[data-bs-target="#v-pills-signature"]');
        if (sigTabBtn) {
            sigTabBtn.addEventListener('shown.bs.tab', resizeCanvas);
        }

        window.addEventListener("resize", resizeCanvas);
        resizeCanvas();

        // دکمه‌های امضا
        document.getElementById('sig-clear')?.addEventListener('click', () => signaturePad.clear());
        document.getElementById('sig-save')?.addEventListener('click', function () {
            if (signaturePad.isEmpty()) {
                Swal.fire({ icon: 'warning', title: 'کادر خالی است', text: 'لطفاً ابتدا امضا را ترسیم کنید.' });
                return;
            }

            Swal.fire({
                title: 'ذخیره امضا؟',
                text: "امضای جدید جایگزین قبلی می‌شود.",
                icon: 'question',
                showCancelButton: true,
                confirmButtonText: 'ذخیره',
                cancelButtonText: 'انصراف'
            }).then((result) => {
                if (result.isConfirmed) {
                    fetch('/Settings/SaveSignature', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ imageData: signaturePad.toDataURL() })
                    }).then(res => res.ok ? location.reload() : null);
                }
            });
        });
    }
});
        // ۵. توابع Global (خارج از Ready برای دسترسی مستقیم HTML)

            window.createUser = function () {
    const name = document.getElementById('add_name').value;
    const email = document.getElementById('add_email').value;
    const pass = document.getElementById('add_pass').value;

    if (!name || !email || !pass) {
        Swal.fire('خطا', 'لطفاً نام، ایمیل و رمز عبور را وارد کنید.', 'error');
        return;
    }

    const params = new URLSearchParams();
    params.append('FullName', name);
    params.append('Email', email);
    params.append('Password', pass);
    params.append('JobTitle', document.getElementById('add_job').value);
    params.append('Gender', document.getElementById('add_gender').value);
    params.append('Role', document.getElementById('add_role').value);
    params.append('ServiceLocation', document.getElementById('add_location').value);
    params.append('Department', document.getElementById('add_department').value);
    params.append('ManagerId', document.getElementById('add_manager').value);
    params.append('IsManager', document.getElementById('add_is_manager').checked);

    Swal.fire({ title: 'در حال ثبت...', didOpen: () => Swal.showLoading() });

    fetch('/Settings/CreateUser', {
        method: 'POST',
        body: params
    })
    .then(res => res.json())
    .then(data => {
        if (data.success) {
            Swal.fire('تبریک!', 'کاربر با موفقیت ایجاد شد.', 'success')
                .then(() => window.location.reload());
        } else {
            Swal.fire('خطا', data.message, 'error');
        }
    })
    .catch(err => {
        Swal.fire('خطا', 'عدم برقراری ارتباط با سرور', 'error');
    });
};

        window.deleteUser = function (id) {
            Swal.fire({
                title: 'آیا مطمئن هستید؟',
                text: "این عملیات غیرقابل بازگشت است!",
                icon: 'warning',
                showCancelButton: true,
                confirmButtonColor: '#d33',
                cancelButtonColor: '#3085d6',
                confirmButtonText: 'بله، حذف شود',
                cancelButtonText: 'انصراف'
            }).then((result) => {
                if (result.isConfirmed) {
                    const params = new URLSearchParams();
                    params.append('userId', id);
                    fetch('/Settings/DeleteUser', {
                        method: 'POST',
                        body: params
                    }).then(res => {
                        if (res.ok) {
                            Swal.fire('حذف شد', 'کاربر با موفقیت حذف گردید.', 'success').then(() => location.reload());
                        } else {
                            Swal.fire('خطا', 'عملیات حذف انجام نشد.', 'error');
                        }
                    });
                }
            });
        };window.editUser = function(id) {
    // نمایش وضعیت در حال لود
    // Swal.fire({ title: 'در حال دریافت اطلاعات...', didOpen: () => Swal.showLoading() });

    fetch(`/Settings/GetUser/${id}`)
        .then(res => {
            if (!res.ok) throw new Error("خطا در پاسخ سرور");
            return res.json();
        })
        .then(user => {
            console.log("Full User Data:", user);

            // پر کردن مقادیر در فیلدهای مودال ویرایش
            document.getElementById('edit_userId').value = user.id || "";
            document.getElementById('edit_name').value = user.fullName || "";
            document.getElementById('edit_email').value = user.userName || user.email || "";
            document.getElementById('edit_job').value = user.jobTitle || "";
            document.getElementById('edit_gender').value = user.gender || "Male";
            document.getElementById('edit_department').value = user.department || "";
            document.getElementById('edit_is_manager').checked = user.isManager || false;
            document.getElementById('edit_manager').value = user.managerId || "";
            document.getElementById('edit_role').value = user.role || "User";
            
            // اصلاح اصلی: استفاده از user.location بر اساس خروجی کنسول شما
            document.getElementById('edit_location').value = user.location || user.serviceLocation || "مرکز اصلی";

            Swal.close(); // بستن لودینگ

            // باز کردن مودال به روش ایمن
            const modalElement = document.getElementById('modal-edit-user');
            let modalInstance = bootstrap.Modal.getInstance(modalElement);
            if (!modalInstance) {
                modalInstance = new bootstrap.Modal(modalElement);
            }
            modalInstance.show();
        })
        .catch(err => {
            console.error("Edit Error:", err);
            Swal.fire('خطا', 'امکان بارگذاری اطلاعات کاربر وجود ندارد', 'error');
        });
};

     window.updateUser = function() {
    const params = new URLSearchParams();

    // گرفتن مقادیر از فیلدهای مودال ویرایش
    const userId = document.getElementById('edit_userId').value;
    const fullName = document.getElementById('edit_name').value;
    const email = document.getElementById('edit_email').value;
    const jobTitle = document.getElementById('edit_job').value;
    const gender = document.getElementById('edit_gender').value;
    const department = document.getElementById('edit_department').value;
    const isManager = document.getElementById('edit_is_manager').checked;
    const managerId = document.getElementById('edit_manager').value;
    const locValue = document.getElementById('edit_location').value; 
    const role = document.getElementById('edit_role').value;
    const newPassword = document.getElementById('edit_new_password').value;

    if (!userId) {
        Swal.fire('خطا', 'شناسه کاربر یافت نشد.', 'error');
        return;
    }

    params.append('Id', userId);
    params.append('FullName', fullName);
    params.append('Email', email);
    params.append('JobTitle', jobTitle);
    params.append('Gender', gender);
    params.append('Department', department);
    params.append('IsManager', isManager);
    params.append('ManagerId', managerId);
    params.append('ServiceLocation', locValue); // ارسال به سمت سرور
    params.append('Role', role);
    if(newPassword) params.append('NewPassword', newPassword);

    Swal.fire({ title: 'در حال ذخیره...', didOpen: () => Swal.showLoading() });

    fetch('/Settings/UpdateUser', {
        method: 'POST',
        body: params
    })
    .then(res => res.json())
    .then(data => {
        if (data.success) {
            Swal.fire('موفقیت', 'تغییرات با موفقیت ثبت شد', 'success')
                .then(() => window.location.reload());
        } else {
            Swal.fire('خطا', data.message || 'خطایی رخ داد', 'error');
        }
    })
    .catch(err => {
        console.error("Update Error:", err);
        Swal.fire('خطا', 'ارتباط با سرور برقرار نشد.', 'error');
    });
};



window.saveProfile = function () {
    const fullName = document.getElementById('p_name').value;
    const jobTitle = document.getElementById('p_job').value;

    if (!fullName) {
        Swal.fire('خطا', 'نام نمی‌تواند خالی باشد', 'error');
        return;
    }

    Swal.fire({ title: 'در حال بروزرسانی...', didOpen: () => Swal.showLoading() });

    const params = new URLSearchParams();
    params.append('FullName', fullName);
    params.append('JobTitle', jobTitle);

    fetch('/Settings/UpdateProfile', { // این اکشن را باید در کنترلر بسازی
        method: 'POST',
        body: params
    })
        .then(res => res.json())
        .then(data => {
            if (data.success) {
                Swal.fire('موفقیت', 'پروفایل شما با موفقیت بروز شد', 'success');
            } else {
                Swal.fire('خطا', data.message, 'error');
            }
        });
};
var canvas = document.getElementById('sig-pad');
var signaturePad = new SignaturePad(canvas, {
    backgroundColor: 'rgba(255, 255, 255, 0)',
    penColor: 'rgb(0, 0, 0)'
});

// تابع تنظیم ابعاد واقعی بوم برای رفع مشکل زوم
function resizeCanvas() {
    var ratio = Math.max(window.devicePixelRatio || 1, 1);
    canvas.width = canvas.offsetWidth * ratio;
    canvas.height = canvas.offsetHeight * ratio;
    canvas.getContext("2d").scale(ratio, ratio);
    signaturePad.clear(); // پاکسازی برای تنظیم مجدد مقیاس
}

// اجرای تنظیم سایز بلافاصله بعد از باز شدن تب امضا
document.querySelector('button[data-bs-target="#tab-signature"]').addEventListener('shown.bs.tab', function () {
    resizeCanvas();
});

window.onresize = resizeCanvas;
resizeCanvas();
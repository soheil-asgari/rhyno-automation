# تسک فوری: اصلاح بهم‌ریختگی UI در داشبورد ممیزی و مودال گزارش‌ساز هوشمند

## مشکل
پس از حذف کامل Bootstrap، دو بخش زیر دچار بهم‌ریختگی شدید ظاهر و عملکرد شده‌اند:
۱. صفحه `/admin/audit-dashboard` (عدم هم‌راستایی فیلترها و جداول)
۲. مودال "گزارش‌ساز هوشمند" (بدنه کاملاً سفید و خالی، تداخل z-index با Sidebar)

---

## دستورالعمل اصلاح (Scope of Fixes)

### ۱. اصلاح صفحه داشبورد ممیزی (`Views/Admin/AuditDashboard.cshtml`)
- **بازطراحی بخش فیلترها:** 
  - تمام کنترل‌های فیلتر (Selectها، فیلدهای تاریخ، Input جستجو) را با Tailwind CSS در یک ساختار شبکه‌ای منظم (`grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-4 bg-white dark:bg-dark-surface p-4 rounded-2xl shadow-sm border border-gray-100 dark:border-dark-border`) قرار بده.
  - ورودی‌های تاریخ (`az date` / `ta date`) و Selectها باید دارای استایل یکدست (`w-full px-3 py-2 text-sm rounded-xl border ...`) باشند.
- **اصلاح جدول لاگ‌ها:**
  - کلاس‌های قدیمی جدول را پاک کن و جدول را با Tailwind بازنویسی کن:
    - container اصلی: `overflow-x-auto rounded-2xl border border-gray-100 dark:border-dark-border`
    - `table`: `w-full text-right text-sm border-collapse`
    - `thead`: `bg-gray-50 dark:bg-dark-elevated text-gray-600 dark:text-dark-muted font-medium`
    - `th` و `td`: padding یکدست (`px-4 py-3 whitespace-nowrap`)
  - مطمئن شو ستون‌های زمان، کاربر، عملیات، ماژول، جدول، حساس و جزئیات دقیقاً زیر عناوین خود هم‌راستا رندر می‌شوند.

### ۲. اصلاح مودال گزارش‌ساز هوشمند (Smart Report Builder Modal)
- **اصلاح z-index و Overlay مودال:**
  - لایه Overlay مودال را روی `z-[100]` بگذار تا بالاتر از Sidebar (`z-40` یا `z-50`) قرار گیرد و از Sidebar بیرون نزند.
- **اصلاح رندر محتوا و چارت‌ها:**
  - بررسی کن چرا بدنه مودال سفارشی کاملاً سفید است. مطمئن شو هنگام فراخوانی `Rhyno.showModal` یا باز شدن تب/کارت گزارش‌ساز، محتوای داخلی (پیکربندی گزارش، فیلدها و کانوای چارت/نمودارها) به درستی در DOM تزریق و Initialize می‌شود.
  - اگر از کتابخانه‌های نمودار (مثل Chart.js یا ApexCharts) استفاده شده، رویداد `resize` یا متد `.render()` / `.update()` را پس از باز شدن مودال فراخوانی کن تا نمودار داخل کانتینر جدید محاسبات عرض/ارتفاع را انجام دهد.

---

## خروجی مورد انتظار
- چیدمان مرتب، شبکه ای و زیباتر فیلترها و جداول در `/admin/audit-dashboard`.
- باز شدن صحیح مودال گزارش‌ساز روی تمام صفحه (بدون تداخل با Sidebar) همراه با نمایش کامل ابزارها و نمودارهای درون آن.
- بیلد بدون خطای پروژه.
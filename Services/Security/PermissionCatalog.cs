using OfficeAutomation.Models;

namespace OfficeAutomation.Services.Security
{
    public static class PermissionCatalog
    {
        public static readonly IReadOnlyList<Permission> CorePermissions =
        [
            new() { Key = "Letters.Read", DisplayName = "مشاهده نامه‌ها", Category = "نامه‌ها", Description = "مشاهده نامه‌ها و موارد کارتابل." },
            new() { Key = "Letters.Create", DisplayName = "ایجاد نامه", Category = "نامه‌ها", Description = "ایجاد و ارجاع نامه‌های جدید." },
            new() { Key = "Letters.Edit", DisplayName = "ویرایش نامه‌ها", Category = "نامه‌ها", Description = "ویرایش نامه‌های موجود." },
            new() { Key = "Letters.Delete", DisplayName = "حذف نامه‌ها", Category = "نامه‌ها", Description = "حذف نامه‌ها." },
            new() { Key = "Letters.Approve", DisplayName = "تایید نامه‌ها", Category = "نامه‌ها", Description = "انجام اقدام‌های تاییدی گردش کار نامه." },
            new() { Key = "Letters.Export", DisplayName = "خروجی نامه‌ها", Category = "نامه‌ها", Description = "دریافت گزارش و خروجی نامه‌ها." },
            new() { Key = "Letters.ViewSensitive", DisplayName = "مشاهده نامه‌های محرمانه", Category = "نامه‌ها", Description = "مشاهده محتوای محرمانه نامه‌ها." },

            new() { Key = "HR.View", DisplayName = "مشاهده منابع انسانی", Category = "منابع انسانی", Description = "مشاهده اطلاعات کارکنان." },
            new() { Key = "HR.Create", DisplayName = "ایجاد اطلاعات منابع انسانی", Category = "منابع انسانی", Description = "ایجاد رکوردهای منابع انسانی." },
            new() { Key = "HR.Edit", DisplayName = "ویرایش اطلاعات منابع انسانی", Category = "منابع انسانی", Description = "ویرایش رکوردهای منابع انسانی." },
            new() { Key = "HR.Delete", DisplayName = "حذف اطلاعات منابع انسانی", Category = "منابع انسانی", Description = "حذف رکوردهای منابع انسانی." },
            new() { Key = "HR.Approve", DisplayName = "تایید منابع انسانی", Category = "منابع انسانی", Description = "تایید گردش کارها و اقدام‌های منابع انسانی." },
            new() { Key = "HR.Export", DisplayName = "خروجی منابع انسانی", Category = "منابع انسانی", Description = "دریافت گزارش و خروجی منابع انسانی." },
            new() { Key = "HR.ViewSensitive", DisplayName = "مشاهده اطلاعات محرمانه منابع انسانی", Category = "منابع انسانی", Description = "مشاهده حقوق و اطلاعات محرمانه منابع انسانی." },

            new() { Key = "Finance.View", DisplayName = "مشاهده مالی", Category = "مالی", Description = "مشاهده داشبوردها و اطلاعات مالی." },
            new() { Key = "Finance.Create", DisplayName = "ایجاد اطلاعات مالی", Category = "مالی", Description = "ایجاد فاکتور و رکوردهای مالی." },
            new() { Key = "Finance.Edit", DisplayName = "ویرایش اطلاعات مالی", Category = "مالی", Description = "ویرایش رکوردهای مالی." },
            new() { Key = "Finance.Delete", DisplayName = "حذف اطلاعات مالی", Category = "مالی", Description = "حذف رکوردهای مالی." },
            new() { Key = "Finance.Approve", DisplayName = "تایید مالی", Category = "مالی", Description = "تایید عملیات مالی." },
            new() { Key = "Finance.Export", DisplayName = "خروجی مالی", Category = "مالی", Description = "دریافت گزارش و خروجی مالی." },
            new() { Key = "Finance.ViewSensitive", DisplayName = "مشاهده ارقام حساس مالی", Category = "مالی", Description = "مشاهده مبالغ و جمع‌های حساس مالی." },

            new() { Key = "Warehouse.View", DisplayName = "مشاهده انبار", Category = "انبار", Description = "مشاهده عملیات انبار." },
            new() { Key = "Warehouse.Create", DisplayName = "ایجاد اطلاعات انبار", Category = "انبار", Description = "ایجاد رکوردهای انبار." },
            new() { Key = "Warehouse.Edit", DisplayName = "ویرایش اطلاعات انبار", Category = "انبار", Description = "ویرایش رکوردهای انبار." },
            new() { Key = "Warehouse.Delete", DisplayName = "حذف اطلاعات انبار", Category = "انبار", Description = "حذف رکوردهای انبار." },
            new() { Key = "Warehouse.Approve", DisplayName = "تایید انبار", Category = "انبار", Description = "تایید اقدام‌های انبار." },
            new() { Key = "Warehouse.Export", DisplayName = "خروجی انبار", Category = "انبار", Description = "دریافت گزارش و خروجی انبار." },
            new() { Key = "Warehouse.ViewSensitive", DisplayName = "مشاهده اطلاعات محرمانه انبار", Category = "انبار", Description = "مشاهده ارزش‌گذاری‌ها و اطلاعات حساس انبار." },

            new() { Key = "Users.Manage", DisplayName = "مدیریت کاربران", Category = "مدیریت سامانه", Description = "ایجاد و نگهداری کاربران." },
            new() { Key = "Roles.Manage", DisplayName = "مدیریت نقش‌ها", Category = "مدیریت سامانه", Description = "ایجاد و نگهداری نقش‌ها." },
            new() { Key = "Permissions.Manage", DisplayName = "مدیریت مجوزها", Category = "مدیریت سامانه", Description = "اختصاص مجوز به نقش‌ها." },
            new() { Key = "Calendar.View", DisplayName = "مشاهده تقویم سازمانی", Category = "تقویم", Description = "مشاهده تقویم یکپارچه سازمانی." },
            new() { Key = "Calendar.Create", DisplayName = "ایجاد رویداد تقویم", Category = "تقویم", Description = "ایجاد رویدادهای تقویم سازمانی." },

            new() { Key = "Archive.View", DisplayName = "مشاهده بایگانی اسناد", Category = "بایگانی", Description = "مشاهده فایل‌ها و پیوست‌های بایگانی‌شده." },
            new() { Key = "Archive.Create", DisplayName = "بارگذاری سند بایگانی", Category = "بایگانی", Description = "بارگذاری و بایگانی فایل‌ها." },
            new() { Key = "Archive.Delete", DisplayName = "حذف سند بایگانی", Category = "بایگانی", Description = "حذف اسناد بایگانی در صورت نبود منع قانونی." },
            new() { Key = "Archive.ViewSensitive", DisplayName = "مشاهده اسناد محدود بایگانی", Category = "بایگانی", Description = "مشاهده اسناد محدود و محرمانه بایگانی." },

            new() { Key = "SystemSettings.View", DisplayName = "مشاهده تنظیمات", Category = "تنظیمات", Description = "مشاهده تنظیمات سامانه." },
            new() { Key = "SystemSettings.Manage", DisplayName = "مدیریت تنظیمات", Category = "تنظیمات", Description = "به‌روزرسانی تنظیمات سامانه." },
            new() { Key = "Security.Manage", DisplayName = "مدیریت امنیت", Category = "امنیت", Description = "مدیریت نقش‌ها، مجوزها و قواعد دسترسی." },
            new() { Key = "AuditLogs.Read", DisplayName = "مشاهده لاگ‌های ممیزی", Category = "امنیت", Description = "مشاهده لاگ‌های ممیزی." },
            new() { Key = "AuditLogs.Export", DisplayName = "خروجی لاگ‌های ممیزی", Category = "امنیت", Description = "دریافت خروجی لاگ‌های ممیزی." },

            new() { Key = "تحلیل.مشاهده", DisplayName = "مشاهده داشبورد تحلیل", Category = "تحلیل", Description = "مشاهده داشبورد تحلیل فرآیند و ممیزی." },
            new() { Key = "تحلیل.خروجی", DisplayName = "خروجی داشبورد تحلیل", Category = "تحلیل", Description = "دریافت فایل خروجی از داشبورد تحلیل." }
        ];

        public static readonly IReadOnlyDictionary<string, string[]> ControllerFallbackPermissions =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["Financial"] = ["Finance.View"],
                ["Payroll"] = ["Finance.View"],
                ["Bimeh"] = ["Finance.View"],
                ["Warehouse"] = ["Warehouse.View"],
                ["Vendors"] = ["Warehouse.View"],
                ["Employers"] = ["Warehouse.View"],
                ["Waybill"] = ["Warehouse.View"],
                ["HumanCapital"] = ["HR.View"],
                ["Settings"] = ["SystemSettings.View"],
                ["Security"] = ["Security.Manage"],
                ["Letters"] = ["Letters.Read"],
                ["Users"] = ["Users.Manage"],
                ["AuditLogs"] = ["AuditLogs.Read"],
                ["OrganizationCalendar"] = ["Calendar.View"],
                ["DocumentArchive"] = ["Archive.View"],
                ["تحلیل‌فرآیند"] = ["تحلیل.مشاهده"]
            };
    }
}

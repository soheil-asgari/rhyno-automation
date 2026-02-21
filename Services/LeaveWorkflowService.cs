using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Data; // نام فضای نام دیتابیس خود را جایگزین کنید
using OfficeAutomation.Models;

namespace OfficeAutomation.Services
{
    public class LeaveWorkflowService
    {
        private readonly ApplicationDbContext _context;

        public LeaveWorkflowService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// تعیین وضعیت متنی بعدی بر اساس تایید یا رد
        /// </summary>
        public string GetNextStatus(string currentStatus, bool isApproved)
        {
            if (!isApproved) return "رد شده";

            return currentStatus switch
            {
                "ثبت اولیه" => "در انتظار تایید مدیر واحد",
                "در انتظار تایید مدیر واحد" => "در انتظار منابع انسانی",
                "در انتظار منابع انسانی" => "تایید نهایی",
                _ => currentStatus
            };
        }

        public async Task<string?> GetManagerIdForUser(string userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return null;

            // ۱. اول چک کن آیا مدیر مستقیم برایش تعریف شده؟
            if (!string.IsNullOrEmpty(user.ManagerId))
            {
                return user.ManagerId;
            }

            // ۲. اگر مدیر مستقیم نداشت، طبق منطق قبلی در واحد خودش دنبال مدیر بگرد
            var manager = await _context.Users
                .FirstOrDefaultAsync(u => u.Department == user.Department
                                       && u.IsManager == true
                                       && u.Id != userId);

            return manager?.Id;
        }

        /// <summary>
        /// پیدا کردن تمام کاربران واحد منابع انسانی برای مرحله دوم
        /// </summary>
        public async Task<List<string>> GetHRUserIds()
        {
            return await _context.Users
                .Where(u => u.Department == Department.HR)
                .Select(u => u.Id)
                .ToListAsync();
        }
    }
}
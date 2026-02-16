using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Data;
using OfficeAutomation.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OfficeAutomation.Controllers
{
    public class LeavesController : Controller
    {
        private readonly ApplicationDbContext _context;
        // اضافه کردن UserManager برای شناسایی کاربر لاگین شده
        private readonly UserManager<User> _userManager;

        // تزریق سرویس‌ها در سازنده (Constructor)
        public LeavesController(ApplicationDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Leaves
        public async Task<IActionResult> Index()
        {
            // گرفتن ID کاربر فعلی برای امنیت (فقط مرخصی‌های خودش را ببیند)
            var currentUserId = _userManager.GetUserId(User);

            var leaves = _context.Leaves
                .Include(l => l.User)
                .Where(l => l.UserId == currentUserId);

            return View(await leaves.ToListAsync());
        }

        // GET: Leaves/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var leave = await _context.Leaves
                .Include(l => l.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (leave == null) return NotFound();

            // چک کردن دسترسی: کاربر غریبه نتواند جزئیات مرخصی دیگری را ببیند
            if (leave.UserId != _userManager.GetUserId(User)) return Forbid();

            return View(leave);
        }

        // GET: Leaves/Create
        public IActionResult Create()
        {
            ViewData["ReceiverId"] = new SelectList(_context.Users, "Id", "FullName");
            return View();
       
        }

        // POST: Leaves/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("StartDate,EndDate,Reason")] Leave leave)
        {
            // ۱. پیدا کردن ID کاربر لاگین شده به صورت خودکار
            var currentUserId = _userManager.GetUserId(User);

            // ۲. ست کردن اطلاعات امنیتی در سمت سرور
#pragma warning disable CS8601 // Possible null reference assignment.
            leave.UserId = _userManager.GetUserId(User);
#pragma warning restore CS8601 // Possible null reference assignment.
            leave.Status = "در انتظار تایید";

            // حذف اعتبارسنجی برای فیلدهایی که کاربر پر نکرده (سیستم پر کرده)
            ModelState.Remove("UserId");
            ModelState.Remove("User");

            if (ModelState.IsValid)
            {
                _context.Add(leave);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(leave);
        }

        // GET: Leaves/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var leave = await _context.Leaves.FindAsync(id);
            if (leave == null) return NotFound();

            // فقط صاحب مرخصی بتواند آن را ویرایش کند
            if (leave.UserId != _userManager.GetUserId(User)) return Forbid();

            return View(leave);
        }

        // POST: Leaves/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,StartDate,EndDate,Reason")] Leave leave)
        {
            if (id != leave.Id) return NotFound();

            // گرفتن اطلاعات اصلی از دیتابیس برای جلوگیری از تغییر UserId یا Status توسط کاربر
            var leaveInDb = await _context.Leaves.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id);
            if (leaveInDb == null || leaveInDb.UserId != _userManager.GetUserId(User)) return Forbid();

            leave.UserId = leaveInDb.UserId;
            leave.Status = leaveInDb.Status;

            ModelState.Remove("UserId");
            ModelState.Remove("User");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(leave);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!LeaveExists(leave.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(leave);
        }

        // GET: Leaves/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var leave = await _context.Leaves
                .Include(l => l.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (leave == null) return NotFound();
            if (leave.UserId != _userManager.GetUserId(User)) return Forbid();

            return View(leave);
        }

        // POST: Leaves/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var leave = await _context.Leaves.FindAsync(id);
            if (leave != null && leave.UserId == _userManager.GetUserId(User))
            {
                _context.Leaves.Remove(leave);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool LeaveExists(int id)
        {
            return _context.Leaves.Any(e => e.Id == id);
        }
    }
}
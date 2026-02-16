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
    public class LettersController : Controller
    {
        private readonly ApplicationDbContext _context;
        // اضافه کردن مدیریت کاربران
        private readonly UserManager<User> _userManager;

        // تزریق هر دو سرویس در سازنده کلاس
        public LettersController(ApplicationDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Letters
        public async Task<IActionResult> Index()
        {
            // ۱. گرفتن ID کاربر فعلی
            var currentUserId = _userManager.GetUserId(User);

            // ۲. فقط نامه‌هایی را بیاور که فرستنده یا گیرنده‌اش این کاربر باشد
            var letters = _context.Letters
                .Include(l => l.Receiver)
                .Include(l => l.Sender)
                .Where(l => l.SenderId == currentUserId || l.ReceiverId == currentUserId)
                .OrderByDescending(l => l.SentDate);

            return View(await letters.ToListAsync());
        }

        // GET: Letters/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var letter = await _context.Letters
                .Include(l => l.Receiver)
                .Include(l => l.Sender)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (letter == null) return NotFound();

            return View(letter);
        }

        // GET: Letters/Create
        public IActionResult Create()
        {
            // اصلاح لیست گیرنده‌ها: نمایش FullName به جای Id
            ViewData["ReceiverId"] = new SelectList(_context.Users, "Id", "FullName");
            return View();
        }

        // POST: Letters/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,Content,ReceiverId")] Letter letter)
        {
            // ۱. گرفتن ID کاربر لاگین شده با استفاده از _userManager تزریق شده
            var userId = _userManager.GetUserId(User);

            // ۲. مقداردهی خودکار فیلدها
#pragma warning disable CS8601 // Possible null reference assignment.
            letter.SenderId = _userManager.GetUserId(User);
#pragma warning restore CS8601 // Possible null reference assignment.
            letter.SentDate = DateTime.Now;

            // حذف خطاهای احتمالی مربوط به فیلدهایی که خودمان پر کردیم
            ModelState.Remove("SenderId");
            ModelState.Remove("Sender");
            ModelState.Remove("Receiver");

            if (ModelState.IsValid)
            {
                _context.Add(letter);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            // اگر خطا داشت، دوباره لیست را با FullName پر کن
            ViewData["ReceiverId"] = new SelectList(_context.Users, "Id", "FullName", letter.ReceiverId);
            return View(letter);
        }

        // بقیه متدها (Edit و Delete) را فعلاً تغییر نده...

        // GET: Letters/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var letter = await _context.Letters
                .Include(l => l.Receiver)
                .Include(l => l.Sender)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (letter == null) return NotFound();

            return View(letter);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var letter = await _context.Letters.FindAsync(id);
            if (letter != null)
            {
                _context.Letters.Remove(letter);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool LetterExists(int id)
        {
            return _context.Letters.Any(e => e.Id == id);
        }
    }
}
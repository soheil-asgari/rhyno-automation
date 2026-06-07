using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
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
    [Authorize]
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
            var currentUserId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Unauthorized();
            }

            var letters = _context.Letters
                .Include(l => l.Receiver)
                .Include(l => l.Sender)
                .Include(l => l.FinalReceiver)
                .Where(l => l.SenderId == currentUserId || l.ReceiverId == currentUserId || l.FinalReceiverId == currentUserId)
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

            // تعیین پیشوند هوشمند
            string prefix = "جناب آقای / سرکار خانم";
            if (letter.Receiver != null)
            {
                prefix = letter.Receiver.Gender switch
                {
                    "Male" => "جناب آقای",
                    "Female" => "سرکار خانم",
                    "Department" => "واحد محترم",
                    _ => "جناب آقای / سرکار خانم"
                };
            }
            ViewBag.Prefix = prefix;

            return View(letter);
        }

        // GET: Letters/Create
        public async Task<IActionResult> Create()
        {
            // ۱. دریافت یوزری که لاگین کرده به روش ایمن
            var currentUser = await _userManager.GetUserAsync(User);

            // مقداردهی برای امضا (حتی اگر کاربر پیدا نشد، سیستم کرش نکند)
            ViewBag.SenderFullName = currentUser?.FullName ?? "نامشخص";
            ViewBag.SenderRole = currentUser?.JobTitle ;
            ViewBag.UserSignature = currentUser?.SignaturePath;

            // ۲. دریافت لیست کاربران به همراه جنسیت
            var rawUsers = await _context.Users
                .Select(u => new { u.Id, u.FullName, u.Gender })
                .ToListAsync();

            // جلوگیری از ارور SelectList در صورت خالی بودن دیتابیس
            ViewData["ReceiverId"] = new SelectList(rawUsers, "Id", "FullName");

            // تبدیل به JSON برای اسکریپت پیشوند هوشمند
            ViewBag.UsersData = System.Text.Json.JsonSerializer.Serialize(rawUsers);

            return View();
        }

        // POST: Letters/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        // تغییر مهم: فیلد Content به Body تغییر یافت تا با مدل همخوانی داشته باشد
        public async Task<IActionResult> Create([Bind("Title,Body,ReceiverId")] Letter letter)
        {
            var currentUser = await _userManager.GetUserAsync(User);

            if (currentUser == null) return Unauthorized();

            letter.SenderId = currentUser.Id;
            letter.SentDate = DateTime.Now;
            letter.DocumentType = "Letter";
            letter.FinalReceiverId = letter.ReceiverId;

            ModelState.Remove("SenderId");
            ModelState.Remove("Sender");
            ModelState.Remove("Receiver");
            ModelState.Remove("FinalReceiver");

            if (ModelState.IsValid)
            {
                await ApplyWorkflowRoutingOnCreateAsync(letter);
                _context.Add(letter);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            // بازگرداندن اطلاعات در صورت بروز خطا
            ViewBag.SenderFullName = currentUser.FullName;
            ViewBag.SenderRole = currentUser.JobTitle;
            ViewBag.UserSignature = currentUser.SignaturePath;
            ViewData["ReceiverId"] = new SelectList(_context.Users, "Id", "FullName", letter.ReceiverId);
            return View(letter);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Unauthorized();
            }

            var letter = await _context.Letters.FirstOrDefaultAsync(item => item.Id == id);
            if (letter == null)
            {
                return NotFound();
            }

            if (letter.ReceiverId != currentUserId && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var nextApprover = await GetNextWorkflowApproverAsync(letter.DocumentType, letter.CurrentWorkflowStep + 1);
            if (!string.IsNullOrWhiteSpace(nextApprover))
            {
                letter.CurrentWorkflowStep += 1;
                letter.ReceiverId = nextApprover;
                letter.IsWorkflowCompleted = false;
            }
            else
            {
                letter.CurrentWorkflowStep = Math.Max(1, letter.CurrentWorkflowStep);
                letter.ReceiverId = letter.FinalReceiverId ?? letter.ReceiverId;
                letter.IsWorkflowCompleted = true;
            }

            letter.IsRead = false;
            letter.ReadDate = null;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
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

        private async Task ApplyWorkflowRoutingOnCreateAsync(Letter letter)
        {
            var firstApprover = await GetNextWorkflowApproverAsync(letter.DocumentType, 1);
            if (!string.IsNullOrWhiteSpace(firstApprover))
            {
                letter.ReceiverId = firstApprover;
                letter.CurrentWorkflowStep = 1;
                letter.IsWorkflowCompleted = false;
                return;
            }

            var sender = await _context.Users
                .AsNoTracking()
                .Where(item => item.Id == letter.SenderId)
                .Select(item => new { item.ParentManagerUserId })
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(sender?.ParentManagerUserId))
            {
                letter.ReceiverId = sender.ParentManagerUserId;
                letter.CurrentWorkflowStep = 1;
                letter.IsWorkflowCompleted = false;
                return;
            }

            letter.IsWorkflowCompleted = true;
        }

        private async Task<string?> GetNextWorkflowApproverAsync(string documentType, int step)
        {
            var approverUserId = await _context.WorkflowRoutes
                .AsNoTracking()
                .Where(item => item.IsActive && item.DocumentType == documentType && item.StepNumber == step)
                .Select(item => item.ApproverUserId)
                .FirstOrDefaultAsync();

            return string.IsNullOrWhiteSpace(approverUserId) ? null : approverUserId;
        }
    }
}

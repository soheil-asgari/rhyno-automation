using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Data;
using OfficeAutomation.Models;
using OfficeAutomation.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OfficeAutomation.Controllers
{
    [Authorize]
    [Services.Security.PermissionAuthorize("Letters.Read")]
    public class LettersController : Controller
    {
        private readonly ApplicationDbContext _context;
        // اضافه کردن مدیریت کاربران
        private readonly UserManager<User> _userManager;
        private readonly AiService _ai;
        private readonly Services.Security.IPermissionAccessService _permissionAccessService;

        // تزریق هر دو سرویس در سازنده کلاس
        public LettersController(ApplicationDbContext context, UserManager<User> userManager, AiService ai, Services.Security.IPermissionAccessService permissionAccessService)
        {
            _context = context;
            _userManager = userManager;
            _ai = ai;
            _permissionAccessService = permissionAccessService;
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DraftWithAi([FromBody] LetterDraftRequest request)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(request.ReceiverId))
            {
                return BadRequest(new { message = "گیرنده نامه مشخص نیست." });
            }

            var receiver = await _context.Users
                .AsNoTracking()
                .Include(item => item.Department)
                .FirstOrDefaultAsync(item => item.Id == request.ReceiverId);

            if (receiver == null)
            {
                return BadRequest(new { message = "گیرنده انتخاب‌شده معتبر نیست." });
            }

            var senderUnit = currentUser.Department?.Name ?? currentUser.ServiceLocation ?? currentUser.JobTitle ?? "نامشخص";
            var receiverUnit = receiver.Department?.Name ?? receiver.ServiceLocation ?? receiver.JobTitle ?? "نامشخص";
            var existingBody = HtmlSanitizer.StripTags(request.CurrentBody);

            var prompt = $"""
            شما دستیار نگارش نامه‌های اداری فارسی در یک سامانه اتوماسیون اداری هستید.
            یک متن رسمی، دقیق و قابل ارسال تولید کنید. فقط متن نامه را برگردانید و هیچ توضیح اضافه‌ای ننویسید.

            فرستنده: {currentUser.FullName ?? "نامشخص"}
            واحد/سمت فرستنده: {senderUnit}
            گیرنده: {receiver.FullName ?? "نامشخص"}
            واحد/سمت گیرنده: {receiverUnit}
            موضوع: {request.Subject}
            خواسته کاربر: {request.Instruction}
            متن فعلی نامه، در صورت وجود: {existingBody}

            متن باید با «با سلام و احترام» شروع شود، لحن رسمی داشته باشد و بدون امضا تمام شود.
            """;

            var reply = await _ai.AskAsync(prompt);
            return Json(new { reply });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SummarizeWithAi([FromBody] LetterSummaryRequest request)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Unauthorized();
            }

            var mode = string.IsNullOrWhiteSpace(request.Mode) ? "week" : request.Mode.Trim();
            var query = _context.Letters
                .AsNoTracking()
                .Include(item => item.Sender)
                .Include(item => item.Receiver)
                .Where(item => item.SenderId == currentUserId || item.ReceiverId == currentUserId || item.FinalReceiverId == currentUserId);

            if (mode == "selected" && request.LetterId.HasValue)
            {
                query = query.Where(item => item.Id == request.LetterId.Value);
            }
            else if (mode == "unread")
            {
                query = query.Where(item => item.ReceiverId == currentUserId && !item.IsRead);
            }
            else if (mode == "sent")
            {
                query = query.Where(item => item.SenderId == currentUserId);
            }
            else
            {
                var weekStart = DateTime.Now.AddDays(-7);
                query = query.Where(item => item.SentDate >= weekStart && (item.ReceiverId == currentUserId || item.FinalReceiverId == currentUserId));
            }

            var letters = await query
                .OrderByDescending(item => item.SentDate)
                .Take(mode == "selected" ? 1 : 12)
                .ToListAsync();

            if (letters.Count == 0)
            {
                return Json(new { reply = "نامه‌ای برای خلاصه‌سازی در این بازه یا حالت پیدا نشد." });
            }

            var letterText = string.Join("\n\n---\n\n", letters.Select(item =>
                $"""
                شناسه: {item.Id}
                موضوع: {item.Title}
                فرستنده: {item.Sender?.FullName ?? "نامشخص"}
                گیرنده فعلی: {item.Receiver?.FullName ?? "نامشخص"}
                تاریخ: {item.SentDate:yyyy/MM/dd HH:mm}
                وضعیت گردش: {(item.IsWorkflowCompleted ? "تکمیل شده" : $"در گردش - مرحله {item.CurrentWorkflowStep}")}
                متن: {HtmlSanitizer.StripTags(item.Body)}
                """));

            var prompt = $"""
            این داده‌ها مربوط به نامه‌های اداری کاربر در سامانه اتوماسیون است.
            بر اساس حالت انتخاب‌شده، خلاصه‌ای کاربردی و مدیریتی به فارسی تولید کن.
            اگر یک نامه انتخاب شده، گردش نامه، موضوع، وضعیت، فرستنده، گیرنده و متن کامل/خلاصه را منظم توضیح بده.
            اگر چند نامه است، اول جمع‌بندی کوتاه بده، سپس فهرست موضوع‌ها و اقدام‌های پیشنهادی را بنویس.
            اگر پاراف جداگانه‌ای در داده‌ها نیست، فقط وضعیت گردش موجود را گزارش کن و چیزی جعل نکن.

            حالت: {mode}
            نامه‌ها:
            {letterText}
            """;

            var reply = await _ai.AskAsync(prompt);
            return Json(new { reply });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReplyWithAi([FromBody] LetterReplyRequest request)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Unauthorized();
            }

            var letter = await _context.Letters
                .AsNoTracking()
                .Include(item => item.Sender)
                .Include(item => item.Receiver)
                .FirstOrDefaultAsync(item => item.Id == request.LetterId);

            if (letter == null)
            {
                return NotFound();
            }

            if (!CanAccessLetter(letter, currentUserId))
            {
                return Forbid();
            }

            var prompt = $"""
            شما دستیار پاسخ‌گویی به نامه‌های اداری فارسی هستید.
            برای نامه زیر یک متن پاسخ رسمی و کامل تولید کن. فقط متن پاسخ را بنویس.

            موضوع نامه اصلی: {letter.Title}
            فرستنده نامه اصلی: {letter.Sender?.FullName ?? "نامشخص"}
            گیرنده فعلی: {letter.Receiver?.FullName ?? "نامشخص"}
            وضعیت گردش: {(letter.IsWorkflowCompleted ? "تکمیل شده" : $"در گردش - مرحله {letter.CurrentWorkflowStep}")}
            متن نامه اصلی: {HtmlSanitizer.StripTags(letter.Body)}
            نظر/درخواست کاربر برای پاسخ: {request.Intent}

            اگر نظر کاربر تأیید است، متن را به شکل تأییدیه اداری بنویس. اگر نیاز به اصلاح یا توضیح دارد، همان را رسمی و شفاف کن.
            """;

            var reply = await _ai.AskAsync(prompt);
            return Json(new { reply });
        }

        // GET: Letters/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var letter = await _context.Letters
                .Include(l => l.Receiver)
                .Include(l => l.Sender)
                .Include(l => l.FinalReceiver)
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
        public async Task<IActionResult> Create(int? replyToId = null)
        {
            // ۱. دریافت یوزری که لاگین کرده به روش ایمن
            var currentUser = await _userManager.GetUserAsync(User);

            // مقداردهی برای امضا (حتی اگر کاربر پیدا نشد، سیستم کرش نکند)
            ViewBag.SenderFullName = currentUser?.FullName ?? "نامشخص";
            ViewBag.SenderRole = currentUser?.JobTitle ;
            ViewBag.UserSignature = currentUser?.SignaturePath;
            ViewBag.IsReplyMode = false;
            ViewBag.ReplyToLetterId = replyToId;
            ViewBag.ReplyToSubject = string.Empty;
            ViewBag.ReplyToReceiverId = string.Empty;

            // ۲. دریافت لیست کاربران به همراه جنسیت
            var rawUsers = await _context.Users
                .Select(u => new { u.Id, u.FullName, u.Gender })
                .ToListAsync();

            if (replyToId.HasValue)
            {
                var replyToLetter = await _context.Letters
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == replyToId.Value);

                if (replyToLetter == null)
                {
                    return NotFound();
                }

                var currentUserId = currentUser?.Id;
                if (!CanAccessLetter(replyToLetter, currentUserId ?? string.Empty))
                {
                    return Forbid();
                }

                ViewBag.IsReplyMode = true;
                ViewBag.ReplyToSubject = $"پاسخ: {replyToLetter.Title}";
                ViewBag.ReplyToReceiverId = replyToLetter.SenderId;
            }

            // جلوگیری از ارور SelectList در صورت خالی بودن دیتابیس
            ViewData["ReceiverId"] = new SelectList(rawUsers, "Id", "FullName", ViewBag.ReplyToReceiverId);

            // تبدیل به JSON برای اسکریپت پیشوند هوشمند
            ViewBag.UsersData = System.Text.Json.JsonSerializer.Serialize(rawUsers);

            return View();
        }

        // POST: Letters/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Services.Security.PermissionAuthorize("Letters.Create")]
        // تغییر مهم: فیلد Content به Body تغییر یافت تا با مدل همخوانی داشته باشد
        public async Task<IActionResult> Create([Bind("Title,Body,ReceiverId,ReplyToLetterId")] Letter letter)
        {
            var currentUser = await _userManager.GetUserAsync(User);

            if (currentUser == null) return Unauthorized();

            letter.Title = (letter.Title ?? string.Empty).Trim();
            letter.Body = HtmlSanitizer.Sanitize(letter.Body);
            letter.SenderId = currentUser.Id;
            letter.SentDate = DateTime.Now;
            letter.DocumentType = "Letter";
            letter.FinalReceiverId = letter.ReceiverId;
            letter.WorkflowStatus = WorkflowStatus.Sent;

            ModelState.Remove("SenderId");
            ModelState.Remove("Sender");
            ModelState.Remove("Receiver");
            ModelState.Remove("FinalReceiver");

            if (letter.ReplyToLetterId.HasValue)
            {
                var sourceExists = await _context.Letters.AnyAsync(item =>
                    item.Id == letter.ReplyToLetterId.Value &&
                    (item.SenderId == currentUser.Id || item.ReceiverId == currentUser.Id || item.FinalReceiverId == currentUser.Id));
                if (!sourceExists)
                {
                    return Forbid();
                }
            }

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
            ViewBag.IsReplyMode = letter.ReplyToLetterId.HasValue;
            ViewBag.ReplyToLetterId = letter.ReplyToLetterId;
            ViewBag.ReplyToSubject = letter.Title;
            ViewBag.ReplyToReceiverId = letter.ReceiverId;
            ViewData["ReceiverId"] = new SelectList(_context.Users, "Id", "FullName", letter.ReceiverId);
            var usersData = await _context.Users
                .AsNoTracking()
                .Select(u => new { u.Id, u.FullName, u.Gender })
                .ToListAsync();
            ViewBag.UsersData = System.Text.Json.JsonSerializer.Serialize(usersData);
            return View(letter);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Services.Security.PermissionAuthorize("Letters.Approve")]
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

            var canBypass = await _permissionAccessService.UserHasPermissionAsync(currentUserId, "Security.Manage");
            if (letter.ReceiverId != currentUserId && !canBypass)
            {
                return Forbid();
            }

            var nextApprover = await GetNextWorkflowApproverAsync(letter.DocumentType, letter.CurrentWorkflowStep + 1);
            if (!string.IsNullOrWhiteSpace(nextApprover))
            {
                letter.CurrentWorkflowStep += 1;
                letter.ReceiverId = nextApprover;
                letter.IsWorkflowCompleted = false;
                letter.WorkflowStatus = WorkflowStatus.PendingApproval;
            }
            else
            {
                letter.CurrentWorkflowStep = Math.Max(1, letter.CurrentWorkflowStep);
                letter.ReceiverId = letter.FinalReceiverId ?? letter.ReceiverId;
                letter.IsWorkflowCompleted = true;
                letter.WorkflowStatus = WorkflowStatus.Approved;
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
        [Services.Security.PermissionAuthorize("Letters.Delete")]
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

        private static bool CanAccessLetter(Letter letter, string userId)
        {
            return letter.SenderId == userId || letter.ReceiverId == userId || letter.FinalReceiverId == userId;
        }

        private async Task ApplyWorkflowRoutingOnCreateAsync(Letter letter)
        {
            var firstApprover = await GetNextWorkflowApproverAsync(letter.DocumentType, 1);
            if (!string.IsNullOrWhiteSpace(firstApprover))
            {
                letter.ReceiverId = firstApprover;
                letter.CurrentWorkflowStep = 1;
                letter.IsWorkflowCompleted = false;
                letter.WorkflowStatus = WorkflowStatus.PendingApproval;
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
                letter.WorkflowStatus = WorkflowStatus.PendingApproval;
                return;
            }

            letter.IsWorkflowCompleted = true;
            letter.WorkflowStatus = WorkflowStatus.Sent;
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

        public sealed class LetterDraftRequest
        {
            public string? ReceiverId { get; set; }
            public string? Subject { get; set; }
            public string? Instruction { get; set; }
            public string? CurrentBody { get; set; }
        }

        public sealed class LetterSummaryRequest
        {
            public string? Mode { get; set; }
            public int? LetterId { get; set; }
        }

        public sealed class LetterReplyRequest
        {
            public int LetterId { get; set; }
            public string? Intent { get; set; }
        }
    }
}

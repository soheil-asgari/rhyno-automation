using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;
using OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;
using OfficeAutomation.Models;
using OfficeAutomation.Services;
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
        private readonly OfficeDbContext _context;
        private readonly WorkflowDbContext _workflowContext;
        // اضافه کردن مديريت کاربران
        private readonly UserManager<User> _userManager;
        private readonly AiService _ai;
        private readonly Services.Security.IPermissionAccessService _permissionAccessService;
        private readonly NotificationService _notificationService;
        private readonly WorkflowService _workflowService;
        private readonly WorkflowDetailService _workflowDetailService;

        // تزريق هر دو سرويس در سازنده کلاس
        public LettersController(OfficeDbContext context, WorkflowDbContext workflowContext, UserManager<User> userManager, AiService ai, Services.Security.IPermissionAccessService permissionAccessService, NotificationService notificationService, WorkflowService workflowService, WorkflowDetailService workflowDetailService)
        {
            _context = context;
            _workflowContext = workflowContext;
            _userManager = userManager;
            _ai = ai;
            _permissionAccessService = permissionAccessService;
            _notificationService = notificationService;
            _workflowService = workflowService;
            _workflowDetailService = workflowDetailService;
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
                return BadRequest(new { message = "گيرنده نامه مشخص نيست." });
            }

            var receiver = await _context.Users
                .AsNoTracking()
                .Include(item => item.Department)
                .FirstOrDefaultAsync(item => item.Id == request.ReceiverId);

            if (receiver == null)
            {
                return BadRequest(new { message = "گيرنده انتخاب‌شده معتبر نيست." });
            }

            var senderUnit = currentUser.Department?.Name ?? currentUser.ServiceLocation ?? currentUser.JobTitle ?? "نامشخص";
            var receiverUnit = receiver.Department?.Name ?? receiver.ServiceLocation ?? receiver.JobTitle ?? "نامشخص";
            var existingBody = HtmlSanitizer.StripTags(request.CurrentBody);

            var prompt = $"""
            شما دستيار نگارش نامه‌هاي اداري فارسي در يک سامانه اتوماسيون اداري هستيد.
            يک متن رسمي، دقيق و قابل ارسال توليد کنيد. فقط متن نامه را برگردانيد و هيچ توضيح اضافه‌اي ننويسيد.

            فرستنده: {currentUser.FullName ?? "نامشخص"}
            واحد/سمت فرستنده: {senderUnit}
            گيرنده: {receiver.FullName ?? "نامشخص"}
            واحد/سمت گيرنده: {receiverUnit}
            موضوع: {request.Subject}
            خواسته کاربر: {request.Instruction}
            متن فعلي نامه، در صورت وجود: {existingBody}

            متن بايد با «با سلام و احترام» شروع شود، لحن رسمي داشته باشد و بدون امضا تمام شود.
            """;

            var reply = await _ai.AskAsync(prompt, HttpContext.RequestAborted);
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
                return Json(new { reply = "نامه‌اي براي خلاصه‌سازي در اين بازه يا حالت پيدا نشد." });
            }

            var letterText = string.Join("\n\n---\n\n", letters.Select(item =>
                $"""
                شناسه: {item.Id}
                موضوع: {item.Title}
                فرستنده: {item.Sender?.FullName ?? "نامشخص"}
                گيرنده فعلي: {item.Receiver?.FullName ?? "نامشخص"}
                تاريخ: {item.SentDate:yyyy/MM/dd HH:mm}
                وضعيت گردش: {(item.IsWorkflowCompleted ? "تکميل شده" : $"در گردش - مرحله {item.CurrentWorkflowStep}")}
                متن: {HtmlSanitizer.StripTags(item.Body)}
                """));

            var prompt = $"""
            اين داده‌ها مربوط به نامه‌هاي اداري کاربر در سامانه اتوماسيون است.
            بر اساس حالت انتخاب‌شده، خلاصه‌اي کاربردي و مديريتي به فارسي توليد کن.
            اگر يک نامه انتخاب شده، گردش نامه، موضوع، وضعيت، فرستنده، گيرنده و متن کامل/خلاصه را منظم توضيح بده.
            اگر چند نامه است، اول جمع‌بندي کوتاه بده، سپس فهرست موضوع‌ها و اقدام‌هاي پيشنهادي را بنويس.
            اگر پاراف جداگانه‌اي در داده‌ها نيست، فقط وضعيت گردش موجود را گزارش کن و چيزي جعل نکن.

            حالت: {mode}
            نامه‌ها:
            {letterText}
            """;

            var reply = await _ai.AskAsync(prompt, HttpContext.RequestAborted);
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
            شما دستيار پاسخ‌گويي به نامه‌هاي اداري فارسي هستيد.
            براي نامه زير يک متن پاسخ رسمي و کامل توليد کن. فقط متن پاسخ را بنويس.

            موضوع نامه اصلي: {letter.Title}
            فرستنده نامه اصلي: {letter.Sender?.FullName ?? "نامشخص"}
            گيرنده فعلي: {letter.Receiver?.FullName ?? "نامشخص"}
            وضعيت گردش: {(letter.IsWorkflowCompleted ? "تکميل شده" : $"در گردش - مرحله {letter.CurrentWorkflowStep}")}
            متن نامه اصلي: {HtmlSanitizer.StripTags(letter.Body)}
            نظر/درخواست کاربر براي پاسخ: {request.Intent}

            اگر نظر کاربر تأييد است، متن را به شکل تأييديه اداري بنويس. اگر نياز به اصلاح يا توضيح دارد، همان را رسمي و شفاف کن.
            """;

            var reply = await _ai.AskAsync(prompt, HttpContext.RequestAborted);
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

            // تعيين پيشوند هوشمند
            string prefix = "جناب آقاي / سرکار خانم";
            if (letter.Receiver != null)
            {
                prefix = letter.Receiver.Gender switch
                {
                    "Male" => "جناب آقاي",
                    "Female" => "سرکار خانم",
                    "Department" => "واحد محترم",
                    _ => "جناب آقاي / سرکار خانم"
                };
            }
            ViewBag.Prefix = prefix;
            ViewBag.WorkflowDecisions = await _workflowContext.WorkflowDecisions
                .AsNoTracking()
                .Include(item => item.DecidedByUser)
                .Include(item => item.WorkflowInstance)
                .Where(item => item.WorkflowInstance != null &&
                               item.WorkflowInstance.DocumentType == letter.DocumentType &&
                               item.WorkflowInstance.DocumentId == letter.Id)
                .OrderByDescending(item => item.DecidedAt)
                .ToListAsync();
            ViewBag.WorkflowDetail = await _workflowDetailService.BuildAsync(
                letter.DocumentType,
                letter.Id,
                letter.Title,
                HtmlSanitizer.StripTags(letter.Body),
                _userManager.GetUserId(User) ?? string.Empty,
                "Letters.Approve",
                HttpContext.RequestAborted);

            return View(letter);
        }

        // GET: Letters/Create
        public async Task<IActionResult> Create(int? replyToId = null)
        {
            // ?. دريافت يوزري که لاگين کرده به روش ايمن
            var currentUser = await _userManager.GetUserAsync(User);

            // مقداردهي براي امضا (حتي اگر کاربر پيدا نشد، سيستم کرش نکند)
            ViewBag.SenderFullName = currentUser?.FullName ?? "نامشخص";
            ViewBag.SenderRole = currentUser?.JobTitle ;
            ViewBag.UserSignature = currentUser?.SignaturePath;
            ViewBag.IsReplyMode = false;
            ViewBag.ReplyToLetterId = replyToId;
            ViewBag.ReplyToSubject = string.Empty;
            ViewBag.ReplyToReceiverId = string.Empty;

            // ?. دريافت ليست کاربران به همراه جنسيت
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

            // جلوگيري از ارور SelectList در صورت خالي بودن ديتابيس
            ViewData["ReceiverId"] = new SelectList(rawUsers, "Id", "FullName", ViewBag.ReplyToReceiverId);

            // تبديل به JSON براي اسکريپت پيشوند هوشمند
            ViewBag.UsersData = System.Text.Json.JsonSerializer.Serialize(rawUsers);

            return View();
        }

        // POST: Letters/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Services.Security.PermissionAuthorize("Letters.Create")]
        // تغيير مهم: فيلد Content به Body تغيير يافت تا با مدل همخواني داشته باشد
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
                await _workflowService.StartRoutingAsync(
                    letter.DocumentType,
                    letter.SenderId,
                    letter.ReceiverId,
                    letter.Id);
                await _notificationService.CreateAsync(
                    letter.ReceiverId,
                    "نامه جديد",
                    $"نامه «{letter.Title}» براي شما ارسال شد.",
                    letter.WorkflowStatus == WorkflowStatus.PendingApproval ? NotificationSeverity.Warning : NotificationSeverity.Info,
                    $"/Letters/Details/{letter.Id}",
                    "Letters",
                    nameof(Letter),
                    letter.Id);
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

            var routing = await _workflowService.AdvanceRoutingAsync(
                letter.DocumentType,
                letter.CurrentWorkflowStep,
                letter.ReceiverId,
                letter.FinalReceiverId,
                letter.Id,
                currentUserId);
            ApplyWorkflowRouting(letter, routing);

            letter.IsRead = false;
            letter.ReadDate = null;

            await _context.SaveChangesAsync();
            await _notificationService.CreateAsync(
                letter.ReceiverId,
                letter.IsWorkflowCompleted ? "گردش نامه تکميل شد" : "نامه در انتظار تاييد",
                letter.IsWorkflowCompleted
                    ? $"نامه «{letter.Title}» تاييد و به گيرنده نهايي ارسال شد."
                    : $"نامه «{letter.Title}» براي مرحله بعدي تاييد به شما ارجاع شد.",
                letter.IsWorkflowCompleted ? NotificationSeverity.Success : NotificationSeverity.Warning,
                $"/Letters/Details/{letter.Id}",
                "Letters",
                nameof(Letter),
                letter.Id);
            return RedirectToAction(nameof(Index));
        }

        // بقيه متدها (Edit و Delete) را فعلاً تغيير نده...

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
            var routing = await _workflowService.StartRoutingAsync(
                letter.DocumentType,
                letter.SenderId,
                letter.ReceiverId,
                letter.Id);
            ApplyWorkflowRouting(letter, routing);
        }

        private static void ApplyWorkflowRouting(Letter letter, WorkflowRoutingResult routing)
        {
            letter.ReceiverId = routing.ReceiverId;
            letter.CurrentWorkflowStep = routing.StepNumber;
            letter.IsWorkflowCompleted = routing.IsCompleted;
            letter.WorkflowStatus = routing.Status;
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


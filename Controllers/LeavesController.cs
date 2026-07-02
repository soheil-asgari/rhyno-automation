using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Identity.Infrastructure.Persistence;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;
using OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;
using OfficeAutomation.Models;
using OfficeAutomation.Services;
using OfficeAutomation.Services.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OfficeAutomation.Controllers
{
    [Authorize]
    public class LeavesController : Controller
    {
        private readonly OfficeDbContext _context;
        private readonly IdentityDbContext _identityContext;
        private readonly WorkflowDbContext _workflowContext;
        // اضافه کردن UserManager براي شناسايي کاربر لاگين شده
        private readonly UserManager<User> _userManager;
        private readonly IAuthorizationFacade _authorizationFacade;
        private readonly NotificationService _notificationService;
        private readonly WorkflowService _workflowService;
        private readonly WorkflowDetailService _workflowDetailService;

        // تزريق سرويس‌ها در سازنده (Constructor)
        public LeavesController(OfficeDbContext context, IdentityDbContext identityContext, WorkflowDbContext workflowContext, UserManager<User> userManager, IAuthorizationFacade authorizationFacade, NotificationService notificationService, WorkflowService workflowService, WorkflowDetailService workflowDetailService)
        {
            _context = context;
            _identityContext = identityContext;
            _workflowContext = workflowContext;
            _userManager = userManager;
            _authorizationFacade = authorizationFacade;
            _notificationService = notificationService;
            _workflowService = workflowService;
            _workflowDetailService = workflowDetailService;
        }

        // GET: Leaves
        public async Task<IActionResult> Index()
        {
            // گرفتن ID کاربر فعلي براي امنيت (فقط مرخصي‌هاي خودش را ببيند)
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

            // چک کردن دسترسي: صاحب درخواست يا مدير امنيت/HR بتواند جزئيات را ببيند
            if (leave.UserId != _userManager.GetUserId(User) && !await _authorizationFacade.IsSecurityAdminAsync()) return Forbid();

            ViewBag.WorkflowDecisions = await LoadWorkflowDecisionsAsync(nameof(Leave), leave.Id);
            ViewBag.WorkflowDetail = await _workflowDetailService.BuildAsync(
                nameof(Leave),
                leave.Id,
                $"مرخصي {leave.User?.FullName ?? leave.UserId}",
                leave.Reason,
                _userManager.GetUserId(User) ?? string.Empty,
                "HR.Approve",
                HttpContext.RequestAborted);

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
            // ?. پيدا کردن ID کاربر لاگين شده به صورت خودکار
            var currentUserId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Unauthorized();
            }

            // ?. ست کردن اطلاعات امنيتي در سمت سرور
            leave.UserId = currentUserId;
            leave.Status = WorkflowStatus.PendingApproval;

            // حذف اعتبارسنجي براي فيلدهايي که کاربر پر نکرده (سيستم پر کرده)
            ModelState.Remove("UserId");
            ModelState.Remove("User");

            if (ModelState.IsValid)
            {
                _context.Add(leave);
                await _context.SaveChangesAsync();
                await _workflowService.StartRoutingAsync(
                    nameof(Leave),
                    leave.UserId,
                    leave.UserId,
                    leave.Id,
                    leave.StartDate);
                await NotifySecurityAdminsAsync(
                    "درخواست مرخصي جديد",
                    "يک درخواست مرخصي جديد در انتظار بررسي است.",
                    NotificationSeverity.Warning,
                    "/Leaves",
                    leave.Id);
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

            // فقط صاحب مرخصي بتواند آن را ويرايش کند
            if (leave.UserId != _userManager.GetUserId(User)) return Forbid();

            return View(leave);
        }

        // POST: Leaves/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,StartDate,EndDate,Reason")] Leave leave)
        {
            if (id != leave.Id) return NotFound();

            // گرفتن اطلاعات اصلي از ديتابيس براي جلوگيري از تغيير UserId يا Status توسط کاربر
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var leave = await _context.Leaves.FirstOrDefaultAsync(item => item.Id == id);
            if (leave == null) return NotFound();

            if (!await _authorizationFacade.IsSecurityAdminAsync()) return Forbid();

            leave.Status = WorkflowStatus.Approved;
            await _context.SaveChangesAsync();
            await _workflowService.RecordDecisionAsync(
                nameof(Leave),
                leave.Id,
                1,
                _userManager.GetUserId(User) ?? string.Empty,
                WorkflowStatus.Approved,
                cancellationToken: HttpContext.RequestAborted);
            await _notificationService.CreateAsync(
                leave.UserId,
                "درخواست مرخصي تاييد شد",
                "درخواست مرخصي شما تاييد شد.",
                NotificationSeverity.Success,
                "/Leaves",
                "Leaves",
                nameof(Leave),
                leave.Id);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var leave = await _context.Leaves.FirstOrDefaultAsync(item => item.Id == id);
            if (leave == null) return NotFound();

            if (!await _authorizationFacade.IsSecurityAdminAsync()) return Forbid();

            leave.Status = WorkflowStatus.Rejected;
            await _context.SaveChangesAsync();
            await _workflowService.RecordDecisionAsync(
                nameof(Leave),
                leave.Id,
                1,
                _userManager.GetUserId(User) ?? string.Empty,
                WorkflowStatus.Rejected,
                cancellationToken: HttpContext.RequestAborted);
            await _notificationService.CreateAsync(
                leave.UserId,
                "درخواست مرخصي رد شد",
                "درخواست مرخصي شما رد شد.",
                NotificationSeverity.Danger,
                "/Leaves",
                "Leaves",
                nameof(Leave),
                leave.Id);
            return RedirectToAction(nameof(Index));
        }

        private async Task NotifySecurityAdminsAsync(string title, string message, string severity, string linkUrl, int sourceEntityId)
        {
            var adminRoleIds = await _identityContext.Roles
                .Where(item => item.Name == "Admin" || item.Name == "HrManager")
                .Select(item => item.Id)
                .ToListAsync();

            if (adminRoleIds.Count == 0)
            {
                return;
            }

            var recipientIds = await _identityContext.UserRoles
                .Where(item => adminRoleIds.Contains(item.RoleId))
                .Select(item => item.UserId)
                .Distinct()
                .ToListAsync();

            foreach (var recipientId in recipientIds)
            {
                await _notificationService.CreateAsync(
                    recipientId,
                    title,
                    message,
                    severity,
                    linkUrl,
                    "Leaves",
                    nameof(Leave),
                    sourceEntityId);
            }
        }

        private async Task<List<WorkflowDecision>> LoadWorkflowDecisionsAsync(string documentType, int documentId)
        {
            return await _workflowContext.WorkflowDecisions
                .AsNoTracking()
                .Include(item => item.DecidedByUser)
                .Include(item => item.WorkflowInstance)
                .Where(item => item.WorkflowInstance != null &&
                               item.WorkflowInstance.DocumentType == documentType &&
                               item.WorkflowInstance.DocumentId == documentId)
                .OrderByDescending(item => item.DecidedAt)
                .ToListAsync();
        }

        private bool LeaveExists(int id)
        {
            return _context.Leaves.Any(e => e.Id == id);
        }
    }
}


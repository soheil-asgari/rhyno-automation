using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;
using OfficeAutomation.Models;

namespace OfficeAutomation.Services
{
    public class LeaveWorkflowService
    {
        private readonly IWorkflowDbContext _context;
        private readonly WorkflowService _workflowService;

        public LeaveWorkflowService(IWorkflowDbContext context, WorkflowService workflowService)
        {
            _context = context;
            _workflowService = workflowService;
        }

        /// <summary>
        ///  ⁄ÌÌ‰ Ê÷⁄Ì  „ ‰Ì »⁄œÌ »— «”«”  «ÌÌœ Ì« —œ
        /// </summary>
        public string GetNextStatus(string currentStatus, bool isApproved)
        {
            return _workflowService.GetLeaveNextStatus(currentStatus, isApproved);
        }

        public async Task<string?> GetManagerIdForUser(string userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return null;

            // ?. «Ê· çò ò‰ ¬Ì« „œÌ— „” ﬁÌ„ »—«Ì‘  ⁄—Ì› ‘œÂø
            if (!string.IsNullOrEmpty(user.ManagerId))
            {
                return user.ManagerId;
            }

            // ?. «ê— „œÌ— „” ﬁÌ„ ‰œ«‘ ° ÿ»ﬁ „‰ÿﬁ ﬁ»·Ì œ— Ê«Õœ ŒÊœ‘ œ‰»«· „œÌ— »ê—œ
            var manager = await _context.Users
                .FirstOrDefaultAsync(u => u.Department == user.Department
                                       && u.IsManager == true
                                       && u.Id != userId);

            return manager?.Id;
        }

        public async Task<List<string>> GetHRUserIds()
        {
            var hrDepartmentId = await _context.Departments
                .Where(d => d.Name == "HR")
                .Select(d => d.Id)
                .FirstOrDefaultAsync();

            return await _context.Users
                .Where(u => u.DepartmentId == hrDepartmentId)
                .Select(u => u.Id)
                .ToListAsync();
        }

    }
}

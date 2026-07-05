using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;

namespace OfficeAutomation.Services
{
    public sealed class OfficeService : IOfficeService
    {
        private readonly OfficeDbContext _context;

        public OfficeService(OfficeDbContext context)
        {
            _context = context;
        }

        public async Task<bool> IsEmployeeActiveAsync(int employeeId, CancellationToken cancellationToken)
        {
            return await _context.HumanCapitalEmployees
                .AsNoTracking()
                .AnyAsync(item => item.Id == employeeId && item.CurrentStatus == "فعال", cancellationToken);
        }

        public async Task<List<EmployeeOptionDto>> GetActiveEmployeeOptionsAsync(CancellationToken cancellationToken)
        {
            return await _context.HumanCapitalEmployees
                .AsNoTracking()
                .Where(item => item.CurrentStatus == "فعال")
                .OrderBy(item => item.FullName)
                .Select(item => new EmployeeOptionDto
                {
                    Id = item.Id,
                    FullName = item.FullName,
                    PersonnelCode = item.PersonnelCode
                })
                .ToListAsync(cancellationToken);
        }
    }
}

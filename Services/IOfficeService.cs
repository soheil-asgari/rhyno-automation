using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OfficeAutomation.Services
{
    public sealed class EmployeeOptionDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string PersonnelCode { get; set; } = string.Empty;
    }

    public interface IOfficeService
    {
        Task<bool> IsEmployeeActiveAsync(int employeeId, CancellationToken cancellationToken);
        Task<List<EmployeeOptionDto>> GetActiveEmployeeOptionsAsync(CancellationToken cancellationToken);
    }
}

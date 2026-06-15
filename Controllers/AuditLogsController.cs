using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Data;
using OfficeAutomation.Filters;
using OfficeAutomation.Models;
using OfficeAutomation.Services.Security;

namespace OfficeAutomation.Controllers
{
    [ApiController]
    [Route("api/admin/audit-logs")]
    [Authorize]
    [PermissionAuthorize("AuditLogs.Read")]
    public class AuditLogsController : ControllerBase
    {
        private static readonly string[] SupportedActions = ["Create", "Update", "Delete"];
        private readonly ApplicationDbContext _context;

        public AuditLogsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<AuditLogListItemDto>>> GetAuditLogs(
            [FromQuery] AuditLogQueryParameters query,
            CancellationToken cancellationToken)
        {
            var page = Math.Max(query.Page, 1);
            var pageSize = Math.Clamp(query.PageSize, 1, 200);
            var auditedTableNames = GetAuditedTableNames();

            var auditQuery = _context.AuditLogs
                .AsNoTracking()
                .Where(item => auditedTableNames.Contains(item.TableName));

            if (!string.IsNullOrWhiteSpace(query.UserId))
            {
                auditQuery = auditQuery.Where(item => item.UserId == query.UserId);
            }

            if (!string.IsNullOrWhiteSpace(query.Action))
            {
                auditQuery = auditQuery.Where(item => item.Action == query.Action);
            }

            if (!string.IsNullOrWhiteSpace(query.TableName))
            {
                auditQuery = auditQuery.Where(item => item.TableName == query.TableName);
            }

            auditQuery = ApplyModuleFilter(auditQuery, query.Module);

            if (query.SensitiveOnly == true)
            {
                auditQuery = auditQuery.Where(item => item.IsSensitive);
            }

            if (query.From.HasValue)
            {
                auditQuery = auditQuery.Where(item => item.DateTime >= query.From.Value);
            }

            if (query.To.HasValue)
            {
                auditQuery = auditQuery.Where(item => item.DateTime <= query.To.Value);
            }

            var totalCount = await auditQuery.CountAsync(cancellationToken);
            var logs = await auditQuery
                .OrderByDescending(item => item.DateTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var userIds = logs
                .Where(item => !string.IsNullOrWhiteSpace(item.UserId))
                .Select(item => item.UserId!)
                .Distinct()
                .ToList();

            var userMap = userIds.Count == 0
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : await _context.Users
                    .AsNoTracking()
                    .Where(item => userIds.Contains(item.Id))
                    .Select(item => new
                    {
                        item.Id,
                        DisplayName = string.IsNullOrWhiteSpace(item.FullName)
                            ? (item.UserName ?? item.Email ?? item.Id)
                            : item.FullName
                    })
                    .ToDictionaryAsync(item => item.Id, item => item.DisplayName, cancellationToken);

            return Ok(new PagedResult<AuditLogListItemDto>
            {
                Items = logs.Select(item => new AuditLogListItemDto
                {
                    Id = item.Id,
                    UserId = item.UserId,
                    UserDisplayName = item.UserId != null && userMap.TryGetValue(item.UserId, out var userName) ? userName : null,
                    Action = item.Action,
                    TableName = item.TableName,
                    DateTime = item.DateTime,
                    OldValues = item.OldValues,
                    NewValues = item.NewValues,
                    AffectedColumns = item.AffectedColumns,
                    UserIP = item.UserIP,
                    UserAgent = item.UserAgent,
                    IsSensitive = item.IsSensitive,
                    Module = ResolveModuleName(item.TableName)
                }).ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }

        [HttpGet("filter-options")]
        public async Task<ActionResult<AuditLogFilterOptionsDto>> GetFilterOptions(CancellationToken cancellationToken)
        {
            var auditedTableNames = GetAuditedTableNames();

            var tableNames = await _context.AuditLogs
                .AsNoTracking()
                .Where(item => auditedTableNames.Contains(item.TableName))
                .Select(item => item.TableName)
                .Distinct()
                .OrderBy(item => item)
                .ToListAsync(cancellationToken);

            var userIds = await _context.AuditLogs
                .AsNoTracking()
                .Where(item => auditedTableNames.Contains(item.TableName))
                .Where(item => item.UserId != null)
                .Select(item => item.UserId!)
                .Distinct()
                .ToListAsync(cancellationToken);

            var users = userIds.Count == 0
                ? []
                : await _context.Users
                    .AsNoTracking()
                    .Where(item => userIds.Contains(item.Id))
                    .OrderBy(item => item.FullName ?? item.UserName)
                    .Select(item => new AuditLogFilterOptionDto
                    {
                        Value = item.Id,
                        Label = string.IsNullOrWhiteSpace(item.FullName)
                            ? (item.UserName ?? item.Email ?? item.Id)
                            : item.FullName
                    })
                    .ToListAsync(cancellationToken);

            var modules = await _context.AuditLogs
                .AsNoTracking()
                .Where(item => auditedTableNames.Contains(item.TableName))
                .Select(item => item.TableName)
                .Distinct()
                .ToListAsync(cancellationToken);

            return Ok(new AuditLogFilterOptionsDto
            {
                Users = users,
                Actions = SupportedActions,
                TableNames = tableNames,
                Modules = modules
                    .Select(ResolveModuleName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(item => item)
                    .ToList()
            });
        }

        [HttpGet("export")]
        [PermissionAuthorize("AuditLogs.Export")]
        public async Task<IActionResult> ExportAuditLogs(
            [FromQuery] AuditLogQueryParameters query,
            CancellationToken cancellationToken)
        {
            var auditedTableNames = GetAuditedTableNames();
            var auditQuery = _context.AuditLogs
                .AsNoTracking()
                .Where(item => auditedTableNames.Contains(item.TableName));

            if (!string.IsNullOrWhiteSpace(query.UserId))
            {
                auditQuery = auditQuery.Where(item => item.UserId == query.UserId);
            }

            if (!string.IsNullOrWhiteSpace(query.Action))
            {
                auditQuery = auditQuery.Where(item => item.Action == query.Action);
            }

            if (!string.IsNullOrWhiteSpace(query.TableName))
            {
                auditQuery = auditQuery.Where(item => item.TableName == query.TableName);
            }

            auditQuery = ApplyModuleFilter(auditQuery, query.Module);

            if (query.SensitiveOnly == true)
            {
                auditQuery = auditQuery.Where(item => item.IsSensitive);
            }

            if (query.From.HasValue)
            {
                auditQuery = auditQuery.Where(item => item.DateTime >= query.From.Value);
            }

            if (query.To.HasValue)
            {
                auditQuery = auditQuery.Where(item => item.DateTime <= query.To.Value);
            }

            var rows = await auditQuery
                .OrderByDescending(item => item.DateTime)
                .Take(5000)
                .ToListAsync(cancellationToken);

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("DateTime,UserId,Action,Module,TableName,Sensitive,IP,AffectedColumns");
            foreach (var row in rows)
            {
                static string Escape(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
                csv.AppendLine(string.Join(",",
                    Escape(row.DateTime.ToString("yyyy-MM-dd HH:mm:ss")),
                    Escape(row.UserId),
                    Escape(row.Action),
                    Escape(ResolveModuleName(row.TableName)),
                    Escape(row.TableName),
                    Escape(row.IsSensitive ? "Yes" : "No"),
                    Escape(row.UserIP),
                    Escape(row.AffectedColumns)));
            }

            var bytes = System.Text.Encoding.UTF8.GetPreamble()
                .Concat(System.Text.Encoding.UTF8.GetBytes(csv.ToString()))
                .ToArray();
            return File(bytes, "text/csv;charset=utf-8", $"audit-logs-{DateTime.Now:yyyyMMddHHmmss}.csv");
        }

        private HashSet<string> GetAuditedTableNames()
        {
            return _context.Model
                .GetEntityTypes()
                .Where(entityType => AuditLogScope.IsAuditedEntity(entityType.ClrType))
                .Select(entityType => entityType.GetTableName() ?? entityType.ClrType.Name)
                .ToHashSet(StringComparer.Ordinal);
        }

        private static string ResolveModuleName(string tableName)
        {
            if (tableName.Contains("Invoice", StringComparison.OrdinalIgnoreCase) ||
                tableName.Contains("Payroll", StringComparison.OrdinalIgnoreCase))
            {
                return "Finance";
            }

            if (tableName.Contains("Warehouse", StringComparison.OrdinalIgnoreCase) ||
                tableName.Contains("Inventory", StringComparison.OrdinalIgnoreCase))
            {
                return "Warehouse";
            }

            if (tableName.Contains("Letter", StringComparison.OrdinalIgnoreCase))
            {
                return "Letters";
            }

            if (tableName.Contains("Leave", StringComparison.OrdinalIgnoreCase) ||
                tableName.Contains("HumanCapital", StringComparison.OrdinalIgnoreCase) ||
                tableName.Contains("Insurance", StringComparison.OrdinalIgnoreCase))
            {
                return "HR";
            }

            if (tableName.Contains("Role", StringComparison.OrdinalIgnoreCase) ||
                tableName.Contains("Permission", StringComparison.OrdinalIgnoreCase) ||
                tableName.Contains("User", StringComparison.OrdinalIgnoreCase))
            {
                return "Security";
            }

            if (tableName.Contains("Calendar", StringComparison.OrdinalIgnoreCase))
            {
                return "Calendar";
            }

            if (tableName.Contains("Archive", StringComparison.OrdinalIgnoreCase) ||
                tableName.Contains("Document", StringComparison.OrdinalIgnoreCase))
            {
                return "Archive";
            }

            return "General";
        }

        private static IQueryable<AuditLog> ApplyModuleFilter(IQueryable<AuditLog> query, string? module)
        {
            if (string.IsNullOrWhiteSpace(module))
            {
                return query;
            }

            return module switch
            {
                "Finance" => query.Where(item => item.TableName.Contains("Invoice") || item.TableName.Contains("Payroll")),
                "Warehouse" => query.Where(item => item.TableName.Contains("Warehouse") || item.TableName.Contains("Inventory")),
                "Letters" => query.Where(item => item.TableName.Contains("Letter")),
                "HR" => query.Where(item => item.TableName.Contains("Leave") || item.TableName.Contains("HumanCapital") || item.TableName.Contains("Insurance")),
                "Security" => query.Where(item => item.TableName.Contains("Role") || item.TableName.Contains("Permission") || item.TableName.Contains("User")),
                "Calendar" => query.Where(item => item.TableName.Contains("Calendar")),
                "Archive" => query.Where(item => item.TableName.Contains("Archive") || item.TableName.Contains("Document")),
                _ => query
            };
        }
    }
}

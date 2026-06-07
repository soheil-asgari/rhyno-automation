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

            var auditQuery = _context.AuditLogs.AsNoTracking();

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
                    UserAgent = item.UserAgent
                }).ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }

        [HttpGet("filter-options")]
        public async Task<ActionResult<AuditLogFilterOptionsDto>> GetFilterOptions(CancellationToken cancellationToken)
        {
            var tableNames = await _context.AuditLogs
                .AsNoTracking()
                .Select(item => item.TableName)
                .Distinct()
                .OrderBy(item => item)
                .ToListAsync(cancellationToken);

            var userIds = await _context.AuditLogs
                .AsNoTracking()
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

            return Ok(new AuditLogFilterOptionsDto
            {
                Users = users,
                Actions = SupportedActions,
                TableNames = tableNames
            });
        }
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Platform.Application.SavedViews;
using OfficeAutomation.Modules.Platform.Domain;
using OfficeAutomation.Modules.Platform.Infrastructure.Persistence;
using OfficeAutomation.Services.Security;

namespace OfficeAutomation.Controllers;

[Authorize]
[ApiController]
[Route("api/saved-views")]
public sealed class SavedViewsController : ControllerBase
{
    private readonly PlatformDbContext _context;
    private readonly ITableSchemaRegistry _schemaRegistry;
    private readonly ICurrentUserContextAccessor _currentUserContextAccessor;

    public SavedViewsController(
        PlatformDbContext context,
        ITableSchemaRegistry schemaRegistry,
        ICurrentUserContextAccessor currentUserContextAccessor)
    {
        _context = context;
        _schemaRegistry = schemaRegistry;
        _currentUserContextAccessor = currentUserContextAccessor;
    }

    [HttpGet("{targetGridId}")]
    public async Task<IActionResult> Get(string targetGridId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var accessTokens = await GetAccessTokensAsync(cancellationToken);

        var views = await _context.SavedViewDefinitions
            .AsNoTracking()
            .Where(item => item.TargetGridId == targetGridId && item.UserId == userId)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        return Ok(views.Select(item => new
        {
            item.Id,
            item.Name,
            item.TargetGridId,
            item.FilterQueryJson,
            columnLayoutJson = _schemaRegistry.MaskColumnLayoutJson(item.TargetGridId, item.ColumnLayoutJson, accessTokens)
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] SaveSavedViewRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.TargetGridId))
        {
            return BadRequest(new { message = "نام نما و شناسه گرید الزامی است." });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var view = await _context.SavedViewDefinitions
            .FirstOrDefaultAsync(item =>
                item.TargetGridId == request.TargetGridId &&
                item.UserId == userId &&
                item.Name == request.Name,
                cancellationToken);

        if (view == null)
        {
            view = new SavedViewDefinition
            {
                Name = request.Name.Trim(),
                TargetGridId = request.TargetGridId.Trim(),
                UserId = userId
            };
            _context.SavedViewDefinitions.Add(view);
        }

        view.ColumnLayoutJson = string.IsNullOrWhiteSpace(request.ColumnLayoutJson) ? "[]" : request.ColumnLayoutJson;
        view.FilterQueryJson = string.IsNullOrWhiteSpace(request.FilterQueryJson) ? "{}" : request.FilterQueryJson;
        view.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { view.Id, view.Name });
    }

    private async Task<IReadOnlyCollection<string>> GetAccessTokensAsync(CancellationToken cancellationToken)
    {
        var profile = await _currentUserContextAccessor.GetAccessProfileAsync(cancellationToken);
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var role in User.FindAll(ClaimTypes.Role).Select(item => item.Value))
        {
            tokens.Add(role);
        }

        if (profile != null)
        {
            foreach (var role in profile.Roles)
            {
                tokens.Add(role);
            }

            foreach (var permission in profile.Permissions)
            {
                tokens.Add(permission);
            }
        }

        return tokens;
    }
}

public sealed class SaveSavedViewRequest
{
    public string Name { get; set; } = string.Empty;
    public string TargetGridId { get; set; } = string.Empty;
    public string ColumnLayoutJson { get; set; } = "[]";
    public string FilterQueryJson { get; set; } = "{}";
}

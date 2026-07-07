using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OfficeAutomation.Models;
using OfficeAutomation.Services;
using OfficeAutomation.Services.Security;

namespace OfficeAutomation.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/work-inbox")]
public sealed class WorkInboxApiController : ControllerBase
{
    private readonly WorkInboxService _workInboxService;
    private readonly IAuthorizationFacade _authorizationFacade;

    public WorkInboxApiController(WorkInboxService workInboxService, IAuthorizationFacade authorizationFacade)
    {
        _workInboxService = workInboxService;
        _authorizationFacade = authorizationFacade;
    }

    [HttpGet]
    public async Task<ActionResult<WorkInboxResponse>> Get([FromQuery] string? filter, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var canApproveGlobal = await _authorizationFacade.IsSecurityAdminAsync();
        var model = await _workInboxService.BuildAsync(userId, canApproveGlobal, filter, cancellationToken);

        return Ok(WorkInboxResponse.From(model));
    }
}

public sealed record WorkInboxResponse(
    string ActiveFilter,
    int TotalCount,
    int ApprovalCount,
    int LetterCount,
    int AlertCount,
    int OverdueCount,
    int DueSoonCount,
    int UnreadCount,
    IReadOnlyCollection<WorkInboxItemDto> Items)
{
    public static WorkInboxResponse From(WorkInboxVM model)
    {
        return new WorkInboxResponse(
            model.ActiveFilter,
            model.TotalCount,
            model.ApprovalCount,
            model.LetterCount,
            model.AlertCount,
            model.OverdueCount,
            model.DueSoonCount,
            model.UnreadCount,
            model.Items.Select(WorkInboxItemDto.From).ToList());
    }
}

public sealed record WorkInboxItemDto(
    string Id,
    string Type,
    string Module,
    string Title,
    string? Description,
    string? Status,
    string? Priority,
    string? CreatedAtText,
    string? Url,
    bool RequiresAction,
    bool IsRead,
    bool IsOverdue,
    bool CanInlineApprove,
    bool CanInlineReject)
{
    public static WorkInboxItemDto From(WorkInboxItemVM item)
    {
        return new WorkInboxItemDto(
            item.Id,
            item.Type,
            item.Module,
            item.Title,
            item.Description,
            item.Status,
            item.Priority,
            item.CreatedAtText,
            item.Url,
            item.RequiresAction,
            item.IsRead,
            item.IsOverdue,
            item.CanInlineApprove,
            item.CanInlineReject);
    }
}

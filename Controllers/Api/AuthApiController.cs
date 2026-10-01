using OfficeAutomation.Services.Licensing;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OfficeAutomation.Models;

namespace OfficeAutomation.Controllers.Api;

[ApiController]
[Route("api/auth")]
[IgnoreAntiforgeryToken]
[LicenseExempt]
public sealed class AuthApiController : ControllerBase
{
    private readonly SignInManager<User> _signInManager;
    private readonly UserManager<User> _userManager;

    public AuthApiController(SignInManager<User> signInManager, UserManager<User> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [HttpGet("me")]
    public async Task<ActionResult<AuthUserDto>> Me(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(AuthUserDto.From(user, roles.ToArray()));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthUserDto>> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "UserName and Password are required." });
        }

        var result = await _signInManager.PasswordSignInAsync(
            request.UserName.Trim(),
            request.Password,
            request.RememberMe,
            lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            return StatusCode(StatusCodes.Status423Locked, new { error = "Account is locked." });
        }

        if (!result.Succeeded)
        {
            return Unauthorized(new { error = "Invalid credentials." });
        }

        var user = await _userManager.FindByNameAsync(request.UserName.Trim());
        if (user == null)
        {
            return Unauthorized();
        }

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(AuthUserDto.From(user, roles.ToArray()));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return NoContent();
    }
}

public sealed record LoginRequest(string UserName, string Password, bool RememberMe);

public sealed record AuthUserDto(
    string Id,
    string? UserName,
    string? FullName,
    string? Email,
    IReadOnlyCollection<string> Roles,
    bool CanAccessFinance,
    bool CanAccessWarehouse,
    bool CanAccessHumanCapital,
    bool CanAccessSystemSettings)
{
    public static AuthUserDto From(User user, IReadOnlyCollection<string> roles)
    {
        return new AuthUserDto(
            user.Id,
            user.UserName,
            string.IsNullOrWhiteSpace(user.FullName) ? user.UserName : user.FullName,
            user.Email,
            roles,
            user.CanAccessFinance,
            user.CanAccessWarehouse,
            user.CanAccessHumanCapital,
            user.CanAccessSystemSettings);
    }
}

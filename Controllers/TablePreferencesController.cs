using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Identity.Infrastructure.Persistence;
using OfficeAutomation.Models;

namespace OfficeAutomation.Controllers
{
    [Authorize]
    [Route("table-preferences")]
    public class TablePreferencesController : Controller
    {
        private readonly IdentityDbContext _context;

        public TablePreferencesController(IdentityDbContext context)
        {
            _context = context;
        }

        [HttpGet("{key}")]
        public async Task<IActionResult> Get(string key, CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var preference = await _context.UserPreferences
                .AsNoTracking()
                .Where(item => item.UserId == userId)
                .Select(item => item.TablePreferencesJson)
                .FirstOrDefaultAsync(cancellationToken);

            var map = ParsePreferenceMap(preference);
            return Json(map.TryGetValue(key, out var value) ? value : new JsonObjectLike());
        }

        [HttpPost("{key}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(string key, [FromBody] JsonElement value, CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var preference = await _context.UserPreferences.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
            if (preference == null)
            {
                preference = new UserPreference
                {
                    UserId = userId,
                    ThemePreference = "System"
                };
                _context.UserPreferences.Add(preference);
            }

            var map = ParsePreferenceMap(preference.TablePreferencesJson);
            map[key] = value.Clone();
            preference.TablePreferencesJson = JsonSerializer.Serialize(map);
            preference.UpdatedAtUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            return Json(new { success = true });
        }

        private static Dictionary<string, JsonElement> ParsePreferenceMap(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new Dictionary<string, JsonElement>();
            }

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? new Dictionary<string, JsonElement>();
            }
            catch (JsonException)
            {
                return new Dictionary<string, JsonElement>();
            }
        }

        private sealed class JsonObjectLike
        {
        }
    }
}


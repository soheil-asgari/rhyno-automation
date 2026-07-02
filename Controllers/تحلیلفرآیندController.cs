using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OfficeAutomation.Services.Security;

namespace OfficeAutomation.Controllers;

[Authorize]
[PermissionAuthorize("تحلیل.مشاهده")]
[Route("تحلیلفرآیند")]
public sealed class تحلیلفرآیندController : Controller
{
    [HttpGet("")]
    [HttpGet("شاخص")]
    public IActionResult شاخص()
    {
        return View("~/Views/تحلیلفرآیند/شاخص.cshtml");
    }

    [HttpGet("دادگانمودار")]
    public IActionResult دادگانمودار()
    {
        var labels = new[] { "نامه", "فاکتور", "انبار", "مرخصی" };
        return Json(new
        {
            زمانچرخه = new
            {
                میانگینساعت = 0,
                تعدادنمونه = 0,
                اقلام = Array.Empty<object>(),
                نمودارروند = Chart("روند زمان چرخه", labels)
            },
            گلوگاهها = new
            {
                اقلام = Array.Empty<object>(),
                نمودارگلوگاهها = Chart("گلوگاه‌ها", labels)
            },
            ماندگاریتایید = new
            {
                تعدادکل = 0,
                اقلام = Array.Empty<object>(),
                نمودارتوزیع = Chart("ماندگاری تایید", labels)
            },
            رویدادهایممیزی = new
            {
                تعدادکل = 0,
                تعدادحساس = 0,
                اقلام = Array.Empty<object>(),
                نموداررویدادها = Chart("رویدادهای ممیزی", labels)
            }
        });
    }

    [HttpGet("خروجیاکسل")]
    [PermissionAuthorize("تحلیل.خروجی")]
    public IActionResult خروجیاکسل()
    {
        return File(
            System.Text.Encoding.UTF8.GetBytes("گزارش تحلیل فرآیند"),
            "text/plain; charset=utf-8",
            "process-analytics.txt");
    }

    private static object Chart(string title, IReadOnlyList<string> labels)
    {
        return new
        {
            عنوان = title,
            برچسبها = labels,
            سریها = new[]
            {
                new
                {
                    برچسب = title,
                    رنگ = "rgba(37, 99, 235, .72)",
                    مقادیر = labels.Select(_ => 0m).ToArray()
                }
            }
        };
    }
}

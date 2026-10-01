using OfficeAutomation.Services.Licensing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OfficeAutomation.Services.Security;

namespace OfficeAutomation.Controllers;

[Authorize]
[PermissionAuthorize("تحلیل.مشاهده")]
[Route("ProcessAnalysis")]
[RequiresModule(LicensableModules.Workflow)]
public sealed class ProcessAnalysisController : Controller
{
    [HttpGet("")]
    [HttpGet("Index")]
    public IActionResult Index()
    {
        return View("~/Views/ProcessAnalysis/Index.cshtml");
    }

    [HttpGet("ChartData")]
    public IActionResult GetChartData()
    {
        var labels = new[] { "نامه", "فاکتور", "انبار", "مرخصی" };
        return Json(new
        {
            cycleTime = new
            {
                averageHours = 0,
                sampleCount = 0,
                items = Array.Empty<object>(),
                trendChart = Chart("روند زمان چرخه", labels)
            },
            bottlenecks = new
            {
                items = Array.Empty<object>(),
                bottleneckChart = Chart("گلوگاه‌ها", labels)
            },
            approvalDuration = new
            {
                totalCount = 0,
                items = Array.Empty<object>(),
                distributionChart = Chart("ماندگاری تایید", labels)
            },
            auditEvents = new
            {
                totalCount = 0,
                sensitiveCount = 0,
                items = Array.Empty<object>(),
                eventChart = Chart("رویدادهای ممیزی", labels)
            }
        });
    }

    [HttpGet("ExportExcel")]
    [PermissionAuthorize("تحلیل.خروجی")]
    public IActionResult ExportExcel()
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
            title = title,
            labels = labels,
            series = new[]
            {
                new
                {
                    label = title,
                    color = "rgba(37, 99, 235, .72)",
                    values = labels.Select(_ => 0m).ToArray()
                }
            }
        };
    }
}

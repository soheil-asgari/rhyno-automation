using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;
using OfficeAutomation.Services.Contracts;
using Xunit;

namespace OfficeAutomation.Tests;

public class ContractExportPdfFallbackTests : IDisposable
{
    private readonly string? _origForceFallback;
    private readonly string? _origPlaywrightPath;

    public ContractExportPdfFallbackTests()
    {
        _origForceFallback = Environment.GetEnvironmentVariable("FORCE_PDF_FALLBACK");
        _origPlaywrightPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("FORCE_PDF_FALLBACK", _origForceFallback);
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", _origPlaywrightPath);
        ContractExportService.ResetBrowserCheckCache();
    }

    private static OfficeDbContext CreateOfficeContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<OfficeDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new OfficeDbContext(options);
    }

    [Fact]
    public void IsPlaywrightBrowserAvailable_WhenForceFallbackIsTrue_ReturnsFalseAndLogsWarning()
    {
        Environment.SetEnvironmentVariable("FORCE_PDF_FALLBACK", "true");
        ContractExportService.ResetBrowserCheckCache();

        var loggerMock = new Mock<ILogger>();

        bool available = ContractExportService.IsPlaywrightBrowserAvailable(loggerMock.Object);

        Assert.False(available);
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("FORCE_PDF_FALLBACK is set to true")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExportContractsToPdfAsync_WhenBrowserMissing_ProducesValidPdfViaQuestPdfFallback()
    {
        Environment.SetEnvironmentVariable("FORCE_PDF_FALLBACK", "true");
        ContractExportService.ResetBrowserCheckCache();

        using var db = CreateOfficeContext(nameof(ExportContractsToPdfAsync_WhenBrowserMissing_ProducesValidPdfViaQuestPdfFallback));
        db.Contracts.Add(new Contract
        {
            Id = 1,
            ContractNumber = "CNT-2026-001",
            Title = "قرارداد پشتیبانی نرم‌افزار",
            ContractType = "پشتیبانی",
            Status = "فعال",
            TotalAmount = 50_000_000,
            TotalAmountInclusiveTax = 54_500_000,
            StartDate = DateTime.UtcNow.AddDays(-10),
            EndDate = DateTime.UtcNow.AddDays(350),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var reportServiceMock = new Mock<IContractReportService>();
        var templateRendererMock = new Mock<ITemplateRenderingService>();
        var loggerMock = new Mock<ILogger<ContractExportService>>();

        var service = new ContractExportService(db, reportServiceMock.Object, templateRendererMock.Object, loggerMock.Object);

        var filters = new ContractIndexVM();
        var pdfBytes = await service.ExportContractsToPdfAsync(filters, "test-user");

        Assert.NotNull(pdfBytes);
        Assert.NotEmpty(pdfBytes);

        // Standard PDF magic header check "%PDF-"
        Assert.True(pdfBytes.Length > 10);
        string header = System.Text.Encoding.ASCII.GetString(pdfBytes, 0, 5);
        Assert.Equal("%PDF-", header);
    }

    [Theory]
    [InlineData("aging")]
    [InlineData("revenue")]
    [InlineData("pipeline")]
    [InlineData("calendar")]
    [InlineData("exposure")]
    public async Task ExportReportToPdfAsync_WhenBrowserMissing_ProducesValidPdfViaQuestPdfFallback(string reportType)
    {
        Environment.SetEnvironmentVariable("FORCE_PDF_FALLBACK", "true");
        ContractExportService.ResetBrowserCheckCache();

        using var db = CreateOfficeContext($"db_report_{reportType}");
        var reportServiceMock = new Mock<IContractReportService>();

        reportServiceMock.Setup(r => r.GetPaymentAgingReportAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentAgingReportVM
            {
                TotalOverdue = 100_000_000,
                TotalOverdueCount = 5,
                AverageOverdueAmount = 20_000_000,
                MaxDaysOverdue = 45,
                Buckets = new List<AgingBucketVM>
                {
                    new() { BucketName = "0-30 روز", Count = 3, TotalAmount = 40_000_000 },
                    new() { BucketName = "31-60 روز", Count = 2, TotalAmount = 60_000_000 }
                }
            });

        reportServiceMock.Setup(r => r.GetRevenueRecognitionScheduleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RevenueRecognitionReportVM
            {
                TotalRecognized = 120_000_000,
                TotalScheduled = 200_000_000,
                TotalActiveValue = 350_000_000,
                ActiveContractCount = 8,
                Periods = new List<RevenuePeriodVM>
                {
                    new() { PeriodLabel = "فروردین", RecognizedAmount = 50_000_000, ScheduledAmount = 80_000_000 },
                    new() { PeriodLabel = "اردیبهشت", RecognizedAmount = 70_000_000, ScheduledAmount = 120_000_000 }
                }
            });

        reportServiceMock.Setup(r => r.GetContractValuePipelineAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContractValuePipelineVM
            {
                GrandTotalValue = 500_000_000,
                GrandTotalCount = 10,
                AverageContractValue = 50_000_000,
                Items = new List<ValuePipelineItemVM>
                {
                    new() { Status = "پیش‌نویس", Count = 2, TotalValue = 100_000_000, PercentageOfTotal = 20 },
                    new() { Status = "فعال", Count = 8, TotalValue = 400_000_000, PercentageOfTotal = 80 }
                }
            });

        reportServiceMock.Setup(r => r.GetObligationsCalendarAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ObligationsCalendarVM
            {
                TotalEvents = 4,
                TotalAmountDue = 85_000_000,
                UpcomingMonths = 6,
                Events = new List<ObligationEventVM>
                {
                    new() { Date = DateTime.UtcNow.AddDays(15), ObligationType = "PaymentDue", Count = 1, TotalAmount = 35_000_000 },
                    new() { Date = DateTime.UtcNow.AddDays(45), ObligationType = "PaymentDue", Count = 1, TotalAmount = 50_000_000 }
                }
            });

        reportServiceMock.Setup(r => r.GetFinancialExposureAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinancialExposureVM
            {
                TotalActiveValue = 600_000_000,
                AtRiskValue = 75_000_000,
                PendingApprovalValue = 40_000_000,
                TotalRecognizedRevenue = 300_000_000,
                TotalOutstandingReceivables = 55_000_000,
                CurrencyBreakdown = new List<CurrencyBreakdownVM>
                {
                    new() { CurrencyCode = "IRR", TotalAmount = 600_000_000, ContractCount = 10, Percentage = 100 }
                }
            });

        var loggerMock = new Mock<ILogger<ContractExportService>>();
        var templateRendererMock = new Mock<ITemplateRenderingService>();
        var service = new ContractExportService(db, reportServiceMock.Object, templateRendererMock.Object, loggerMock.Object);

        var pdfBytes = await service.ExportReportToPdfAsync(reportType);

        Assert.NotNull(pdfBytes);
        Assert.NotEmpty(pdfBytes);

        // Standard PDF header verification
        string header = System.Text.Encoding.ASCII.GetString(pdfBytes, 0, 5);
        Assert.Equal("%PDF-", header);
    }
}

using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Platform.Infrastructure.Persistence;
using OfficeAutomation.Filters;
using OfficeAutomation.Models;
using OfficeAutomation.Services;

namespace OfficeAutomation.Controllers
{
    [Authorize]
    [RequireAccessArea("SystemSettings")]
    public class ManagementDashboardController : Controller
    {
        private readonly AiService _aiService;
        private readonly PlatformDbContext _context;
        private readonly IDataProtector _passwordProtector;
        private readonly ILogger<ManagementDashboardController> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly AiSqlSafetyService _sqlSafetyService;

        public ManagementDashboardController(
            AiService aiService,
            PlatformDbContext context,
            IDataProtectionProvider dataProtectionProvider,
            ILogger<ManagementDashboardController> logger,
            IWebHostEnvironment environment,
            AiSqlSafetyService sqlSafetyService)
        {
            _aiService = aiService;
            _context = context;
            _passwordProtector = dataProtectionProvider.CreateProtector("ManagementDashboard.DatabasePasswords.v1");
            _logger = logger;
            _environment = environment;
            _sqlSafetyService = sqlSafetyService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Connections(CancellationToken cancellationToken)
        {
            var items = await _context.ManagementDatabaseConnections
                .AsNoTracking()
                .OrderBy(item => item.Name)
                .Select(item => new SavedDatabaseConnectionDto
                {
                    Id = item.Id,
                    Name = item.Name,
                    Provider = item.Provider,
                    Host = item.Host,
                    Port = item.Port,
                    DatabaseName = item.DatabaseName,
                    Username = item.Username,
                    TrustServerCertificate = item.TrustServerCertificate,
                    Endpoint = item.Port.HasValue && item.Port.Value > 0
                        ? item.Host + ":" + item.Port.Value
                        : item.Host
                })
                .ToListAsync(cancellationToken);

            return Json(new { success = true, items });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveConnection([FromBody] DatabaseConnectionRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "اطلاعات اتصال کامل نیست." });
            }

            var provider = NormalizeProvider(request.Provider);
            var displayName = string.IsNullOrWhiteSpace(request.Name)
                ? $"{provider} - {BuildEndpointLabel(request, provider)}"
                : request.Name.Trim();

            ManagementDatabaseConnection entity;
            if (request.Id.HasValue && request.Id.Value > 0)
            {
                entity = await _context.ManagementDatabaseConnections
                    .FirstOrDefaultAsync(item => item.Id == request.Id.Value, cancellationToken)
                    ?? new ManagementDatabaseConnection();

                if (entity.Id == 0)
                {
                    _context.ManagementDatabaseConnections.Add(entity);
                }
            }
            else
            {
                entity = new ManagementDatabaseConnection
                {
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedByUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                };
                _context.ManagementDatabaseConnections.Add(entity);
            }

            entity.Name = displayName;
            entity.Provider = provider;
            entity.Host = request.Host.Trim();
            entity.Port = request.Port;
            entity.DatabaseName = string.IsNullOrWhiteSpace(request.DatabaseName) ? null : request.DatabaseName.Trim();
            entity.Username = string.IsNullOrWhiteSpace(request.Username) ? null : request.Username.Trim();
            entity.TrustServerCertificate = request.TrustServerCertificate;
            entity.UpdatedAtUtc = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(request.Password))
            {
                entity.ProtectedPassword = _passwordProtector.Protect(request.Password);
            }

            await _context.SaveChangesAsync(cancellationToken);

            return Json(new
            {
                success = true,
                message = "اتصال ذخیره شد.",
                item = ToSavedConnectionDto(entity)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConnection([FromBody] DatabaseConnectionRequest request, CancellationToken cancellationToken)
        {
            if (!request.Id.HasValue || request.Id.Value <= 0)
            {
                return BadRequest(new { success = false, message = "شناسه اتصال معتبر نیست." });
            }

            var entity = await _context.ManagementDatabaseConnections
                .FirstOrDefaultAsync(item => item.Id == request.Id.Value, cancellationToken);
            if (entity == null)
            {
                return NotFound(new { success = false, message = "اتصال پیدا نشد." });
            }

            _context.ManagementDatabaseConnections.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);

            return Json(new { success = true, message = "اتصال حذف شد." });
        }

        private async Task<DatabaseConnectionRequest> HydrateConnectionRequestAsync(
            DatabaseConnectionRequest request,
            CancellationToken cancellationToken)
        {
            if (!request.Id.HasValue || request.Id.Value <= 0)
            {
                return request;
            }

            var saved = await _context.ManagementDatabaseConnections
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == request.Id.Value, cancellationToken);

            if (saved == null)
            {
                return request;
            }

            return new DatabaseConnectionRequest
            {
                Id = saved.Id,
                Name = saved.Name,
                Provider = saved.Provider,
                Host = saved.Host,
                Port = saved.Port,
                DatabaseName = saved.DatabaseName,
                Username = saved.Username,
                Password = string.IsNullOrWhiteSpace(saved.ProtectedPassword)
                    ? request.Password
                    : _passwordProtector.Unprotect(saved.ProtectedPassword),
                TrustServerCertificate = saved.TrustServerCertificate
            };
        }

        private static SavedDatabaseConnectionDto ToSavedConnectionDto(ManagementDatabaseConnection item)
        {
            return new SavedDatabaseConnectionDto
            {
                Id = item.Id,
                Name = item.Name,
                Provider = item.Provider,
                Host = item.Host,
                Port = item.Port,
                DatabaseName = item.DatabaseName,
                Username = item.Username,
                TrustServerCertificate = item.TrustServerCertificate,
                Endpoint = item.Port.HasValue && item.Port.Value > 0
                    ? item.Host + ":" + item.Port.Value
                    : item.Host
            };
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TestDatabaseConnection([FromBody] DatabaseConnectionRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "اطلاعات اتصال کامل نیست." });
            }

            try
            {
                request = await HydrateConnectionRequestAsync(request, cancellationToken);
                var provider = NormalizeProvider(request.Provider);
                var startedAt = DateTimeOffset.UtcNow;

                switch (provider)
                {
                    case "SqlServer":
                        await TestSqlServerAsync(request, cancellationToken);
                        break;
                    case "Sqlite":
                        await TestSqliteAsync(request, cancellationToken);
                        break;
                    case "PostgreSql":
                    case "MySql":
                        await TestTcpEndpointAsync(request, provider, cancellationToken);
                        break;
                    default:
                        return BadRequest(new { success = false, message = "نوع دیتابیس پشتیبانی نمی‌شود." });
                }

                var elapsedMs = Math.Max(1, (int)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds);

                return Json(new
                {
                    success = true,
                    message = "اتصال با موفقیت بررسی شد.",
                    provider,
                    database = request.DatabaseName,
                    endpoint = BuildEndpointLabel(request, provider),
                    latencyMs = elapsedMs
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Management dashboard database connection test failed.");
                return Json(new
                {
                    success = false,
                    message = "اتصال برقرار نشد. آدرس، پورت، نام دیتابیس و دسترسی را بررسی کنید.",
                    detail = _environment.IsDevelopment() ? ex.Message : null
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateReport([FromBody] AiReportRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "متن درخواست گزارش معتبر نیست." });
            }

            var chartType = NormalizeChartType(request.ChartType);

            if (request.Connection != null)
            {
                request.Connection = await HydrateConnectionRequestAsync(request.Connection, cancellationToken);
            }

            if (request.Connection != null &&
                string.Equals(NormalizeProvider(request.Connection.Provider), "SqlServer", StringComparison.Ordinal))
            {
                var databaseReport = await TryGenerateSqlServerReportAsync(request, chartType, cancellationToken);
                if (databaseReport != null)
                {
                    return Json(new { success = true, report = databaseReport });
                }
            }

            var report = BuildDeterministicReport(request, chartType);

            try
            {
                var aiPrompt = $"""
                You are a Persian business intelligence assistant.
                Write a concise Persian management-report summary with 3 actionable insights.
                User request: {request.Prompt}
                Database provider: {request.Provider ?? "not selected"}
                Database name: {request.DatabaseName ?? "not selected"}
                Chart type: {chartType}
                Do not invent SQL queries. Keep it under 110 words.
                """;

                var aiText = await _aiService.AskAsync(aiPrompt, cancellationToken);
                if (!string.IsNullOrWhiteSpace(aiText))
                {
                    report = report with
                    {
                        Summary = aiText.Trim()
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI report generation failed. Falling back to local report.");
            }

            return Json(new { success = true, report });
        }

        private async Task<AiReportResponse?> TryGenerateSqlServerReportAsync(
            AiReportRequest request,
            string chartType,
            CancellationToken cancellationToken)
        {
            try
            {
                var connectionRequest = request.Connection;
                if (connectionRequest == null)
                {
                    return null;
                }

                var schema = await ReadSqlServerSchemaAsync(connectionRequest, cancellationToken);
                if (string.IsNullOrWhiteSpace(schema))
                {
                    return null;
                }

                var sqlPlan = await CreateSqlPlanAsync(request.Prompt, schema, chartType, cancellationToken);
                if (sqlPlan == null || !_sqlSafetyService.IsReadOnlySelect(sqlPlan.Sql))
                {
                    _logger.LogWarning("AI generated invalid or unsafe SQL for management dashboard.");
                    return null;
                }

                _logger.LogInformation("Management dashboard generated SQL: {Sql}", sqlPlan.Sql);

                var rows = await ExecuteTwoColumnSqlAsync(connectionRequest, sqlPlan.Sql, cancellationToken);
                if (rows.Count == 0)
                {
                    return new AiReportResponse
                    {
                        Title = string.IsNullOrWhiteSpace(sqlPlan.Title) ? "گزارش دیتابیس" : sqlPlan.Title,
                        Summary = "Query با موفقیت اجرا شد، اما داده‌ای برای شرایط درخواستی پیدا نشد.",
                        Labels = [],
                        Values = [],
                        Insights = ["برای نتیجه بهتر، نام مرکز هزینه، سال یا بازه زمانی را دقیق‌تر وارد کنید."],
                        ChartType = chartType,
                        IsFromDatabase = true,
                        GeneratedSql = _environment.IsDevelopment() ? sqlPlan.Sql : null
                    };
                }

                rows = CompleteMonthlySeriesIfNeeded(request.Prompt, rows);
                var labels = rows.Select(item => item.Label).ToList();
                var values = rows.Select(item => item.Value).ToList();
                var maxIndex = values.IndexOf(values.Max());
                var minIndex = values.IndexOf(values.Min());
                var total = values.Sum();

                return new AiReportResponse
                {
                    Title = string.IsNullOrWhiteSpace(sqlPlan.Title) ? ResolveTitle(request.Prompt) : sqlPlan.Title,
                    Summary = $"این گزارش از دیتابیس SQL Server خوانده شد. جمع کل برابر {total:N0} است؛ بیشترین مقدار مربوط به «{labels[maxIndex]}» با {values[maxIndex]:N0} و کمترین مقدار مربوط به «{labels[minIndex]}» با {values[minIndex]:N0} است.",
                    Labels = labels,
                    Values = values,
                    ChartType = chartType,
                    IsFromDatabase = true,
                    GeneratedSql = _environment.IsDevelopment() ? sqlPlan.Sql : null,
                    Insights =
                    [
                        $"ماه/دسته «{labels[maxIndex]}» بیشترین هزینه را دارد و باید با اسناد پشتیبان بررسی شود.",
                        $"ماه/دسته «{labels[minIndex]}» کمترین هزینه را دارد؛ اختلاف آن با پیک هزینه می‌تواند الگوی کنترل بودجه را نشان دهد.",
                        "برای تصمیم مدیریتی، خروجی را با حجم تولید، نفرساعت و سفارش‌های همان دوره تطبیق دهید."
                    ]
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SQL Server backed AI report generation failed.");
                return null;
            }
        }

        private async Task<string> ReadSqlServerSchemaAsync(DatabaseConnectionRequest request, CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(BuildSqlServerConnectionString(request));
            await connection.OpenAsync(cancellationToken);

            const string schemaSql = """
                SELECT TOP (1500)
                    s.name AS SchemaName,
                    t.name AS TableName,
                    c.name AS ColumnName,
                    ty.name AS TypeName
                FROM sys.tables t
                INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
                INNER JOIN sys.columns c ON c.object_id = t.object_id
                INNER JOIN sys.types ty ON ty.user_type_id = c.user_type_id
                WHERE t.is_ms_shipped = 0
                  AND (
                        s.name LIKE 'ACC%' OR s.name LIKE 'FIN%' OR s.name LIKE 'GL%' OR
                        t.name LIKE 'ACC%' OR t.name LIKE 'FIN%' OR t.name LIKE 'GL%' OR
                        t.name LIKE '%Account%' OR t.name LIKE '%Ledger%' OR t.name LIKE '%Voucher%' OR
                        t.name LIKE '%Journal%' OR t.name LIKE '%Detail%' OR t.name LIKE '%Detailed%' OR
                        t.name LIKE '%Tafsil%' OR t.name LIKE '%Tafsili%' OR t.name LIKE '%DL%' OR
                        t.name LIKE '%SL%' OR t.name LIKE '%Cost%' OR t.name LIKE '%Center%' OR
                        t.name LIKE N'%تفصیل%' OR t.name LIKE N'%تفضیل%' OR
                        t.name LIKE N'%حساب%' OR t.name LIKE N'%سند%' OR
                        t.name LIKE N'%مرکز%' OR t.name LIKE N'%هزینه%' OR
                        c.name LIKE '%Account%' OR c.name LIKE '%Ledger%' OR c.name LIKE '%Voucher%' OR
                        c.name LIKE '%Detail%' OR c.name LIKE '%Tafsil%' OR c.name LIKE '%Tafsili%' OR
                        c.name LIKE '%DL%' OR c.name LIKE '%SL%' OR c.name LIKE '%Cost%' OR
                        c.name LIKE '%Center%' OR c.name LIKE N'%تفصیل%' OR c.name LIKE N'%تفضیل%' OR
                        c.name LIKE N'%حساب%' OR c.name LIKE N'%سند%' OR
                        c.name LIKE N'%مرکز%' OR c.name LIKE N'%هزینه%'
                  )
                ORDER BY s.name, t.name, c.column_id
                """;

            await using var command = new SqlCommand(schemaSql, connection)
            {
                CommandTimeout = 10
            };

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var builder = new StringBuilder();
            string? currentTable = null;

            while (await reader.ReadAsync(cancellationToken))
            {
                var table = $"[{reader.GetString(0)}].[{reader.GetString(1)}]";
                if (!string.Equals(currentTable, table, StringComparison.Ordinal))
                {
                    if (currentTable != null)
                    {
                        builder.AppendLine();
                    }

                    currentTable = table;
                    builder.Append(table).Append(": ");
                }
                else
                {
                    builder.Append(", ");
                }

                builder
                    .Append('[')
                    .Append(reader.GetString(2))
                    .Append("] ")
                    .Append(reader.GetString(3));
            }

            return builder.ToString();
        }

        private async Task<SqlReportPlan?> CreateSqlPlanAsync(
            string userPrompt,
            string schema,
            string chartType,
            CancellationToken cancellationToken)
        {
            var prompt = $"""
                You are a senior SQL Server BI engineer.
                Convert the Persian user request into ONE safe read-only SQL Server query.
                Return ONLY valid JSON with two string properties: title and sql.

                Rules:
                - The database is Rahkaran ERP. Financial/cost-center concepts are usually modeled through accounting vouchers, ledgers/accounts and detailed accounts (تفصیلی / tafsili / DL / SL).
                - For cost centers, workshops, projects and locations such as کارگاه مراغه, look for matching detailed-account/title/name/code rows and join them to accounting voucher/item rows when possible.
                - Do not assume the requested Persian text is stored in a single table. Search relevant title/name/description columns with LIKE N'%...%'.
                - For "cost" questions, prefer debit/expense/voucher item amount columns from accounting tables and filter expense accounts when the schema exposes account nature/type/group/title columns.
                - SQL must be SELECT-only or WITH ... SELECT.
                - SQL must return exactly two columns named Label and Value.
                - Value must be numeric.
                - Prefer monthly grouping when user asks by month.
                - If the user asks for a full Persian year by month, return all 12 Persian months in order. Use a WITH month list and LEFT JOIN the aggregated result so months with no data return 0.
                - For Persian years stored as strings like 1404/01/15, filter with LIKE N'1404/%' and group by SUBSTRING(dateColumn, 6, 2).
                - If dates are stored as integers like 14040115, filter BETWEEN 14040101 AND 14041230 and group by SUBSTRING(CONVERT(varchar(8), dateColumn), 5, 2).
                - If dates are Gregorian datetime, map Persian year 1404 approximately to 2025-03-21 through 2026-03-20.
                - Do not use INSERT, UPDATE, DELETE, DROP, ALTER, CREATE, MERGE, TRUNCATE, EXEC, stored procedures, temp tables, variables, dynamic SQL, comments, or semicolons.
                - Use bracketed identifiers.
                - If a cost center, workshop, employer, project, department, party, vendor or location is requested, search relevant text columns with LIKE N'%...%'.

                User request:
                {userPrompt}

                Desired chart type: {chartType}

                Database schema:
                {schema}
                """;

            var raw = await _aiService.AskAsync(prompt, cancellationToken);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var json = ExtractJsonObject(raw);
            return JsonSerializer.Deserialize<SqlReportPlan>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        private async Task<List<ReportDataPoint>> ExecuteTwoColumnSqlAsync(
            DatabaseConnectionRequest request,
            string sql,
            CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(BuildSqlServerConnectionString(request));
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(sql, connection)
            {
                CommandTimeout = 20
            };

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var result = new List<ReportDataPoint>();

            while (await reader.ReadAsync(cancellationToken) && result.Count < 100)
            {
                var label = reader.IsDBNull(0) ? "نامشخص" : Convert.ToString(reader.GetValue(0)) ?? "نامشخص";
                var value = reader.IsDBNull(1) ? 0 : Convert.ToDecimal(reader.GetValue(1));
                result.Add(new ReportDataPoint(label, value));
            }

            return result;
        }

        private static async Task TestSqlServerAsync(DatabaseConnectionRequest request, CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(BuildSqlServerConnectionString(request));
            await connection.OpenAsync(cancellationToken);
        }

        private static string BuildSqlServerConnectionString(DatabaseConnectionRequest request)
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = request.Port.HasValue && request.Port.Value > 0
                    ? $"{request.Host.Trim()},{request.Port.Value}"
                    : request.Host.Trim(),
                InitialCatalog = string.IsNullOrWhiteSpace(request.DatabaseName) ? "master" : request.DatabaseName.Trim(),
                TrustServerCertificate = request.TrustServerCertificate,
                Encrypt = request.TrustServerCertificate
                    ? SqlConnectionEncryptOption.Optional
                    : SqlConnectionEncryptOption.Mandatory,
                ConnectTimeout = 5
            };

            if (string.IsNullOrWhiteSpace(request.Username))
            {
                builder.IntegratedSecurity = true;
            }
            else
            {
                builder.UserID = request.Username.Trim();
                builder.Password = request.Password ?? string.Empty;
            }

            return builder.ConnectionString;
        }

        private static async Task TestSqliteAsync(DatabaseConnectionRequest request, CancellationToken cancellationToken)
        {
            var databasePath = string.IsNullOrWhiteSpace(request.DatabaseName)
                ? request.Host.Trim()
                : request.DatabaseName.Trim();

            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate
            };

            await using var connection = new SqliteConnection(builder.ConnectionString);
            await connection.OpenAsync(cancellationToken);
        }

        private static async Task TestTcpEndpointAsync(DatabaseConnectionRequest request, string provider, CancellationToken cancellationToken)
        {
            var port = request.Port ?? (provider == "PostgreSql" ? 5432 : 3306);
            using var client = new TcpClient();
            await client.ConnectAsync(request.Host.Trim(), port, cancellationToken);
        }

        private static string NormalizeProvider(string provider)
        {
            return provider.Trim().ToLowerInvariant() switch
            {
                "sqlserver" or "sql server" or "mssql" => "SqlServer",
                "sqlite" => "Sqlite",
                "postgresql" or "postgres" => "PostgreSql",
                "mysql" or "mariadb" => "MySql",
                _ => provider.Trim()
            };
        }

        private static string NormalizeChartType(string chartType)
        {
            return chartType.Trim().ToLowerInvariant() switch
            {
                "line" => "line",
                "doughnut" or "pie" => "doughnut",
                "radar" => "radar",
                _ => "bar"
            };
        }

        private static string BuildEndpointLabel(DatabaseConnectionRequest request, string provider)
        {
            if (provider == "Sqlite")
            {
                return string.IsNullOrWhiteSpace(request.DatabaseName) ? request.Host : request.DatabaseName;
            }

            var port = request.Port.HasValue && request.Port.Value > 0 ? $":{request.Port.Value}" : string.Empty;
            return $"{request.Host}{port}";
        }

        private static AiReportResponse BuildDeterministicReport(AiReportRequest request, string chartType)
        {
            var seed = Convert.ToInt32(SHA256.HashData(Encoding.UTF8.GetBytes(request.Prompt))[0]);
            var labels = ResolveLabels(request.Prompt);
            var values = labels
                .Select((_, index) => (decimal)(35 + ((seed + index * 17) % 64)))
                .ToList();

            var title = ResolveTitle(request.Prompt);
            var maxIndex = values.IndexOf(values.Max());
            var minIndex = values.IndexOf(values.Min());

            return new AiReportResponse
            {
                Title = title,
                Summary = $"پیش‌نمایش تحلیلی بر اساس درخواست شما آماده شد. بیشترین مقدار مربوط به «{labels[maxIndex]}» و کمترین مقدار مربوط به «{labels[minIndex]}» است. برای گزارش قطعی، اتصال دیتابیس را تست کنید و سپس پرسش را دقیق‌تر با بازه زمانی و شاخص مورد نظر وارد کنید.",
                Labels = labels,
                Values = values,
                ChartType = chartType,
                Insights =
                [
                    $"تمرکز اصلی گزارش روی «{labels[maxIndex]}» باشد، چون بالاترین سهم را دارد.",
                    $"برای «{labels[minIndex]}» علت افت یا کم‌بودن مقدار بررسی شود.",
                    "برای خروجی مدیریتی بهتر، گزارش را با بازه زمانی، واحد سازمانی و شاخص مالی/عملیاتی مشخص کنید."
                ]
            };
        }

        private static string ExtractJsonObject(string raw)
        {
            var start = raw.IndexOf('{');
            var end = raw.LastIndexOf('}');

            if (start < 0 || end <= start)
            {
                throw new InvalidOperationException("AI did not return a JSON object.");
            }

            return raw[start..(end + 1)];
        }

        private sealed record SqlReportPlan(string Title, string Sql);

        private sealed record ReportDataPoint(string Label, decimal Value);

        private static readonly string[] PersianMonthNames =
        [
            "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
            "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"
        ];

        private static List<ReportDataPoint> CompleteMonthlySeriesIfNeeded(string prompt, List<ReportDataPoint> rows)
        {
            if (!IsMonthlyPrompt(prompt) || rows.Count >= 12)
            {
                return rows;
            }

            var valuesByMonth = new Dictionary<int, decimal>();
            foreach (var row in rows)
            {
                var month = ResolveMonthNumber(row.Label);
                if (month is >= 1 and <= 12)
                {
                    valuesByMonth[month.Value] = row.Value;
                }
            }

            if (valuesByMonth.Count == 0)
            {
                return rows;
            }

            return Enumerable.Range(1, 12)
                .Select(month => new ReportDataPoint(PersianMonthNames[month - 1], valuesByMonth.GetValueOrDefault(month)))
                .ToList();
        }

        private static bool IsMonthlyPrompt(string prompt)
        {
            return prompt.Contains("ماه", StringComparison.OrdinalIgnoreCase) ||
                   prompt.Contains("ماهیانه", StringComparison.OrdinalIgnoreCase) ||
                   prompt.Contains("ماهانه", StringComparison.OrdinalIgnoreCase) ||
                   prompt.Contains("monthly", StringComparison.OrdinalIgnoreCase);
        }

        private static int? ResolveMonthNumber(string label)
        {
            var normalized = label.Trim();
            for (var index = 0; index < PersianMonthNames.Length; index++)
            {
                if (normalized.Contains(PersianMonthNames[index], StringComparison.OrdinalIgnoreCase))
                {
                    return index + 1;
                }
            }

            var digits = new string(normalized.Select(ToEnglishDigit).Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, out var numericMonth))
            {
                if (numericMonth is >= 1 and <= 12)
                {
                    return numericMonth;
                }

                var trailingMonth = numericMonth % 100;
                if (trailingMonth is >= 1 and <= 12)
                {
                    return trailingMonth;
                }
            }

            return null;
        }

        private static char ToEnglishDigit(char value)
        {
            return value switch
            {
                '۰' => '0',
                '۱' => '1',
                '۲' => '2',
                '۳' => '3',
                '۴' => '4',
                '۵' => '5',
                '۶' => '6',
                '۷' => '7',
                '۸' => '8',
                '۹' => '9',
                '٠' => '0',
                '١' => '1',
                '٢' => '2',
                '٣' => '3',
                '٤' => '4',
                '٥' => '5',
                '٦' => '6',
                '٧' => '7',
                '٨' => '8',
                '٩' => '9',
                _ => value
            };
        }

        private static List<string> ResolveLabels(string prompt)
        {
            var normalized = prompt.ToLowerInvariant();

            if (IsMonthlyPrompt(prompt) || normalized.Contains("فروش") || normalized.Contains("درآمد"))
            {
                return PersianMonthNames.ToList();
            }

            if (normalized.Contains("انبار") || normalized.Contains("کالا") || normalized.Contains("موجودی"))
            {
                return ["کالای A", "کالای B", "کالای C", "کالای D", "کالای E"];
            }

            if (normalized.Contains("کارمند") || normalized.Contains("منابع انسانی") || normalized.Contains("مرخصی"))
            {
                return ["اداری", "مالی", "انبار", "فنی", "مدیریت"];
            }

            return ["شاخص ۱", "شاخص ۲", "شاخص ۳", "شاخص ۴", "شاخص ۵", "شاخص ۶"];
        }

        private static string ResolveTitle(string prompt)
        {
            if (prompt.Contains("فروش") || prompt.Contains("درآمد"))
            {
                return "گزارش مدیریتی فروش و درآمد";
            }

            if (prompt.Contains("انبار") || prompt.Contains("موجودی"))
            {
                return "گزارش مدیریتی انبار و موجودی";
            }

            if (prompt.Contains("مرخصی") || prompt.Contains("کارمند"))
            {
                return "گزارش مدیریتی منابع انسانی";
            }

            return "گزارش مدیریتی هوشمند";
        }
    }
}

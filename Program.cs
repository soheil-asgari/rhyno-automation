using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.OpenApi.Models;
using System.Globalization;
using System.Threading.RateLimiting;
using OfficeAutomation.Data;
using OfficeAutomation.Filters;
using OfficeAutomation.Models;
using OfficeAutomation.Services;
using OfficeAutomation.Services.Connectors;
using OfficeAutomation.Services.Decisioning;
using OfficeAutomation.Services.Auditing;
using OfficeAutomation.Services.Outbox;
using OfficeAutomation.Services.Security;
using OfficeAutomation.Services.Tenancy;
using OfficeAutomation.Services.Middleware;
using OfficeAutomation.Services.HealthChecks;
using OfficeAutomation.Services.Contracts;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;
using OfficeAutomation.Modules.Identity.Infrastructure.Persistence;
using OfficeAutomation.Modules.Inventory.Infrastructure.Persistence;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;
using OfficeAutomation.Modules.Platform.Infrastructure.Persistence;
using OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;
using OfficeAutomation.Modules.Finance.Application;
using OfficeAutomation.Modules.Commercial.Application;
using OfficeAutomation.Modules.Platform.Application.SavedViews;
using OfficeAutomation.Modules.Ocr;
using OfficeAutomation.Modules.InvoiceIntake;
using OfficeAutomation.Modules.InvoiceStructuring;
using OfficeAutomation.Modules.TaxEngine;
using OfficeAutomation.Modules.Persistence;
using OfficeAutomation.Modules.MowadiyanIntegration;
using OfficeAutomation.Modules.Reconciliation;
using OfficeAutomation.Modules.IntakeAutomation;
using OfficeAutomation.Modules.AiEngine;
using OfficeAutomation.Modules.TaxAnalytics;
using OfficeAutomation.Modules.SecurityAndAudit;

var builder = WebApplication.CreateBuilder(args);
var isEfDesignTime = Environment.GetCommandLineArgs()
    .Any(arg =>
        arg.EndsWith("ef.dll", StringComparison.OrdinalIgnoreCase) ||
        arg.Contains("dotnet-ef", StringComparison.OrdinalIgnoreCase));

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection("OpenAI"));
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.Configure<OutboxOptions>(builder.Configuration.GetSection(OutboxOptions.SectionName));
builder.Services.Configure<ConnectorOptions>(builder.Configuration.GetSection(ConnectorOptions.SectionName));
builder.Services.Configure<TenantOptions>(builder.Configuration.GetSection(TenantOptions.SectionName));
builder.Services.Configure<AuditRetentionOptions>(builder.Configuration.GetSection(AuditRetentionOptions.SectionName));
builder.Services.Configure<OcrOptions>(builder.Configuration.GetSection(OcrOptions.SectionName));

// â"€â"€ Swagger / OpenAPI â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Rhyno ERP API",
        Version = "v1",
        Description = "ط±ط§ط¨ط· ط¨ط±ظ†ط§ظ…ظ‡â€Œظ†ظˆغŒط³غŒ ع©ط§ط±ط¨ط±ط¯غŒ (API) ط¹ظ…ظˆظ…غŒ ط³غŒط³طھظ… ط¨ط±ظ†ط§ظ…ظ‡â€Œط±غŒط²غŒ ظ…ظ†ط§ط¨ط¹ ط³ط§ط²ظ…ط§ظ†غŒ ط±ط§غŒظ†ظˆ",
        Contact = new OpenApiContact
        {
            Name = "Rhyno Support",
            Email = "support@rhyno.app",
            Url = new Uri("https://rhyno.app")
        }
    });

    // Add API Key authentication definition
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "API Key ط±ط§ ط¯ط± ظ‡ط¯ط± ظˆط§ط±ط¯ ع©ظ†غŒط¯. ظ…ط«ط§ظ„: X-Api-Key: your-api-key",
        Name = "X-Api-Key",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "ApiKey"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>()
        }
    });

    // Include XML comments
    var xmlFiles = Directory.GetFiles(AppContext.BaseDirectory, "*.xml", SearchOption.TopDirectoryOnly);
    foreach (var xmlFile in xmlFiles)
    {
        options.IncludeXmlComments(xmlFile);
    }
});

// â"€â"€ Redis Caching â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€
var redisConnectionString = builder.Configuration.GetValue<string>("Redis:ConnectionString");
if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
        options.InstanceName = "rhyno:";
    });
}

// â"€â"€ API Key Authentication â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€
builder.Services.AddAuthentication()
    .AddApiKeyAuthentication(options =>
    {
        var apiKeys = builder.Configuration.GetSection("Authentication:ApiKey").Get<string[]>()
                      ?? throw new InvalidOperationException("Critical Security Exception: API Key configuration 'Authentication:ApiKey' is missing in settings.");
        options.ValidApiKeys = apiKeys;
    });

// â"€â"€ HTTP Client Factory â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€
builder.Services.AddHttpClient("ExternalServices", client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Rhyno-ERP/1.0");
    client.Timeout = TimeSpan.FromSeconds(10);
});

// â"€â"€ Export & Import Services â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€
builder.Services.AddScoped<IExportService, ExportService>();
builder.Services.AddScoped<IContractExportService, ContractExportService>();
builder.Services.AddScoped<IBulkImportService, BulkImportService>();
builder.Services.AddScoped<IContractService, ContractService>();
builder.Services.AddScoped<IContractFieldValidator, ContractFieldValidator>();
builder.Services.AddScoped<ContractTypeFieldSeeder>();
builder.Services.AddScoped<IApprovalService, ApprovalService>();
builder.Services.AddScoped<IVersionService, VersionService>();
builder.Services.AddScoped<IContractAccountingService, ContractAccountingService>();
builder.Services.AddScoped<IPaymentScheduleService, PaymentScheduleService>();
        builder.Services.AddScoped<IAmendmentService, AmendmentService>();
        builder.Services.AddScoped<ISignatureService, SignatureService>();
builder.Services.AddScoped<ITemplateRenderingService, TemplateRenderingService>();
builder.Services.AddScoped<IAttachmentVersionService, AttachmentVersionService>();
builder.Services.AddSingleton<ITenantRegistry, TenantRegistry>();
builder.Services.AddScoped<TenantMigrationOrchestrator>();
builder.Services.AddHostedService<TenantMigrationHostedService>();
builder.Services.AddScoped<ICurrentTenantAccessor, CurrentTenantAccessor>();
builder.Services.AddScoped<ITenantResolver, TenantResolver>();
builder.Services.AddScoped<ITenantExecutionScope, TenantExecutionScope>();
builder.Services.AddScoped<ITenantIsolationService, TenantIsolationService>();
builder.Services.AddScoped<ITenantConnectionStringResolver, TenantConnectionStringResolver>();
builder.Services.AddScoped<ITenantPathResolver, TenantPathResolver>();
builder.Services.AddScoped<ITenantCacheKeyProvider, TenantCacheKeyProvider>();
builder.Services.AddScoped<ITenantQueueNameResolver, TenantQueueNameResolver>();
builder.Services.AddScoped<ITenantSettingsService, TenantSettingsService>();
builder.Services.AddScoped<ITenantQuotaService, TenantQuotaService>();
builder.Services.AddScoped<ITenantBackgroundJobCoordinator, TenantBackgroundJobCoordinator>();
builder.Services.AddScoped<ICurrentDataAccessScope, CurrentDataAccessScope>();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddScoped<RowLevelSecuritySaveChangesInterceptor>();
builder.Services.AddScoped<TenantSchemaCommandInterceptor>();
static void ConfigureTenantDbContext(IServiceProvider serviceProvider, DbContextOptionsBuilder options)
{
    options
        .UseSqlServer(serviceProvider.GetRequiredService<ITenantConnectionStringResolver>().GetConnectionString())
        .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
        .ReplaceService<IModelCacheKeyFactory, TenantDbContextModelCacheKeyFactory>()
        .AddInterceptors(
            serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>(),
            serviceProvider.GetRequiredService<RowLevelSecuritySaveChangesInterceptor>(),
            serviceProvider.GetRequiredService<TenantSchemaCommandInterceptor>());
}

static string GetPlatformConnectionString(IConfiguration configuration)
{
    return configuration.GetConnectionString("PlatformConnection")
        ?? configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("ConnectionStrings:PlatformConnection or ConnectionStrings:DefaultConnection must be configured.");
}

builder.Services.AddDbContext<IdentityDbContext>(ConfigureTenantDbContext);
builder.Services.AddDbContext<WorkflowDbContext>(ConfigureTenantDbContext);
builder.Services.AddScoped<IWorkflowDbContext>(sp => sp.GetRequiredService<WorkflowDbContext>());
builder.Services.AddDbContext<FinanceDbContext>(ConfigureTenantDbContext);
builder.Services.AddDbContext<InventoryDbContext>(ConfigureTenantDbContext);
builder.Services.AddDbContext<OfficeDbContext>(ConfigureTenantDbContext);
// TaxDbContext is resolved through a SCOPED factory (see Modules/Persistence/TaxDbContextFactory.cs).
// Registering it as a scoped factory ensures the options (and the scoped tenancy services such as
// ITenantConnectionStringResolver and the EF interceptors) are resolved from a real IServiceScope
// instead of from the application root provider â€" fixing the
// "Cannot resolve scoped service 'ITenantConnectionStringResolver' from root provider" exception.
builder.Services.AddScoped<IDbContextFactory<TaxDbContext>, TaxDbContextFactory>();
builder.Services.AddDbContext<PlatformDbContext>((serviceProvider, options) =>
    options
        .UseSqlServer(GetPlatformConnectionString(serviceProvider.GetRequiredService<IConfiguration>()))
        .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
        .AddInterceptors(
            serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>(),
            serviceProvider.GetRequiredService<RowLevelSecuritySaveChangesInterceptor>()));
builder.Services.AddIdentity<User, ApplicationRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

    // طھظ†ط¸غŒظ…ط§طھ ظ…ظ‡ظ… ط¨ط±ط§غŒ ظ†ط§ظ… ع©ط§ط±ط¨ط±غŒ ط¨ظ‡ ط¬ط§غŒ ط§غŒظ…غŒظ„
    options.User.AllowedUserNameCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+ " +
        "ط¢ط§ط¨ظ¾طھط«ط¬ع†ط­ط®ط¯ط°ط±ط²عکط³ط´طµط¶ط·ط¸ط¹ط؛ظپظ‚ع©ع¯ظ„ظ…ظ†ظˆظ‡غŒ";

    options.User.RequireUniqueEmail = false;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<IdentityDbContext>()
.AddDefaultTokenProviders()
.AddDefaultUI()
;
builder.Services.ConfigureApplicationCookie(options =>
{
    // Standard ASP.NET Identity login path. The Razor Page at
    // Areas/Identity/Pages/Account/Login.cshtml is registered under this route.
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Home/AccessDenied";
});

builder.Services.AddSingleton<AiService>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContextService, UserContextService>();
builder.Services.AddScoped<IAuditContextProvider, HttpAuditContextProvider>();
builder.Services.AddScoped<IAuditLogger, AuditLogger>();
builder.Services.AddScoped<AuditRetentionService>();
builder.Services.AddScoped<IPermissionAccessService, PermissionAccessService>();
builder.Services.AddScoped<IAbacAuthorizationService, AbacAuthorizationService>();
builder.Services.AddScoped<ISegregationOfDutiesService, SegregationOfDutiesService>();
builder.Services.AddScoped<ISecurityFieldMaskingService, SecurityFieldMaskingService>();
builder.Services.AddScoped<ICurrentUserContextAccessor, CurrentUserContextAccessor>();
builder.Services.AddScoped<IAuthorizationFacade, AuthorizationFacade>();
builder.Services.AddScoped<IDataIsolationService, DataIsolationService>();
builder.Services.AddScoped<WarehouseDashboardService>();
builder.Services.AddScoped<FinancialInvoiceService>();
builder.Services.AddScoped<FinanceLedgerService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IOfficeService, OfficeService>();
builder.Services.AddScoped<PeriodClosingService>();
builder.Services.AddScoped<TrialBalanceService>();
builder.Services.AddScoped<LedgerService>();
builder.Services.AddScoped<RecurringJournalService>();
builder.Services.AddScoped<VoucherRenumberingService>();
builder.Services.AddSingleton<ITableSchemaRegistry, TableSchemaRegistry>();
builder.Services.AddScoped<IDigitalSignatureService, DigitalSignatureService>();
builder.Services.AddScoped<IDecisionEngine, DecisionEngine>();
builder.Services.AddScoped<IWorkflowDefinitionSelector, WorkflowDefinitionSelector>();
builder.Services.AddScoped<WorkflowGovernanceService>();
builder.Services.AddScoped<WorkflowService>();
builder.Services.AddScoped<BootstrapAdminSeeder>();
builder.Services.AddScoped<WorkflowSlaScheduler>();
builder.Services.AddScoped<IWorkflowAttachmentService, WorkflowAttachmentService>();
builder.Services.AddScoped<IWorkflowSlaService, WorkflowSlaService>();
builder.Services.AddScoped<WorkflowSlaEscalationNotifier>();
builder.Services.AddScoped<FinancialAppService>();
builder.Services.AddScoped<CommercialService>();
builder.Services.AddScoped<IOcrEngine, TesseractOcrEngine>();
builder.Services.AddScoped<IIntakeSessionStore, DatabaseIntakeSessionStore>();
builder.Services.AddScoped<InvoiceIntakeService>();
builder.Services.AddScoped<ITaxCalculatorService, TaxCalculatorService>();
builder.Services.Configure<MowadiyanOptions>(builder.Configuration.GetSection(MowadiyanOptions.SectionName));
builder.Services.AddScoped<IMowadiyanPayloadBuilder, MowadiyanPayloadBuilder>();
builder.Services.AddScoped<IMowadiyanCryptoService, MowadiyanCryptoService>();
builder.Services.AddHttpClient<IMowadiyanApiClient, MowadiyanApiClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<MowadiyanOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(60);
    if (!string.IsNullOrWhiteSpace(options.BaseUrl))
    {
        client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
    }
});
builder.Services.AddScoped<SandboxMowadiyanApiClient>();
builder.Services.AddSingleton<IMowadiyanSubmissionService, MowadiyanSubmissionService>();
builder.Services.AddHostedService<MowadiyanSubmissionQueueHostedService>();
builder.Services.AddScoped<IInvoiceReconciliationService, InvoiceReconciliationService>();
// Refactored modular Tax Portal exporter (TaxExporterService): persistent-profile
// Chromium (LaunchPersistentContextAsync against ./tax_browser_profile), strict
// SSO navigation order, glyph-normalized Persian selectors and an HttpClient-backed
// CAPTCHA OCR wrapper with retry/refresh. Registered against a scoped HTTP client so
// the orchestrator and its OCR dependency both get a managed HttpClient.
builder.Services.Configure<CaptchaOcrOptions>(builder.Configuration.GetSection(CaptchaOcrOptions.SectionName));
// ddddocr (in-process, Microsoft.ML.OnnxRuntime) is the primary CAPTCHA solver. The
// typed ExternalCaptchaOcrClient is kept as the fallback (used only when OcrBaseUrl is
// configured and the local model yields nothing), so no external OCR service is required.
builder.Services.AddHttpClient<ExternalCaptchaOcrClient>();
// ICaptchaOcrClient must be a SINGLETON (not scoped): TaxExporterService is a
// singleton that owns the long-lived persistent Chromium session across the
// two-step initiate -> complete flow, so its OCR dependency must live just as
// long. All of its transitive dependencies (IOptions, IHostEnvironment,
// ILoggerFactory, IHttpClientFactory) are singletons, so this is safe.
builder.Services.AddSingleton<ICaptchaOcrClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<CaptchaOcrOptions>>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var external = new ExternalCaptchaOcrClient(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(ExternalCaptchaOcrClient)),
        options,
        loggerFactory.CreateLogger<ExternalCaptchaOcrClient>());
    return new DdddCaptchaOcrClient(
        sp.GetRequiredService<IOptions<MowadiyanOptions>>(),
        sp.GetRequiredService<IHostEnvironment>(),
        loggerFactory.CreateLogger<DdddCaptchaOcrClient>(),
        external);
});
// CRITICAL: TaxExporterService MUST be a singleton. It stores the live persistent
// Chromium browser session (parked on the OTP screen) in its instance `_sessions`
// dictionary during InitiateAsync, and CompleteAsync must resolve that SAME context
// via ResolveContext(sessionToken). The initiate and complete steps run in TWO
// SEPARATE HTTP requests (InitiatePortalLogin then CreatePortalExport); a scoped
// lifetime would hand each request a fresh instance with an empty `_sessions`,
// forcing a self-healing relaunch that loses the OTP-pending SSO state and throws
// "ع©ط§ط¯ط± ظˆط±ظˆط¯ ع©ط¯ طھط£غŒغŒط¯ (OTP) ط¯ط± طµظپط­ظ‡ ظ¾غŒط¯ط§ ظ†ط´ط¯".
builder.Services.AddSingleton<ITaxExporterService, TaxExporterService>();

builder.Services.AddScoped<ZipArchiveProcessor>();
builder.Services.AddScoped<TelegramBotWebhookHandler>();
builder.Services.AddScoped<ISemanticDisambiguationService, SemanticDisambiguationService>();
builder.Services.AddScoped<IEntityResolutionService, EntityResolutionService>();
builder.Services.AddScoped<ITaxInsightsService, TaxInsightsService>();
builder.Services.AddScoped<ITaxQueryService, TaxQueryService>();
builder.Services.AddScoped<IInvoiceExportService, InvoiceExportService>();
builder.Services.AddScoped<ITaxAuditService, TaxAuditService>();
builder.Services.AddScoped<IWorkflowExecutionService, WorkflowExecutionService>();
builder.Services.AddScoped<ConnectorExecutionService>();
builder.Services.AddScoped<ProcessMiningService>();
builder.Services.AddScoped<IOutboxService, OutboxService>();
builder.Services.AddSingleton<IEventBusPublisher, RabbitMqEventBusPublisher>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<IContractGuaranteeService, ContractGuaranteeService>();
builder.Services.AddScoped<IPaymentPenaltyService, PaymentPenaltyService>();
builder.Services.AddScoped<IDocumentRequirementService, DocumentRequirementService>();
builder.Services.AddScoped<IContractReportService, ContractReportService>();

        builder.Services.AddScoped<IAlertService, AlertService>();
builder.Services.AddScoped<SecurityAuditNotificationService>();
builder.Services.AddScoped<WorkInboxService>();
builder.Services.AddScoped<WorkflowDetailService>();
builder.Services.AddScoped<AiSqlSafetyService>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization();

builder.Services.AddScoped<PermissionAccessFilter>();
builder.Services.AddSignalR();

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<NormalizeInputFilter>();
    options.Filters.Add<PermissionAccessFilter>();
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddRazorPages(options =>
{
    // TEMPORARILY COMMENTED OUT to isolate whether the custom area page-route alias
    // is breaking the default Identity UI route. The standard Identity route
    // "/Identity/Account/Login" is used instead.
    // options.Conventions.AddAreaPageRoute("Identity", "/Account/Login", "/Account/Login");
});
builder.Services.AddScoped<LeaveWorkflowService>();
builder.Services.AddScoped<IDocumentVersioningService, DocumentVersioningService>();
builder.Services.AddSingleton<ITotpService, TotpService>();
builder.Services.AddTransient<IEmailService, EmailService>();
builder.Services.AddHostedService<DocumentRetentionBackgroundService>();
builder.Services.AddHostedService<ScheduledReportBackgroundService>();
builder.Services.AddHostedService<ContractBackgroundJobCoordinator>();


// â"€â"€ Health Checks (Advanced) â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "db", "sql" })
    .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: new[] { "messaging" })
    .AddCheck<RedisHealthCheck>("redis", tags: new[] { "cache" })
    .AddCheck<DiskSpaceHealthCheck>("disk", tags: new[] { "infrastructure" })
    .AddCheck<MemoryHealthCheck>("memory", tags: new[] { "infrastructure" })
    .AddCheck<ExternalServicesHealthCheck>("external", tags: new[] { "network" });
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("ai", context =>
    {
        var tenantResolver = context.RequestServices.GetRequiredService<ITenantResolver>();
        var tenantRegistry = context.RequestServices.GetRequiredService<ITenantRegistry>();
        var tenant = tenantRegistry.GetTenant(tenantResolver.ResolveTenantId());
        return
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"{tenant.TenantId}:ai:{context.User?.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous"}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Math.Max(1, tenant.AiRequestsPerMinute),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var tenantResolver = context.RequestServices.GetRequiredService<ITenantResolver>();
        var tenantRegistry = context.RequestServices.GetRequiredService<ITenantRegistry>();
        var tenant = tenantRegistry.GetTenant(tenantResolver.ResolveTenantId());

        // For anonymous / ambiguous requests, include the remote IP and a
        // "resolution-failure" sentinel so misbehaving clients cannot exhaust
        // the whole tenant budget and cause a self-inflicted DoS. Authenticated
        // requests partition by tenant as before.
        var identity = context.User?.Identity?.Name;
        var isAuthenticated = context.User?.Identity?.IsAuthenticated == true;
        var partitionKey = string.IsNullOrWhiteSpace(identity) || !isAuthenticated
            ? $"anon:{tenant.TenantId}:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}"
            : $"{tenant.TenantId}:user:{identity}";

        return
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: partitionKey,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Math.Max(1, tenant.RequestsPerMinute),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var faCulture = new CultureInfo("fa-IR");
    var enCulture = new CultureInfo("en-US");
    options.DefaultRequestCulture = new RequestCulture(faCulture);
    options.SupportedCultures = [faCulture, enCulture];
    options.SupportedUICultures = [faCulture, enCulture];
});

if (isEfDesignTime)
{
    return;
}

var app = builder.Build();
var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

// 3. طھظ†ط¸غŒظ…ط§طھ ظ…ط­غŒط· ط§ط¬ط±ط§
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();

// â"€â"€ Force UTF-8 Encoding for all responses â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€
// NOTE: We must NOT set context.Request.ContentType here. Modifying Request.ContentType
// on incoming GET requests corrupts ASP.NET Core's internal Endpoint Routing engine and
// can cause 404s. Only the Response.ContentType is set below.
app.Use(async (context, next) =>
{
    await next();
    if (context.Response.HasStarted)
        return;
    context.Response.ContentType = context.Response.ContentType ?? "text/html; charset=utf-8";
});

// â"€â"€ API JSON exception handling â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€
// Registered before routing so it catches exceptions from downstream
// middleware and controllers. API/JSON requests receive a stable JSON
// error envelope; browser requests fall through to the HTML error page.
app.UseApiExceptionHandling();

// â"€â"€ Security Headers â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€
app.UseSecurityHeaders();

// â"€â"€ Swagger (only in Development or when enabled) â"€â"€â"€â"€â"€â"€
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger(options =>
    {
        options.RouteTemplate = "swagger/{documentName}/swagger.json";
    });
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Rhyno ERP API v1");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "Rhyno ERP - API Documentation";
        options.DefaultModelsExpandDepth(-1); // Collapse schemas by default
    });
}

// â"€â"€ 404 Handling â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€
app.UseStatusCodePagesWithReExecute("/Home/Error?code={0}");

app.UseRouting();
app.UseRequestLocalization();
app.Use(async (context, next) =>
{
    var tenantResolver = context.RequestServices.GetRequiredService<ITenantResolver>();
    var tenantRegistry = context.RequestServices.GetRequiredService<ITenantRegistry>();
    var tenantAccessor = context.RequestServices.GetRequiredService<ICurrentTenantAccessor>();
    var tenantId = await tenantResolver.ResolveTenantIdAsync(context.RequestAborted);
    var tenant = await tenantRegistry.GetTenantAsync(tenantId, context.RequestAborted);
    tenantAccessor.Initialize(tenant);

    if (context.RequestServices.GetRequiredService<ICurrentDataAccessScope>() is CurrentDataAccessScope dataScope)
    {
        dataScope.SetTenant(tenant.TenantId);
    }

    var loggerFactory = context.RequestServices.GetRequiredService<ILoggerFactory>();
    using (loggerFactory.CreateLogger("Tenant").BeginScope(new Dictionary<string, object> { ["TenantId"] = tenant.TenantId }))
    {
        await next();
    }
});
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    if (HttpMethods.IsPost(context.Request.Method) ||
        HttpMethods.IsPut(context.Request.Method) ||
        HttpMethods.IsPatch(context.Request.Method) ||
        HttpMethods.IsDelete(context.Request.Method))
    {
        var tenantAccessor = context.RequestServices.GetRequiredService<ICurrentTenantAccessor>();
        var tenant = tenantAccessor.Tenant;
        if (tenant != null)
        {
            var quota = await context.RequestServices
                .GetRequiredService<ITenantQuotaService>()
                .ValidateWriteAsync(tenant, context.RequestAborted);
            if (!quota.IsAllowed)
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.Response.WriteAsJsonAsync(new { error = quota.Reason }, context.RequestAborted);
                return;
            }
        }
    }

    await next();
});
app.Use(async (context, next) =>
{
    var start = DateTimeOffset.UtcNow;
    try
    {
        await next();
    }
    finally
    {
        var elapsed = DateTimeOffset.UtcNow - start;
        if (elapsed.TotalMilliseconds >= 500)
        {
            logger.LogInformation(
                "Slow request {Method} {Path} responded {StatusCode} in {ElapsedMs}ms",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                elapsed.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture));
        }
    }
});

// 4. طھط±طھغŒط¨ ط­غŒط§طھغŒ ط§ط­ط±ط§ط² ظ‡ظˆغŒطھ
app.UseAuthentication();
app.Use(async (context, next) =>
{
    logger.LogDebug("After authentication {Path} Authenticated={Authenticated}", context.Request.Path, context.User.Identity?.IsAuthenticated == true);
    await next();
    logger.LogDebug("After authentication complete {Path}", context.Request.Path);
});
app.Use(async (context, next) =>
{
    logger.LogDebug("Current-user middleware enter {Path}", context.Request.Path);
    var currentUserContext = context.RequestServices.GetRequiredService<ICurrentUserContextAccessor>();
    var currentDataAccessScope = context.RequestServices.GetRequiredService<ICurrentDataAccessScope>();
    PermissionAccessProfile? profile = null;

    if (context.User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(currentUserContext.UserId))
    {
        profile = await currentUserContext.GetAccessProfileAsync(context.RequestAborted);
        currentUserContext.SetCurrentProfile(profile);
    }

    currentDataAccessScope.Initialize(currentUserContext.UserId, profile);
    await next();
    logger.LogDebug("Current-user middleware exit {Path}", context.Request.Path);
});
app.UseAuthorization();
app.Use(async (context, next) =>
{
    logger.LogDebug("After authorization {Path}", context.Request.Path);
    await next();
    logger.LogDebug("After authorization complete {Path}", context.Request.Path);
});

// 5. ظ†ظ‚ط´ظ‡ ظ…ط³غŒط±ظ‡ط§
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();
app.MapHealthChecks("/health");
app.MapHub<OfficeAutomation.Hubs.AutomationHub>("/automationHub");

// --- Auto-migrate ALL database contexts on startup (fail-fast) ---
// Each context is migrated via Database.MigrateAsync(). If any context fails,
// the exception is logged and re-thrown immediately so the app does NOT start
// with a partially-migrated database.
logger.LogInformation("Starting database migrations for all contexts...");
await DatabaseMigrationService.MigrateAllAsync(app.Services, logger);
logger.LogInformation("All database context migrations applied successfully on startup.");

// --- Seed ContractTypeField definitions (idempotent, runs after migration) ---
try
{
    using var seedScope = app.Services.CreateScope();
    var fieldSeeder = seedScope.ServiceProvider.GetRequiredService<ContractTypeFieldSeeder>();
    await fieldSeeder.SeedAsync();
    logger.LogInformation("ContractTypeFieldSeeder executed successfully.");
}
catch (Exception ex)
{
    logger.LogWarning(ex, "ContractTypeFieldSeeder failed (DB may not be ready): {Message}", ex.Message);
}

if (builder.Configuration.GetValue("Bootstrap:RunOnStartup", false))
{
    logger.LogWarning("Bootstrap:RunOnStartup is enabled. Run database/bootstrap tasks only after SQL Server is confirmed available.");

    // Additional bootstrap tasks can be added here.
}
else
{
    logger.LogInformation("Startup bootstrap skipped because Bootstrap:RunOnStartup is disabled.");
}

// â"€â"€ Bootstrap Admin User (always runs, idempotent) â"€â"€
try
{
    using var adminScope = app.Services.CreateScope();
    var seeder = adminScope.ServiceProvider.GetRequiredService<BootstrapAdminSeeder>();
    await seeder.SeedAsync();
    logger.LogInformation("BootstrapAdmin: seed completed.");
}
catch (Exception ex)
{
    logger.LogWarning(ex, "BootstrapAdmin: seed failed (DB may not be ready): {Message}", ex.Message);
}

// â"€â"€ Startup Route Diagnostics â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€â"€
// Outputs all active mapped routes to the console on startup so we can verify that
// Areas/Identity/Pages/Account/Login appears in the [REGISTERED ROUTE] logs.
app.Lifetime.ApplicationStarted.Register(() =>
{
    var endpoints = app.Services.GetRequiredService<EndpointDataSource>().Endpoints;
    foreach (var ep in endpoints)
    {
        Console.WriteLine($"[REGISTERED ROUTE] {ep.DisplayName} -> {(ep as RouteEndpoint)?.RoutePattern.RawText}");
    }
});

app.Run();

public partial class Program { }








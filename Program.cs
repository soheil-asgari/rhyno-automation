using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
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
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;
using OfficeAutomation.Modules.Identity.Infrastructure.Persistence;
using OfficeAutomation.Modules.Inventory.Infrastructure.Persistence;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;
using OfficeAutomation.Modules.Platform.Infrastructure.Persistence;
using OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;
using OfficeAutomation.Modules.Finance.Application;
using OfficeAutomation.Modules.Platform.Application.SavedViews;

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
static void ConfigureTenantDbContext(IServiceProvider serviceProvider, DbContextOptionsBuilder options)
{
    options
        .UseSqlServer(serviceProvider.GetRequiredService<ITenantConnectionStringResolver>().GetConnectionString())
        .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
        .ReplaceService<IModelCacheKeyFactory, TenantDbContextModelCacheKeyFactory>()
        .AddInterceptors(
            serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>(),
            serviceProvider.GetRequiredService<RowLevelSecuritySaveChangesInterceptor>());
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
builder.Services.AddDbContext<PlatformDbContext>((serviceProvider, options) =>
    options
        .UseSqlServer(GetPlatformConnectionString(serviceProvider.GetRequiredService<IConfiguration>()))
        .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
        .AddInterceptors(
            serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>(),
            serviceProvider.GetRequiredService<RowLevelSecuritySaveChangesInterceptor>()));
builder.Services.AddIdentity<User, ApplicationRole>(options => {
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

    // تنظیمات مهم برای نام کاربری به جای ایمیل
    options.User.AllowedUserNameCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+ " +
        "آابپتثجچحخدذرزژسشصضطظعغفقکگلمنوهی";

    options.User.RequireUniqueEmail = false;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<IdentityDbContext>()
.AddDefaultTokenProviders()
;
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Home/AccessDenied";
});

builder.Services.AddSingleton<AiService>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
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
builder.Services.AddScoped<PeriodClosingService>();
builder.Services.AddScoped<TrialBalanceService>();
builder.Services.AddScoped<VoucherRenumberingService>();
builder.Services.AddSingleton<ITableSchemaRegistry, TableSchemaRegistry>();
builder.Services.AddScoped<IDigitalSignatureService, DigitalSignatureService>();
builder.Services.AddScoped<IDecisionEngine, DecisionEngine>();
builder.Services.AddScoped<IWorkflowDefinitionSelector, WorkflowDefinitionSelector>();
builder.Services.AddScoped<WorkflowGovernanceService>();
builder.Services.AddScoped<WorkflowService>();
builder.Services.AddScoped<WorkflowSlaScheduler>();
builder.Services.AddScoped<WorkflowSlaEscalationNotifier>();
builder.Services.AddScoped<ConnectorExecutionService>();
builder.Services.AddScoped<ProcessMiningService>();
builder.Services.AddScoped<IOutboxService, OutboxService>();
builder.Services.AddSingleton<IEventBusPublisher, RabbitMqEventBusPublisher>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<SecurityAuditNotificationService>();
builder.Services.AddScoped<WorkInboxService>();
builder.Services.AddScoped<WorkflowDetailService>();
builder.Services.AddScoped<AiSqlSafetyService>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization();

builder.Services.AddScoped<PermissionAccessFilter>();

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<NormalizeInputFilter>();
    options.Filters.Add<PermissionAccessFilter>();
});
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AddAreaPageRoute("Identity", "/Account/Login", "/Account/Login");
});
builder.Services.AddScoped<LeaveWorkflowService>();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");
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
        return
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: tenant.TenantId,
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

// 3. تنظیمات محیط اجرا
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

// 4. ترتیب حیاتی احراز هویت
app.UseAuthentication();
app.Use(async (context, next) =>
{
    logger.LogInformation("After authentication {Path} Authenticated={Authenticated}", context.Request.Path, context.User.Identity?.IsAuthenticated == true);
    await next();
    logger.LogInformation("After authentication complete {Path}", context.Request.Path);
});
app.Use(async (context, next) =>
{
    logger.LogInformation("Current-user middleware enter {Path}", context.Request.Path);
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
    logger.LogInformation("Current-user middleware exit {Path}", context.Request.Path);
});
app.UseAuthorization();
app.Use(async (context, next) =>
{
    logger.LogInformation("After authorization {Path}", context.Request.Path);
    await next();
    logger.LogInformation("After authorization complete {Path}", context.Request.Path);
});

// 5. نقشه مسیرها
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();
app.MapHealthChecks("/health");

if (builder.Configuration.GetValue("Bootstrap:RunOnStartup", false))
{
    logger.LogWarning("Bootstrap:RunOnStartup is enabled. Run database/bootstrap tasks only after SQL Server is confirmed available.");
}
else
{
    logger.LogInformation("Startup bootstrap skipped because Bootstrap:RunOnStartup is disabled.");
}

app.Run();

public partial class Program { }




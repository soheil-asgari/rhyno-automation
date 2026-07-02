using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using OfficeAutomation.Data;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;
using OfficeAutomation.Modules.Identity.Infrastructure.Persistence;
using OfficeAutomation.Modules.Inventory.Infrastructure.Persistence;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;
using OfficeAutomation.Modules.Platform.Infrastructure.Persistence;
using OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;
using OfficeAutomation.Services;
using OfficeAutomation.Services.Auditing;
using OfficeAutomation.Services.Outbox;
using OfficeAutomation.Services.Security;
using OfficeAutomation.Services.Tenancy;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.Configure<OutboxOptions>(builder.Configuration.GetSection(OutboxOptions.SectionName));
builder.Services.Configure<TenantOptions>(builder.Configuration.GetSection(TenantOptions.SectionName));
builder.Services.Configure<AuditRetentionOptions>(builder.Configuration.GetSection(AuditRetentionOptions.SectionName));

builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ITenantRegistry, TenantRegistry>();
builder.Services.AddScoped<TenantMigrationOrchestrator>();
builder.Services.AddScoped<ICurrentTenantAccessor, CurrentTenantAccessor>();
builder.Services.AddScoped<ITenantExecutionScope, TenantExecutionScope>();
builder.Services.AddScoped<ITenantIsolationService, TenantIsolationService>();
builder.Services.AddScoped<ITenantConnectionStringResolver, TenantConnectionStringResolver>();
builder.Services.AddScoped<ITenantQueueNameResolver, TenantQueueNameResolver>();
builder.Services.AddScoped<ITenantBackgroundJobCoordinator, TenantBackgroundJobCoordinator>();
builder.Services.AddScoped<ICurrentDataAccessScope, CurrentDataAccessScope>();
builder.Services.AddScoped<IAuditContextProvider, HttpAuditContextProvider>();
builder.Services.AddScoped<IAuditLogger, AuditLogger>();
builder.Services.AddScoped<AuditRetentionService>();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddScoped<RowLevelSecuritySaveChangesInterceptor>();

builder.Services.AddDbContext<PlatformDbContext>((serviceProvider, options) =>
    options
        .UseSqlServer(GetPlatformConnectionString(serviceProvider.GetRequiredService<IConfiguration>()))
        .AddInterceptors(
            serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>(),
            serviceProvider.GetRequiredService<RowLevelSecuritySaveChangesInterceptor>()));

builder.Services.AddDbContext<WorkflowDbContext>((serviceProvider, options) =>
    options
        .UseSqlServer(serviceProvider.GetRequiredService<ITenantConnectionStringResolver>().GetConnectionString())
        .ReplaceService<IModelCacheKeyFactory, TenantDbContextModelCacheKeyFactory>()
        .AddInterceptors(
            serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>(),
            serviceProvider.GetRequiredService<RowLevelSecuritySaveChangesInterceptor>()));
builder.Services.AddScoped<IWorkflowDbContext>(sp => sp.GetRequiredService<WorkflowDbContext>());

builder.Services.AddDbContext<OfficeDbContext>((serviceProvider, options) =>
    options
        .UseSqlServer(serviceProvider.GetRequiredService<ITenantConnectionStringResolver>().GetConnectionString())
        .ReplaceService<IModelCacheKeyFactory, TenantDbContextModelCacheKeyFactory>()
        .AddInterceptors(
            serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>(),
            serviceProvider.GetRequiredService<RowLevelSecuritySaveChangesInterceptor>()));

builder.Services.AddDbContext<FinanceDbContext>((serviceProvider, options) =>
    options
        .UseSqlServer(serviceProvider.GetRequiredService<ITenantConnectionStringResolver>().GetConnectionString())
        .ReplaceService<IModelCacheKeyFactory, TenantDbContextModelCacheKeyFactory>()
        .AddInterceptors(
            serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>(),
            serviceProvider.GetRequiredService<RowLevelSecuritySaveChangesInterceptor>()));

builder.Services.AddDbContext<InventoryDbContext>((serviceProvider, options) =>
    options
        .UseSqlServer(serviceProvider.GetRequiredService<ITenantConnectionStringResolver>().GetConnectionString())
        .ReplaceService<IModelCacheKeyFactory, TenantDbContextModelCacheKeyFactory>()
        .AddInterceptors(
            serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>(),
            serviceProvider.GetRequiredService<RowLevelSecuritySaveChangesInterceptor>()));

builder.Services.AddDbContext<IdentityDbContext>((serviceProvider, options) =>
    options
        .UseSqlServer(serviceProvider.GetRequiredService<ITenantConnectionStringResolver>().GetConnectionString())
        .ReplaceService<IModelCacheKeyFactory, TenantDbContextModelCacheKeyFactory>()
        .AddInterceptors(
            serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>(),
            serviceProvider.GetRequiredService<RowLevelSecuritySaveChangesInterceptor>()));
builder.Services.AddScoped<IOutboxService, OutboxService>();
builder.Services.AddSingleton<IEventBusPublisher, RabbitMqEventBusPublisher>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<WorkflowSlaEscalationNotifier>();

builder.Services.AddHostedService<TenantMigrationHostedService>();
builder.Services.AddHostedService<OutboxPublisherBackgroundService>();
builder.Services.AddHostedService<WorkflowSlaJobBackgroundService>();
builder.Services.AddHostedService<AuditRetentionBackgroundService>();

await builder.Build().RunAsync();

static string GetPlatformConnectionString(IConfiguration configuration)
{
    return configuration.GetConnectionString("PlatformConnection")
        ?? configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("ConnectionStrings:PlatformConnection or ConnectionStrings:DefaultConnection must be configured.");
}


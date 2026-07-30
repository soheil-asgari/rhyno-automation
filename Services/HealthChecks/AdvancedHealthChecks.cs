using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;
using StackExchange.Redis;

namespace OfficeAutomation.Services.HealthChecks;

/// <summary>
/// Health Check برای RabbitMQ
/// </summary>
public sealed class RabbitMqHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;

    public RabbitMqHealthCheck(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var host = _configuration["RabbitMQ:Host"] ?? "localhost";
            var username = _configuration["RabbitMQ:Username"] ?? "guest";
            var password = _configuration["RabbitMQ:Password"] ?? "guest";

            var factory = new ConnectionFactory
            {
                HostName = host,
                UserName = username,
                Password = password,
                RequestedConnectionTimeout = TimeSpan.FromSeconds(5)
            };

            using var connection = await factory.CreateConnectionAsync(cancellationToken);
            using var channel = await connection.CreateChannelAsync();

            if (connection.IsOpen)
            {
                return HealthCheckResult.Healthy("RabbitMQ is reachable and responsive.");
            }

            return HealthCheckResult.Degraded("RabbitMQ connection is not fully open.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ is unreachable.", ex);
        }
    }
}

/// <summary>
/// Health Check برای Redis
/// </summary>
public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;

    public RedisHealthCheck(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var connectionString = _configuration["Redis:ConnectionString"] ?? "localhost:6379";
            var options = ConfigurationOptions.Parse(connectionString);
            options.ConnectTimeout = 5000;
            options.SyncTimeout = 5000;
            options.AbortOnConnectFail = false;

            using var connection = await ConnectionMultiplexer.ConnectAsync(options);
            var db = connection.GetDatabase();

            var pingResult = await db.PingAsync();
            if (pingResult.TotalMilliseconds > 0)
            {
                return HealthCheckResult.Healthy($"Redis is responsive (ping: {pingResult.TotalMilliseconds:F0}ms).");
            }

            return HealthCheckResult.Degraded("Redis ping returned zero.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis is unreachable.", ex);
        }
    }
}

/// <summary>
/// Health Check برای بررسی فضای دیسک
/// </summary>
public sealed class DiskSpaceHealthCheck : IHealthCheck
{
    private const long MinimumFreeMegabytes = 500; // 500 MB minimum

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var currentDrive = DriveInfo.GetDrives()
                .FirstOrDefault(d => d.IsReady && d.Name == Path.GetPathRoot(Environment.CurrentDirectory));

            if (currentDrive == null)
            {
                return Task.FromResult(HealthCheckResult.Degraded("Could not determine current drive."));
            }

            var freeMegabytes = currentDrive.AvailableFreeSpace / (1024 * 1024);

            if (freeMegabytes < MinimumFreeMegabytes)
            {
                return Task.FromResult(
                    HealthCheckResult.Degraded($"Low disk space: {freeMegabytes:N0} MB free (minimum: {MinimumFreeMegabytes:N0} MB).",
                    data: new Dictionary<string, object>
                    {
                        ["FreeMegabytes"] = freeMegabytes,
                        ["TotalMegabytes"] = currentDrive.TotalSize / (1024 * 1024),
                        ["DriveName"] = currentDrive.Name
                    }));
            }

            return Task.FromResult(
                HealthCheckResult.Healthy($"Disk space OK: {freeMegabytes:N0} MB free.",
                data: new Dictionary<string, object>
                {
                    ["FreeMegabytes"] = freeMegabytes,
                    ["TotalMegabytes"] = currentDrive.TotalSize / (1024 * 1024),
                    ["DriveName"] = currentDrive.Name
                }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Failed to check disk space.", ex));
        }
    }
}

/// <summary>
/// Health Check برای Memory Usage
/// </summary>
public sealed class MemoryHealthCheck : IHealthCheck
{
    private const long MemoryWarningThresholdBytes = 1024L * 1024 * 1024; // 1 GB warning

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var workingSet = process.WorkingSet64;
            var workingSetMb = workingSet / (1024.0 * 1024.0);
            var totalMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            var totalMemoryMb = totalMemory / (1024.0 * 1024.0);

            var data = new Dictionary<string, object>
            {
                ["WorkingSetMB"] = $"{workingSetMb:F1}",
                ["TotalAvailableMemoryMB"] = $"{totalMemoryMb:F1}",
                ["MemoryUsagePercent"] = $"{workingSet * 100.0 / totalMemory:F1}%",
                ["GCCollectionCount"] = GC.CollectionCount(0).ToString(),
                ["ThreadCount"] = process.Threads.Count.ToString()
            };

            if (workingSet > MemoryWarningThresholdBytes)
            {
                return Task.FromResult(
                    HealthCheckResult.Degraded($"High memory usage: {workingSetMb:F1} MB.", data: data));
            }

            return Task.FromResult(
                HealthCheckResult.Healthy($"Memory usage: {workingSetMb:F1} MB.", data: data));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Failed to check memory.", ex));
        }
    }
}

/// <summary>
/// Health Check برای سرویس‌های خارجی (مثل درگاه پرداخت، پیامک و ...)
/// </summary>
public sealed class ExternalServicesHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ExternalServicesHealthCheck(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("ExternalServices");
            client.Timeout = TimeSpan.FromSeconds(5);

            // Check basic external connectivity (can be extended for specific services)
            var response = await client.GetAsync("https://www.google.com", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy("External connectivity is OK.");
            }

            return HealthCheckResult.Degraded($"External connectivity limited (status: {response.StatusCode}).");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("External connectivity check failed.", ex);
        }
    }
}

/// <summary>
/// Health Check سفارشی برای Database (SQL Server)
/// </summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly IServiceProvider _serviceProvider;

    public DatabaseHealthCheck(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<Modules.Platform.Infrastructure.Persistence.PlatformDbContext>();

            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

            if (canConnect)
            {
                return HealthCheckResult.Healthy("Database connection is healthy.");
            }

            return HealthCheckResult.Degraded("Database connection check returned false.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database is unreachable.", ex);
        }
    }
}
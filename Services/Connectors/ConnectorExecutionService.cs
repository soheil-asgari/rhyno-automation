using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;
using OfficeAutomation.Models;
using Polly;
using Polly.CircuitBreaker;

namespace OfficeAutomation.Services.Connectors;

public sealed class ConnectorExecutionService
{
    private readonly WorkflowDbContext _context;
    private readonly ConnectorOptions _options;
    private readonly Dictionary<string, IAsyncPolicy> _policies = new(StringComparer.OrdinalIgnoreCase);

    public ConnectorExecutionService(WorkflowDbContext context, IOptions<ConnectorOptions> options)
    {
        _context = context;
        _options = options.Value;
    }

    public async Task<ConnectorResponse> ExecuteAsync(IExternalConnector connector, ConnectorRequest request, CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.StartNew();
        var attempts = 0;
        var policy = GetPolicy(connector.Name);

        try
        {
            var response = await policy.ExecuteAsync(async ct =>
            {
                attempts++;
                var result = await connector.ExecuteAsync(request, ct);
                if (!result.Succeeded)
                {
                    throw new ConnectorExecutionException($"Connector {connector.Name} returned a failed response.", result.StatusCode);
                }

                return result;
            }, cancellationToken);

            await WriteExecutionLogAsync(connector.Name, request.OperationName, request.CorrelationId, true, attempts, started.ElapsedMilliseconds, null, cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            await WriteExecutionLogAsync(connector.Name, request.OperationName, request.CorrelationId, false, attempts, started.ElapsedMilliseconds, ex.Message, cancellationToken);
            await WriteDeadLetterAsync(connector.Name, request, attempts, ex.Message, cancellationToken);
            return new ConnectorResponse { Succeeded = false, ResponseBody = ex.Message };
        }
    }

    public async Task<IReadOnlyList<ConnectorDeadLetterMessage>> GetPendingDeadLettersAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ConnectorDeadLetterMessages
            .AsNoTracking()
            .Where(item => item.Status == ConnectorDeadLetterStatus.Pending)
            .OrderBy(item => item.FailedAt)
            .ToListAsync(cancellationToken);
    }

    private IAsyncPolicy GetPolicy(string connectorName)
    {
        if (_policies.TryGetValue(connectorName, out var existing))
        {
            return existing;
        }

        var retry = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                _options.RetryCount,
                retryAttempt => TimeSpan.FromSeconds(Math.Max(1, _options.RetryDelaySeconds * retryAttempt)));

        var breaker = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: Math.Max(2, _options.CircuitBreakerFailures),
                durationOfBreak: TimeSpan.FromSeconds(Math.Max(5, _options.CircuitBreakerBreakSeconds)));

        var wrapped = retry.WrapAsync(breaker);
        _policies[connectorName] = wrapped;
        return wrapped;
    }

    private async Task WriteExecutionLogAsync(
        string connectorName,
        string operationName,
        string? correlationId,
        bool succeeded,
        int attempts,
        long durationMs,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        _context.ConnectorExecutionLogs.Add(new ConnectorExecutionLog
        {
            ConnectorName = connectorName,
            OperationName = operationName,
            CorrelationId = correlationId,
            Succeeded = succeeded,
            AttemptCount = attempts,
            DurationMs = durationMs,
            ErrorMessage = errorMessage
        });

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task WriteDeadLetterAsync(
        string connectorName,
        ConnectorRequest request,
        int attempts,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        _context.ConnectorDeadLetterMessages.Add(new ConnectorDeadLetterMessage
        {
            ConnectorName = connectorName,
            OperationName = request.OperationName,
            CorrelationId = request.CorrelationId,
            PayloadJson = request.PayloadJson,
            AttemptCount = attempts,
            ErrorMessage = errorMessage
        });

        await _context.SaveChangesAsync(cancellationToken);
    }

    private sealed class ConnectorExecutionException : Exception
    {
        public ConnectorExecutionException(string message, int? statusCode) : base(statusCode.HasValue ? $"{message} StatusCode={statusCode}" : message)
        {
        }
    }
}



using System.Text.Json;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;

namespace OfficeAutomation.Services.Outbox;

public sealed class OutboxService : IOutboxService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RabbitMqOptions _rabbitMqOptions;
    private readonly Tenancy.ICurrentTenantAccessor _currentTenantAccessor;

    public OutboxService(Microsoft.Extensions.Options.IOptions<RabbitMqOptions> rabbitMqOptions, Tenancy.ICurrentTenantAccessor currentTenantAccessor)
    {
        _rabbitMqOptions = rabbitMqOptions.Value;
        _currentTenantAccessor = currentTenantAccessor;
    }

    public void EnqueueWorkflowStatusChanged(IWorkflowDbContext context, WorkflowStatusChangedIntegrationEvent integrationEvent)
    {
        context.OutboxMessages.Add(new OutboxMessage
        {
            MessageId = string.IsNullOrWhiteSpace(integrationEvent.CorrelationId)
                ? Guid.NewGuid().ToString("N")
                : $"{integrationEvent.CorrelationId}:{integrationEvent.EventType}",
            TenantId = _currentTenantAccessor.TenantId,
            EventType = integrationEvent.EventType,
            AggregateType = integrationEvent.DocumentType,
            AggregateId = $"{integrationEvent.DocumentType}:{integrationEvent.DocumentId}",
            ExchangeName = _rabbitMqOptions.ExchangeName,
            RoutingKey = $"{integrationEvent.DocumentType.ToLowerInvariant()}.{integrationEvent.DecisionType.ToLowerInvariant()}",
            PayloadJson = JsonSerializer.Serialize(integrationEvent, JsonOptions),
            CorrelationId = integrationEvent.CorrelationId,
            Status = OutboxMessageStatus.Pending,
            OccurredAt = integrationEvent.OccurredAt
        });
    }

    public void EnqueueWorkflowEscalated(IWorkflowDbContext context, WorkflowEscalatedIntegrationEvent integrationEvent)
    {
        context.OutboxMessages.Add(new OutboxMessage
        {
            MessageId = string.IsNullOrWhiteSpace(integrationEvent.CorrelationId)
                ? Guid.NewGuid().ToString("N")
                : $"{integrationEvent.CorrelationId}:{integrationEvent.EventType}",
            TenantId = _currentTenantAccessor.TenantId,
            EventType = integrationEvent.EventType,
            AggregateType = integrationEvent.DocumentType,
            AggregateId = $"{integrationEvent.DocumentType}:{integrationEvent.DocumentId}",
            ExchangeName = _rabbitMqOptions.ExchangeName,
            RoutingKey = $"{integrationEvent.DocumentType.ToLowerInvariant()}.escalated",
            PayloadJson = JsonSerializer.Serialize(integrationEvent, JsonOptions),
            CorrelationId = integrationEvent.CorrelationId,
            Status = OutboxMessageStatus.Pending,
            OccurredAt = integrationEvent.OccurredAt
        });
    }
}

namespace OfficeAutomation.Services.Outbox;

public interface IEventBusPublisher
{
    Task PublishAsync(string exchangeName, string routingKey, string payloadJson, string? messageId = null, CancellationToken cancellationToken = default);
}

using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace OfficeAutomation.Services.Outbox;

public sealed class RabbitMqEventBusPublisher : IEventBusPublisher, IDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private IConnection? _connection;
    private IChannel? _channel;
    private readonly SemaphoreSlim _sync = new(1, 1);

    public RabbitMqEventBusPublisher(IOptions<RabbitMqOptions> options, IServiceScopeFactory scopeFactory)
    {
        _options = options.Value;
        _scopeFactory = scopeFactory;
    }

    public async Task PublishAsync(string exchangeName, string routingKey, string payloadJson, string? messageId = null, CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);

        var channel = _channel ?? throw new InvalidOperationException("RabbitMQ channel is not available.");
        using var scope = _scopeFactory.CreateScope();
        var tenantQueueNameResolver = scope.ServiceProvider.GetRequiredService<Tenancy.ITenantQueueNameResolver>();
        var tenantExchange = tenantQueueNameResolver.ResolveExchangeName(exchangeName);
        var tenantRoutingKey = tenantQueueNameResolver.ResolveRoutingKey(routingKey);
        await channel.ExchangeDeclareAsync(tenantExchange, ExchangeType.Topic, durable: true, autoDelete: false, arguments: null, cancellationToken: cancellationToken);

        var body = Encoding.UTF8.GetBytes(payloadJson);
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = messageId
        };

        await channel.BasicPublishAsync(
            exchange: tenantExchange,
            routingKey: tenantRoutingKey,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_connection?.IsOpen == true && _channel?.IsOpen == true)
        {
            return;
        }

        await _sync.WaitAsync(cancellationToken);
        try
        {
            if (_connection?.IsOpen == true && _channel?.IsOpen == true)
            {
                return;
            }

            DisposeCore();

            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password,
                VirtualHost = _options.VirtualHost
            };

            if (_options.UseSsl)
            {
                factory.Ssl.Enabled = true;
            }

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        }
        finally
        {
            _sync.Release();
        }
    }

    public void Dispose()
    {
        DisposeCore();
        _sync.Dispose();
    }

    private void DisposeCore()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        _channel = null;
        _connection = null;
    }
}

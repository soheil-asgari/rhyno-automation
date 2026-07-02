namespace OfficeAutomation.Services.Connectors;

public sealed class ConnectorRequest
{
    public string OperationName { get; init; } = string.Empty;
    public string PayloadJson { get; init; } = string.Empty;
    public string? CorrelationId { get; init; }
    public IDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed class ConnectorResponse
{
    public bool Succeeded { get; init; }
    public int? StatusCode { get; init; }
    public string? ResponseBody { get; init; }
}

public interface IExternalConnector
{
    string Name { get; }
    Task<ConnectorResponse> ExecuteAsync(ConnectorRequest request, CancellationToken cancellationToken = default);
}

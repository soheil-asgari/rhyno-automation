using System.ClientModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;
using OfficeAutomation.Services;
using OpenAI;
using OpenAI.Chat;

public class AiService
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<AiService> _logger;
    private readonly TimeSpan _timeout;

    public AiService(IOptions<AiOptions> options, ILogger<AiService> logger)
    {
        var aiOptions = options.Value;
        if (string.IsNullOrWhiteSpace(aiOptions.ApiKey))
        {
            throw new InvalidOperationException("OpenAI:ApiKey is not configured.");
        }

        if (!Uri.TryCreate(aiOptions.Endpoint, UriKind.Absolute, out var endpoint))
        {
            throw new InvalidOperationException("OpenAI:Endpoint is not a valid absolute URI.");
        }

        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = endpoint
        };

        var credential = new ApiKeyCredential(aiOptions.ApiKey);
        var client = new OpenAIClient(credential, clientOptions);

        _chatClient = client.GetChatClient(aiOptions.Model);
        _logger = logger;
        _timeout = TimeSpan.FromSeconds(Math.Clamp(aiOptions.TimeoutSeconds, 5, 180));
    }

    public async Task<string> AskAsync(string message, CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_timeout);

        var response = await _chatClient.CompleteChatAsync(
            [new UserChatMessage(message)],
            cancellationToken: timeoutCts.Token);
        return response.Value.Content[0].Text;
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string message,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_timeout);

        var stream = _chatClient.CompleteChatStreamingAsync(
            [new UserChatMessage(message)],
            cancellationToken: timeoutCts.Token);
        await foreach (var update in stream)
        {
            foreach (var part in update.ContentUpdate)
            {
                _logger.LogTrace("Streaming AI chunk with length {ChunkLength}", part.Text?.Length ?? 0);
                if (!string.IsNullOrEmpty(part.Text))
                {
                    yield return part.Text;
                }
            }
        }
    }
}

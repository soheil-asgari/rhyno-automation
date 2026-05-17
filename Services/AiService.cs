using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

public class AiService
{
    private readonly ChatClient _chatClient;

    public AiService(IConfiguration config)
    {
        var apiKey = config["OpenAI:ApiKey"];

        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri("https://api.gapgpt.app/v1")
        };

        var credential = new ApiKeyCredential(apiKey);

        var client = new OpenAIClient(credential, options);

        _chatClient = client.GetChatClient("gapgpt-qwen-3.5");
    }

    public async Task<string> AskAsync(string message)
    {
        var response = await _chatClient.CompleteChatAsync(message);

        return response.Value.Content[0].Text;
    }
    public async IAsyncEnumerable<string> StreamAsync(string message)
    {
        var stream = _chatClient.CompleteChatStreamingAsync(message);
        await foreach (var update in stream)
        {
            Console.WriteLine("chunk received");

            foreach (var part in update.ContentUpdate)
            {
                yield return part.Text;
            }
        }

    }



}

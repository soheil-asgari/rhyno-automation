namespace OfficeAutomation.Services
{
    public sealed class AiOptions
    {
        public string? ApiKey { get; set; }
        public string Endpoint { get; set; } = "https://api.openai.com/v1";
        public string Model { get; set; } = "gpt-4.1-mini";
        public int TimeoutSeconds { get; set; } = 45;
    }
}

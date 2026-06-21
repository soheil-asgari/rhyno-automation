using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OfficeAutomation.Models;

public class AiAssistantController : Controller
{
    private readonly AiService _ai;
    private readonly ILogger<AiAssistantController> _logger;

    public AiAssistantController(AiService ai, ILogger<AiAssistantController> logger)
    {
        _ai = ai;
        _logger = logger;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AskAI([FromBody] UserMessage request)
    {
        var reply = await _ai.AskAsync(request.Message ?? string.Empty);
        return Json(new { reply });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task StreamAI([FromBody] UserMessage request)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";

        HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>()?.DisableBuffering();

        await foreach (var chunk in _ai.StreamAsync(request.Message ?? string.Empty))
        {
            _logger.LogDebug("Streaming AI chunk with length {ChunkLength}", chunk.Length);
            await Response.WriteAsync($"data: {chunk}\n\n");
            await Response.Body.FlushAsync();
        }
    }

    public IActionResult Index()
    {
        return View();
    }
}

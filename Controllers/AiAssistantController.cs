using Microsoft.AspNetCore.Mvc;
using OfficeAutomation.Models;

public class AiAssistantController : Controller
{
    private readonly AiService _ai;

    public AiAssistantController(AiService ai)
    {
        _ai = ai;
    }

    [HttpPost]
    public async Task<IActionResult> AskAI([FromBody] UserMessage request)
    {
        var reply = await _ai.AskAsync(request.Message);

        return Json(new { reply });
    }
   
    [HttpPost]
    public async Task StreamAI([FromBody] UserMessage request)
    {
        Response.Headers.Add("Content-Type", "text/event-stream");
        Response.Headers.Add("Cache-Control", "no-cache");

        HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>()?.DisableBuffering();

        await foreach (var chunk in _ai.StreamAsync(request.Message))
        {
            Console.Write(chunk);
            await Response.WriteAsync($"data: {chunk}\n\n");
            await Response.Body.FlushAsync();
        }
    }


    public IActionResult Index()
    {
        return View();
    }


}

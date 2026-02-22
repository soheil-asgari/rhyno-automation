using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OfficeAutomation.Controllers
{
    [Authorize]
    public class HumanCapitalController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
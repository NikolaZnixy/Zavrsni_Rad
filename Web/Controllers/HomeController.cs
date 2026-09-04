using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers
{
    // Public marketing landing page - no [Authorize] here. Signed-in users are offered a
    // "Dashboard" button in the top bar instead of being redirected away from this page.
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        // Reachable only from the Dashboard sidebar, so it lives under the /Dashboard/ prefix
        // like every other sidebar destination - it just doesn't require [Authorize] itself.
        [Route("Dashboard/Privacy")]
        public IActionResult Privacy()
        {
            return View();
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers
{
    // Landing page for signed-in users - the calendar overview that used to live at Home/Index.
    // Everything reachable from the floating sidebar hangs off this /Dashboard/ prefix.
    [Authorize]
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}

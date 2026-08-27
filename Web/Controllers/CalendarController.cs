using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers
{
    [Authorize]
    public class CalendarController : Controller
    {
        public IActionResult Index(Guid? accountId, int? year, int? month, int? day)
        {
            ViewBag.AccountId = accountId;
            ViewBag.Year = year;
            ViewBag.Month = month;
            ViewBag.Day = day;
            return View();
        }
    }
}

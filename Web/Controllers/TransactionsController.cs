using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers
{
    public class TransactionsController : Controller
    {
        public IActionResult Transactions()
        {
            return View();
        }
    }
}

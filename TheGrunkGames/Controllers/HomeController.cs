using Microsoft.AspNetCore.Mvc;

namespace TheGrunkGames.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}

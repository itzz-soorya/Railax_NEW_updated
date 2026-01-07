using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace RAILWAY_BACKEND.Controllers
{
    public class UserPost : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}

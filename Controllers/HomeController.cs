using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PerfumeStore.Models;

namespace PerfumeStore.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly PerfumeStore.Models.PerfumeStoreContext _db;

        public HomeController(ILogger<HomeController> logger, PerfumeStore.Models.PerfumeStoreContext db)
        {
            _logger = logger;
            _db = db;
        }

        public IActionResult Index()
        {
            List<Product> products = _db.Products
                            .Include(p => p.ProductImages)
                            .Include(p => p.Brand)
                            .Include(p => p.Categories)
                            .Where(p => p.IsPublished == true)
                            .OrderByDescending(p => p.ProductId)
                            .Take(10)
                            .ToList();
            var featured = products;
            return View(featured);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult ChatBot()
        {
            return View();
        }

        public IActionResult AdminPortal()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}

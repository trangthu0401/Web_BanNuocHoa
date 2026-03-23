using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PerfumeStore.Models;
using PerfumeStore.DesignPatterns.Proxy; // Import Proxy Pattern

namespace PerfumeStore.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        // Sử dụng Interface của Proxy thay vì gọi trực tiếp DbContext
        private readonly IProductQueryService _productQueryService;

        // Tiêm Proxy vào Constructor
        public HomeController(ILogger<HomeController> logger, IProductQueryService productQueryService)
        {
            _logger = logger;
            _productQueryService = productQueryService;
        }

        // Đổi thành async Task vì Proxy có gọi Database bất đồng bộ
        public async Task<IActionResult> Index()
        {
            // SỬ DỤNG PROXY PATTERN: 
            // - Lần đầu tiên: Sẽ mất khoảng 0.5s để chọc xuống Database.
            // - Trong 10 phút tiếp theo: Proxy sẽ trả ngay dữ liệu từ RAM (0.001s).
            var featuredProducts = await _productQueryService.GetFeaturedProductsAsync();

            return View(featuredProducts);
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
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PerfumeStore.Models;
using System.Security.Claims;

namespace PerfumeStore.Controllers
{
    public class FavoritesController : Controller
    {
        private readonly PerfumeStoreContext _context;

        public FavoritesController(PerfumeStoreContext context)
        {
            _context = context;
        }

        // GET: Favorites
        public async Task<IActionResult> Index()
        {
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(userEmail))
            {
                return RedirectToAction("Login", "Auth");
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Email == userEmail);

            if (customer == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            var favoriteProducts = await _context.Customers
                .Where(c => c.CustomerId == customer.CustomerId)
                .SelectMany(c => c.Products)
                .Include(p => p.Brand)
                .Include(p => p.ProductImages)
                .Include(p => p.Liters)
                .Where(p => p.IsPublished == true)
                .ToListAsync();

            return View(favoriteProducts);
        }



        // POST: Add to favorites
        [HttpPost]
        public async Task<IActionResult> AddToFavorites(int productId)
        {
            try
            {
                var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(userEmail))
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập để thêm vào yêu thích" });
            }

            var customer = await _context.Customers
                .Include(c => c.Products)
                .FirstOrDefaultAsync(c => c.Email == userEmail);

            if (customer == null)
            {
                return Json(new { success = false, message = "Không tìm thấy thông tin khách hàng" });
            }

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.ProductId == productId && p.IsPublished == true);

            if (product == null)
            {
                return Json(new { success = false, message = "Sản phẩm không tồn tại" });
            }

            // Check if already in favorites
            if (customer.Products.Any(p => p.ProductId == productId))
            {
                return Json(new { success = false, message = "Sản phẩm đã có trong danh sách yêu thích" });
            }

            customer.Products.Add(product);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã thêm vào danh sách yêu thích" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        // POST: Remove from favorites
        [HttpPost]
        public async Task<IActionResult> RemoveFromFavorites(int productId)
        {
            try
            {
                var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(userEmail))
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập" });
            }

            var customer = await _context.Customers
                .Include(c => c.Products)
                .FirstOrDefaultAsync(c => c.Email == userEmail);

            if (customer == null)
            {
                return Json(new { success = false, message = "Không tìm thấy thông tin khách hàng" });
            }

            var product = customer.Products.FirstOrDefault(p => p.ProductId == productId);
            if (product != null)
            {
                customer.Products.Remove(product);
                await _context.SaveChangesAsync();
            }

            return Json(new { success = true, message = "Đã xóa khỏi danh sách yêu thích" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        // GET: Check if product is in favorites
        [HttpGet]
        public async Task<IActionResult> CheckFavorite(int productId)
        {
            try
            {
                var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(userEmail))
            {
                return Json(new { isFavorite = false });
            }

            var customer = await _context.Customers
                .Include(c => c.Products)
                .FirstOrDefaultAsync(c => c.Email == userEmail);

            if (customer == null)
            {
                return Json(new { isFavorite = false });
            }

            var isFavorite = customer.Products.Any(p => p.ProductId == productId);
            return Json(new { isFavorite = isFavorite });
            }
            catch (Exception ex)
            {
                return Json(new { isFavorite = false });
            }
        }
    }
}
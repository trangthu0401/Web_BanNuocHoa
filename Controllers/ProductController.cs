using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PerfumeStore.Models;

namespace PerfumeStore.Controllers
{
    public class ProductController : Controller
    {
        private readonly PerfumeStoreContext _context;
        public ProductController(PerfumeStoreContext context)
        {
            _context = context;
        }
        public async Task<IActionResult> Index(int id)
        {
            var product = await _context.Products
                .Include(p => p.Brand)
                .Include(p => p.Discount)
                .Include(p => p.ProductImages)
                .Include(p => p.Categories)
                .Include(p => p.Liters)
                .Include(p => p.Comments.Where(c => c.IsPublished == true))
                .Include(p => p.OrderDetails)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null)
            {
                return NotFound();
            }

            // Tính phần "Bạn đang xem" = ProductName - SuggestionName
            string viewingType = product.ProductName;
            if (!string.IsNullOrEmpty(product.SuggestionName) && product.ProductName.Contains(product.SuggestionName))
            {
                viewingType = product.ProductName.Replace(product.SuggestionName, "").Trim();
                // Xóa các ký tự đặc biệt ở đầu/cuối nếu có
                viewingType = viewingType.TrimStart(' ', '-', '–', '—').TrimEnd(' ', '-', '–', '—');
            }
            // Nếu viewingType rỗng hoặc chỉ còn khoảng trắng, dùng Concentration hoặc mặc định
            if (string.IsNullOrWhiteSpace(viewingType))
            {
                viewingType = product.Concentration ?? "EDP";
            }
            ViewBag.ViewingType = viewingType;

            // Lấy danh sách sản phẩm liên quan: các sản phẩm có ProductName chứa SuggestionName (trừ sản phẩm hiện tại)
            var relatedProducts = new List<Product>();
            if (!string.IsNullOrEmpty(product.SuggestionName))
            {
                relatedProducts = await _context.Products
                    .Include(p => p.ProductImages)
                    .Where(p => p.ProductId != id && 
                                p.ProductName.Contains(product.SuggestionName) &&
                                (p.IsPublished == true || p.IsPublished == null))
                    .Take(4) // Lấy tối đa 4 sản phẩm
                    .ToListAsync();
            }
            ViewBag.RelatedProducts = relatedProducts;

            return View(product);
        }



        // Trả ảnh từ DB theo ProductID
        public IActionResult GetImage(int id)
        {
            var image = _context.ProductImages.FirstOrDefault(i => i.ProductId == id);
            if (image != null && image.ImageData != null)
            {
                return File(image.ImageData, image.ImageMimeType);
            }

            // Nếu không có ảnh, trả về ảnh mặc định
            var defaultPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/no-image.png");
            if (System.IO.File.Exists(defaultPath))
            {
                var imageData = System.IO.File.ReadAllBytes(defaultPath);
                return File(imageData, "image/png");
            }
            return NotFound();
        }

        // Trả ảnh từ DB theo ImageID
        public IActionResult GetImageById(int imageId)
        {
            var image = _context.ProductImages.FirstOrDefault(i => i.ImageId == imageId);
            if (image != null && image.ImageData != null)
            {
                return File(image.ImageData, image.ImageMimeType);
            }

            // Nếu không có ảnh, trả về ảnh mặc định
            var defaultPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/no-image.png");
            if (System.IO.File.Exists(defaultPath))
            {
                var imageData = System.IO.File.ReadAllBytes(defaultPath);
                return File(imageData, "image/png");
            }
            return NotFound();
        }
    }
}

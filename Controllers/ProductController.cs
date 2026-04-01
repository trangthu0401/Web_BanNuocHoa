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

            // ======================================================================
            // ÁP DỤNG FACTORY PATTERN: Tự động phân bổ chính sách giá & khuyến mãi
            // ======================================================================
            // ⚠️ LƯU Ý SƯ PHẠM (TẠI SAO KHÔNG ĐĂNG KÝ DI VÀ BỎ HARDCODE ID?):
            // 1. Không dùng DI: Vì ProductProcessorFactory dùng phương thức tĩnh (static GetProcessor) 
            //    nên ta gọi trực tiếp thông qua tên Class mà không cần đăng ký trong Program.cs.
            // 2. Không Hardcode ID: Ta truyền thẳng Tên Danh Mục (CategoryName) lấy từ Database 
            //    vào Nhà máy thay vì kiểm tra ID cứng (switch case 1, 2). Việc này đảm bảo chuẩn 
            //    kiến trúc. Giả sử Database xóa danh mục cũ và sinh ID mới, Pattern vẫn tự động 
            //    nhận diện đúng chữ "Nam" hay "Nữ" và xuất ra đúng chính sách khuyến mãi.
            // ======================================================================

            // Lấy tên danh mục một cách an toàn (tránh lỗi NullReferenceException)
            string categoryName = product.Categories?.FirstOrDefault()?.CategoryName ?? "";

            // Gọi Nhà máy (Factory) để chế tạo ra đúng Bộ xử lý (Nam/Nữ/Unisex)
            var processor = PerfumeStore.DesignPatterns.Factory.ProductProcessorFactory.GetProcessor(categoryName);

            // Nhận kết quả từ Pattern và đẩy ra ViewBag để giao diện (View) hiển thị
            ViewBag.FinalPrice = processor.CalculateFinalPrice(product.Price);
            ViewBag.PromotionNote = processor.GetPromotionNote();


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

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PerfumeStore.Models;
using PerfumeStore.Areas.Admin.Filters;

namespace PerfumeStore.Areas.Admin.Controllers
{
    [Area("Admin")]
    [AdminAuthorize] // Kiểm tra đăng nhập cơ bản giống ProductsController
    public class CategoryController : Controller
    {
        private readonly PerfumeStoreContext _context;

        public CategoryController(PerfumeStoreContext context)
        {
            _context = context;
        }

        // GET: Admin/Category
        [RequirePermission("View Categories")]
        public async Task<IActionResult> Index()
        {
            var categories = await _context.Categories
                .Include(c => c.Products)
                .OrderBy(c => c.CategoryName)
                .ToListAsync();
            
            return View(categories);
        }

        // GET: Admin/Category/Details/5
        [RequirePermission("View Categories")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var category = await _context.Categories
                .Include(c => c.Products)
                .FirstOrDefaultAsync(m => m.CategoryId == id);
            
            if (category == null)
            {
                return NotFound();
            }

            return View(category);
        }

        // GET: Admin/Category/Create
        [RequirePermission("Create Category")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Admin/Category/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("Create Category")]
        public async Task<IActionResult> Create([Bind("CategoryName")] Category category)
        {
            if (ModelState.IsValid)
            {
                // Kiểm tra trùng tên danh mục
                var existingCategory = await _context.Categories
                    .FirstOrDefaultAsync(c => c.CategoryName.ToLower() == category.CategoryName.ToLower());
                
                if (existingCategory != null)
                {
                    ModelState.AddModelError("CategoryName", "Tên danh mục đã tồn tại.");
                    return View(category);
                }

                _context.Add(category);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Thêm danh mục thành công!";
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        // GET: Admin/Category/Edit/5
        [RequirePermission("Edit Category")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                return NotFound();
            }
            return View(category);
        }

        // POST: Admin/Category/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("Edit Category")]
        public async Task<IActionResult> Edit(int id, [Bind("CategoryId,CategoryName")] Category category)
        {
            if (id != category.CategoryId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Kiểm tra trùng tên danh mục (trừ chính nó)
                    var existingCategory = await _context.Categories
                        .FirstOrDefaultAsync(c => c.CategoryName.ToLower() == category.CategoryName.ToLower() 
                                                && c.CategoryId != category.CategoryId);
                    
                    if (existingCategory != null)
                    {
                        ModelState.AddModelError("CategoryName", "Tên danh mục đã tồn tại.");
                        return View(category);
                    }

                    _context.Update(category);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Cập nhật danh mục thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CategoryExists(category.CategoryId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        // GET: Admin/Category/Delete/5
        [RequirePermission("Delete Category")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var category = await _context.Categories
                .Include(c => c.Products)
                .FirstOrDefaultAsync(m => m.CategoryId == id);
            
            if (category == null)
            {
                return NotFound();
            }

            // Double check số lượng sản phẩm từ database
            var actualProductCount = await _context.Products
                .Where(p => p.Categories.Any(c => c.CategoryId == id))
                .CountAsync();

            // Cập nhật ViewBag để hiển thị số lượng chính xác
            ViewBag.ActualProductCount = actualProductCount;

            return View(category);
        }

        // POST: Admin/Category/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [RequirePermission("Delete Category")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var category = await _context.Categories
                .Include(c => c.Products)
                .FirstOrDefaultAsync(c => c.CategoryId == id);
            
            if (category == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy danh mục cần xóa.";
                return RedirectToAction(nameof(Index));
            }

            // Kiểm tra xem danh mục có sản phẩm không (double check)
            var productCount = await _context.Products
                .Where(p => p.Categories.Any(c => c.CategoryId == id))
                .CountAsync();

            if (productCount > 0)
            {
                TempData["ErrorMessage"] = $"Không thể xóa danh mục '{category.CategoryName}' vì còn {productCount} sản phẩm thuộc danh mục này. Vui lòng di chuyển hoặc xóa các sản phẩm trước.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã xóa danh mục '{category.CategoryName}' thành công!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Có lỗi xảy ra khi xóa danh mục: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool CategoryExists(int id)
        {
            return _context.Categories.Any(e => e.CategoryId == id);
        }

        // AJAX endpoint để lấy thống kê nhanh
        [HttpGet]
        [RequirePermission("View Categories")]
        public async Task<IActionResult> GetCategoryStats()
        {
            var stats = await _context.Categories
                .Select(c => new
                {
                    CategoryId = c.CategoryId,
                    CategoryName = c.CategoryName,
                    ProductCount = c.Products.Count()
                })
                .OrderByDescending(c => c.ProductCount)
                .ToListAsync();

            return Json(stats);
        }

        // AJAX endpoint để kiểm tra số lượng sản phẩm trong danh mục
        [HttpGet]
        [RequirePermission("View Categories")]
        public async Task<IActionResult> CheckCategoryProducts(int id)
        {
            var productCount = await _context.Products
                .Where(p => p.Categories.Any(c => c.CategoryId == id))
                .CountAsync();

            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.CategoryId == id);

            return Json(new
            {
                categoryId = id,
                categoryName = category?.CategoryName ?? "Unknown",
                productCount = productCount,
                canDelete = productCount == 0
            });
        }
    }
}
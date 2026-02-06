using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PerfumeStore.Areas.Admin.Models;
using PerfumeStore.Areas.Admin.Models.ViewModels;
using PerfumeStore.Areas.Admin.Services;
using PerfumeStore.Areas.Admin.Filters;

namespace PerfumeStore.Areas.Admin.Controllers
{
    [Area("Admin")]
    [AdminAuthorize]
    public class CommentsController : Controller
    {
        private readonly PerfumeStoreContext _db;
        private readonly DBQueryService.IDbQueryService _queryService;
        private readonly IPaginationService _paginationService;

        public CommentsController(PerfumeStoreContext db, DBQueryService.IDbQueryService queryService, IPaginationService paginationService)
        {
            _db = db;
            _queryService = queryService;
            _paginationService = paginationService;
        }

        public async Task<IActionResult> Index(string? searchProduct, string? searchBrand, int? categoryId, int page = 1)
        {
            var productsQuery = _db.Products
                .Include(p => p.Brand)
                .Include(p => p.Categories)
                .Include(p => p.Comments)
                    .ThenInclude(c => c.Customer)
                .Include(p => p.ProductImages)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrWhiteSpace(searchProduct))
            {
                productsQuery = productsQuery.Where(p => p.ProductName.Contains(searchProduct));
            }

            if (!string.IsNullOrWhiteSpace(searchBrand))
            {
                productsQuery = productsQuery.Where(p => p.Brand.BrandName.Contains(searchBrand));
            }

            if (categoryId.HasValue)
            {
                productsQuery = productsQuery.Where(p => p.Categories.Any(c => c.CategoryId == categoryId.Value));
            }

            var products = await productsQuery.ToListAsync();

            var productCommentsVMs = products.Select(p => new ProductCommentsVM
            {
                ProductId = p.ProductId,
                ProductName = p.ProductName,
                BrandName = p.Brand?.BrandName,
                CategoryNames = string.Join(", ", p.Categories.Select(c => c.CategoryName)),
                TotalComments = p.Comments.Count,
                PublishedComments = p.Comments.Count(c => c.IsPublished == true),
                PendingComments = p.Comments.Count(c => c.IsPublished != true),
                AverageRating = p.Comments.Any() ? p.Comments.Average(c => c.Rating) : 0,
                FirstImageId = p.ProductImages.FirstOrDefault()?.ImageId
            })
            .OrderByDescending(p => p.TotalComments)
            .ToList();

            // Apply pagination
            var pagedResult = _paginationService.Paginate(productCommentsVMs, page, 10);

            ViewBag.Categories = await _db.Categories.OrderBy(c => c.CategoryName).ToListAsync();
            ViewBag.SearchProduct = searchProduct;
            ViewBag.SearchBrand = searchBrand;
            ViewBag.CategoryId = categoryId;

            return View(pagedResult);
        }

        public async Task<IActionResult> Details(int productId, bool? isPublished)
        {
            var product = await _db.Products
                .Include(p => p.Brand)
                .Include(p => p.Categories)
                .FirstOrDefaultAsync(p => p.ProductId == productId);

            if (product == null)
            {
                return NotFound();
            }

            var commentsQuery = _db.Comments
                .Include(c => c.Customer)
                .Include(c => c.Product)
                .Where(c => c.ProductId == productId)
                .AsQueryable();

            // Filter by publish status
            if (isPublished.HasValue)
            {
                commentsQuery = commentsQuery.Where(c => c.IsPublished == isPublished.Value);
            }

            var comments = await commentsQuery
                .OrderByDescending(c => c.CommentDate)
                .ToListAsync();

            var commentDetailsVMs = comments.Select(c => new CommentDetailsVM
            {
                ProductId = c.ProductId,
                CustomerId = c.CustomerId,
                ProductName = c.Product.ProductName,
                CustomerName = c.Customer.Name ?? "Unknown",
                CustomerEmail = c.Customer.Email,
                CommentDate = c.CommentDate,
                Rating = c.Rating,
                Content = c.Content,
                IsPublished = c.IsPublished
            }).ToList();

            ViewBag.Product = product;
            ViewBag.IsPublished = isPublished;

            return View(commentDetailsVMs);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int productId, int customerId)
        {
            var comment = await _db.Comments
                .FirstOrDefaultAsync(c => c.ProductId == productId && c.CustomerId == customerId);

            if (comment == null)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Không tìm thấy bình luận" });
                }
                TempData["ErrorMessage"] = "Không tìm thấy bình luận";
                return RedirectToAction(nameof(Details), new { productId });
            }

            comment.IsPublished = true;

            try
            {
                await _db.SaveChangesAsync();
                
                // Nếu là AJAX request, trả về JSON
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, message = "Đã phê duyệt bình luận!" });
                }
                
                // Nếu là request thông thường, redirect về trang Details
                TempData["SuccessMessage"] = "Đã phê duyệt bình luận thành công!";
                return RedirectToAction(nameof(Details), new { productId });
            }
            catch (Exception ex)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Lỗi: " + ex.Message });
                }
                TempData["ErrorMessage"] = "Lỗi: " + ex.Message;
                return RedirectToAction(nameof(Details), new { productId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unpublish(int productId, int customerId)
        {
            var comment = await _db.Comments
                .FirstOrDefaultAsync(c => c.ProductId == productId && c.CustomerId == customerId);

            if (comment == null)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Không tìm thấy bình luận" });
                }
                TempData["ErrorMessage"] = "Không tìm thấy bình luận";
                return RedirectToAction(nameof(Details), new { productId });
            }

            comment.IsPublished = false;
            
            // Mark entity as modified to ensure EF tracks the change
            _db.Entry(comment).State = Microsoft.EntityFrameworkCore.EntityState.Modified;

            try
            {
                await _db.SaveChangesAsync();
                
                // Nếu là AJAX request, trả về JSON
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, message = "Đã ẩn bình luận!" });
                }
                
                // Nếu là request thông thường, redirect về trang Details
                TempData["SuccessMessage"] = "Đã ẩn bình luận thành công!";
                return RedirectToAction(nameof(Details), new { productId });
            }
            catch (Exception ex)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Lỗi: " + ex.Message });
                }
                TempData["ErrorMessage"] = "Lỗi: " + ex.Message;
                return RedirectToAction(nameof(Details), new { productId });
            }
        }



        public async Task<IActionResult> Create(int? productId)
        {
            ViewBag.Products = await _db.Products
                .OrderBy(p => p.ProductName)
                .Select(p => new { p.ProductId, p.ProductName })
                .ToListAsync();

            ViewBag.Customers = await _db.Customers
                .OrderBy(c => c.Name)
                .Select(c => new { c.CustomerId, c.Name, c.Email })
                .ToListAsync();

            var comment = new Comment
            {
                ProductId = productId ?? 0,
                CommentDate = DateTime.Now,
                IsPublished = true
            };

            return View(comment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Comment comment)
        {
            // Check if comment already exists
            var existingComment = await _db.Comments
                .FirstOrDefaultAsync(c => c.ProductId == comment.ProductId && c.CustomerId == comment.CustomerId);

            if (existingComment != null)
            {
                ModelState.AddModelError("", "Khách hàng này đã bình luận sản phẩm này rồi!");
                
                ViewBag.Products = await _db.Products
                    .OrderBy(p => p.ProductName)
                    .Select(p => new { p.ProductId, p.ProductName })
                    .ToListAsync();

                ViewBag.Customers = await _db.Customers
                    .OrderBy(c => c.Name)
                    .Select(c => new { c.CustomerId, c.Name, c.Email })
                    .ToListAsync();

                return View(comment);
            }

            // Get IsPublished from form (checkbox returns "true" when checked, "false" when unchecked)
            var isPublishedValues = Request.Form["IsPublished"];
            bool? isPublished = isPublishedValues.Contains("true") ? true : false;

            comment.CommentDate = DateTime.Now;
            comment.IsPublished = isPublished;

            try
            {
                _db.Comments.Add(comment);
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "Tạo bình luận thành công!";
                return RedirectToAction(nameof(Details), new { productId = comment.ProductId });
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("", "Không thể tạo bình luận. Vui lòng thử lại.");
            }

            ViewBag.Products = await _db.Products
                .OrderBy(p => p.ProductName)
                .Select(p => new { p.ProductId, p.ProductName })
                .ToListAsync();

            ViewBag.Customers = await _db.Customers
                .OrderBy(c => c.Name)
                .Select(c => new { c.CustomerId, c.Name, c.Email })
                .ToListAsync();

            return View(comment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int productId, int customerId)
        {
            var comment = await _db.Comments
                .FirstOrDefaultAsync(c => c.ProductId == productId && c.CustomerId == customerId);

            if (comment == null)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Không tìm thấy bình luận" });
                }
                TempData["ErrorMessage"] = "Không tìm thấy bình luận";
                return RedirectToAction(nameof(Details), new { productId });
            }

            try
            {
                _db.Comments.Remove(comment);
                await _db.SaveChangesAsync();
                
                // Nếu là AJAX request, trả về JSON
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, message = "Đã xóa bình luận!" });
                }
                
                // Nếu là request thông thường, redirect về trang Details
                TempData["SuccessMessage"] = "Đã xóa bình luận thành công!";
                return RedirectToAction(nameof(Details), new { productId });
            }
            catch (Exception ex)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Lỗi: " + ex.Message });
                }
                TempData["ErrorMessage"] = "Lỗi: " + ex.Message;
                return RedirectToAction(nameof(Details), new { productId });
            }
        }

        // Simple test action for unpublish
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TestUnpublish(int productId, int customerId)
        {
            try
            {
                // Use raw SQL to update
                var rowsAffected = await _db.Database.ExecuteSqlRawAsync(
                    "UPDATE Comments SET IsPublished = 0 WHERE ProductId = {0} AND CustomerId = {1}",
                    productId, customerId);

                if (rowsAffected > 0)
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = true, message = "Đã ẩn bình luận!" });
                    }
                    TempData["SuccessMessage"] = "Đã ẩn bình luận thành công!";
                }
                else
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = false, message = "Không tìm thấy bình luận" });
                    }
                    TempData["ErrorMessage"] = "Không tìm thấy bình luận";
                }

                return RedirectToAction(nameof(Details), new { productId });
            }
            catch (Exception ex)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Lỗi: " + ex.Message });
                }
                TempData["ErrorMessage"] = "Lỗi: " + ex.Message;
                return RedirectToAction(nameof(Details), new { productId });
            }
        }
    }
}

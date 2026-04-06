using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PerfumeStore.Areas.Admin.Models;
using PerfumeStore.Areas.Admin.Services;
using PerfumeStore.Areas.Admin.Filters;

namespace PerfumeStore.Areas.Admin.Controllers
{
    [Area("Admin")]
    [AdminAuthorize] // Kiểm tra đăng nhập cơ bản
    public class ProductsController : Controller
    {
        private readonly PerfumeStoreContext _db;
        private readonly DBQueryService.IDbQueryService _queryService;
        private readonly IPaginationService _paginationService;
        private readonly PerfumeStore.DesignPatterns.Proxy.ProtectionProxy.IProductDeleteService _proxyService;

        public ProductsController(
            PerfumeStoreContext db, 
            DBQueryService.IDbQueryService queryService, 
            IPaginationService paginationService,
            PerfumeStore.DesignPatterns.Proxy.ProtectionProxy.IProductDeleteService proxyService)
        {
            _db = db;
            _queryService = queryService;
            _paginationService = paginationService;
            _proxyService = proxyService;
        }

        [RequirePermission("View Products")]
        public async Task<IActionResult> Index(int? categoryId, string searchName, int page = 1)
        {
            var items = await _queryService.GetProductsByCategory(categoryId, searchName);
            var pagedResult = _paginationService.Paginate(items, page, 10);
            
            ViewBag.Categories = await _queryService.GetCategoriesOrderedByNameAsync();
            ViewBag.SearchName = searchName;
            ViewBag.CategoryId = categoryId;
            
            return View(pagedResult);
        }

        [RequirePermission("Create Product")]
        public async Task<IActionResult> Create()
        {
            var vm = await BuildFormVM();
            return View("Edit", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("Create Product")]
        public async Task<IActionResult> Create(AdminProductFormVM vm)
        {
            Console.WriteLine($"Create action called with ImageFiles count: {vm.ImageFiles?.Count ?? 0}");
            if (vm.ImageFiles != null)
            {
                foreach (var file in vm.ImageFiles)
                {
                    Console.WriteLine($"Received file: {file.FileName}, Size: {file.Length}");
                }
            }

            await ValidateAndBindSelects(vm);
            if (!ModelState.IsValid)
            {
                Console.WriteLine("ModelState is invalid");
                foreach (var kvp in ModelState)
                {
                    if (kvp.Value.Errors.Any())
                    {
                        Console.WriteLine($"Field '{kvp.Key}' has errors:");
                        foreach (var error in kvp.Value.Errors)
                        {
                            Console.WriteLine($"  - {error.ErrorMessage}");
                        }
                    }
                }
                return View("Edit", vm);
            }

            var product = new Product
            {
                ProductName = vm.ProductName,
                SuggestionName = vm.SuggestionName,
                Price = vm.Price,
                Stock = vm.Stock,
                Scent = vm.Scent,
                BrandId = vm.BrandId,
                WarrantyPeriodMonths = vm.WarrantyPeriodMonths,
                IsPublished = vm.IsPublished,
                Origin = vm.Origin,
                ReleaseYear = vm.ReleaseYear,
                Introduction = vm.Introduction,
                Concentration = vm.Concentration,
                Craftsman = vm.Craftsman,
                Style = vm.Style,
                UsingOccasion = vm.UsingOccasion,
                TopNote = vm.TopNote,
                HeartNote = vm.HeartNote,
                BaseNote = vm.BaseNote,
                DiscountPrice = vm.DiscountPrice,
                DiscountId = vm.DiscountId,
                DescriptionNo1 = vm.DescriptionNo1,
                DescriptionNo2 = vm.DescriptionNo2
            };

            // Categories
            var categories = await _db.Categories.Where(c => vm.SelectedCategoryIds.Contains(c.CategoryId)).ToListAsync();
            product.Categories = categories;

            _db.Products.Add(product);
            await _db.SaveChangesAsync();

            // Handle image uploads
            var imageFiles = Request.Form.Files.Where(f => f.Name == "ImageFiles").ToList();
            await ProcessImageUploads(product.ProductId, imageFiles);

            TempData["SuccessMessage"] = "Thêm sản phẩm thành công!";
            return RedirectToAction(nameof(Index));
        }

        [RequirePermission("Edit Product")]
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _queryService.GetProductWithCategoriesAsync(id);
            if (product == null) return NotFound();

            var vm = await BuildFormVM(product);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("Edit Product")]
        public async Task<IActionResult> Edit(int id, AdminProductFormVM vm)
        {
            await ValidateAndBindSelects(vm);
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var product = await _queryService.GetProductWithCategoriesAsync(id);
            if (product == null) return NotFound();

            product.ProductName = vm.ProductName;
            product.SuggestionName = vm.SuggestionName;
            product.Price = vm.Price;
            product.Stock = vm.Stock;
            product.Scent = vm.Scent;
            product.BrandId = vm.BrandId;
            product.WarrantyPeriodMonths = vm.WarrantyPeriodMonths;
            product.IsPublished = vm.IsPublished;
            product.Origin = vm.Origin;
            product.ReleaseYear = vm.ReleaseYear;
            product.Introduction = vm.Introduction;
            product.Concentration = vm.Concentration;
            product.Craftsman = vm.Craftsman;
            product.Style = vm.Style;
            product.UsingOccasion = vm.UsingOccasion;
            product.TopNote = vm.TopNote;
            product.HeartNote = vm.HeartNote;
            product.BaseNote = vm.BaseNote;
            product.DiscountPrice = vm.DiscountPrice;
            product.DiscountId = vm.DiscountId;
            product.DescriptionNo1 = vm.DescriptionNo1;
            product.DescriptionNo2 = vm.DescriptionNo2;

            // update categories
            product.Categories.Clear();
            var categories = await _db.Categories.Where(c => vm.SelectedCategoryIds.Contains(c.CategoryId)).ToListAsync();
            foreach (var c in categories) product.Categories.Add(c);

            // Handle image deletions
            if (vm.DeletedImageIds.Any())
            {
                var imagesToDelete = await _db.ProductImages
                    .Where(pi => vm.DeletedImageIds.Contains(pi.ImageId) && pi.ProductId == id)
                    .ToListAsync();
                _db.ProductImages.RemoveRange(imagesToDelete);
            }

            // Handle new image uploads
            var imageFiles = Request.Form.Files.Where(f => f.Name == "ImageFiles").ToList();
            await ProcessImageUploads(id, imageFiles);

            await _db.SaveChangesAsync();
            
            TempData["SuccessMessage"] = "Cập nhật sản phẩm thành công!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("Delete Product")]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _queryService.GetProductWithCategoriesAsync(id);
            if (product == null)
            {
                TempData["Error"] = "Không tìm thấy sản phẩm.";
                return RedirectToAction(nameof(Index));
            }

            // Validation: Kiểm tra sản phẩm đã có đơn hàng chưa
            bool hasOrder = await _queryService.ProductHasOrdersAsync(id);
            if (hasOrder)
            {
                TempData["Error"] = "Không thể xóa sản phẩm đã phát sinh đơn hàng.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // Lấy RoleName từ Claims do tiến trình đăng nhập Admin thiết lập
                var roleName = User.FindFirst("RoleName")?.Value ?? "";

                // Gọi xóa thông qua Protection Proxy
                await _proxyService.DeleteProductAsync(id, roleName);

                TempData["SuccessMessage"] = "Đã xóa sản phẩm thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (UnauthorizedAccessException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi khi xóa sản phẩm: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        private async Task<AdminProductFormVM> BuildFormVM(Product? product = null)
        {
            var vm = new AdminProductFormVM();
            if (product != null)
            {
                vm.ProductId = product.ProductId;
                vm.ProductName = product.ProductName;
                vm.SuggestionName = product.SuggestionName;
                vm.Price = product.Price;
                vm.Stock = product.Stock;
                vm.Scent = product.Scent;
                vm.BrandId = product.BrandId;
                vm.WarrantyPeriodMonths = product.WarrantyPeriodMonths;
                vm.IsPublished = product.IsPublished ?? true;
                vm.Origin = product.Origin;
                vm.ReleaseYear = product.ReleaseYear;
                vm.Introduction = product.Introduction;
                vm.SelectedCategoryIds = product.Categories.Select(c => c.CategoryId).ToList();
                vm.Concentration = product.Concentration;
                vm.Craftsman = product.Craftsman;
                vm.Style = product.Style;
                vm.UsingOccasion = product.UsingOccasion;
                vm.TopNote = product.TopNote;
                vm.HeartNote = product.HeartNote;
                vm.BaseNote = product.BaseNote;
                vm.DiscountPrice = product.DiscountPrice;
                vm.DiscountId = product.DiscountId;
                vm.DescriptionNo1 = product.DescriptionNo1;
                vm.DescriptionNo2 = product.DescriptionNo2;
                
                // Load existing images
                vm.ExistingImages = await _db.ProductImages
                    .Where(pi => pi.ProductId == product.ProductId)
                    .ToListAsync();
            }

            var brands = await _queryService.GetBrandsOrderedByNameAsync();
            vm.BrandOptions = brands
                .Select(b => new SelectListItem { Value = b.BrandId.ToString(), Text = b.BrandName })
                .ToList();

            var categories = await _queryService.GetCategoriesOrderedByNameAsync();
            vm.CategoryOptions = categories
                .Select(c => new SelectListItem { Value = c.CategoryId.ToString(), Text = c.CategoryName })
                .ToList();

            var discountPrograms = await _queryService.GetDiscountProgramsOrderedByNameAsync();
            vm.DiscountOptions = discountPrograms
                .Select(d => new SelectListItem { Value = d.DiscountId.ToString(), Text = d.DiscountName })
                .ToList();

            return vm;
        }

        private async Task ValidateAndBindSelects(AdminProductFormVM vm)
        {
            var brands = await _queryService.GetBrandsOrderedByNameAsync();
            vm.BrandOptions = brands
                .Select(b => new SelectListItem { Value = b.BrandId.ToString(), Text = b.BrandName })
                .ToList();

            var categories = await _queryService.GetCategoriesOrderedByNameAsync();
            vm.CategoryOptions = categories
                .Select(c => new SelectListItem { Value = c.CategoryId.ToString(), Text = c.CategoryName })
                .ToList();

            var discountPrograms = await _queryService.GetDiscountProgramsOrderedByNameAsync();
            vm.DiscountOptions = discountPrograms
                .Select(d => new SelectListItem { Value = d.DiscountId.ToString(), Text = d.DiscountName })
                .ToList();

            if (!vm.SelectedCategoryIds.Any())
            {
                ModelState.AddModelError("SelectedCategoryIds", "Chọn ít nhất 1 danh mục");
            }

            bool brandExists = await _queryService.BrandExistsAsync(vm.BrandId);
            if (!brandExists) ModelState.AddModelError("BrandId", "Thương hiệu không hợp lệ");
        }

        private async Task ProcessImageUploads(int productId, List<IFormFile> imageFiles)
        {
            Console.WriteLine($"ProcessImageUploads called with productId: {productId}, imageFiles count: {imageFiles?.Count ?? 0}");
            
            if (imageFiles == null || !imageFiles.Any()) 
            {
                Console.WriteLine("No image files to process");
                return;
            }

            int processedCount = 0;
            foreach (var file in imageFiles)
            {
                Console.WriteLine($"Processing file: {file.FileName}, Size: {file.Length}, ContentType: {file.ContentType}");
                
                if (file.Length > 0)
                {
                    // Validate file type
                    var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
                    if (!allowedTypes.Contains(file.ContentType.ToLower()))
                    {
                        Console.WriteLine($"File type not allowed: {file.ContentType}");
                        continue;
                    }

                    // Validate file size (max 5MB)
                    if (file.Length > 5 * 1024 * 1024)
                    {
                        Console.WriteLine($"File too large: {file.Length} bytes");
                        continue;
                    }

                    using var memoryStream = new MemoryStream();
                    await file.CopyToAsync(memoryStream);

                    var productImage = new ProductImage
                    {
                        ProductId = productId,
                        ImageData = memoryStream.ToArray(),
                        ImageMimeType = file.ContentType
                    };

                    _db.ProductImages.Add(productImage);
                    processedCount++;
                    Console.WriteLine($"Added image to database: {file.FileName}");
                }
            }

            if (processedCount > 0)
            {
                await _db.SaveChangesAsync();
                Console.WriteLine($"Saved {processedCount} images to database");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetImage(int imageId)
        {
            var image = await _db.ProductImages.FindAsync(imageId);
            if (image == null)
                return NotFound();

            return File(image.ImageData, image.ImageMimeType);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteImage(int imageId)
        {
            var image = await _db.ProductImages.FindAsync(imageId);
            if (image == null)
                return Json(new { success = false, message = "Không tìm thấy ảnh" });

            _db.ProductImages.Remove(image);
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Đã xóa ảnh thành công" });
        }
    }
}



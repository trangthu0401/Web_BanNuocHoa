using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PerfumeStore.Models;
using PerfumeStore.Areas.Admin.Filters;
using PerfumeStore.Areas.Admin.Models;
using PerfumeStore.Areas.Admin.Services;

namespace PerfumeStore.Areas.Admin.Controllers
{
    [Area("Admin")]
    [AdminAuthorize]
    public class WarrantyController : Controller
    {
        private readonly Models.PerfumeStoreContext _context;
        private readonly IPaginationService _paginationService;

        public WarrantyController(Models.PerfumeStoreContext context, IPaginationService paginationService)
        {
            _context = context;
            _paginationService = paginationService;
        }

        // GET: Admin/Warranty
        // [RequirePermission("View Warranties")] // Tạm thời bỏ để test
        public async Task<IActionResult> Index(string? status, string? search, DateTime? fromDate, DateTime? toDate, int page = 1)
        {
            try
            {
                // Test connection
                var warrantyCount = await _context.Warranties.CountAsync();
                ViewBag.TestMessage = $"Kết nối thành công! Có {warrantyCount} bảo hành trong database.";
                
                var query = _context.Warranties
                    .Include(w => w.WarrantyClaims)
                    .AsQueryable();

            // Filter by status
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(w => w.Status == status);
            }

            // Filter by warranty code or customer info
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(w => w.WarrantyCode.Contains(search) || 
                                        w.Notes.Contains(search));
            }

            // Filter by date range
            if (fromDate.HasValue)
            {
                query = query.Where(w => w.StartDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(w => w.StartDate <= toDate.Value);
            }

                query = query.OrderByDescending(w => w.CreatedDate);

                var pagedResult = await _paginationService.PaginateAsync(query, page, 10);

            // Statistics for dashboard
            ViewBag.TotalWarranties = await _context.Warranties.CountAsync();
            ViewBag.ActiveWarranties = await _context.Warranties.CountAsync(w => w.Status == "Active");
            ViewBag.ExpiredWarranties = await _context.Warranties.CountAsync(w => w.Status == "Expired");
            ViewBag.ClaimedWarranties = await _context.Warranties.CountAsync(w => w.WarrantyClaims.Any());

            // Filter values for view
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentSearch = search;
            ViewBag.CurrentFromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.CurrentToDate = toDate?.ToString("yyyy-MM-dd");

                return View(pagedResult);
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"Lỗi: {ex.Message}";
                ViewBag.TestMessage = "Có lỗi xảy ra khi truy cập database.";
                return View(new PagedResult<Models.Warranty> { Items = new List<Models.Warranty>() });
            }
        }

        // GET: Admin/Warranty/Details/5
        // [RequirePermission("View Warranties")] // Tạm thời bỏ để test
        public async Task<IActionResult> Details(int? id)
        {
            try
            {
                if (id == null)
                {
                    ViewBag.ErrorMessage = "ID không hợp lệ";
                    return View();
                }

                ViewBag.TestMessage = $"Đang tìm bảo hành với ID: {id}";

                var warranty = await _context.Warranties
                    .Include(w => w.WarrantyClaims)
                    .FirstOrDefaultAsync(m => m.WarrantyId == id);

                if (warranty == null)
                {
                    ViewBag.ErrorMessage = $"Không tìm thấy bảo hành với ID: {id}";
                    return View();
                }

                return View(warranty);
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"Lỗi khi tải chi tiết bảo hành: {ex.Message}";
                ViewBag.TestMessage = $"Exception: {ex.GetType().Name}";
                return View();
            }
        }

        // GET: Admin/Warranty/Create
        // [RequirePermission("Create Warranty")] // Tạm thời bỏ để test
        public async Task<IActionResult> Create()
        {
            // Lấy danh sách OrderDetail chưa có bảo hành để admin có thể chọn
            var orderDetailsWithWarranty = await _context.Warranties
                .Select(w => w.OrderDetailId)
                .ToListAsync();

            var availableOrderDetails = await _context.OrderDetails
                .Include(od => od.Product)
                .Include(od => od.Order)
                .ThenInclude(o => o.Customer)
                .Where(od => !orderDetailsWithWarranty.Contains(od.OrderDetailId))
                .OrderByDescending(od => od.Order.OrderDate)
                .ToListAsync();

            ViewBag.OrderDetails = availableOrderDetails;
            ViewBag.Customers = await _context.Customers
                .OrderBy(c => c.Name)
                .ToListAsync();

            return View();
        }

        // POST: Admin/Warranty/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        // [RequirePermission("Create Warranty")] // Tạm thời bỏ để test
        public async Task<IActionResult> Create([Bind("OrderDetailId,CustomerId,StartDate,EndDate,WarrantyPeriodMonths,Status,Notes,WarrantyCode")] Models.Warranty warranty)
        {
            // Tạo mã bảo hành tự động ngay từ đầu (trước khi validate)
            warranty.WarrantyCode = await GenerateUniqueWarrantyCodeAsync();
            warranty.CreatedDate = DateTime.Now;

            // Xóa lỗi ModelState cho WarrantyCode vì chúng ta tự tạo nó
            ModelState.Remove("WarrantyCode");
            ModelState.Remove("CreatedDate");

            // Validate required fields manually
            if (warranty.OrderDetailId == 0)
            {
                ModelState.AddModelError("OrderDetailId", "Vui lòng chọn chi tiết đơn hàng.");
            }
            if (warranty.CustomerId == 0)
            {
                ModelState.AddModelError("CustomerId", "Vui lòng chọn khách hàng.");
            }
            if (warranty.StartDate == default(DateTime))
            {
                ModelState.AddModelError("StartDate", "Vui lòng chọn ngày bắt đầu.");
            }
            if (warranty.WarrantyPeriodMonths <= 0)
            {
                ModelState.AddModelError("WarrantyPeriodMonths", "Thời gian bảo hành phải lớn hơn 0.");
            }
            if (string.IsNullOrEmpty(warranty.Status))
            {
                ModelState.AddModelError("Status", "Vui lòng chọn trạng thái.");
            }

            // Xử lý EndDate - nếu không có thì tính tự động
            if (warranty.EndDate == default(DateTime) && warranty.WarrantyPeriodMonths > 0 && warranty.StartDate != default(DateTime))
            {
                warranty.EndDate = warranty.StartDate.AddMonths(warranty.WarrantyPeriodMonths);
            }
            else if (warranty.EndDate != default(DateTime) && warranty.WarrantyPeriodMonths == 0 && warranty.StartDate != default(DateTime))
            {
                // Tính WarrantyPeriodMonths từ StartDate và EndDate
                var months = (warranty.EndDate.Year - warranty.StartDate.Year) * 12 + 
                            (warranty.EndDate.Month - warranty.StartDate.Month);
                warranty.WarrantyPeriodMonths = months > 0 ? months : 1;
            }

            // Validate EndDate phải sau StartDate
            if (warranty.EndDate != default(DateTime) && warranty.StartDate != default(DateTime) && warranty.EndDate <= warranty.StartDate)
            {
                ModelState.AddModelError("EndDate", "Ngày kết thúc phải sau ngày bắt đầu.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Kiểm tra xem OrderDetail đã có bảo hành chưa
                    var existingWarranty = await _context.Warranties
                        .FirstOrDefaultAsync(w => w.OrderDetailId == warranty.OrderDetailId);

                    if (existingWarranty != null)
                    {
                        ModelState.AddModelError("OrderDetailId", "Chi tiết đơn hàng này đã có bảo hành rồi.");
                        await LoadCreateViewDataAsync();
                        return View(warranty);
                    }

                    _context.Warranties.Add(warranty);
                    await _context.SaveChangesAsync();
                    
                    TempData["SuccessMessage"] = "Tạo bảo hành thành công!";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException ex)
                {
                    ModelState.AddModelError("", $"Lỗi khi lưu vào database: {ex.InnerException?.Message ?? ex.Message}");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Lỗi khi tạo bảo hành: {ex.Message}");
                }
            }

            // Nếu có lỗi, load lại dữ liệu cho dropdown
            await LoadCreateViewDataAsync();

            return View(warranty);
        }

        private async Task LoadCreateViewDataAsync()
        {
            var orderDetailsWithWarranty = await _context.Warranties
                .Select(w => w.OrderDetailId)
                .ToListAsync();

            ViewBag.OrderDetails = await _context.OrderDetails
                .Include(od => od.Product)
                .Include(od => od.Order)
                .ThenInclude(o => o.Customer)
                .Where(od => !orderDetailsWithWarranty.Contains(od.OrderDetailId))
                .OrderByDescending(od => od.Order.OrderDate)
                .ToListAsync();
            ViewBag.Customers = await _context.Customers
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        // GET: Admin/Warranty/Delete/5
        // [RequirePermission("Delete Warranty")] // Tạm thời bỏ để test
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var warranty = await _context.Warranties
                .Include(w => w.WarrantyClaims)
                .FirstOrDefaultAsync(m => m.WarrantyId == id);

            if (warranty == null)
            {
                return NotFound();
            }

            return View(warranty);
        }

        // POST: Admin/Warranty/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        // [RequirePermission("Delete Warranty")] // Tạm thời bỏ để test
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var warranty = await _context.Warranties
                .Include(w => w.WarrantyClaims)
                .FirstOrDefaultAsync(m => m.WarrantyId == id);

            if (warranty == null)
            {
                return NotFound();
            }

            // Kiểm tra xem có yêu cầu bảo hành nào đang xử lý không
            if (warranty.WarrantyClaims.Any(c => c.Status == "Pending" || c.Status == "Processing"))
            {
                TempData["ErrorMessage"] = "Không thể xóa bảo hành này vì có yêu cầu bảo hành đang được xử lý.";
                return RedirectToAction(nameof(Delete), new { id });
            }

            // Xóa tất cả yêu cầu bảo hành liên quan trước
            _context.WarrantyClaims.RemoveRange(warranty.WarrantyClaims);
            
            // Sau đó xóa bảo hành
            _context.Warranties.Remove(warranty);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Xóa bảo hành thành công!";
            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Warranty/Edit/5
        // [RequirePermission("Edit Warranty")] // Tạm thời bỏ để test
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var warranty = await _context.Warranties.FindAsync(id);
            if (warranty == null)
            {
                return NotFound();
            }
            return View(warranty);
        }

        // POST: Admin/Warranty/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        // [RequirePermission("Edit Warranty")] // Tạm thời bỏ để test
        public async Task<IActionResult> Edit(int id, [Bind("WarrantyId,OrderDetailId,CustomerId,WarrantyCode,StartDate,EndDate,WarrantyPeriodMonths,Status,Notes,CreatedDate")] Models.Warranty warranty)
        {
            if (id != warranty.WarrantyId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    warranty.UpdatedDate = DateTime.Now;
                    _context.Update(warranty);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Cập nhật bảo hành thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!WarrantyExists(warranty.WarrantyId))
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
            return View(warranty);
        }

        // POST: Admin/Warranty/UpdateStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        // [RequirePermission("Edit Warranty")] // Tạm thời bỏ để test
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var warranty = await _context.Warranties.FindAsync(id);
            if (warranty == null)
            {
                return NotFound();
            }

            warranty.Status = status;
            warranty.UpdatedDate = DateTime.Now;
            
            await _context.SaveChangesAsync();
            
            TempData["SuccessMessage"] = $"Đã cập nhật trạng thái bảo hành thành '{status}'.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: Admin/Warranty/Claims
        // [RequirePermission("View Warranties")] // Tạm thời bỏ để test
        public async Task<IActionResult> Claims(string? status, string? search)
        {
            var query = _context.WarrantyClaims
                .Include(wc => wc.Warranty)
                .AsQueryable();

            // Filter by status
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(wc => wc.Status == status);
            }

            // Filter by claim code or issue description
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(wc => wc.ClaimCode.Contains(search) || 
                                         wc.IssueDescription.Contains(search));
            }

            var claims = await query
                .OrderByDescending(wc => wc.SubmittedDate)
                .ToListAsync();

            // Statistics
            ViewBag.TotalClaims = await _context.WarrantyClaims.CountAsync();
            ViewBag.PendingClaims = await _context.WarrantyClaims.CountAsync(wc => wc.Status == "Pending");
            ViewBag.ProcessingClaims = await _context.WarrantyClaims.CountAsync(wc => wc.Status == "Processing");
            ViewBag.CompletedClaims = await _context.WarrantyClaims.CountAsync(wc => wc.Status == "Completed");

            ViewBag.CurrentStatus = status;
            ViewBag.CurrentSearch = search;

            return View(claims);
        }

        // GET: Admin/Warranty/ClaimDetails/5
        // [RequirePermission("View Warranties")] // Tạm thời bỏ để test
        public async Task<IActionResult> ClaimDetails(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var claim = await _context.WarrantyClaims
                .Include(wc => wc.Warranty)
                .FirstOrDefaultAsync(m => m.WarrantyClaimId == id);

            if (claim == null)
            {
                return NotFound();
            }

            return View(claim);
        }

        // POST: Admin/Warranty/ProcessClaim/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        // [RequirePermission("Edit Warranty")] // Tạm thời bỏ để test
        public async Task<IActionResult> ProcessClaim(int id, string status, string? resolution, string? resolutionType, string? adminNotes)
        {
            var claim = await _context.WarrantyClaims.FindAsync(id);
            if (claim == null)
            {
                return NotFound();
            }

            // Cập nhật thông tin xử lý (Giữ nguyên logic cũ của nhóm)
            claim.Resolution = resolution;
            claim.ResolutionType = resolutionType;
            claim.AdminNotes = adminNotes;
            claim.ProcessedByAdmin = User.Identity?.Name;

            // ==========================================
            // ÁP DỤNG STATE PATTERN (Đã giải quyết xung đột Namespace)
            // ==========================================
            // LƯU Ý SƯ PHẠM: 
            // Chúng ta chỉ truyền chuỗi (claim.Status) vào Pattern thay vì truyền nguyên Object.
            // Điều này giúp State Pattern tuân thủ chặt chẽ Nguyên lý Đảo ngược Phụ thuộc (Dependency Inversion).
            var warrantyContext = new PerfumeStore.DesignPatterns.State.WarrantyContext(claim.Status ?? "Chờ xử lý");

            try
            {
                // Thay vì gán chuỗi cứng nguy hiểm: claim.Status = status;
                // Ta gọi các hàm chuyển đổi của State Pattern
                if (status == "Processing" || status == "Đang xử lý")
                {
                    warrantyContext.Approve(); // Chuyển sang Đang xử lý
                    if (claim.ProcessedDate == null) claim.ProcessedDate = DateTime.Now;
                }
                else if (status == "Completed" || status == "Hoàn tất")
                {
                    warrantyContext.Complete(); // Chuyển sang Hoàn tất
                    claim.CompletedDate = DateTime.Now;
                    if (claim.ProcessedDate == null) claim.ProcessedDate = DateTime.Now;
                }
                else if (status == "Rejected" || status == "Từ chối")
                {
                    warrantyContext.Reject(); // Chuyển sang Từ chối
                }

                // Lấy trạng thái đã được Pattern xử lý và gán ngược lại cho DB Model
                claim.Status = warrantyContext.GetStatusString();

                // Lưu xuống DB
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã cập nhật yêu cầu bảo hành thành '{claim.Status}'.";
            }
            catch (Exception ex)
            {
                // Bắt lỗi logic nếu Admin cố tình chuyển trạng thái sai quy tắc
                TempData["ErrorMessage"] = $"Lỗi logic trạng thái: {ex.Message}";
            }

            return RedirectToAction(nameof(ClaimDetails), new { id });
        }

        // AJAX: Get warranty statistics
        [HttpGet]
        // [RequirePermission("View Warranties")] // Tạm thời bỏ để test
        public async Task<IActionResult> GetWarrantyStats()
        {
            var stats = new
            {
                TotalWarranties = await _context.Warranties.CountAsync(),
                ActiveWarranties = await _context.Warranties.CountAsync(w => w.Status == "Active"),
                ExpiredWarranties = await _context.Warranties.CountAsync(w => w.Status == "Expired"),
                ClaimedWarranties = await _context.Warranties.CountAsync(w => w.WarrantyClaims.Any()),
                TotalClaims = await _context.WarrantyClaims.CountAsync(),
                PendingClaims = await _context.WarrantyClaims.CountAsync(wc => wc.Status == "Pending"),
                ProcessingClaims = await _context.WarrantyClaims.CountAsync(wc => wc.Status == "Processing"),
                CompletedClaims = await _context.WarrantyClaims.CountAsync(wc => wc.Status == "Completed")
            };

            return Json(stats);
        }

        private bool WarrantyExists(int id)
        {
            return _context.Warranties.Any(e => e.WarrantyId == id);
        }

        // Method để tạo bảo hành tự động khi đơn hàng được xác nhận
        public async Task<bool> CreateWarrantyForOrderAsync(int orderDetailId, int customerId, int warrantyPeriodMonths)
        {
            try
            {
                // Kiểm tra xem đã có bảo hành cho OrderDetail này chưa
                var existingWarranty = await _context.Warranties
                    .FirstOrDefaultAsync(w => w.OrderDetailId == orderDetailId);

                if (existingWarranty != null)
                {
                    return false; // Đã có bảo hành rồi
                }

                var warranty = new Models.Warranty
                {
                    OrderDetailId = orderDetailId,
                    CustomerId = customerId,
                    WarrantyCode = await GenerateUniqueWarrantyCodeAsync(),
                    StartDate = DateTime.Now,
                    EndDate = DateTime.Now.AddMonths(warrantyPeriodMonths),
                    WarrantyPeriodMonths = warrantyPeriodMonths,
                    Status = "Active",
                    Notes = "Bảo hành được tạo tự động khi xác nhận đơn hàng",
                    CreatedDate = DateTime.Now
                };

                _context.Warranties.Add(warranty);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Method để tạo bảo hành cho tất cả sản phẩm trong đơn hàng
        public async Task<int> CreateWarrantiesForOrderAsync(int orderId)
        {
            try
            {
                var orderDetails = await _context.OrderDetails
                    .Include(od => od.Product)
                    .Include(od => od.Order)
                    .Where(od => od.OrderId == orderId)
                    .ToListAsync();

                int createdCount = 0;

                foreach (var orderDetail in orderDetails)
                {
                    // Chỉ tạo bảo hành cho sản phẩm có thời gian bảo hành > 0
                    if (orderDetail.Product.WarrantyPeriodMonths > 0)
                    {
                        var success = await CreateWarrantyForOrderAsync(
                            orderDetail.OrderDetailId,
                            orderDetail.Order.CustomerId,
                            orderDetail.Product.WarrantyPeriodMonths
                        );

                        if (success)
                        {
                            createdCount++;
                        }
                    }
                }

                return createdCount;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        // GET: Admin/Warranty/Test - Action test đơn giản
        public IActionResult Test()
        {
            ViewBag.Message = "WarrantyController hoạt động bình thường!";
            ViewBag.DateTime = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
            return View("TestView");
        }

        private string GenerateWarrantyCode()
        {
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var random = new Random().Next(1000, 9999);
            return $"WR{timestamp}{random}";
        }

        private async Task<string> GenerateUniqueWarrantyCodeAsync()
        {
            string warrantyCode;
            bool isUnique = false;
            int maxAttempts = 10;
            int attempts = 0;

            do
            {
                // Tạo mã bảo hành với timestamp và random number
                var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                var random = new Random().Next(1000, 9999);
                warrantyCode = $"WR{timestamp}{random}";

                // Kiểm tra xem mã đã tồn tại chưa
                isUnique = !await _context.Warranties
                    .AnyAsync(w => w.WarrantyCode == warrantyCode);

                attempts++;
                
                // Nếu đã thử quá nhiều lần, thêm thêm random để đảm bảo unique
                if (!isUnique && attempts < maxAttempts)
                {
                    await Task.Delay(10); // Đợi một chút để timestamp thay đổi
                }
            } while (!isUnique && attempts < maxAttempts);

            // Nếu vẫn không unique sau nhiều lần thử, thêm GUID vào cuối
            if (!isUnique)
            {
                var guid = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                warrantyCode = $"WR{DateTime.Now:yyyyMMddHHmmss}{guid}";
            }

            return warrantyCode;
        }
    }
}
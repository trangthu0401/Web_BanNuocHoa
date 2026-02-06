using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PerfumeStore.Areas.Admin.Models;
using PerfumeStore.Areas.Admin.Services;
using PerfumeStore.Areas.Admin.Filters;

namespace PerfumeStore.Areas.Admin.Controllers
{
    [Area("Admin")]
    [AdminAuthorize]
    public class OrdersController : Controller
    {
        private readonly PerfumeStoreContext _db;
        private readonly DBQueryService.IDbQueryService _queryService;
        private readonly IWarrantyService _warrantyService;
        private readonly IPaginationService _paginationService;

        public OrdersController(PerfumeStoreContext db, DBQueryService.IDbQueryService queryService, IWarrantyService warrantyService, IPaginationService paginationService)
        {
            _db = db;
            _queryService = queryService;
            _warrantyService = warrantyService;
            _paginationService = paginationService;
        }

        [RequirePermission("View Orders")]
        public async Task<IActionResult> Index(string? searchName, string? status, DateTime? fromDate, DateTime? toDate, int page = 1)
        {
            var orders = await _queryService.GetOrdersWithIncludesAsync();

            // Apply filters
            if (!string.IsNullOrWhiteSpace(searchName))
            {
                orders = orders.Where(o => 
                    o.Customer.Name != null && o.Customer.Name.Contains(searchName) ||
                    o.Customer.Email.Contains(searchName) ||
                    o.Customer.Phone != null && o.Customer.Phone.Contains(searchName)
                ).ToList();
            }



            if (!string.IsNullOrWhiteSpace(status))
            {
                orders = orders.Where(o => o.Status == status).ToList();
            }

            if (fromDate.HasValue)
            {
                orders = orders.Where(o => o.OrderDate >= fromDate.Value).ToList();
            }

            if (toDate.HasValue)
            {
                orders = orders.Where(o => o.OrderDate <= toDate.Value.AddDays(1).AddMilliseconds(-1)).ToList();
            }

            // Apply pagination
            var pagedResult = _paginationService.Paginate(orders, page, 10);

            ViewBag.Statuses = await GetOrderStatuses();
            ViewBag.SearchName = searchName;
            ViewBag.Status = status;
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;

            return View(pagedResult);
        }

        [RequirePermission("View Orders")]
        public async Task<IActionResult> Details(int id)
        {
            var order = await _db.Orders
                .Include(o => o.Customer)
                .Include(o => o.Address)
                .Include(o => o.Coupon)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                        .ThenInclude(p => p.Brand)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                        .ThenInclude(p => p.ProductImages)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        [RequirePermission("Edit Order")]
        public async Task<IActionResult> Edit(int id)
        {
            var order = await _db.Orders
                .Include(o => o.Customer)
                .Include(o => o.Address)
                .Include(o => o.Coupon)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                return NotFound();
            }

            ViewBag.Statuses = await GetOrderStatuses();
            ViewBag.PaymentMethods = GetPaymentMethods();

            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("Edit Order")]
        public async Task<IActionResult> Edit(int id, Models.Order order)
        {
            if (id != order.OrderId)
            {
                return NotFound();
            }

            var existingOrder = await _db.Orders.FindAsync(id);
            if (existingOrder == null)
            {
                return NotFound();
            }

            // Lưu trạng thái cũ để so sánh
            var oldStatus = existingOrder.Status;
            var newStatus = order.Status;

            // Update status
            existingOrder.Status = newStatus;
            
            if (!string.IsNullOrWhiteSpace(order.Notes))
            {
                existingOrder.Notes = order.Notes;
            }

            try
            {
                await _db.SaveChangesAsync();

                // Xử lý bảo hành dựa trên thay đổi trạng thái
                if (oldStatus != newStatus)
                {
                    if (newStatus == "Đã giao hàng")
                    {
                        // Xóa bảo hành cũ trước (nếu có) để tránh duplicate
                        await _warrantyService.DeleteWarrantiesForOrderAsync(id);
                        
                        // Tạo bảo hành khi đơn hàng được set ở trạng thái "Đã giao hàng"
                        try
                        {
                            var warrantyCount = await _warrantyService.CreateWarrantiesForOrderAsync(id);
                            TempData["SuccessMessage"] = $"Cập nhật đơn hàng thành công! Đã tạo {warrantyCount} bảo hành cho đơn hàng.";
                        }
                        catch (Exception ex)
                        {
                            TempData["SuccessMessage"] = "Cập nhật đơn hàng thành công!";
                            TempData["WarningMessage"] = $"Có lỗi khi tạo bảo hành: {ex.Message}";
                        }
                    }
                    else if (oldStatus == "Đã giao hàng")
                    {
                        // Chỉ xóa bảo hành nếu đơn hàng đang ở trạng thái "Đã giao hàng" và được đổi sang trạng thái khác
                        try
                        {
                            var deletedCount = await _warrantyService.DeleteWarrantiesForOrderAsync(id);
                            if (deletedCount > 0)
                            {
                                TempData["SuccessMessage"] = $"Cập nhật đơn hàng thành công! Đã xóa {deletedCount} bảo hành của đơn hàng.";
                            }
                            else
                            {
                                TempData["SuccessMessage"] = "Cập nhật đơn hàng thành công!";
                            }
                        }
                        catch (Exception ex)
                        {
                            TempData["SuccessMessage"] = "Cập nhật đơn hàng thành công!";
                            TempData["WarningMessage"] = $"Có lỗi khi xóa bảo hành: {ex.Message}";
                        }
                    }
                }
                else
                {
                    TempData["SuccessMessage"] = "Cập nhật đơn hàng thành công!";
                }

                return RedirectToAction(nameof(Details), new { id });
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("", "Không thể cập nhật đơn hàng. Vui lòng thử lại.");
            }

            ViewBag.Statuses = await GetOrderStatuses();
            ViewBag.PaymentMethods = GetPaymentMethods();
            
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("Cancel Order")]
        public async Task<IActionResult> Cancel(int id, string? reason)
        {
            var order = await _db.Orders.FindAsync(id);
            if (order == null)
            {
                return Json(new { success = false, message = "Không tìm thấy đơn hàng" });
            }

            var oldStatus = order.Status;
            order.Status = "Đã hủy";
            if (!string.IsNullOrWhiteSpace(reason))
            {
                order.Notes = reason;
            }

            try
            {
                await _db.SaveChangesAsync();

                // Xóa bảo hành khi hủy đơn hàng (vì trạng thái không còn là "Đã giao hàng")
                if (oldStatus == "Đã giao hàng")
                {
                    try
                    {
                        var deletedCount = await _warrantyService.DeleteWarrantiesForOrderAsync(id);
                        return Json(new { success = true, message = $"Đã hủy đơn hàng thành công! Đã xóa {deletedCount} bảo hành của đơn hàng." });
                    }
                    catch (Exception ex)
                    {
                        return Json(new { success = true, message = "Đã hủy đơn hàng thành công!", warning = $"Có lỗi khi xóa bảo hành: {ex.Message}" });
                    }
                }

                return Json(new { success = true, message = "Đã hủy đơn hàng thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi hủy đơn hàng: " + ex.Message });
            }
        }

        private async Task<List<string>> GetOrderStatuses()
        {
            var statuses = await _db.Orders
                .Where(o => o.Status != null)
                .Select(o => o.Status!)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync();
            
            // Add default statuses if not present
            var defaultStatuses = new List<string> { "Chờ xác nhận", "Đã xác nhận", "Đang giao hàng", "Đã giao hàng", "Đã hủy" };
            foreach (var status in defaultStatuses)
            {
                if (!statuses.Contains(status))
                {
                    statuses.Add(status);
                }
            }
            
            return statuses.OrderBy(s => s).ToList();
        }

        private List<string> GetPaymentMethods()
        {
            return new List<string> { "COD", "Chuyển khoản", "Ví điện tử", "Thẻ tín dụng" };
        }
    }
}

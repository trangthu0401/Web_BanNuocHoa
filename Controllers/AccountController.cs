using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PerfumeStore.Models;
using PerfumeStore.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Linq;

namespace PerfumeStore.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private readonly PerfumeStoreContext _db;

        public AccountController(PerfumeStoreContext db)
        {
            _db = db;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var model = new CustomerAccountVM();
            if (!string.IsNullOrWhiteSpace(customerIdClaim) && int.TryParse(customerIdClaim, out var customerId))
            {
                var customer = _db.Customers
                    .Include(c => c.Membership)
                    .FirstOrDefault(c => c.CustomerId == customerId);
                if (customer != null)
                {
                    model.CustomerId = customer.CustomerId;
                    model.Name = customer.Name;
                    model.Email = customer.Email;
                    model.Phone = customer.Phone;
                    model.BirthYear = customer.BirthYear;
                    model.CreatedDate = customer.CreatedDate;
                    model.MembershipName = customer.Membership?.Name;
                    // them tich diem kh
                    model.RewardPoints = customer.SpinNumber ?? 0;
                }
            }
            ViewData["Title"] = "Tài khoản";
            if (TempData.ContainsKey("AlertMessage"))
            {
                ViewBag.AlertMessage = TempData["AlertMessage"]?.ToString();
                ViewBag.AlertType = TempData["AlertType"]?.ToString() ?? "success";
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(CustomerAccountVM model)
        {
            ViewData["Title"] = "Tài khoản";

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(customerIdClaim) || !int.TryParse(customerIdClaim, out var customerId))
            {
                TempData["AlertMessage"] = "Không xác định được tài khoản.";
                TempData["AlertType"] = "danger";
                return RedirectToAction(nameof(Index));
            }

            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId);
            if (customer == null)
            {
                TempData["AlertMessage"] = "Tài khoản không tồn tại.";
                TempData["AlertType"] = "danger";
                return RedirectToAction(nameof(Index));
            }

            // Email đã xác thực: không cho phép thay đổi - giữ nguyên email từ database
            // Chỉ cập nhật Name, Phone, BirthYear
            
            // Xử lý Name: trim và kiểm tra độ dài
            if (!string.IsNullOrWhiteSpace(model.Name))
            {
                var trimmedName = model.Name.Trim();
                if (trimmedName.Length > 100)
                {
                    ModelState.AddModelError(nameof(model.Name), "Họ tên tối đa 100 ký tự");
                    model.Email = customer.Email; // Đảm bảo Email luôn từ database
                    return View(model);
                }
                customer.Name = trimmedName;
            }
            else
            {
                customer.Name = null;
            }

            // Xử lý Phone: chỉ chứa số, tối đa 13 chữ số
            if (!string.IsNullOrWhiteSpace(model.Phone))
            {
                var trimmedPhone = model.Phone.Trim();
                // Loại bỏ các ký tự không phải số
                trimmedPhone = new string(trimmedPhone.Where(char.IsDigit).ToArray());
                
                if (trimmedPhone.Length > 13)
                {
                    ModelState.AddModelError(nameof(model.Phone), "Số điện thoại tối đa 13 chữ số");
                    model.Email = customer.Email; // Đảm bảo Email luôn từ database
                    return View(model);
                }
                
                customer.Phone = trimmedPhone.Length > 0 ? trimmedPhone : null;
            }
            else
            {
                customer.Phone = null;
            }

            // Xử lý Email
            customer.Email = model.Email.Trim();

            // Xử lý BirthYear
            if (model.BirthYear.HasValue)
            {
                var currentYear = DateTime.Now.Year;
                if (model.BirthYear.Value < 1900 || model.BirthYear.Value > currentYear)
                {
                    ModelState.AddModelError(nameof(model.BirthYear), $"Năm sinh chỉ trong khoảng 1900 đến {currentYear}");
                    model.Email = customer.Email; // Đảm bảo Email luôn từ database
                    return View(model);
                }
                customer.BirthYear = model.BirthYear;
            }
            else
            {
                customer.BirthYear = null;
            }

            // Xử lý MembershipId (giữ nguyên hạng hiện tại)
            // Không thay đổi MembershipId từ trang này

            _db.Customers.Update(customer);
            await _db.SaveChangesAsync();

            TempData["AlertMessage"] = "Cập nhật thông tin thành công!";
            TempData["AlertType"] = "success";
            return RedirectToAction(nameof(Index));
        }
        public async Task<IActionResult> MyOrders()
        {
            ViewData["Title"] = "Đơn hàng của tôi";

            var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(customerIdClaim) || !int.TryParse(customerIdClaim, out var customerId))
            {
                TempData["AlertMessage"] = "Không xác định được tài khoản.";
                TempData["AlertType"] = "danger";
                return RedirectToAction(nameof(Index));
            }

            // Lấy đơn hàng từ database kèm OrderDetails và Product
            var orders = await _db.Orders
                .Where(o => o.CustomerId == customerId)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                        .ThenInclude(p => p.ProductImages)
                .Include(o => o.Address)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            // Log để debug
            Console.WriteLine($"MyOrders: CustomerId từ claims: {customerId}");
            Console.WriteLine($"MyOrders: Số lượng đơn hàng tìm thấy: {orders.Count}");
            if (orders.Any())
            {
                Console.WriteLine($"MyOrders: Đơn hàng đầu tiên - OrderId: {orders.First().OrderId}, Date: {orders.First().OrderDate}, Total: {orders.First().TotalAmount}");
            }

            // Kiểm tra xem có đơn hàng nào trong database không (để debug)
            var allOrdersCount = await _db.Orders.CountAsync();
            var allOrdersForCustomer = await _db.Orders
                .Where(o => o.CustomerId == customerId)
                .CountAsync();
            Console.WriteLine($"MyOrders: Tổng số đơn hàng trong DB: {allOrdersCount}, Đơn hàng của customer {customerId}: {allOrdersForCustomer}");

            if (TempData.ContainsKey("AlertMessage"))
            {
                ViewBag.AlertMessage = TempData["AlertMessage"]?.ToString();
                ViewBag.AlertType = TempData["AlertType"]?.ToString() ?? "success";
            }

            return View(orders);
        }

        [HttpGet]
        public async Task<IActionResult> Vouchers()
        {
            ViewData["Title"] = "Voucher của tôi";

            var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(customerIdClaim) || !int.TryParse(customerIdClaim, out var customerId))
            {
                TempData["AlertMessage"] = "Không xác định được tài khoản.";
                TempData["AlertType"] = "danger";
                return RedirectToAction(nameof(Index));
            }

            var now = DateTime.Now;

            var personalCoupons = await _db.Coupons
                .Where(c => c.CustomerId == customerId)
                .OrderBy(c => c.IsUsed ?? false)
                .ThenBy(c => c.ExpiryDate)
                .Select(c => new CustomerVoucherVM.CouponItem
                {
                    CouponId = c.CouponId,
                    Code = c.Code ?? string.Empty,
                    DiscountAmount = c.DiscountAmount,
                    CreatedDate = c.CreatedDate,
                    ExpiryDate = c.ExpiryDate,
                    IsUsed = c.IsUsed ?? false,
                    UsedDate = c.UsedDate,
                    IsExpired = c.ExpiryDate.HasValue && c.ExpiryDate.Value.Date < now.Date,
                    IsAssignedToCustomer = true
                })
                .ToListAsync();

            var model = new CustomerVoucherVM
            {
                PersonalCoupons = personalCoupons
            };

            if (TempData.ContainsKey("AlertMessage"))
            {
                ViewBag.AlertMessage = TempData["AlertMessage"]?.ToString();
                ViewBag.AlertType = TempData["AlertType"]?.ToString() ?? "success";
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> OrderDetail(int id)
        {
            ViewData["Title"] = "Chi tiết đơn hàng";

            var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(customerIdClaim) || !int.TryParse(customerIdClaim, out var customerId))
            {
                TempData["AlertMessage"] = "Không xác định được tài khoản.";
                TempData["AlertType"] = "danger";
                return RedirectToAction(nameof(MyOrders));
            }

            var order = await _db.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .Include(o => o.Address)
                .Include(o => o.Coupon)
                .FirstOrDefaultAsync(o => o.OrderId == id && o.CustomerId == customerId);

            if (order == null)
            {
                TempData["AlertMessage"] = "Không tìm thấy đơn hàng hoặc bạn không có quyền xem đơn hàng này.";
                TempData["AlertType"] = "danger";
                return RedirectToAction(nameof(MyOrders));
            }

            if (TempData.ContainsKey("AlertMessage"))
            {
                ViewBag.AlertMessage = TempData["AlertMessage"]?.ToString();
                ViewBag.AlertType = TempData["AlertType"]?.ToString() ?? "success";
            }

            return View(order);
        }

        // Action hủy đơn hàng
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int id)
        {
            var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(customerIdClaim) || !int.TryParse(customerIdClaim, out var customerId))
            {
                TempData["AlertMessage"] = "Không xác định được tài khoản.";
                TempData["AlertType"] = "danger";
                return RedirectToAction(nameof(MyOrders));
            }

            var order = await _db.Orders
                .FirstOrDefaultAsync(o => o.OrderId == id && o.CustomerId == customerId);

            if (order == null)
            {
                TempData["AlertMessage"] = "Không tìm thấy đơn hàng hoặc bạn không có quyền thực hiện thao tác này.";
                TempData["AlertType"] = "danger";
                return RedirectToAction(nameof(MyOrders));
            }

            // Kiểm tra trạng thái - chỉ cho phép hủy khi: Đã đặt hàng, Chờ xác nhận, Chờ lấy hàng
            var status = order.Status ?? "";
            var canCancelStatuses = new[] { "Đã đặt hàng", "Chờ xác nhận", "Chờ lấy hàng" };
            
            if (!canCancelStatuses.Any(s => status.Contains(s)))
            {
                TempData["AlertMessage"] = $"Không thể hủy đơn hàng khi trạng thái là: {status}. Chỉ có thể hủy đơn hàng khi đơn hàng ở trạng thái: Đã đặt hàng, Chờ xác nhận, hoặc Chờ lấy hàng.";
                TempData["AlertType"] = "warning";
                return RedirectToAction(nameof(OrderDetail), new { id = id });
            }

            // Cập nhật trạng thái thành "Đã hủy"
            order.Status = "Đã hủy";
            _db.Orders.Update(order);
            await _db.SaveChangesAsync();

            TempData["AlertMessage"] = $"Đơn hàng #{order.OrderId} đã được hủy thành công.";
            TempData["AlertType"] = "success";
            return RedirectToAction(nameof(OrderDetail), new { id = id });
        }
    }
}



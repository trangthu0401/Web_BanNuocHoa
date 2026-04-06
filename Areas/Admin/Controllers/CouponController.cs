using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PerfumeStore.Areas.Admin.Models;
using PerfumeStore.Areas.Admin.Services;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using PerfumeStore.DesignPatterns.Prototype;

namespace PerfumeStore.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class CouponController : Controller
    {
        private readonly PerfumeStoreContext _context;
        private readonly IPaginationService _paginationService;

        public CouponController(PerfumeStoreContext context, IPaginationService paginationService)
        {
            _context = context;
            _paginationService = paginationService;
        }

        // GET: Admin/Coupon
        public async Task<IActionResult> Index(int page = 1)
        {
            var couponsQuery = _context.Coupons
                .Include(c => c.Customer)
                .OrderByDescending(c => c.CreatedDate)
                .AsQueryable();
            
            var pagedResult = await _paginationService.PaginateAsync(couponsQuery, page, 10);
            return View("Index", pagedResult); // View riêng Index.cshtml
        }

        // GET: Admin/Coupon/Create
        public async Task<IActionResult> Create()
        {
            await PopulateCustomerOptionsAsync();
            var model = new Coupon
            {
                Code = await GenerateUniqueCodeAsync()
            };
            return View("Create", model); // View riêng Create.cshtml
        }

        // POST: Admin/Coupon/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Coupon coupon)
        {
            if (string.IsNullOrWhiteSpace(coupon.Code))
            {
                coupon.Code = await GenerateUniqueCodeAsync();
                ModelState.Clear();
                TryValidateModel(coupon);
            }

            if (!ModelState.IsValid)
            {
                await PopulateCustomerOptionsAsync(coupon.CustomerId);
                return View("Create", coupon);
            }

            // Chuẩn hoá dữ liệu
            coupon.Code = (coupon.Code ?? string.Empty).Trim().ToUpperInvariant();
            if (coupon.Code.Length != 30 || !coupon.Code.All(char.IsLetterOrDigit))
            {
                ModelState.AddModelError(nameof(coupon.Code), "Code phải gồm 30 ký tự chữ hoa và số.");
            }
            if (!ModelState.IsValid)
            {
                await PopulateCustomerOptionsAsync(coupon.CustomerId);
                return View("Create", coupon);
            }

            // Validate: không trùng Code
            var isDuplicate = await _context.Coupons
                .AnyAsync(c => c.Code == coupon.Code);
            if (isDuplicate)
            {
                ModelState.AddModelError(nameof(coupon.Code), "Code đã tồn tại.");
                await PopulateCustomerOptionsAsync(coupon.CustomerId);
                return View("Create", coupon);
            }

            SetDefaultValues(coupon);

            _context.Coupons.Add(coupon);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Tạo coupon thành công!";
            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Coupon/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var coupon = await _context.Coupons.FindAsync(id);
            if (coupon == null) return NotFound();

            await PopulateCustomerOptionsAsync(coupon.CustomerId);
            return View("Edit", coupon); // View riêng Edit.cshtml
        }

        // POST: Admin/Coupon/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Coupon coupon)
        {
            if (id != coupon.CouponId) return NotFound();

            if (!ModelState.IsValid)
            {
                await PopulateCustomerOptionsAsync(coupon.CustomerId);
                return View("Edit", coupon);
            }

            coupon.Code = (coupon.Code ?? string.Empty).Trim().ToUpperInvariant();
            if (coupon.Code.Length != 30 || !coupon.Code.All(char.IsLetterOrDigit))
            {
                ModelState.AddModelError(nameof(coupon.Code), "Code phải gồm 30 ký tự chữ hoa và số.");
            }

            // Validate: không trùng Code với coupon khác
            var isDuplicate = await _context.Coupons
                .AnyAsync(c => c.CouponId != coupon.CouponId && c.Code == coupon.Code);
            if (isDuplicate)
            {
                ModelState.AddModelError(nameof(coupon.Code), "Code đã tồn tại.");
                await PopulateCustomerOptionsAsync(coupon.CustomerId);
                return View("Edit", coupon);
            }

            try
            {
                _context.Update(coupon);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Cập nhật coupon thành công!";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CouponExists(coupon.CouponId)) return NotFound();
                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Coupon/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var coupon = await _context.Coupons.FindAsync(id);
            if (coupon == null) return NotFound();

            return View("Delete", coupon); // View riêng Delete.cshtml
        }

        // POST: Admin/Coupon/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var coupon = await _context.Coupons
                .Include(c => c.Orders)
                .FirstOrDefaultAsync(c => c.CouponId == id);
            if (coupon == null) return RedirectToAction(nameof(Index));

            // Chặn xoá nếu đã dùng hoặc đã gắn với đơn hàng
            if ((coupon.IsUsed ?? false) || (coupon.Orders?.Any() ?? false))
            {
                TempData["SuccessMessage"] = "Không thể xóa coupon đã sử dụng hoặc đã gắn với đơn hàng.";
                return RedirectToAction(nameof(Index));
            }

            _context.Coupons.Remove(coupon);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Xóa coupon thành công!";

            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Coupon/Clone/5
        public async Task<IActionResult> Clone(int? id)
        {
            if (id == null) return NotFound();

            var originalCoupon = await _context.Coupons.FindAsync(id);
            if (originalCoupon == null) return NotFound();

            // 1. ÁP DỤNG PROTOTYPE PATTERN (Đưa dữ liệu vào Trạm trung chuyển)
            var prototypeObj = new PerfumeStore.DesignPatterns.Prototype.DiscountProgram
            {
                ProgramId = originalCoupon.CouponId,
                DiscountName = originalCoupon.Code,
                DiscountRate = originalCoupon.DiscountAmount ?? 0,
                StartDate = originalCoupon.CreatedDate ?? DateTime.Now,
                EndDate = originalCoupon.ExpiryDate ?? DateTime.Now.AddDays(30)
            };

            // 2. KÍCH HOẠT NHÂN BẢN TỪ BÁO CÁO
            // Tạo một mã ngẫu nhiên 30 ký tự chuẩn hệ thống thay vì dùng chữ "_COPY"
            string newValidCode = await GenerateUniqueCodeAsync();

            var clonedPrototype = prototypeObj.DuplicateForNewSeason(
                newValidCode,
                DateTime.Now,
                DateTime.Now.AddDays(30)
            );

            // 3. TRẢ DỮ LIỆU VỀ COUPON ĐỂ HIỂN THỊ
            var newCoupon = new Coupon
            {
                Code = clonedPrototype.DiscountName, // Đã là mã 30 ký tự hợp lệ
                DiscountAmount = clonedPrototype.DiscountRate, // Copy số tiền giảm
                CreatedDate = clonedPrototype.StartDate,
                ExpiryDate = clonedPrototype.EndDate,
                IsUsed = false, // Trạng thái chưa sử dụng
                CouponId = 0,
                CustomerId = originalCoupon.CustomerId // Tùy chọn: Copy luôn khách hàng được gán
            };

            // 4. GỌI HÀM NÀY ĐỂ TRÁNH LỖI VIEW (Render dropdown chọn khách hàng)
            await PopulateCustomerOptionsAsync(newCoupon.CustomerId);

            // Hiện thông báo nhỏ gọn để Admin biết họ đang ở chế độ nhân bản
            TempData["SuccessMessage"] = "Đã nhân bản dữ liệu! Hệ thống đã tạo mã Code mới, bạn có thể kiểm tra và lưu lại.";

            return View("Create", newCoupon);
        }

        // --- Private helper methods ---
        [HttpGet]
        public async Task<IActionResult> GenerateCode()
        {
            var code = await GenerateUniqueCodeAsync();
            return Json(new { code });
        }

        private bool CouponExists(int id) => _context.Coupons.Any(c => c.CouponId == id);

        private void SetDefaultValues(Coupon coupon)
        {
            if (coupon.CreatedDate == null)
                coupon.CreatedDate = DateTime.Now;
            if (coupon.IsUsed == null)
                coupon.IsUsed = false;
        }

        private async Task PopulateCustomerOptionsAsync(int? selectedCustomerId = null)
        {
            var customers = await _context.Customers
                .OrderBy(c => c.Name)
                .Select(c => new SelectListItem
                {
                    Value = c.CustomerId.ToString(),
                    Text = string.IsNullOrWhiteSpace(c.Name)
                        ? $"{c.Email ?? "Khách hàng"} ({c.Phone ?? "Chưa có SĐT"})"
                        : $"{c.Name} - {c.Phone ?? c.Email ?? "Chưa có thông tin"}",
                    Selected = selectedCustomerId.HasValue && c.CustomerId == selectedCustomerId
                })
                .ToListAsync();

            customers.Insert(0, new SelectListItem
            {
                Value = string.Empty,
                Text = "— Không gán khách hàng —",
                Selected = !selectedCustomerId.HasValue
            });

            ViewBag.CustomerOptions = customers;
        }

        private async Task<string> GenerateUniqueCodeAsync(int length = 30)
        {
            string code;
            do
            {
                code = GenerateRandomCode(length);
            } while (await _context.Coupons.AnyAsync(c => c.Code == code));

            return code;
        }

        private static string GenerateRandomCode(int length)
        {
            const string charset = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var buffer = new byte[length];
            RandomNumberGenerator.Fill(buffer);
            var chars = new char[length];
            for (int i = 0; i < length; i++)
            {
                chars[i] = charset[buffer[i] % charset.Length];
            }

            return new string(chars);
        }
    }
}

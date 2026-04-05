using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PerfumeStore.DesignPatterns.State;
using PerfumeStore.Models;
using System.Linq;
using System.Security.Claims;

namespace PerfumeStore.Controllers
{
    [Authorize]
    public class WarrantyController : Controller
    {
        private readonly PerfumeStoreContext _db;

        public WarrantyController(PerfumeStoreContext db)
        {
            _db = db;
        }

        // GET: Trang yêu cầu bảo hành
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Yêu cầu bảo hành";

            var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(customerIdClaim) || !int.TryParse(customerIdClaim, out var customerId))
            {
                TempData["AlertMessage"] = "Không xác định được tài khoản.";
                TempData["AlertType"] = "danger";
                return RedirectToAction("Index", "Account");
            }

            // Lấy danh sách bảo hành của khách hàng
            var warranties = await _db.Warranties
                .Include(w => w.WarrantyClaims)
                .Where(w => w.CustomerId == customerId && w.Status == "Active")
                .OrderByDescending(w => w.StartDate)
                .ToListAsync();

            // ==========================================
            // ÁP DỤNG STATE PATTERN: 
            // Lấy tên trạng thái từ Class chuẩn thay vì gõ chuỗi cứng "Chờ xử lý", "Đang xử lý"
            // ==========================================
            string pendingState = new PerfumeStore.DesignPatterns.State.PendingState().StateName;
            string processingState = new PerfumeStore.DesignPatterns.State.ProcessingState().StateName;

            // Lấy thông tin OrderDetail để hiển thị tên sản phẩm
            var warrantyList = new List<object>();
            foreach (var warranty in warranties)
            {
                var orderDetail = await _db.OrderDetails
                    .Include(od => od.Product)
                    .FirstOrDefaultAsync(od => od.OrderDetailId == warranty.OrderDetailId);

                warrantyList.Add(new
                {
                    WarrantyId = warranty.WarrantyId,
                    WarrantyCode = warranty.WarrantyCode,
                    ProductName = orderDetail?.Product?.ProductName ?? "Sản phẩm không xác định",
                    StartDate = warranty.StartDate,
                    EndDate = warranty.EndDate,
                    Status = warranty.Status,
                    // Dùng biến State chuẩn để kiểm tra
                    HasActiveClaim = warranty.WarrantyClaims.Any(c => c.Status == pendingState || c.Status == processingState)
                });
            }

            ViewBag.Warranties = warrantyList;

            if (TempData.ContainsKey("AlertMessage"))
            {
                ViewBag.AlertMessage = TempData["AlertMessage"]?.ToString();
                ViewBag.AlertType = TempData["AlertType"]?.ToString() ?? "success";
            }

            return View();
        }

        // POST: Tạo yêu cầu bảo hành
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int warrantyId, string issueType, string issueDescription)
        {
            var customerIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(customerIdClaim) || !int.TryParse(customerIdClaim, out var customerId))
            {
                TempData["AlertMessage"] = "Không xác định được tài khoản.";
                TempData["AlertType"] = "danger";
                return RedirectToAction(nameof(Index));
            }

            // Kiểm tra warranty có thuộc về customer này không
            var warranty = await _db.Warranties
                .FirstOrDefaultAsync(w => w.WarrantyId == warrantyId && w.CustomerId == customerId);

            if (warranty == null)
            {
                TempData["AlertMessage"] = "Không tìm thấy bảo hành hoặc bạn không có quyền thực hiện thao tác này.";
                TempData["AlertType"] = "danger";
                return RedirectToAction(nameof(Index));
            }

            // Kiểm tra bảo hành còn hiệu lực không
            if (warranty.EndDate < DateTime.Now)
            {
                TempData["AlertMessage"] = "Bảo hành đã hết hạn. Vui lòng liên hệ hỗ trợ khách hàng.";
                TempData["AlertType"] = "warning";
                return RedirectToAction(nameof(Index));
            }

            // ==========================================
            // ÁP DỤNG STATE PATTERN: Lấy chuẩn trạng thái
            // ==========================================
            string pendingState = new PerfumeStore.DesignPatterns.State.PendingState().StateName;
            string processingState = new PerfumeStore.DesignPatterns.State.ProcessingState().StateName;

            // Kiểm tra đã có yêu cầu đang chờ xử lý chưa
            var existingClaim = await _db.WarrantyClaims
                .FirstOrDefaultAsync(c => c.WarrantyId == warrantyId &&
                    (c.Status == pendingState || c.Status == processingState));

            if (existingClaim != null)
            {
                TempData["AlertMessage"] = $"Bạn đã có yêu cầu bảo hành đang được xử lý (Mã: {existingClaim.ClaimCode}). Vui lòng chờ phản hồi từ admin.";
                TempData["AlertType"] = "warning";
                return RedirectToAction(nameof(Index));
            }

            // Validation
            if (string.IsNullOrWhiteSpace(issueType))
            {
                TempData["AlertMessage"] = "Vui lòng chọn loại vấn đề.";
                TempData["AlertType"] = "danger";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(issueDescription) || issueDescription.Trim().Length < 10)
            {
                TempData["AlertMessage"] = "Vui lòng mô tả vấn đề chi tiết (ít nhất 10 ký tự).";
                TempData["AlertType"] = "danger";
                return RedirectToAction(nameof(Index));
            }

            // Tạo mã yêu cầu bảo hành
            var claimCode = $"WC{DateTime.Now:yyyyMMdd}{warrantyId:D4}{new Random().Next(1000, 9999)}";

            // Tạo WarrantyClaim
            var warrantyClaim = new WarrantyClaim
            {
                WarrantyId = warrantyId,
                ClaimCode = claimCode,
                IssueType = issueType,
                IssueDescription = issueDescription.Trim(),
                SubmittedDate = DateTime.Now
            };

            // ==========================================
            // ÁP DỤNG STATE PATTERN: Khởi tạo trạng thái gốc
            // ==========================================
            // Khởi tạo Context với chuỗi mặc định
            var warrantyContext = new PerfumeStore.DesignPatterns.State.WarrantyContext(new PendingState());

            // Lấy chuỗi trạng thái chuẩn từ Pattern để gán vào Entity
            warrantyClaim.Status = warrantyContext.GetStatusString();

            _db.WarrantyClaims.Add(warrantyClaim);
            await _db.SaveChangesAsync();

            TempData["AlertMessage"] = $"Yêu cầu bảo hành đã được gửi thành công! Mã yêu cầu: {claimCode}";
            TempData["AlertType"] = "success";
            return RedirectToAction(nameof(MyClaims));
        }

        // GET: Danh sách yêu cầu bảo hành của tôi
        [HttpGet]
        public async Task<IActionResult> MyClaims()
        {
            ViewData["Title"] = "Yêu cầu bảo hành của tôi";

            var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(customerIdClaim) || !int.TryParse(customerIdClaim, out var customerId))
            {
                TempData["AlertMessage"] = "Không xác định được tài khoản.";
                TempData["AlertType"] = "danger";
                return RedirectToAction("Index", "Account");
            }

            // Lấy tất cả yêu cầu bảo hành của khách hàng
            var warranties = await _db.Warranties
                .Where(w => w.CustomerId == customerId)
                .Select(w => w.WarrantyId)
                .ToListAsync();

            var claims = await _db.WarrantyClaims
                .Include(c => c.Warranty)
                .Where(c => warranties.Contains(c.WarrantyId))
                .OrderByDescending(c => c.SubmittedDate)
                .ToListAsync();

            // Lấy thông tin sản phẩm cho mỗi claim
            var claimsList = new List<object>();
            foreach (var claim in claims)
            {
                var warranty = await _db.Warranties
                    .Include(w => w.WarrantyClaims)
                    .FirstOrDefaultAsync(w => w.WarrantyId == claim.WarrantyId);

                if (warranty != null)
                {
                    var orderDetail = await _db.OrderDetails
                        .Include(od => od.Product)
                        .FirstOrDefaultAsync(od => od.OrderDetailId == warranty.OrderDetailId);

                    claimsList.Add(new
                    {
                        WarrantyClaimId = claim.WarrantyClaimId,
                        ClaimCode = claim.ClaimCode,
                        ProductName = orderDetail?.Product?.ProductName ?? "Sản phẩm không xác định",
                        IssueType = claim.IssueType,
                        IssueDescription = claim.IssueDescription,
                        Status = claim.Status,
                        SubmittedDate = claim.SubmittedDate,
                        ProcessedDate = claim.ProcessedDate,
                        CompletedDate = claim.CompletedDate,
                        Resolution = claim.Resolution,
                        ResolutionType = claim.ResolutionType,
                        AdminNotes = claim.AdminNotes
                    });
                }
            }

            ViewBag.Claims = claimsList;

            if (TempData.ContainsKey("AlertMessage"))
            {
                ViewBag.AlertMessage = TempData["AlertMessage"]?.ToString();
                ViewBag.AlertType = TempData["AlertType"]?.ToString() ?? "success";
            }

            return View();
        }
    }
}


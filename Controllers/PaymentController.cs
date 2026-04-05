using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Net.payOS;
using Net.payOS.Types;
using PerfumeStore.Models;
using PerfumeStore.Areas.Admin.Services;
using System.Text.Json;

namespace PerfumeStore.Controllers
{
    
    ///     Xử lý luồng thanh toán chuyển khoản (PayOS):
    ///     - Tạo payment link dựa trên đơn đã lưu từ Checkout.
    ///     - Nhận trang thành công/hủy từ PayOS.
    ///     - Đồng bộ trạng thái đơn hàng & coupon sau khi thanh toán.
    
    public class PaymentController : Controller
    {
        private readonly PayOS _payOS;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly PerfumeStoreContext _context;
        private readonly ILogger<PaymentController> _logger;
        private readonly IWarrantyService _warrantyService;

        public PaymentController(
            PayOS payOS, 
            IHttpContextAccessor httpContextAccessor,
            PerfumeStoreContext context,
            ILogger<PaymentController> logger,
            IWarrantyService warrantyService)
        {
            _payOS = payOS;
            _httpContextAccessor = httpContextAccessor;
            _context = context;
            _logger = logger;
            _warrantyService = warrantyService;
        }

        [HttpGet("/cancel-payment")]

        ///     Callback khi người dùng hoặc PayOS báo hủy giao dịch.
        ///     Cập nhật trạng thái đơn sang “Đã hủy” và hướng dẫn khách thử lại.

        public async Task<IActionResult> CancelPayment([FromQuery] string status, [FromQuery] string code)
        {
            try
            {
                // ==========================================
                // ÁP DỤNG ADAPTER PATTERN: Dịch trạng thái từ PayOS sang chuẩn nội bộ
                // ==========================================
                var internalStatus = PerfumeStore.DesignPatterns.Adapter.PayOSAdapter.ConvertExternalStatusToInternal(status, code);

                // Lấy thông tin đơn hàng từ session
                var orderIdStr = HttpContext.Session.GetString("PENDING_ORDER_ID");

                if (!string.IsNullOrEmpty(orderIdStr) && int.TryParse(orderIdStr, out int orderId))
                {
                    // Chỉ tiến hành hủy đơn khi Adapter xác nhận trạng thái trả về đúng là Cancelled hoặc Unknown
                    if (internalStatus == PerfumeStore.DesignPatterns.Adapter.InternalOrderStatus.Cancelled ||
                        internalStatus == PerfumeStore.DesignPatterns.Adapter.InternalOrderStatus.Unknown)
                    {
                        // Cập nhật trạng thái đơn hàng thành "Đã hủy"
                        var order = await _context.Orders.FindAsync(orderId);
                        if (order != null)
                        {
                            order.Status = "Đã hủy";
                            order.PaymentMethod = "Chuyển khoản ngân hàng (Đã hủy)";
                            await _context.SaveChangesAsync();

                            ViewBag.OrderId = orderId;
                        }

                        // Xóa session
                        HttpContext.Session.Remove("PENDING_ORDER_ID");
                        HttpContext.Session.Remove("PENDING_ORDER_AMOUNT");
                    }
                }

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CancelPayment");
                return View();
            }
        }

        [HttpGet("/payment-success")]

        ///     Callback thành công từ PayOS.
        ///     Xác thực orderId trong session, cập nhật trạng thái “Đã thanh toán” và hiển thị lại thông tin đơn hàng.

        public async Task<IActionResult> PaymentSuccess([FromQuery] string status, [FromQuery] string code)
        {
            try
            {
                // ==========================================
                // ÁP DỤNG ADAPTER PATTERN
                // ==========================================
                var internalStatus = PerfumeStore.DesignPatterns.Adapter.PayOSAdapter.ConvertExternalStatusToInternal(status, code);

                // BẢO VỆ LUỒNG: Nếu URL trả về báo lỗi, dù khách ráng truy cập trang success cũng sẽ bị chặn lại
                if (internalStatus != PerfumeStore.DesignPatterns.Adapter.InternalOrderStatus.PaidSuccess)
                {
                    _logger.LogWarning($"Payment attempt failed. PayOS Status: {status}, Code: {code}");
                    TempData["Error"] = "Thanh toán chưa hoàn tất hoặc đã bị hủy. Vui lòng kiểm tra lại.";
                    return RedirectToAction("Index", "Cart");
                }
                // ==========================================

                // BẢO MẬT: Luôn ưu tiên sử dụng orderId từ session (PENDING_ORDER_ID)
                // Đây là cách duy nhất để đảm bảo người dùng chỉ có thể xem đơn hàng của chính họ
                var orderIdStr = HttpContext.Session.GetString("PENDING_ORDER_ID");

                if (string.IsNullOrEmpty(orderIdStr) || !int.TryParse(orderIdStr, out int orderId))
                {
                    _logger.LogWarning($"PaymentSuccess: No valid order ID found in session. IP: {Request.HttpContext.Connection.RemoteIpAddress}");
                    TempData["Error"] = "Không tìm thấy thông tin đơn hàng. Vui lòng đặt hàng lại.";
                    return RedirectToAction("Index", "Cart");
                }

                // Kiểm tra xem PayOS có gửi orderCode trong query string không
                var payOSOrderCode = Request.Query["orderCode"].ToString();
                if (!string.IsNullOrEmpty(payOSOrderCode) && int.TryParse(payOSOrderCode, out int payOSOrderId))
                {
                    // BẢO MẬT: Validate orderCode từ PayOS phải khớp với orderId trong session
                    if (payOSOrderId != orderId)
                    {
                        _logger.LogWarning($"PaymentSuccess: SECURITY ALERT - OrderCode mismatch! Session OrderId: {orderId}, PayOS OrderCode: {payOSOrderId}, IP: {Request.HttpContext.Connection.RemoteIpAddress}");
                        TempData["Error"] = "Thông tin đơn hàng không hợp lệ. Vui lòng liên hệ hỗ trợ.";
                        return RedirectToAction("Index", "Cart");
                    }
                    _logger.LogInformation($"PaymentSuccess: Received orderCode from PayOS: {payOSOrderId} (validated against session)");
                }

                _logger.LogInformation($"PaymentSuccess: Processing order ID: {orderId}");

                // Lấy đơn hàng từ database với tất cả thông tin cần thiết
                var order = await _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.Address)
                    .Include(o => o.Coupon)
                    .Include(o => o.OrderDetails)
                        .ThenInclude(od => od.Product)
                            .ThenInclude(p => p.ProductImages)
                    .FirstOrDefaultAsync(o => o.OrderId == orderId);

                if (order == null)
                {
                    _logger.LogWarning($"PaymentSuccess: Order {orderId} not found in database");
                    TempData["Error"] = "Không tìm thấy đơn hàng trong hệ thống";
                    return RedirectToAction("Index", "Cart");
                }

                // Kiểm tra xem đơn hàng đã được thanh toán chưa (tránh cập nhật 2 lần)
                if (order.Status == "Đã thanh toán")
                {
                    _logger.LogInformation($"PaymentSuccess: Order {orderId} already marked as paid");
                }
                else
                {
                    // Cập nhật trạng thái đơn hàng thành "Đã thanh toán"
                    _logger.LogInformation($"PaymentSuccess: Updating order {orderId} status to 'Đã thanh toán'");
                    order.Status = "Đã thanh toán";
                    order.PaymentMethod = "Chuyển khoản ngân hàng";

                    // Đánh dấu coupon đã sử dụng nếu có
                    if (order.CouponId.HasValue)
                    {
                        var coupon = await _context.Coupons.FindAsync(order.CouponId.Value);
                        if (coupon != null && (coupon.IsUsed == null || coupon.IsUsed == false))
                        {
                            coupon.IsUsed = true;
                            coupon.UsedDate = DateTime.Now;
                            _logger.LogInformation($"PaymentSuccess: Marked coupon {coupon.Code} as used for order {orderId}");
                        }
                    }

                    // Lưu đơn hàng vào database - ĐẢM BẢO LƯU THÀNH CÔNG TRƯỚC KHI TIẾP TỤC
                    try
                    {
                        await _context.SaveChangesAsync();
                        _logger.LogInformation($"PaymentSuccess: Successfully saved order {orderId} to database");
                    }
                    catch (Exception saveEx)
                    {
                        _logger.LogError(saveEx, $"PaymentSuccess: Failed to save order {orderId} to database");
                        TempData["Error"] = "Có lỗi xảy ra khi lưu đơn hàng vào database. Vui lòng liên hệ hỗ trợ.";
                        return RedirectToAction("Index", "Cart");
                    }
                }

                // Đảm bảo tất cả OrderDetails đã được lưu
                if (order.OrderDetails == null || !order.OrderDetails.Any())
                {
                    _logger.LogWarning($"PaymentSuccess: Order {orderId} has no order details");
                    TempData["Error"] = "Đơn hàng không có chi tiết sản phẩm";
                    return RedirectToAction("Index", "Cart");
                }

                // Kiểm tra lại từ database một lần nữa để đảm bảo dữ liệu đã được lưu
                var verifiedOrder = await _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.Address)
                    .Include(o => o.Coupon)
                    .Include(o => o.OrderDetails)
                        .ThenInclude(od => od.Product)
                            .ThenInclude(p => p.ProductImages)
                    .FirstOrDefaultAsync(o => o.OrderId == orderId);

                if (verifiedOrder == null || verifiedOrder.Status != "Đã thanh toán")
                {
                    _logger.LogError($"PaymentSuccess: Verification failed for order {orderId}");
                    TempData["Error"] = "Xác thực đơn hàng không thành công";
                    return RedirectToAction("Index", "Cart");
                }

                _logger.LogInformation($"PaymentSuccess: Order {orderId} verified successfully in database");

                // Bảo hành sẽ được tạo tự động khi admin set đơn hàng ở trạng thái "Đã giao hàng"
                // Không tạo bảo hành ở đây nữa

                // Xóa giỏ hàng chỉ sau khi đã lưu đơn hàng thành công
                HttpContext.Session.Remove("CART_SESSION");
                HttpContext.Session.Remove("AppliedVoucher");

                // Tính toán lại các khoản phí từ OrderDetails và Coupon
                var subtotal = verifiedOrder.OrderDetails.Sum(od => od.TotalPrice);

                // Tính VAT từ database
                var vatFee = await _context.Fees.FirstOrDefaultAsync(f => f.Name == "VAT");
                decimal vat = 0m;
                if (vatFee != null)
                {
                    vat = subtotal * Math.Min(vatFee.Value, 100) / 100;
                }

                // Tính Shipping fee từ database
                var shippingFee = await _context.Fees.FirstOrDefaultAsync(f => f.Name == "Shipping");
                decimal shipping = 0m;
                if (shippingFee != null)
                {
                    var threshold = shippingFee.Threshold ?? 5000000m;
                    shipping = subtotal >= threshold ? 0m : shippingFee.Value;
                }

                // Tính discount từ coupon
                decimal discount = 0m;
                if (verifiedOrder.Coupon != null && verifiedOrder.Coupon.DiscountAmount.HasValue)
                {
                    discount = verifiedOrder.Coupon.DiscountAmount.Value;
                }

                // Tạo view model từ order đã được xác thực
                var orderViewModel = new
                {
                    OrderId = verifiedOrder.OrderId.ToString(),
                    OrderDate = verifiedOrder.OrderDate,
                    TotalAmount = verifiedOrder.TotalAmount,
                    Subtotal = subtotal,
                    Discount = discount,
                    ShippingFee = shipping,
                    VAT = vat,
                    Status = verifiedOrder.Status,
                    PaymentMethod = verifiedOrder.PaymentMethod,
                    Coupon = verifiedOrder.Coupon != null ? new
                    {
                        Code = verifiedOrder.Coupon.Code,
                        DiscountAmount = verifiedOrder.Coupon.DiscountAmount ?? 0
                    } : null,
                    Customer = new
                    {
                        Name = verifiedOrder.Customer.Name,
                        Email = verifiedOrder.Customer.Email,
                        Phone = verifiedOrder.Customer.Phone
                    },
                    Address = new
                    {
                        RecipientName = verifiedOrder.Address.RecipientName,
                        Phone = verifiedOrder.Address.Phone,
                        Province = verifiedOrder.Address.Province,
                        District = verifiedOrder.Address.District,
                        Ward = verifiedOrder.Address.Ward,
                        AddressLine = verifiedOrder.Address.AddressLine
                    },
                    OrderDetails = verifiedOrder.OrderDetails.Select(od => new
                    {
                        ProductName = od.Product.ProductName,
                        Quantity = od.Quantity,
                        UnitPrice = od.UnitPrice,
                        TotalPrice = od.TotalPrice,
                        ImageUrl = od.Product.ProductImages?.FirstOrDefault()?.ImageData != null
                            ? $"data:{od.Product.ProductImages.First().ImageMimeType};base64,{Convert.ToBase64String(od.Product.ProductImages.First().ImageData)}"
                            : "/images/ProductSummary/chanel-bleu-de-chanel-edp-100-ml.webp"
                    }).ToList()
                };

                // Xóa session chỉ sau khi đã xác thực và lưu thành công
                HttpContext.Session.Remove("PENDING_ORDER_ID");
                HttpContext.Session.Remove("PENDING_ORDER_AMOUNT");

                _logger.LogInformation($"PaymentSuccess: Returning success page for order {orderId}");

                return View(orderViewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PaymentSuccess: Unexpected error occurred");
                TempData["Error"] = "Có lỗi xảy ra khi xử lý thanh toán. Vui lòng liên hệ hỗ trợ.";
                return RedirectToAction("Index", "Cart");
            }
        }

        [HttpGet("/create-payment-progress")]
        
        ///     Lấy thông tin đơn chờ thanh toán (được lưu trong session từ CartController),
        ///     tạo payment link PayOS và redirect khách đến trang thanh toán của cổng.
        
        public async Task<IActionResult> CreatePaymentProgress()
        {
            try
            {
                // Lấy thông tin đơn hàng từ session
                var orderIdStr = HttpContext.Session.GetString("PENDING_ORDER_ID");
                var amountStr = HttpContext.Session.GetString("PENDING_ORDER_AMOUNT");
                
                if (string.IsNullOrEmpty(orderIdStr) || string.IsNullOrEmpty(amountStr))
                {
                    TempData["Error"] = "Không tìm thấy thông tin đơn hàng";
                    return RedirectToAction("Index", "Cart");
                }
                
                if (!int.TryParse(orderIdStr, out int orderId) || !decimal.TryParse(amountStr, out decimal amount))
                {
                    TempData["Error"] = "Thông tin đơn hàng không hợp lệ";
                    return RedirectToAction("Index", "Cart");
                }
                
                // Lấy chi tiết đơn hàng từ database
                var order = await _context.Orders
                    .Include(o => o.OrderDetails)
                        .ThenInclude(od => od.Product)
                    .FirstOrDefaultAsync(o => o.OrderId == orderId);
                
                if (order == null)
                {
                    TempData["Error"] = "Không tìm thấy đơn hàng";
                    return RedirectToAction("Index", "Cart");
                }

                // Tạo danh sách items cho PayOS
                List<ItemData> items = new List<ItemData>
                {
                    // Gộp toàn bộ thành 1 item duy nhất đại diện cho tổng hóa đơn
                    // Điều này giúp vượt qua cơ chế Validate tổng tiền cực kỳ khắt khe của PayOS
                    new ItemData($"Thanh toan don hang {orderId}", 1, (int)amount)
                };

                // Get the current request's base URL
                var request = _httpContextAccessor.HttpContext.Request;
                var baseUrl = $"{request.Scheme}://{request.Host}";

                // Tạo payment data
                PaymentData paymentData = new PaymentData(
                    orderId,
                    (int)amount,
                    $"Thanh toan don hang #{orderId}",
                    items,
                    $"{baseUrl}/cancel-payment",
                    $"{baseUrl}/payment-success"
                );

                CreatePaymentResult createPayment = await _payOS.createPaymentLink(paymentData);

                return Redirect(createPayment.checkoutUrl);
            }
            catch (System.Exception exception)
            {
                _logger.LogError(exception, "Error creating payment link");
                TempData["Error"] = "Có lỗi xảy ra khi tạo link thanh toán: " + exception.Message;
                return RedirectToAction("Index", "Cart");
            }
        }
    }
}

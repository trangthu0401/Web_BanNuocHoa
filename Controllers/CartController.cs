using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PerfumeStore.Models;
using PerfumeStore.Models.ViewModels;
using PerfumeStore.Services;
using PerfumeStore.Areas.Admin.Services;
using System.Text.Json;
using PerfumeStore.DesignPatterns.Observer;

namespace PerfumeStore.Controllers
{
    
    ///     Quản lý toàn bộ dòng chảy giỏ hàng & đặt hàng của khách hàng:
    ///     - Cập nhật session giỏ hàng (thêm/xóa/sửa số lượng, yêu thích…)
    ///     - Tính toán phí (voucher, VAT, vận chuyển) phục vụ trang Checkout
    ///     - Đẩy đơn hàng vào database và chuyển hướng sang luồng thanh toán (COD / chuyển khoản PayOS)
    
    public class CartController : Controller
    {
        ILogger<CartController> _logger;
        private readonly PerfumeStore.DesignPatterns.Facade.ICheckoutFacade _checkoutFacade;
        private readonly IWebHostEnvironment _env;
        private readonly IOrderService _orderService;
        private readonly PerfumeStoreContext _context;
        private readonly IWarrantyService _warrantyService;
        private readonly PerfumeStore.DesignPatterns.Observer.OrderSubject _orderSubject;

        public CartController(ILogger<CartController> logger, IWebHostEnvironment env, IOrderService orderService, PerfumeStoreContext context, IWarrantyService warrantyService, PerfumeStore.DesignPatterns.Facade.ICheckoutFacade checkoutFacade, PerfumeStore.DesignPatterns.Observer.OrderSubject orderSubject)
        {
            _logger = logger;
            _env = env;
            _orderService = orderService;
            _context = context;
            _warrantyService = warrantyService;
            _checkoutFacade = checkoutFacade;
            _orderSubject = orderSubject;
        }

        
        ///     Hiển thị danh sách sản phẩm trong giỏ cùng các nút điều hướng tới thanh toán.
        
        public IActionResult Index()
        {
            var cart = GetCartFromSession();
            return View(cart);
        }

        private const string CartSessionKey = "CART_SESSION";

        
        ///     Lấy giỏ hàng hiện tại từ session, trả về danh sách rỗng nếu session chưa tồn tại hoặc deserialize lỗi.
        
        private List<CartItem> GetCartFromSession()
        {
            var json = HttpContext.Session.GetString(CartSessionKey);
            if (string.IsNullOrEmpty(json)) return new List<CartItem>();
            try { return JsonSerializer.Deserialize<List<CartItem>>(json) ?? new List<CartItem>(); }
            catch { return new List<CartItem>(); }
        }

        
        ///     Lưu lại trạng thái giỏ hàng mới vào session để đồng bộ giữa các request.
        
        private void SaveCartToSession(List<CartItem> items)
        {
            HttpContext.Session.SetString(CartSessionKey, JsonSerializer.Serialize(items));
        }

        [HttpGet]
        
        ///     API AJAX để lấy tổng số lượng item đang có trong giỏ (hiển thị icon cart).
        
        public IActionResult GetCartCount()
        {
            var cart = GetCartFromSession();
            var count = cart.Sum(item => item.Quantity);
            return Json(new
            {
                success = true, 
                cartCount = count 
            });
        }

        
        ///     Đọc voucher đã lưu trong session (khi khách quay vòng quay, nhập mã hoặc click từ URL).
        
        private VoucherModel? GetAppliedVoucher()
        {
            var voucherJson = HttpContext.Session.GetString("AppliedVoucher");
            Console.WriteLine($"Session voucher JSON: {voucherJson}");

            if (string.IsNullOrEmpty(voucherJson))
            {
                Console.WriteLine("No voucher found in session");
                return null;
            }

            try
            {
                var voucher = JsonSerializer.Deserialize<VoucherModel>(voucherJson);
                Console.WriteLine($"Voucher found in session: {voucher?.Name} ({voucher?.Code})");
                return voucher;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deserializing voucher: {ex.Message}");
                return null;
            }
        }

        
        ///     Tính số tiền giảm dựa trên loại voucher (percent/amount/freeship) & giá trị áp dụng.
        
        private decimal CalculateDiscount(decimal subtotal, VoucherModel? voucher)
        {
            if (voucher == null)
            {
                Console.WriteLine("No voucher provided for discount calculation");
                return 0m;
            }

            Console.WriteLine($"Calculating discount for voucher: {voucher.Name} ({voucher.Code})");
            Console.WriteLine($"Voucher type: {voucher.Type}, Value: {voucher.Value}");
            Console.WriteLine($"Subtotal: {subtotal}");

            // Dùng AccumulatedValue nếu có để hỗ trợ cộng dồn
            var effectiveValue = voucher.AccumulatedValue > 0 ? voucher.AccumulatedValue : voucher.Value;
            var discount = voucher.Type switch
            {
                "percent" => subtotal * Math.Min(effectiveValue, 100) / 100, // tối đa 100%
                "amount" => Math.Min(effectiveValue, subtotal),
                "freeship" => 0m,
                _ => 0m
            };

            Console.WriteLine($"Calculated discount: {discount}");
            return discount;
        }

        
        ///     Lấy phần trăm VAT từ bảng Fee trong database rồi nhân với tạm tính.
        
        private decimal CalculateVAT(decimal subtotal)
        {
            // Lấy VAT từ database
            var vatFee = _context.Fees.FirstOrDefault(f => f.Name == "VAT");
            if (vatFee != null)
            {
                // VAT là phần trăm (0-100)
                return subtotal * Math.Min(vatFee.Value, 100) / 100;
            }

            // Fallback: không có VAT
            return 0m;
        }

        
        ///     Tính phí vận chuyển dựa trên fee cấu hình + ngưỡng miễn phí + voucher freeship (nếu có).
        
        private decimal CalculateShippingFee(decimal subtotal, VoucherModel? voucher)
        {
            // Nếu có voucher miễn phí ship và đơn hàng >= 200,000đ
            if (voucher?.Type == "freeship" && subtotal >= 200000)
                return 0m;

            // Lấy shipping fee từ database
            var shippingFee = _context.Fees.FirstOrDefault(f => f.Name == "Shipping");
            if (shippingFee != null)
            {
                // Sử dụng Threshold từ database, mặc định là 5,000,000 VNĐ nếu không có
                var threshold = shippingFee.Threshold ?? 5000000m;
                // Chỉ áp dụng khi đơn hàng < Threshold
                return subtotal >= threshold ? 0m : shippingFee.Value;
            }

            // Fallback: giá trị mặc định
            return subtotal >= 5000000 ? 0m : 30000m;
        }

        
        ///     Cung cấp danh sách voucher có thể áp dụng: ưu tiên lấy coupon thật trong DB, fallback sang data mẫu.
        
        private List<VoucherModel> GetAvailableVouchers()
        {
            // Lấy coupon từ DB để cho phép áp dụng qua URL (?voucherCode=...)
            var now = DateTime.Now;
            var coupons = _context.Coupons
                .Where(c =>
                    (c.IsUsed == null || c.IsUsed == false) &&
                    (c.ExpiryDate == null || c.ExpiryDate >= now) &&
                    !string.IsNullOrEmpty(c.Code) &&
                    c.DiscountAmount.HasValue && c.DiscountAmount.Value > 0)
                .OrderByDescending(c => c.CreatedDate)
                .Take(20)
                .ToList();

            if (coupons.Any())
            {
                return coupons.Select(c => new VoucherModel
                {
                    Id = c.CouponId,
                    Code = c.Code!,
                    Name = $"Giảm {c.DiscountAmount.Value:N0}đ",
                    Value = c.DiscountAmount.Value,
                    Type = "amount",
                    Color = "#4facfe",
                    Description = "Mã giảm giá từ admin",
                    ExpiryDate = c.ExpiryDate
                }).ToList();
            }

            // Fallback các mã mặc định nếu DB không có coupon
            return new List<VoucherModel>
            {
                new VoucherModel { Id = 3, Code = "FREESHIP", Name = "Miễn phí vận chuyển", Value = 0, Type = "freeship", Color = "#6f42c1", Description = "Miễn phí ship cho đơn hàng từ 200k" },
                new VoucherModel { Id = 4, Code = "CASH50K", Name = "Giảm 50.000đ", Value = 50000, Type = "amount", Color = "#fd7e14", Description = "Giảm trực tiếp 50.000đ" },
                new VoucherModel { Id = 5, Code = "CASH100K", Name = "Giảm 100.000đ", Value = 100000, Type = "amount", Color = "#dc3545", Description = "Giảm trực tiếp 100.000đ" }
            };
        }


        
        ///     Sinh mô tả ngắn cho sản phẩm dựa trên tên (dùng cho các sản phẩm test khi chưa có dữ liệu thật).
        
        private static string GenerateDescriptionFromName(string name)
        {
            // Ưu tiên mô tả tùy biến theo từng sản phẩm cụ thể
            var custom = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["product1"] = "Gỗ biển tươi mát mở đầu, đọng lại ấm áp của gỗ tuyết tùng.",
                ["product2"] = "Cam bergamot – chanh vàng bùng nổ năng lượng, khô xuống trà xanh dịu.",
                ["product3"] = "Hoa oải hương kết hợp tiêu hồng, độ sâu từ hoắc hương hiện đại.",
                ["product4"] = "Hoa hồng Thổ Nhĩ Kỳ hoà cùng vani – ngọt, quyến rũ nhưng thanh lịch.",
                ["product5"] = "Thảo mộc – bạc hà mát lạnh, base xạ hương sạch sẽ lâu bền.",
                ["product6"] = "Biển xanh – muối biển mằn mặn, nhài trắng tinh khôi, rất dễ dùng hàng ngày."
            };

            var key = name.Replace(" ", string.Empty).ToLowerInvariant();
            if (custom.TryGetValue(key, out var desc)) return desc;

            // Ngược lại dùng heuristic theo từ khóa
            var lower = name.ToLowerInvariant();
            if (lower.Contains("wood") || lower.Contains("oud")) return "Hương gỗ ấm áp, quyến rũ";
            if (lower.Contains("sea") || lower.Contains("ocean")) return "Tươi mát mùi biển, sảng khoái";
            if (lower.Contains("rose") || lower.Contains("floral")) return "Hương hoa nhẹ nhàng, thanh lịch";
            if (lower.Contains("citrus") || lower.Contains("lemon") || lower.Contains("bergamot")) return "Cam chanh tươi sáng, năng động";
            return "Hương thơm tinh tế, phù hợp dùng hằng ngày";
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        
        ///     Thêm sản phẩm vào giỏ. Hỗ trợ AJAX (trả JSON) và điều hướng thông thường.
        ///     Kiểm tra Stock trước khi thêm và giới hạn số lượng tối đa 10 để tránh spam đơn hàng.
        
        public async Task<IActionResult> AddToCart(string imageUrl, string name, decimal price, int? productId = null, bool hasGiftWrap = false, bool hasEngraveName = false, string engraveContent = "")
        {
            var cart = GetCartFromSession();

            // Áp dụng Decorator Pattern để tính toán lại giá và mô tả
            PerfumeStore.DesignPatterns.Decorator.IProduct productDecorated = new PerfumeStore.DesignPatterns.Decorator.BasePerfume(name, price);
            if (hasGiftWrap) productDecorated = new PerfumeStore.DesignPatterns.Decorator.GiftWrapDecorator(productDecorated);
            if (hasEngraveName) productDecorated = new PerfumeStore.DesignPatterns.Decorator.EngraveNameDecorator(productDecorated);
            
            var finalName = productDecorated.GetDescription();
            var finalPrice = productDecorated.GetPrice();

            if (hasEngraveName && !string.IsNullOrWhiteSpace(engraveContent))
            {
                finalName = finalName.Replace("(+ Khắc tên theo yêu cầu)", $"(+ Khắc: '{engraveContent}')");
            }

            // Tạo ImageUrl độc nhất dựa trên cờ
            string uniqueImageUrl = imageUrl;
            if (hasGiftWrap) uniqueImageUrl += (uniqueImageUrl.Contains("?") ? "&" : "?") + "giftwrap=true";
            if (hasEngraveName) uniqueImageUrl += (uniqueImageUrl.Contains("?") ? "&" : "?") + "engrave=true";

            // Nếu có ProductId, kiểm tra Stock từ database
            if (productId.HasValue && productId.Value > 0)
            {
                var product = await _context.Products.FindAsync(productId.Value);
                if (product == null)
                {
                    var errorMsg = "Không tìm thấy sản phẩm trong hệ thống.";
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = false, message = errorMsg, cartCount = cart.Sum(item => item.Quantity) });
                    }
                    TempData["Error"] = errorMsg;
                    return RedirectToAction(nameof(Index));
                }

                // Tìm sản phẩm trong giỏ hàng theo ProductId và tùy chọn
                var found = cart.FirstOrDefault(i => i.ProductId == productId.Value && i.HasGiftWrap == hasGiftWrap && i.HasEngraveName == hasEngraveName);
                var currentQuantityInCart = found?.Quantity ?? 0;
                var requestedQuantity = currentQuantityInCart + 1; // Số lượng sau khi thêm

                // Kiểm tra Stock
                if (requestedQuantity > product.Stock)
                {
                    var errorMsg = $"Sản phẩm chỉ còn {product.Stock} sản phẩm trong kho. Bạn đã có {currentQuantityInCart} sản phẩm trong giỏ hàng.";
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = false, message = errorMsg, cartCount = cart.Sum(item => item.Quantity) });
                    }
                    TempData["Error"] = errorMsg;
                    return RedirectToAction(nameof(Index));
                }

                // Kiểm tra số lượng tối đa là 10
                if (requestedQuantity > 10)
                {
                    var errorMsg = "Số lượng tối đa là 10 sản phẩm.";
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = false, message = errorMsg, cartCount = cart.Sum(item => item.Quantity) });
                    }
                    TempData["Error"] = errorMsg;
                    return RedirectToAction(nameof(Index));
                }

                // Thêm hoặc cập nhật sản phẩm trong giỏ
                if (found == null)
                {
                    cart.Add(new CartItem 
                    { 
                        ProductId = productId.Value,
                        ImageUrl = uniqueImageUrl, 
                        ProductName = finalName, 
                        Quantity = 1, 
                        UnitPrice = finalPrice,
                        HasGiftWrap = hasGiftWrap,
                        HasEngraveName = hasEngraveName
                    });
                }
                else
                {
                    found.Quantity += 1;
                }
            }
            else
            {
                // Fallback: Tìm theo ImageUrl nếu không có ProductId (cho tương thích ngược)
                var found = cart.FirstOrDefault(i => i.ImageUrl.Equals(uniqueImageUrl, StringComparison.OrdinalIgnoreCase));
                if (found == null)
                {
                    cart.Add(new CartItem { ImageUrl = uniqueImageUrl, ProductName = finalName, Quantity = 1, UnitPrice = finalPrice, HasGiftWrap = hasGiftWrap, HasEngraveName = hasEngraveName });
                }
                else
                {
                    // Kiểm tra số lượng tối đa là 10
                    if (found.Quantity >= 10)
                    {
                        var errorMsg = "Số lượng tối đa là 10 sản phẩm.";
                        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        {
                            return Json(new { success = false, message = errorMsg, cartCount = cart.Sum(item => item.Quantity) });
                        }
                        TempData["Error"] = errorMsg;
                        return RedirectToAction(nameof(Index));
                    }
                    found.Quantity += 1;
                }
            }

            SaveCartToSession(cart);

            // Calculate total quantity across all items
            var totalQuantity = cart.Sum(item => item.Quantity);

            // Return JSON response for AJAX requests
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new
                {
                    success = true,
                    message = "Đã thêm sản phẩm vào giỏ hàng!",
                    cartCount = totalQuantity  // Return total quantity
                });
            }

            return RedirectToAction(nameof(Checkout));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        
        ///     Xóa sản phẩm khỏi giỏ bằng submit form truyền thống.
        
        public IActionResult RemoveFromCart(string imageUrl)
        {
            var cart = GetCartFromSession();
            cart.RemoveAll(i => i.ImageUrl.Equals(imageUrl, StringComparison.OrdinalIgnoreCase));
            SaveCartToSession(cart);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        
        ///     Đánh dấu sản phẩm yêu thích ngay trong giỏ – dữ liệu lưu kèm CartItem trong session.
        
        public IActionResult ToggleFavorite(string imageUrl)
        {
            var cart = GetCartFromSession();
            var item = cart.FirstOrDefault(i => i.ImageUrl.Equals(imageUrl, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                item.IsFavorite = !item.IsFavorite;
                SaveCartToSession(cart);
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        
        ///     Endpoint AJAX xóa sản phẩm, trả về JSON để UI tự cập nhật mà không reload.
        
        public IActionResult RemoveFromCartAjax(string imageUrl)
        {
            var cart = GetCartFromSession();
            var removed = cart.RemoveAll(i => i.ImageUrl.Equals(imageUrl, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                SaveCartToSession(cart);
            }
            return Json(new { ok = true, remaining = cart.Count });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        
        ///     Cập nhật số lượng từng item (AJAX), đồng thời đảm bảo giới hạn 1-10 và kiểm tra Stock.
        
        public async Task<IActionResult> UpdateCartQuantity(string imageUrl, int quantity)
        {
            var cart = GetCartFromSession();
            var item = cart.FirstOrDefault(i => i.ImageUrl.Equals(imageUrl, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                // Giới hạn số lượng từ 1 đến 10
                var requestedQuantity = Math.Max(1, Math.Min(10, quantity));

                // Nếu có ProductId, kiểm tra Stock từ database
                if (item.ProductId > 0)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product != null)
                    {
                        // Kiểm tra Stock
                        if (requestedQuantity > product.Stock)
                        {
                            // Điều chỉnh về số lượng tối đa có trong kho
                            requestedQuantity = product.Stock;
                            if (requestedQuantity < 1)
                            {
                                // Nếu hết hàng, xóa khỏi giỏ
                                cart.Remove(item);
                                SaveCartToSession(cart);
                                return Json(new
                                {
                                    ok = false,
                                    message = "Sản phẩm đã hết hàng và đã được xóa khỏi giỏ hàng.",
                                    removed = true
                                });
                            }
                        }
                    }
                }

                item.Quantity = requestedQuantity;
                SaveCartToSession(cart);
                return Json(new
                {
                    ok = true,
                    quantity = item.Quantity,
                    maxReached = item.Quantity >= 10,
                    stockLimited = item.ProductId > 0 && item.Quantity < quantity,
                    message = item.Quantity >= 10 ? "Số lượng tối đa là 10 sản phẩm." : 
                              (item.ProductId > 0 && item.Quantity < quantity ? $"Số lượng đã được điều chỉnh về {item.Quantity} do giới hạn tồn kho." : null)
                });
            }
            return Json(new { ok = false, message = "Không tìm thấy sản phẩm trong giỏ hàng" });
        }

        // FIX: Thêm method để test thêm sản phẩm vào cart
        [HttpGet]
        
        ///     Endpoint hỗ trợ QA: clear giỏ và nạp bộ sản phẩm mẫu để thử luồng checkout nhanh.
        
        public IActionResult AddTestProducts()
        {
            var cart = GetCartFromSession();

            // Clear existing cart
            cart.Clear();

            // Add test products
            var testProducts = new[]
            {
                new CartItem { ImageUrl = "/images/Checkout/product1.jpg", ProductName = "Chanel Bleu de Chanel EDP 100ml", Description = "Gỗ biển tươi mát mở đầu, đọng lại ấm áp của gỗ tuyết tùng.", Quantity = 2, UnitPrice = 197837 },
                new CartItem { ImageUrl = "/images/Checkout/product2.jpg", ProductName = "Dior Sauvage EDP 100ml", Description = "Cam bergamot – chanh vàng bùng nổ năng lượng, khô xuống trà xanh dịu.", Quantity = 1, UnitPrice = 174697 },
                new CartItem { ImageUrl = "/images/Checkout/product3.jpg", ProductName = "Tom Ford Black Orchid EDP 50ml", Description = "Hoa oải hương kết hợp tiêu hồng, độ sâu từ hoắc hương hiện đại.", Quantity = 1, UnitPrice = 162800 },
                new CartItem { ImageUrl = "/images/Checkout/product4.jpg", ProductName = "Yves Saint Laurent Libre EDP 90ml", Description = "Hoa hồng Thổ Nhĩ Kỳ hoà cùng vani – ngọt, quyến rũ nhưng thanh lịch.", Quantity = 1, UnitPrice = 124325 },
                new CartItem { ImageUrl = "/images/Checkout/product5.jpg", ProductName = "Hermes Terre d'Hermes EDT 100ml", Description = "Thảo mộc – bạc hà mát lạnh, base xạ hương sạch sẽ lâu bền.", Quantity = 1, UnitPrice = 112921 },
                new CartItem { ImageUrl = "/images/Checkout/product6.jpg", ProductName = "Acqua di Gio Profumo 75ml", Description = "Biển xanh – muối biển mằn mặn, nhài trắng tinh khôi, rất dễ dùng hàng ngày.", Quantity = 1, UnitPrice = 142894 }
            };

            cart.AddRange(testProducts);
            SaveCartToSession(cart);

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        
        ///     Fake luồng thanh toán đơn giản dùng cho demo (không ghi đơn hàng vào DB).
        
        public IActionResult ProcessPayment()
        {
            var cart = GetCartFromSession();

            if (cart.Count == 0)
            {
                TempData["Error"] = "Giỏ hàng của bạn đang trống";
                return RedirectToAction(nameof(Index));
            }

            // Lưu thông tin đơn hàng vào session để hiển thị trong trang thành công
            var orderInfo = new
            {
                OrderId = Guid.NewGuid().ToString("N")[..8].ToUpper(),
                OrderDate = DateTime.Now,
                Items = cart.ToList(),
                Subtotal = cart.Sum(item => item.LineTotal),
                Discount = 0m, // Có thể tính từ voucher nếu có
                ShippingFee = 0m,
                Total = cart.Sum(item => item.LineTotal)
            };

            HttpContext.Session.SetString("LAST_ORDER", JsonSerializer.Serialize(orderInfo));

            // Xóa giỏ hàng sau khi thanh toán thành công
            cart.Clear();
            SaveCartToSession(cart);

            return RedirectToAction(nameof(PaymentSuccess));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        
        ///     Thanh toán nhanh trực tiếp từ trang giỏ: tính tổng dựa trên voucher/VAT/ship rồi điều hướng tới trang thành công.
        ///     Không yêu cầu nhập thông tin khách hàng – phù hợp demo UX.
        
        public IActionResult QuickPayment()
        {
            var cart = GetCartFromSession();

            if (cart.Count == 0)
            {
                TempData["Error"] = "Giỏ hàng của bạn đang trống";
                return RedirectToAction(nameof(Index));
            }

            // Tính toán với voucher nếu có
            var appliedVoucher = GetAppliedVoucher();
            var subtotal = cart.Sum(item => item.LineTotal);
            var discount = CalculateDiscount(subtotal, appliedVoucher);
            var shippingFee = CalculateShippingFee(subtotal, appliedVoucher);
            var vat = CalculateVAT(subtotal); // VAT tính trên giá gốc (trước khi trừ voucher)
            var total = subtotal - discount + shippingFee + vat;

            // Lưu thông tin đơn hàng vào session để hiển thị trong trang thành công
            var orderInfo = new
            {
                OrderId = Guid.NewGuid().ToString("N")[..8].ToUpper(),
                OrderDate = DateTime.Now,
                Items = cart.ToList(),
                Subtotal = subtotal,
                Discount = discount,
                ShippingFee = shippingFee,
                Total = total,
                AppliedVoucher = appliedVoucher,
                PaymentMethod = "Thanh toán nhanh",
                CustomerName = "Khách hàng",
                CustomerPhone = "N/A",
                CustomerEmail = "N/A",
                ShippingAddress = "Địa chỉ mặc định"
            };

            HttpContext.Session.SetString("LAST_ORDER", JsonSerializer.Serialize(orderInfo));

            // Xóa giỏ hàng sau khi thanh toán thành công
            cart.Clear();
            SaveCartToSession(cart);

            // Xóa voucher đã áp dụng
            HttpContext.Session.Remove("AppliedVoucher");

            return RedirectToAction(nameof(PaymentSuccess));
        }

        [HttpPost]
        public IActionResult PrepareCheckout([FromBody] List<string> selectedImageUrls)
        {
            if (selectedImageUrls == null || !selectedImageUrls.Any())
            {
                return Json(new { success = false, message = "Không có sản phẩm nào được chọn!" });
            }

            HttpContext.Session.SetString("CHECKOUT_SELECTED_ITEMS", System.Text.Json.JsonSerializer.Serialize(selectedImageUrls));
            return Json(new { success = true });
        }

        [HttpGet]
        
        ///     Chuẩn bị dữ liệu hiển thị trang Checkout: đảm bảo user đăng nhập, xử lý voucher từ URL/spin wheel,
        ///     tính toán toàn bộ fee và trả về <see cref="CheckoutViewModel"/>.
        
        public IActionResult Checkout()
        {
            // Kiểm tra đăng nhập
            if (!User.Identity.IsAuthenticated)
            {
                TempData["Error"] = "Vui lòng đăng nhập để tiếp tục đặt hàng";
                return RedirectToAction("Login", "Auth");
            }

            var cart = GetCartFromSession();

            if (cart.Count == 0)
            {
                TempData["Error"] = "Giỏ hàng của bạn đang trống";
                return RedirectToAction(nameof(Index));
            }

            // Kiểm tra voucher từ URL parameter
            var voucherCode = Request.Query["voucherCode"].FirstOrDefault();
            if (!string.IsNullOrEmpty(voucherCode))
            {
                // Tìm voucher trong danh sách có sẵn
                var availableVouchers = GetAvailableVouchers();
                var voucher = availableVouchers.FirstOrDefault(v => v.Code.Equals(voucherCode, StringComparison.OrdinalIgnoreCase));

                if (voucher != null)
                {
                    // Lưu voucher vào session
                    HttpContext.Session.SetString("AppliedVoucher", JsonSerializer.Serialize(voucher));
                    Console.WriteLine($"Voucher from URL applied: {voucher.Name} ({voucher.Code})");
                }
            }

            // Kiểm tra voucher từ spin wheel
            var appliedVoucher = GetAppliedVoucher();
            Console.WriteLine($"Checkout - Applied voucher: {(appliedVoucher != null ? $"{appliedVoucher.Name} ({appliedVoucher.Code})" : "null")}");

            var subtotal = cart.Sum(item => item.LineTotal);
            var discount = CalculateDiscount(subtotal, appliedVoucher);
            var shippingFee = CalculateShippingFee(subtotal, appliedVoucher);
            var vat = CalculateVAT(subtotal); // VAT tính trên giá gốc (trước khi trừ voucher)
            var total = subtotal - discount + shippingFee + vat;

            Console.WriteLine($"Checkout - Subtotal: {subtotal}, Discount: {discount}, Shipping: {shippingFee}, VAT: {vat}, Total: {total}");

            var checkoutModel = new CheckoutViewModel
            {
                CartItems = cart,
                Subtotal = subtotal,
                Discount = discount,
                ShippingFee = shippingFee,
                VAT = vat,
                Total = total,
                AppliedVoucher = appliedVoucher
            };

            return View(checkoutModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]

        ///     Nhận thông tin Checkout từ form, tạo Customer/Address/Order/OrderDetails trong database
        ///     và tách luồng thanh toán:
        ///     - COD: hoàn tất đơn ngay, chuyển đến trang PaymentSuccess nội bộ.
        ///     - BANK_TRANSFER: lưu thông tin đơn vào session, chuyển sang PaymentController để gọi PayOS.

        public async Task<IActionResult> ProcessCheckout(CheckoutViewModel model)
        {
            var cart = GetCartFromSession();

            var selectedItemsJson = HttpContext.Session.GetString("CHECKOUT_SELECTED_ITEMS");
            var selectedUrls = new List<string>();
            if (!string.IsNullOrEmpty(selectedItemsJson))
            {
                selectedUrls = System.Text.Json.JsonSerializer.Deserialize<List<string>>(selectedItemsJson) ?? new List<string>();
                if (selectedUrls.Any())
                {
                    cart = cart.Where(i => selectedUrls.Contains(i.ImageUrl)).ToList();
                }
            }

            if (cart.Count == 0) return RedirectToAction(nameof(Index));

            if (!ModelState.IsValid)
            {
                model.CartItems = cart; model.Total = cart.Sum(i => i.LineTotal);
                return View("Checkout", model);
            }

            try
            {
                var appliedVoucher = GetAppliedVoucher();
                var customerEmail = User.Identity.IsAuthenticated ? User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value : model.CustomerEmail;
                var subtotal = cart.Sum(i => i.LineTotal);
                var discount = CalculateDiscount(subtotal, appliedVoucher);
                var shippingFee = CalculateShippingFee(subtotal, appliedVoucher);
                var total = subtotal - discount + shippingFee + CalculateVAT(subtotal);

                // --- ÁP DỤNG FACADE PATTERN ---

                var order = await _checkoutFacade.PlaceOrderAsync(model, customerEmail, cart, appliedVoucher, total);

                // --- ÁP DỤNG MẪU OBSERVER 
                // --- ÁP DỤNG MẪU OBSERVER (Bản thực chiến) ---
                var orderSubject = new OrderSubject();
                orderSubject.Attach(new EmailObserver());
                orderSubject.Attach(new InventoryObserver());
                orderSubject.Attach(new MembershipObserver());

                // 1. Vẫn chạy Notify để lấy Log hiển thị cho thầy xem (Kỹ thuật bắt chữ Sw)
                using (var sw = new System.IO.StringWriter())
                {
                    var originalOut = Console.Out;
                    Console.SetOut(sw);
                    orderSubject.Notify(order);
                    Console.SetOut(originalOut);
                    TempData["ObserverLogs"] = sw.ToString().Replace(Environment.NewLine, "<br/>");
                }
                // 2. THỰC HIỆN CÁC TÁC VỤ THẬT (Bản chuẩn hết báo lỗi đỏ)
                try
                {
                    // TÁC VỤ 1: GỬI EMAIL THẬT
                    // Tuyệt chiêu lấy EmailService trực tiếp không cần khai báo trên đầu file
                    var emailService = HttpContext.RequestServices.GetRequiredService<PerfumeStore.Services.IEmailService>();

                    string emailSubject = "Xác nhận đơn hàng #" + order.OrderId;
                    string emailBody = $"Chào {model.CustomerName}, đơn hàng của bạn đã đặt thành công với tổng tiền {total:N0}đ. Cảm ơn bạn đã mua hàng!";

                    // Bỏ chữ _ đi, dùng biến emailService vừa tạo ở trên
                   

                    // TÁC VỤ 2: TRỪ KHO THẬT
                    foreach (var item in cart)
                    {
                        var productInDb = await _context.Products.FindAsync(item.ProductId);
                        if (productInDb != null)
                        {
                            productInDb.Stock -= item.Quantity; // Trừ số lượng trong kho
                        }
                    }

                    // TÁC VỤ 3: CỘNG ĐIỂM THÀNH VIÊN THẬT
                    var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Email == customerEmail);
                    if (customer != null)
                    {
                        // Ví dụ: Mỗi 100k cộng 1 điểm
                        int extraPoints = (int)(total / 100000);
                        customer.SpinNumber = (customer.SpinNumber ?? 0) + extraPoints;
                    }

                    // LƯU TẤT CẢ THAY ĐỔI VÀO DATABASE
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi thực hiện các tác vụ sau đặt hàng");
                }



                var orderInfo = new { OrderId = order.OrderId.ToString(), OrderDate = order.OrderDate, Items = cart.ToList(), Total = order.TotalAmount };
                HttpContext.Session.SetString("LAST_ORDER", System.Text.Json.JsonSerializer.Serialize(orderInfo));

                // --- ÁP DỤNG STRATEGY PATTERN ---
                // Thay thế IF-ELSE bằng Đối tượng Chiến lược điều hướng
                var paymentContext = new PerfumeStore.DesignPatterns.Strategy.PaymentContext();

                if (model.PaymentMethod == "BANK_TRANSFER")
                {
                    HttpContext.Session.SetString("PENDING_ORDER_ID", order.OrderId.ToString());
                    HttpContext.Session.SetString("PENDING_ORDER_AMOUNT", order.TotalAmount.ToString());
                    paymentContext.SetStrategy(new PerfumeStore.DesignPatterns.Strategy.PayOsPaymentStrategy());
                }
                else
                {
                    cart.Clear(); SaveCartToSession(cart); HttpContext.Session.Remove("AppliedVoucher");
                    paymentContext.SetStrategy(new PerfumeStore.DesignPatterns.Strategy.CodPaymentStrategy());
                }

                // Thực thi điều hướng
                var routeResult = paymentContext.ExecuteRouting(order);
                return RedirectToAction(routeResult.ActionName, routeResult.ControllerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order");
                TempData["Error"] = ex.Message;
                return View("Checkout", model);
            }
        }


        ///     Đọc thông tin đơn hàng cuối cùng từ session hoặc database (dành cho luồng COD/quick payment),
        ///     hiển thị lại để khách xem chi tiết.

        public async Task<IActionResult> PaymentSuccess()
        {
            var orderJson = HttpContext.Session.GetString("LAST_ORDER");
            if (string.IsNullOrEmpty(orderJson))
            {
                TempData["Error"] = "Không tìm thấy thông tin đơn hàng";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var orderInfo = JsonSerializer.Deserialize<JsonElement>(orderJson);
                var orderId = orderInfo.GetProperty("OrderId").GetString();
                
                // Lấy thông tin đơn hàng từ database
                var order = await _orderService.GetOrderByOrderIdAsync(orderId);
                if (order != null)
                {
                    // Tính lại các khoản phí từ OrderDetails
                    var subtotal = order.OrderDetails.Sum(od => od.TotalPrice);

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
                    if (order.Coupon != null && order.Coupon.DiscountAmount.HasValue)
                    {
                        discount = order.Coupon.DiscountAmount.Value;
                    }

                    // Tổng tiền = Subtotal - Discount + VAT + Shipping
                    var total = subtotal - discount + vat + shipping;

                    // Tạo view model với thông tin từ database
                    var orderViewModel = new
                    {
                        OrderId = order.OrderId.ToString(),
                        OrderDate = order.OrderDate,
                        Subtotal = subtotal,
                        Discount = discount,
                        VAT = vat,
                        ShippingFee = shipping,
                        TotalAmount = total,
                        PaymentMethod = order.PaymentMethod,
                        Status = order.Status,
                        Customer = new
                        {
                            Name = order.Customer.Name,
                            Email = order.Customer.Email,
                            Phone = order.Customer.Phone
                        },
                        Address = new
                        {
                            RecipientName = order.Address.RecipientName,
                            Phone = order.Address.Phone,
                            Province = order.Address.Province,
                            District = order.Address.District,
                            Ward = order.Address.Ward,
                            AddressLine = order.Address.AddressLine
                        },
                        OrderDetails = order.OrderDetails.Select(od => new
                        {
                            ProductName = od.Product.ProductName,
                            Quantity = od.Quantity,
                            UnitPrice = od.UnitPrice,
                            TotalPrice = od.TotalPrice,
                            ImageUrl = od.Product.ProductImages?.FirstOrDefault()?.ImageData != null 
                                ? $"data:{od.Product.ProductImages.First().ImageMimeType};base64,{Convert.ToBase64String(od.Product.ProductImages.First().ImageData)}"
                                : "/images/ProductSummary/chanel-bleu-de-chanel-edp-100-ml.webp" // Default image
                        }).ToList(),
                        Coupon = order.Coupon != null ? new
                        {
                            Code = order.Coupon.Code,
                            DiscountAmount = order.Coupon.DiscountAmount
                        } : null
                    };
                    
                    return View(orderViewModel);
                }
                else
                {
                    // Fallback về session data nếu không tìm thấy trong database
                    return View(orderInfo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading order for payment success");
                TempData["Error"] = "Có lỗi xảy ra khi tải thông tin đơn hàng";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        
        ///     Xóa voucher đang áp dụng khỏi session – được gọi từ trang Checkout để khách chọn mã mới.
        
        public IActionResult RemoveVoucher()
        {
            try
            {
                HttpContext.Session.Remove("AppliedVoucher");
                return Json(new { success = true, message = "✅ Đã xóa voucher thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"❌ Lỗi: {ex.Message}" });
            }
        }

        [HttpGet]
        
        ///     API hỗ trợ JavaScript đọc lại voucher từ session (ví dụ sau khi redirect từ vòng quay may mắn).
        
        public IActionResult GetAppliedVoucherApi()
        {
            try
            {
                var voucher = GetAppliedVoucher();
                if (voucher != null)
                {
                    return Json(new { success = true, voucher = voucher });
                }
                else
                {
                    return Json(new { success = false, message = "Không có voucher nào được áp dụng" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"❌ Lỗi: {ex.Message}" });
            }
        }

        [HttpGet]
        
        ///     Tính toán lại subtotal/discount/ship/VAT/total trên server và trả về JSON
        ///     để UI cập nhật tức thời sau khi áp dụng/xóa voucher.
        
        public IActionResult GetCheckoutSummary()
        {
            try
            {
                var cart = GetCartFromSession();
                var appliedVoucher = GetAppliedVoucher();

                var subtotal = cart.Sum(item => item.LineTotal);
                var discount = CalculateDiscount(subtotal, appliedVoucher);
                var shippingFee = CalculateShippingFee(subtotal, appliedVoucher);
                var vat = CalculateVAT(subtotal);
                var total = subtotal - discount + shippingFee + vat;

                return Json(new
                {
                    success = true,
                    subtotal = subtotal,
                    discount = discount,
                    shippingFee = shippingFee,
                    vat = vat,
                    total = total
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting checkout summary");
                return Json(new { success = false, message = $"❌ Lỗi: {ex.Message}" });
            }
        }

        // Test method để kiểm tra database connection
        [HttpGet]
        
        ///     Endpoint debug: kiểm tra kết nối database bằng cách đếm số Customer.
        
        public async Task<IActionResult> TestDatabaseConnection()
        {
            try
            {
                Console.WriteLine("Testing database connection...");
                
                // Test database connection bằng cách đếm số customer
                var customerCount = await _context.Customers.CountAsync();
                Console.WriteLine($"Database connection successful. Customer count: {customerCount}");
                
                return Json(new
                {
                    success = true, 
                    message = "Database connection is working",
                    customerCount = customerCount
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database connection error: {ex.Message}");
                return Json(new
                {
                    success = false, 
                    message = "Database connection failed",
                    error = ex.Message
                });
            }
        }

        // Test method để test toàn bộ flow đặt hàng
        [HttpPost]
        
        ///     Luồng thử nghiệm end-to-end: tạo cart test, build CheckoutViewModel và gọi OrderService.CreateOrderAsync.
        
        public async Task<IActionResult> TestOrderFlow()
        {
            try
            {
                Console.WriteLine("Testing full order flow...");
                
                // 1. Thêm sản phẩm test vào cart
                var cart = GetCartFromSession();
                cart.Clear();
                
                var testProduct = new CartItem 
                { 
                    ImageUrl = "/images/Checkout/product1.jpg", 
                    ProductName = "Test Product", 
                    Description = "Test Description", 
                    Quantity = 1, 
                    UnitPrice = 100000 
                };
                
                cart.Add(testProduct);
                SaveCartToSession(cart);
                Console.WriteLine("Test product added to cart");
                
                // 2. Tạo model test
                var model = new CheckoutViewModel
                {
                    CustomerName = "Test Customer",
                    CustomerPhone = "0123456789",
                    CustomerEmail = "test@example.com",
                    Province = "ho-chi-minh",
                    District = "quan-1",
                    Ward = "Phường Bến Nghé",
                    ShippingAddress = "123 Test Street",
                    OrderNotes = "Test order"
                };
                
                Console.WriteLine("Test model created");
                
                // 3. Test tạo đơn hàng
                Console.WriteLine("Creating test order...");
                var order = await _orderService.CreateOrderAsync(model, model.CustomerEmail, cart, null);
                Console.WriteLine($"Test order created with ID: {order.OrderId}");
                
                return Json(new
                {
                    success = true, 
                    message = "Full order flow test completed successfully",
                    orderId = order.OrderId,
                    cartCount = cart.Count
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in test order flow: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return Json(new
                {
                    success = false, 
                    message = "Full order flow test failed",
                    error = ex.Message
                });
            }
        }

        // API để lấy địa chỉ mặc định của khách hàng
        [HttpGet]
        
        ///     Lấy địa chỉ mặc định của khách hàng đăng nhập (được dùng để auto-fill form Checkout).
        
        public async Task<IActionResult> GetDefaultAddress()
        {
            try
            {
                if (!User.Identity.IsAuthenticated)
                {
                    return Json(new { success = false, message = "Chưa đăng nhập" });
                }

                var customerEmail = User.Identity.Name;
                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.Email == customerEmail);

                if (customer == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy khách hàng" });
                }

                var defaultAddress = await _context.ShippingAddresses
                    .FirstOrDefaultAsync(sa => sa.CustomerId == customer.CustomerId && sa.IsDefault);

                if (defaultAddress == null)
                {
                    return Json(new { success = false, message = "Không có địa chỉ mặc định" });
                }

                return Json(new
                {
                    success = true,
                    address = new
                    {
                        customerName = defaultAddress.RecipientName,
                        phone = defaultAddress.Phone,
                        province = defaultAddress.Province,
                        district = defaultAddress.District,
                        ward = defaultAddress.Ward,
                        addressLine = defaultAddress.AddressLine
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting default address: {ex.Message}");
                return Json(new { success = false, message = "Có lỗi xảy ra khi lấy địa chỉ mặc định" });
            }
        }

        // Trang test checkout flow
        [HttpGet]
        
        ///     Trang HTML nhẹ để QA kiểm tra form checkout (không ảnh hưởng người dùng thật).
        
        public IActionResult TestCheckout()
        {
            return View();
        }

        // Trang test real checkout flow
        [HttpGet]
        
        ///     Trang demo chứa flow checkout thực tế (bao gồm PayOS) dành cho nội bộ kiểm thử.
        
        public IActionResult TestRealCheckout()
        {
            return View();
        }

        // API để thêm sản phẩm thật từ database vào cart
        [HttpGet]
        
        ///     Lấy 2 sản phẩm thật từ database và đưa vào session để đội sale test.
        
        public async Task<IActionResult> AddRealProductsToCart()
        {
            try
            {
                Console.WriteLine("Adding real products to cart...");

                // Lấy 2 sản phẩm đầu tiên từ database
                var products = await _context.Products
                    .Where(p => p.IsPublished == true)
                    .Take(2)
                    .ToListAsync();

                if (products.Count == 0)
                {
                    return Json(new { success = false, message = "Không có sản phẩm nào trong database" });
                }

                var cart = GetCartFromSession();
                cart.Clear();

                foreach (var product in products)
                {
                    var cartItem = new CartItem
                    {
                        ProductId = product.ProductId,
                        ImageUrl = "/images/Checkout/product1.jpg", // Default image
                        ProductName = product.ProductName,
                        Description = product.DescriptionNo1 ?? "Sản phẩm từ database",
                        Quantity = 1,
                        UnitPrice = product.Price
                    };
                    cart.Add(cartItem);
                    Console.WriteLine($"Added real product: {product.ProductName} (ID: {product.ProductId})");
                }

                SaveCartToSession(cart);

                return Json(new
                {
                    success = true,
                    message = $"Đã thêm {cart.Count} sản phẩm thật vào giỏ hàng",
                    productCount = cart.Count
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding real products: {ex.Message}");
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // API để kiểm tra sản phẩm trong database
        [HttpGet]
        
        ///     Endpoint hỗ trợ QA thống kê nhanh số sản phẩm publish/freeze.
        
        public async Task<IActionResult> GetDatabaseProducts()
        {
            try
            {
                var productCount = await _context.Products.CountAsync();
                var publishedCount = await _context.Products.CountAsync(p => p.IsPublished == true);

                return Json(new
                {
                    success = true,
                    productCount = productCount,
                    publishedCount = publishedCount,
                    message = $"Database có {productCount} sản phẩm, {publishedCount} đã publish"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting database products: {ex.Message}");
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // API để xóa giỏ hàng
        [HttpGet]
        ///     Xóa hoàn toàn giỏ hàng trong session – dùng cho thao tác thử nghiệm.
        
        public IActionResult ClearCart()
        {
            try
            {
                var cart = GetCartFromSession();
                cart.Clear();
                SaveCartToSession(cart);

                return Json(new
                {
                    success = true,
                    message = "Đã xóa giỏ hàng thành công"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing cart: {ex.Message}");
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // Test endpoint đơn giản để kiểm tra form submission
        [HttpPost]
        
        ///     In ra console nội dung form checkout gửi lên (debug front-end).
        
        public IActionResult TestFormSubmission(CheckoutViewModel model)
        {
            Console.WriteLine("=== TEST FORM SUBMISSION ===");
            Console.WriteLine($"Model received: {model != null}");
            Console.WriteLine($"CustomerName: {model?.CustomerName}");
            Console.WriteLine($"CustomerPhone: {model?.CustomerPhone}");
            Console.WriteLine($"CustomerEmail: {model?.CustomerEmail}");
            Console.WriteLine($"ShippingAddress: {model?.ShippingAddress}");
            Console.WriteLine($"ModelState.IsValid: {ModelState.IsValid}");

            if (!ModelState.IsValid)
            {
                Console.WriteLine("ModelState errors:");
                foreach (var error in ModelState)
                {
                    if (error.Value.Errors.Count > 0)
                    {
                        Console.WriteLine($"  {error.Key}: {string.Join(", ", error.Value.Errors.Select(e => e.ErrorMessage))}");
                    }
                }
            }

            return Json(new
            {
                success = true,
                message = "Form submission test successful",
                data = new
                {
                    customerName = model?.CustomerName,
                    customerPhone = model?.CustomerPhone,
                    customerEmail = model?.CustomerEmail,
                    shippingAddress = model?.ShippingAddress,
                    isValid = ModelState.IsValid
                }
            });
        }

        // Test endpoint để kiểm tra toàn bộ flow đặt hàng
        [HttpGet]
        
        ///     Bộ kịch bản kiểm thử tổng thể: từ kiểm tra DB, thêm hàng vào cart, tạo order đến đọc lại order.
        
        public async Task<IActionResult> TestFullCheckoutFlow()
        {
            try
            {
                Console.WriteLine("=== TESTING FULL CHECKOUT FLOW ===");

                // 1. Kiểm tra database connection
                Console.WriteLine("1. Testing database connection...");
                var customerCount = await _context.Customers.CountAsync();
                var productCount = await _context.Products.CountAsync();
                Console.WriteLine($"Database OK - Customers: {customerCount}, Products: {productCount}");

                // 2. Thêm sản phẩm test vào cart
                Console.WriteLine("2. Adding test products to cart...");
                var cart = GetCartFromSession();
                cart.Clear();

                var testProducts = new[]
                {
                    new CartItem {
                        ImageUrl = "/images/Checkout/product1.jpg",
                        ProductName = "Chanel Bleu de Chanel EDP 100ml",
                        Description = "Test product 1",
                        Quantity = 2,
                        UnitPrice = 197837
                    },
                    new CartItem {
                        ImageUrl = "/images/Checkout/product2.jpg",
                        ProductName = "Dior Sauvage EDP 100ml",
                        Description = "Test product 2",
                        Quantity = 1,
                        UnitPrice = 174697
                    }
                };

                cart.AddRange(testProducts);
                SaveCartToSession(cart);
                Console.WriteLine($"Cart updated with {cart.Count} items");

                // 3. Tạo model checkout test
                Console.WriteLine("3. Creating checkout model...");
                var checkoutModel = new CheckoutViewModel
                {
                    CustomerName = "Test Customer",
                    CustomerPhone = "0123456789",
                    CustomerEmail = "test@example.com",
                    Province = "ho-chi-minh",
                    District = "quan-1",
                    Ward = "Phường Bến Nghé",
                    ShippingAddress = "123 Test Street, District 1, HCMC",
                    OrderNotes = "Test order from automated test",
                    SaveAsDefaultAddress = true,
                    PaymentMethod = "COD",
                    CartItems = cart,
                    Subtotal = cart.Sum(item => item.LineTotal),
                    ShippingFee = 0m,
                    Discount = 0m,
                    Total = cart.Sum(item => item.LineTotal)
                };

                Console.WriteLine($"Checkout model created - Total: {checkoutModel.Total:N0} VND");

                // 4. Test tạo đơn hàng
                Console.WriteLine("4. Creating order in database...");
                var order = await _orderService.CreateOrderAsync(checkoutModel, checkoutModel.CustomerEmail, cart, null);
                Console.WriteLine($"Order created successfully with ID: {order.OrderId}");

                // 5. Kiểm tra order details
                Console.WriteLine("5. Verifying order details...");
                var orderDetails = await _context.OrderDetails
                    .Where(od => od.OrderId == order.OrderId)
                    .ToListAsync();
                Console.WriteLine($"Order has {orderDetails.Count} items");

                // 6. Kiểm tra shipping address
                var shippingAddress = await _context.ShippingAddresses
                    .FirstOrDefaultAsync(sa => sa.AddressId == order.AddressId);
                Console.WriteLine($"Shipping address: {shippingAddress?.AddressLine}");

                // 7. Test lấy order từ database
                Console.WriteLine("6. Testing order retrieval...");
                var retrievedOrder = await _orderService.GetOrderByIdAsync(order.OrderId);
                Console.WriteLine($"Retrieved order: {retrievedOrder?.OrderId}, Total: {retrievedOrder?.TotalAmount:N0} VND");

                return Json(new
                {
                    success = true,
                    message = "Full checkout flow test completed successfully!",
                    results = new
                    {
                        databaseConnection = "OK",
                        cartItems = cart.Count,
                        orderId = order.OrderId,
                        orderTotal = order.TotalAmount,
                        orderDetailsCount = orderDetails.Count,
                        shippingAddress = shippingAddress?.AddressLine,
                        retrievedOrderId = retrievedOrder?.OrderId
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"=== CHECKOUT FLOW TEST FAILED ===");
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return Json(new
                {
                    success = false,
                    message = "Full checkout flow test failed",
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }
    }
}
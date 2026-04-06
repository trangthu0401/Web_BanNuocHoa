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
    public class CartController : Controller
    {
        ILogger<CartController> _logger;
        private readonly PerfumeStore.DesignPatterns.Facade.ICheckoutFacade _checkoutFacade;
        private readonly IWebHostEnvironment _env;
        private readonly IOrderService _orderService;
        private readonly PerfumeStoreContext _context;
        private readonly IWarrantyService _warrantyService;
        private readonly PerfumeStore.DesignPatterns.Observer.OrderSubject _orderSubject;
        private readonly IEmailService _emailService;

        public CartController(
            ILogger<CartController> logger,
            IWebHostEnvironment env,
            IOrderService orderService,
            PerfumeStoreContext context,
            IWarrantyService warrantyService,
            PerfumeStore.DesignPatterns.Facade.ICheckoutFacade checkoutFacade,
            PerfumeStore.DesignPatterns.Observer.OrderSubject orderSubject,
            IEmailService emailService)
        {
            _logger = logger;
            _env = env;
            _orderService = orderService;
            _context = context;
            _warrantyService = warrantyService;
            _checkoutFacade = checkoutFacade;
            _orderSubject = orderSubject;
            _emailService = emailService;
        }

        public IActionResult Index()
        {
            var cart = GetCartFromSession();
            return View(cart);
        }

        private const string CartSessionKey = "CART_SESSION";

        private List<CartItem> GetCartFromSession()
        {
            var json = HttpContext.Session.GetString(CartSessionKey);
            if (string.IsNullOrEmpty(json)) return new List<CartItem>();
            try { return JsonSerializer.Deserialize<List<CartItem>>(json) ?? new List<CartItem>(); }
            catch { return new List<CartItem>(); }
        }

        private void SaveCartToSession(List<CartItem> items)
        {
            HttpContext.Session.SetString(CartSessionKey, JsonSerializer.Serialize(items));
        }

        [HttpGet]
        public IActionResult GetCartCount()
        {
            var cart = GetCartFromSession();
            var count = cart.Sum(item => item.Quantity);
            return Json(new { success = true, cartCount = count });
        }

        private VoucherModel? GetAppliedVoucher()
        {
            var voucherJson = HttpContext.Session.GetString("AppliedVoucher");
            if (string.IsNullOrEmpty(voucherJson)) return null;

            try
            {
                return JsonSerializer.Deserialize<VoucherModel>(voucherJson);
            }
            catch
            {
                return null;
            }
        }

        private decimal CalculateDiscount(decimal subtotal, VoucherModel? voucher)
        {
            if (voucher == null) return 0m;

            var effectiveValue = voucher.AccumulatedValue > 0 ? voucher.AccumulatedValue : voucher.Value;
            var discount = voucher.Type switch
            {
                "percent" => subtotal * Math.Min(effectiveValue, 100) / 100,
                "amount" => Math.Min(effectiveValue, subtotal),
                "freeship" => 0m,
                _ => 0m
            };
            return discount;
        }

        private decimal CalculateVAT(decimal subtotal)
        {
            var vatFee = _context.Fees.FirstOrDefault(f => f.Name == "VAT");
            if (vatFee != null)
            {
                return subtotal * Math.Min(vatFee.Value, 100) / 100;
            }
            return 0m;
        }

        private decimal CalculateShippingFee(decimal subtotal, VoucherModel? voucher)
        {
            if (voucher?.Type == "freeship" && subtotal >= 200000) return 0m;

            var shippingFee = _context.Fees.FirstOrDefault(f => f.Name == "Shipping");
            if (shippingFee != null)
            {
                var threshold = shippingFee.Threshold ?? 5000000m;
                return subtotal >= threshold ? 0m : shippingFee.Value;
            }
            return subtotal >= 5000000 ? 0m : 30000m;
        }

        private List<VoucherModel> GetAvailableVouchers()
        {
            var now = DateTime.Now;
            var coupons = _context.Coupons
                .Where(c => (c.IsUsed == null || c.IsUsed == false) &&
                            (c.ExpiryDate == null || c.ExpiryDate >= now) &&
                            !string.IsNullOrEmpty(c.Code) &&
                            c.DiscountAmount.HasValue && c.DiscountAmount.Value > 0)
                .OrderByDescending(c => c.CreatedDate)
                .Take(20).ToList();

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

            return new List<VoucherModel>
            {
                new VoucherModel { Id = 3, Code = "FREESHIP", Name = "Miễn phí vận chuyển", Value = 0, Type = "freeship", Color = "#6f42c1", Description = "Miễn phí ship cho đơn hàng từ 200k" },
                new VoucherModel { Id = 4, Code = "CASH50K", Name = "Giảm 50.000đ", Value = 50000, Type = "amount", Color = "#fd7e14", Description = "Giảm trực tiếp 50.000đ" }
            };
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(string imageUrl, string name, decimal price, int? productId = null, bool hasGiftWrap = false, bool hasEngraveName = false, string engraveContent = "")
        {
            var cart = GetCartFromSession();

            PerfumeStore.DesignPatterns.Decorator.IProduct productDecorated = new PerfumeStore.DesignPatterns.Decorator.BasePerfume(name, price);
            if (hasGiftWrap) productDecorated = new PerfumeStore.DesignPatterns.Decorator.GiftWrapDecorator(productDecorated);
            if (hasEngraveName) productDecorated = new PerfumeStore.DesignPatterns.Decorator.EngraveNameDecorator(productDecorated);

            var finalName = productDecorated.GetDescription();
            var finalPrice = productDecorated.GetPrice();

            if (hasEngraveName && !string.IsNullOrWhiteSpace(engraveContent))
            {
                finalName = finalName.Replace("(+ Khắc tên theo yêu cầu)", $"(+ Khắc: '{engraveContent}')");
            }

            string uniqueImageUrl = imageUrl;
            if (hasGiftWrap) uniqueImageUrl += (uniqueImageUrl.Contains("?") ? "&" : "?") + "giftwrap=true";
            if (hasEngraveName) uniqueImageUrl += (uniqueImageUrl.Contains("?") ? "&" : "?") + "engrave=true";

            if (productId.HasValue && productId.Value > 0)
            {
                var product = await _context.Products.FindAsync(productId.Value);
                if (product == null)
                {
                    return Request.Headers["X-Requested-With"] == "XMLHttpRequest" ?
                           Json(new { success = false, message = "Không tìm thấy sản phẩm trong hệ thống.", cartCount = cart.Sum(item => item.Quantity) }) :
                           RedirectToAction(nameof(Index));
                }

                var found = cart.FirstOrDefault(i => i.ProductId == productId.Value && i.HasGiftWrap == hasGiftWrap && i.HasEngraveName == hasEngraveName);
                var requestedQuantity = (found?.Quantity ?? 0) + 1;

                if (requestedQuantity > product.Stock)
                {
                    var msg = $"Sản phẩm chỉ còn {product.Stock} sản phẩm trong kho.";
                    return Request.Headers["X-Requested-With"] == "XMLHttpRequest" ? Json(new { success = false, message = msg, cartCount = cart.Sum(item => item.Quantity) }) : RedirectToAction(nameof(Index));
                }

                if (requestedQuantity > 10)
                {
                    return Request.Headers["X-Requested-With"] == "XMLHttpRequest" ? Json(new { success = false, message = "Số lượng tối đa là 10 sản phẩm." }) : RedirectToAction(nameof(Index));
                }

                if (found == null)
                {
                    cart.Add(new CartItem { ProductId = productId.Value, ImageUrl = uniqueImageUrl, ProductName = finalName, Quantity = 1, UnitPrice = finalPrice, HasGiftWrap = hasGiftWrap, HasEngraveName = hasEngraveName });
                }
                else
                {
                    found.Quantity += 1;
                }
            }
            else
            {
                var found = cart.FirstOrDefault(i => i.ImageUrl.Equals(uniqueImageUrl, StringComparison.OrdinalIgnoreCase));
                if (found == null)
                {
                    cart.Add(new CartItem { ImageUrl = uniqueImageUrl, ProductName = finalName, Quantity = 1, UnitPrice = finalPrice, HasGiftWrap = hasGiftWrap, HasEngraveName = hasEngraveName });
                }
                else
                {
                    if (found.Quantity >= 10) return Request.Headers["X-Requested-With"] == "XMLHttpRequest" ? Json(new { success = false, message = "Số lượng tối đa là 10 sản phẩm." }) : RedirectToAction(nameof(Index));
                    found.Quantity += 1;
                }
            }

            SaveCartToSession(cart);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = true, message = "Đã thêm sản phẩm vào giỏ hàng!", cartCount = cart.Sum(item => item.Quantity) });
            }

            return RedirectToAction(nameof(Checkout));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveFromCart(string imageUrl)
        {
            var cart = GetCartFromSession();
            cart.RemoveAll(i => i.ImageUrl.Equals(imageUrl, StringComparison.OrdinalIgnoreCase));
            SaveCartToSession(cart);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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
        public IActionResult RemoveFromCartAjax(string imageUrl)
        {
            var cart = GetCartFromSession();
            var removed = cart.RemoveAll(i => i.ImageUrl.Equals(imageUrl, StringComparison.OrdinalIgnoreCase));
            if (removed > 0) SaveCartToSession(cart);
            return Json(new { ok = true, remaining = cart.Count });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCartQuantity(string imageUrl, int quantity)
        {
            var cart = GetCartFromSession();
            var item = cart.FirstOrDefault(i => i.ImageUrl.Equals(imageUrl, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                var requestedQuantity = Math.Max(1, Math.Min(10, quantity));
                if (item.ProductId > 0)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product != null && requestedQuantity > product.Stock)
                    {
                        requestedQuantity = product.Stock;
                        if (requestedQuantity < 1)
                        {
                            cart.Remove(item);
                            SaveCartToSession(cart);
                            return Json(new { ok = false, message = "Sản phẩm đã hết hàng và đã được xóa khỏi giỏ hàng.", removed = true });
                        }
                    }
                }
                item.Quantity = requestedQuantity;
                SaveCartToSession(cart);
                return Json(new { ok = true, quantity = item.Quantity, maxReached = item.Quantity >= 10, stockLimited = item.ProductId > 0 && item.Quantity < quantity });
            }
            return Json(new { ok = false, message = "Không tìm thấy sản phẩm trong giỏ hàng" });
        }

        [HttpPost]
        public IActionResult PrepareCheckout([FromBody] List<string> selectedImageUrls)
        {
            if (selectedImageUrls == null || !selectedImageUrls.Any())
                return Json(new { success = false, message = "Không có sản phẩm nào được chọn!" });

            HttpContext.Session.SetString("CHECKOUT_SELECTED_ITEMS", JsonSerializer.Serialize(selectedImageUrls));
            return Json(new { success = true });
        }

        [HttpGet]
        public IActionResult Checkout()
        {
            if (!User.Identity.IsAuthenticated)
            {
                TempData["Error"] = "Vui lòng đăng nhập để tiếp tục đặt hàng";
                return RedirectToAction("Login", "Auth");
            }

            var cart = GetCartFromSession();

            var selectedItemsJson = HttpContext.Session.GetString("CHECKOUT_SELECTED_ITEMS");
            if (!string.IsNullOrEmpty(selectedItemsJson))
            {
                var selectedUrls = JsonSerializer.Deserialize<List<string>>(selectedItemsJson) ?? new List<string>();
                if (selectedUrls.Any())
                {
                    cart = cart.Where(i => selectedUrls.Contains(i.ImageUrl)).ToList();
                }
            }

            if (cart.Count == 0)
            {
                TempData["Error"] = "Không có sản phẩm nào được chọn để thanh toán";
                return RedirectToAction(nameof(Index));
            }

            var voucherCode = Request.Query["voucherCode"].FirstOrDefault();
            if (!string.IsNullOrEmpty(voucherCode))
            {
                var availableVouchers = GetAvailableVouchers();
                var voucher = availableVouchers.FirstOrDefault(v => v.Code.Equals(voucherCode, StringComparison.OrdinalIgnoreCase));
                if (voucher != null) HttpContext.Session.SetString("AppliedVoucher", JsonSerializer.Serialize(voucher));
            }

            var appliedVoucher = GetAppliedVoucher();
            var subtotal = cart.Sum(item => item.LineTotal);
            var discount = CalculateDiscount(subtotal, appliedVoucher);
            var shippingFee = CalculateShippingFee(subtotal, appliedVoucher);
            var vat = CalculateVAT(subtotal);
            var total = subtotal - discount + shippingFee + vat;

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

        // =================================================================================================
        // HÀM XỬ LÝ CHÍNH: ĐÃ SỬA CHỮA ĐỂ TƯƠNG THÍCH VỚI OBSERVER VÀ THỰC THI CHỨC NĂNG THẬT
        // =================================================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessCheckout(CheckoutViewModel model)
        {
            var fullCart = GetCartFromSession();
            var cart = fullCart.ToList();

            var selectedItemsJson = HttpContext.Session.GetString("CHECKOUT_SELECTED_ITEMS");
            if (!string.IsNullOrEmpty(selectedItemsJson))
            {
                var selectedUrls = JsonSerializer.Deserialize<List<string>>(selectedItemsJson) ?? new List<string>();
                if (selectedUrls.Any())
                {
                    cart = cart.Where(i => selectedUrls.Contains(i.ImageUrl)).ToList();
                }
            }

            if (cart.Count == 0) return RedirectToAction(nameof(Index));

            if (!ModelState.IsValid)
            {
                model.CartItems = cart;
                model.Total = cart.Sum(i => i.LineTotal);
                return View("Checkout", model);
            }

            try
            {
                var appliedVoucher = GetAppliedVoucher();
                var customerEmail = User.Identity.IsAuthenticated ? User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value : model.CustomerEmail;
                var subtotal = cart.Sum(i => i.LineTotal);
                var discount = CalculateDiscount(subtotal, appliedVoucher);
                var shippingFee = CalculateShippingFee(subtotal, appliedVoucher);
                var vat = CalculateVAT(subtotal);
                var total = subtotal - discount + shippingFee + vat;

                // --- 1. LƯU ĐƠN HÀNG VÀO DATABASE (ÁP DỤNG FACADE PATTERN) ---
                var order = await _checkoutFacade.PlaceOrderAsync(model, customerEmail, cart, appliedVoucher, total);

                // --- 2. ÁP DỤNG MẪU OBSERVER (TẠO LOG GIẢ QUA STRINGWRITER) ---
                var orderSubject = new PerfumeStore.DesignPatterns.Observer.OrderSubject();

                orderSubject.Attach(new PerfumeStore.DesignPatterns.Observer.EmailObserver(_emailService));
                orderSubject.Attach(new PerfumeStore.DesignPatterns.Observer.InventoryObserver());
                orderSubject.Attach(new PerfumeStore.DesignPatterns.Observer.MembershipObserver());

                using (var sw = new System.IO.StringWriter())
                {
                    var originalOut = Console.Out;
                    Console.SetOut(sw);
                    orderSubject.Notify(order);
                    Console.SetOut(originalOut);
                    TempData["ObserverLogs"] = sw.ToString().Replace(Environment.NewLine, "<br/>");
                }

                // --- 3. THỰC THI CÁC TÁC VỤ THẬT TẾ ---
                try
                {
                    // Tác vụ 3.1: Gửi Email xác nhận đơn hàng cho khách
                    if (!string.IsNullOrEmpty(customerEmail))
                    {
                        string emailSub = $"Xác nhận đơn hàng #{order.OrderId} từ PerfumeStore";
                        string emailBody = $"<h3>Cảm ơn {model.CustomerName} đã đặt hàng!</h3>" +
                                           $"<p>Mã đơn hàng: <b>{order.OrderId}</b></p>" +
                                           $"<p>Tổng tiền: <b>{total:N0} VNĐ</b></p>" +
                                           $"<p>Phương thức: {model.PaymentMethod}</p>";
                        await _emailService.SendSimpleTextEmailAsync(customerEmail, emailSub, emailBody);
                    }

                    // Tác vụ 3.2: Trừ số lượng Tồn kho & Gửi Email báo cho Admin/Kho
                    bool stockUpdated = false;
                    string inventoryAlertBody = $"<h3>Báo cáo cập nhật kho - Đơn hàng #{order.OrderId}</h3><ul>";

                    foreach (var item in cart)
                    {
                        if (item.ProductId > 0)
                        {
                            var prodInDb = await _context.Products.FindAsync(item.ProductId);
                            if (prodInDb != null)
                            {
                                prodInDb.Stock = prodInDb.Stock >= item.Quantity ? prodInDb.Stock - item.Quantity : 0;
                                inventoryAlertBody += $"<li>{prodInDb.ProductName}: Đã bán <b>{item.Quantity}</b> -> Tồn kho còn: <b>{prodInDb.Stock}</b></li>";
                                stockUpdated = true;
                            }
                        }
                    }
                    inventoryAlertBody += "</ul>";

                    // Gửi mail cho bộ phận Kho (Bạn có thể thay đổi email này)
                    if (stockUpdated)
                    {
                        await _emailService.SendSimpleTextEmailAsync("kho@perfumestore.vn", $"[Hệ thống] Trừ kho thành công - Đơn #{order.OrderId}", inventoryAlertBody);
                    }

                    // Tác vụ 3.3: Tích điểm cho Khách hàng & Gửi Email thông báo cộng điểm
                    var customerDb = await _context.Customers.FirstOrDefaultAsync(c => c.Email == customerEmail);
                    if (customerDb != null)
                    {
                        int pointsEarned = (int)(total / 100000);
                        if (pointsEarned > 0)
                        {
                            customerDb.SpinNumber = (customerDb.SpinNumber ?? 0) + pointsEarned;

                            // Gửi mail báo cộng điểm cho Khách hàng
                            if (!string.IsNullOrEmpty(customerEmail))
                            {
                                string pointsSub = $"Bạn vừa được cộng {pointsEarned} điểm thưởng!";
                                string pointsBody = $"<h3>Chúc mừng {model.CustomerName}!</h3>" +
                                                    $"<p>Hệ thống đã cộng <b>{pointsEarned} điểm</b> vào tài khoản của bạn từ đơn hàng #{order.OrderId}.</p>" +
                                                    $"<p>Tổng điểm hiện tại của bạn là: <b>{customerDb.SpinNumber} điểm</b>.</p>" +
                                                    $"<p>Đăng nhập vào website để xem và sử dụng điểm ngay nhé!</p>";
                                await _emailService.SendSimpleTextEmailAsync(customerEmail, pointsSub, pointsBody);
                            }
                        }
                    }

                    // Lưu thay đổi kho và điểm vào Database
                    await _context.SaveChangesAsync();
                }
                catch (Exception bgEx)
                {
                    _logger.LogError(bgEx, "Lỗi khi chạy tác vụ nền (Trừ kho/Cộng điểm/Gửi mail) sau khi tạo đơn");
                }
                // --- KẾT THÚC KHỐI THỰC THI THẬT ---

                // --- 4. CHUYỂN HƯỚNG THEO PHƯƠNG THỨC THANH TOÁN (STRATEGY PATTERN) ---
                var orderInfo = new { OrderId = order.OrderId.ToString(), OrderDate = order.OrderDate, Items = cart.ToList(), Total = order.TotalAmount };
                HttpContext.Session.SetString("LAST_ORDER", JsonSerializer.Serialize(orderInfo));

                var paymentContext = new PerfumeStore.DesignPatterns.Strategy.PaymentContext();

                if (model.PaymentMethod == "BANK_TRANSFER")
                {
                    HttpContext.Session.SetString("PENDING_ORDER_ID", order.OrderId.ToString());
                    HttpContext.Session.SetString("PENDING_ORDER_AMOUNT", order.TotalAmount.ToString());
                    paymentContext.SetStrategy(new PerfumeStore.DesignPatterns.Strategy.PayOsPaymentStrategy());
                }
                else
                {
                    fullCart.RemoveAll(i => cart.Any(c => c.ImageUrl == i.ImageUrl));
                    SaveCartToSession(fullCart);

                    HttpContext.Session.Remove("AppliedVoucher");
                    HttpContext.Session.Remove("CHECKOUT_SELECTED_ITEMS");

                    paymentContext.SetStrategy(new PerfumeStore.DesignPatterns.Strategy.CodPaymentStrategy());
                }

                var routeResult = paymentContext.ExecuteRouting(order);
                return RedirectToAction(routeResult.ActionName, routeResult.ControllerName);
            }
            catch (Exception ex)
            {
                var realError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                _logger.LogError(ex, "Lỗi nghiêm trọng khi tạo đơn hàng: " + realError);
                TempData["Error"] = "Lỗi khi lưu đơn hàng: " + realError;

                model.CartItems = cart;
                return View("Checkout", model);
            }
        }

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

                var order = await _orderService.GetOrderByOrderIdAsync(orderId);
                if (order != null)
                {
                    var subtotal = order.OrderDetails.Sum(od => od.TotalPrice);
                    var vatFee = await _context.Fees.FirstOrDefaultAsync(f => f.Name == "VAT");
                    decimal vat = vatFee != null ? subtotal * Math.Min(vatFee.Value, 100) / 100 : 0m;

                    var shippingFee = await _context.Fees.FirstOrDefaultAsync(f => f.Name == "Shipping");
                    decimal shipping = 0m;
                    if (shippingFee != null)
                    {
                        var threshold = shippingFee.Threshold ?? 5000000m;
                        shipping = subtotal >= threshold ? 0m : shippingFee.Value;
                    }

                    decimal discount = order.Coupon?.DiscountAmount ?? 0m;
                    var total = subtotal - discount + vat + shipping;

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
                        Customer = new { Name = order.Customer.Name, Email = order.Customer.Email, Phone = order.Customer.Phone },
                        Address = new { RecipientName = order.Address.RecipientName, Phone = order.Address.Phone, Province = order.Address.Province, District = order.Address.District, Ward = order.Address.Ward, AddressLine = order.Address.AddressLine },
                        OrderDetails = order.OrderDetails.Select(od => new
                        {
                            ProductName = od.Product.ProductName,
                            Quantity = od.Quantity,
                            UnitPrice = od.UnitPrice,
                            TotalPrice = od.TotalPrice,
                            ImageUrl = od.Product.ProductImages?.FirstOrDefault()?.ImageData != null
                                ? $"data:{od.Product.ProductImages.First().ImageMimeType};base64,{Convert.ToBase64String(od.Product.ProductImages.First().ImageData)}"
                                : "/images/ProductSummary/chanel-bleu-de-chanel-edp-100-ml.webp"
                        }).ToList(),
                        Coupon = order.Coupon != null ? new { Code = order.Coupon.Code, DiscountAmount = order.Coupon.DiscountAmount } : null
                    };

                    return View(orderViewModel);
                }
                else
                {
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
        public IActionResult GetAppliedVoucherApi()
        {
            try
            {
                var voucher = GetAppliedVoucher();
                if (voucher != null) return Json(new { success = true, voucher = voucher });
                return Json(new { success = false, message = "Không có voucher nào được áp dụng" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"❌ Lỗi: {ex.Message}" });
            }
        }

        [HttpGet]
        public IActionResult GetCheckoutSummary()
        {
            try
            {
                var cart = GetCartFromSession();

                var selectedItemsJson = HttpContext.Session.GetString("CHECKOUT_SELECTED_ITEMS");
                if (!string.IsNullOrEmpty(selectedItemsJson))
                {
                    var selectedUrls = JsonSerializer.Deserialize<List<string>>(selectedItemsJson) ?? new List<string>();
                    if (selectedUrls.Any())
                    {
                        cart = cart.Where(i => selectedUrls.Contains(i.ImageUrl)).ToList();
                    }
                }

                var appliedVoucher = GetAppliedVoucher();

                var subtotal = cart.Sum(item => item.LineTotal);
                var discount = CalculateDiscount(subtotal, appliedVoucher);
                var shippingFee = CalculateShippingFee(subtotal, appliedVoucher);
                var vat = CalculateVAT(subtotal);
                var total = subtotal - discount + shippingFee + vat;

                return Json(new { success = true, subtotal = subtotal, discount = discount, shippingFee = shippingFee, vat = vat, total = total });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting checkout summary");
                return Json(new { success = false, message = $"❌ Lỗi: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDefaultAddress()
        {
            try
            {
                if (!User.Identity.IsAuthenticated) return Json(new { success = false, message = "Chưa đăng nhập" });

                var customerEmail = User.Identity.Name;
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Email == customerEmail);
                if (customer == null) return Json(new { success = false, message = "Không tìm thấy khách hàng" });

                var defaultAddress = await _context.ShippingAddresses.FirstOrDefaultAsync(sa => sa.CustomerId == customer.CustomerId && sa.IsDefault);
                if (defaultAddress == null) return Json(new { success = false, message = "Không có địa chỉ mặc định" });

                return Json(new
                {
                    success = true,
                    address = new { customerName = defaultAddress.RecipientName, phone = defaultAddress.Phone, province = defaultAddress.Province, district = defaultAddress.District, ward = defaultAddress.Ward, addressLine = defaultAddress.AddressLine }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra khi lấy địa chỉ mặc định" });
            }
        }

        [HttpGet]
        public IActionResult TestCheckout() => View();

        [HttpGet]
        public IActionResult TestRealCheckout() => View();

        [HttpGet]
        public async Task<IActionResult> AddRealProductsToCart()
        {
            try
            {
                var products = await _context.Products.Where(p => p.IsPublished == true).Take(2).ToListAsync();
                if (products.Count == 0) return Json(new { success = false, message = "Không có sản phẩm nào trong database" });

                var cart = GetCartFromSession();
                cart.Clear();

                foreach (var product in products)
                {
                    cart.Add(new CartItem { ProductId = product.ProductId, ImageUrl = "/images/Checkout/product1.jpg", ProductName = product.ProductName, Description = product.DescriptionNo1 ?? "Sản phẩm từ database", Quantity = 1, UnitPrice = product.Price });
                }

                SaveCartToSession(cart);
                return Json(new { success = true, message = $"Đã thêm {cart.Count} sản phẩm thật vào giỏ hàng", productCount = cart.Count });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDatabaseProducts()
        {
            try
            {
                var productCount = await _context.Products.CountAsync();
                var publishedCount = await _context.Products.CountAsync(p => p.IsPublished == true);
                return Json(new { success = true, productCount = productCount, publishedCount = publishedCount, message = $"Database có {productCount} sản phẩm, {publishedCount} đã publish" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        [HttpGet]
        public IActionResult ClearCart()
        {
            try
            {
                var cart = GetCartFromSession();
                cart.Clear();
                SaveCartToSession(cart);
                return Json(new { success = true, message = "Đã xóa giỏ hàng thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }
    }
}
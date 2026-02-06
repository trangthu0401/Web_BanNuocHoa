# Câu hỏi và Trả lời về Chức năng Đặt hàng, Thanh toán và Bảo mật

## PHẦN 1: CHỨC NĂNG GIỎ HÀNG (CART)

### Q1: Giỏ hàng được lưu trữ như thế nào? Có những hạn chế gì?

**Trả lời:**
- Giỏ hàng được lưu trong **Session** (không phải database) với key `"CART_SESSION"`
- Dữ liệu được serialize thành JSON và lưu trong `HttpContext.Session`
- **Hạn chế:**
  - Mỗi sản phẩm tối đa **10 sản phẩm** (giới hạn để tránh spam đơn hàng)
  - Giỏ hàng sẽ mất khi session hết hạn hoặc người dùng đóng trình duyệt
  - Không đồng bộ giữa các thiết bị/trình duyệt khác nhau

**Code liên quan:**
```csharp
private const string CartSessionKey = "CART_SESSION";
private List<CartItem> GetCartFromSession()
{
    var json = HttpContext.Session.GetString(CartSessionKey);
    if (string.IsNullOrEmpty(json)) return new List<CartItem>();
    try { return JsonSerializer.Deserialize<List<CartItem>>(json) ?? new List<CartItem>(); }
    catch { return new List<CartItem>(); }
}
```

---

### Q2: Khi thêm sản phẩm vào giỏ hàng, hệ thống kiểm tra gì?

**Trả lời:**
Hệ thống kiểm tra **3 điều kiện**:

1. **Kiểm tra Stock (tồn kho):**
   - Nếu có `ProductId`, hệ thống kiểm tra số lượng tồn kho từ database
   - Nếu số lượng yêu cầu > Stock, trả về lỗi: "Sản phẩm chỉ còn X sản phẩm trong kho"
   - Tính cả số lượng đã có trong giỏ hàng hiện tại

2. **Kiểm tra số lượng tối đa:**
   - Mỗi sản phẩm tối đa 10 sản phẩm
   - Nếu vượt quá, trả về lỗi: "Số lượng tối đa là 10 sản phẩm"

3. **Kiểm tra sản phẩm tồn tại:**
   - Nếu có `ProductId`, kiểm tra sản phẩm có tồn tại trong database không
   - Nếu không tìm thấy, trả về lỗi: "Không tìm thấy sản phẩm trong hệ thống"

**Code:**
```csharp
// Kiểm tra Stock
if (requestedQuantity > product.Stock)
{
    var errorMsg = $"Sản phẩm chỉ còn {product.Stock} sản phẩm trong kho...";
    return Json(new { success = false, message = errorMsg });
}

// Kiểm tra số lượng tối đa
if (requestedQuantity > 10)
{
    var errorMsg = "Số lượng tối đa là 10 sản phẩm.";
    return Json(new { success = false, message = errorMsg });
}
```

---

### Q3: Khi cập nhật số lượng trong giỏ hàng, hệ thống xử lý như thế nào?

**Trả lời:**
1. **Giới hạn số lượng:** Tự động điều chỉnh về khoảng 1-10
2. **Kiểm tra Stock:** 
   - Nếu số lượng yêu cầu > Stock, tự động điều chỉnh về số lượng tối đa có trong kho
   - Nếu Stock = 0, tự động xóa sản phẩm khỏi giỏ hàng
3. **Trả về thông báo:** Thông báo cho người dùng nếu số lượng bị điều chỉnh

**Code:**
```csharp
// Điều chỉnh số lượng
var requestedQuantity = Math.Max(1, Math.Min(10, quantity));

// Kiểm tra Stock
if (requestedQuantity > product.Stock)
{
    requestedQuantity = product.Stock;
    if (requestedQuantity < 1)
    {
        cart.Remove(item); // Xóa nếu hết hàng
        return Json(new { ok = false, message = "Sản phẩm đã hết hàng..." });
    }
}
```

---

## PHẦN 2: QUY TRÌNH CHECKOUT

### Q4: Trình bày quy trình checkout từ đầu đến cuối?

**Trả lời:**

**Bước 1: Kiểm tra điều kiện**
- Kiểm tra đăng nhập (yêu cầu đăng nhập để checkout)
- Kiểm tra giỏ hàng không rỗng
- Xử lý voucher từ URL hoặc session (nếu có)

**Bước 2: Tính toán phí**
- **Subtotal:** Tổng giá sản phẩm trong giỏ
- **Discount:** Giảm giá từ voucher (percent/amount/freeship)
- **Shipping Fee:** Phí vận chuyển (miễn phí nếu đơn >= 5,000,000đ hoặc có voucher freeship)
- **VAT:** Thuế VAT tính trên subtotal (lấy từ database)
- **Total:** Subtotal - Discount + Shipping + VAT

**Bước 3: Hiển thị form checkout**
- Form nhập thông tin khách hàng (tên, SĐT, email)
- Form nhập địa chỉ giao hàng (tỉnh/thành, quận/huyện, phường/xã, địa chỉ chi tiết)
- Chọn phương thức thanh toán (COD hoặc BANK_TRANSFER)
- Áp dụng/xóa voucher

**Bước 4: Xử lý đơn hàng (ProcessCheckout)**
- Validate form
- Tìm hoặc tạo Customer
- Tạo ShippingAddress
- Tìm hoặc tạo Coupon (nếu có voucher)
- **Kiểm tra Stock** trước khi tạo đơn
- Tạo Order với trạng thái phù hợp
- Tạo OrderDetails và **giảm Stock**
- Lưu vào database

**Bước 5: Xử lý thanh toán**
- **COD:** Xóa giỏ hàng → Redirect đến PaymentSuccess
- **BANK_TRANSFER:** Lưu OrderId vào session → Redirect đến PaymentController

---

### Q5: Các loại phí được tính như thế nào?

**Trả lời:**

1. **VAT (Thuế VAT):**
   - Lấy từ bảng `Fees` trong database (Name = "VAT")
   - Tính trên **subtotal** (trước khi trừ voucher)
   - Công thức: `VAT = subtotal * (VAT_percent / 100)`
   - Tối đa 100%

2. **Shipping Fee (Phí vận chuyển):**
   - Lấy từ bảng `Fees` trong database (Name = "Shipping")
   - Miễn phí nếu:
     - Subtotal >= Threshold (mặc định 5,000,000đ)
     - Hoặc có voucher freeship và subtotal >= 200,000đ
   - Công thức: `Shipping = (subtotal >= threshold) ? 0 : shippingFee.Value`

3. **Discount (Giảm giá):**
   - **Percent:** `discount = subtotal * (percent / 100)` (tối đa 100%)
   - **Amount:** `discount = min(voucher.Value, subtotal)`
   - **Freeship:** `discount = 0` (chỉ ảnh hưởng shipping fee)

4. **Total (Tổng tiền):**
   - `Total = Subtotal - Discount + Shipping + VAT`

**Code:**
```csharp
var subtotal = cart.Sum(item => item.LineTotal);
var discount = CalculateDiscount(subtotal, appliedVoucher);
var shippingFee = CalculateShippingFee(subtotal, appliedVoucher);
var vat = CalculateVAT(subtotal); // Tính trên subtotal
var total = subtotal - discount + shippingFee + vat;
```

---

### Q6: Voucher được xử lý như thế nào trong checkout?

**Trả lời:**

**Các cách áp dụng voucher:**
1. **Từ URL:** `?voucherCode=XXX` → Lưu vào session
2. **Từ vòng quay may mắn:** Lưu vào session với key `"AppliedVoucher"`
3. **Nhập mã thủ công:** (nếu có chức năng này)

**Lưu trữ:**
- Voucher được serialize thành JSON và lưu trong session
- Key: `"AppliedVoucher"`

**Xử lý khi checkout:**
1. Đọc voucher từ session
2. Tính discount dựa trên loại voucher
3. Tìm hoặc tạo Coupon trong database
4. Gắn CouponId vào Order
5. Đánh dấu coupon đã sử dụng (sau khi thanh toán thành công)

**Xóa voucher:**
- Sau khi đặt hàng thành công (COD)
- Sau khi thanh toán thành công (BANK_TRANSFER)
- Khi người dùng xóa voucher thủ công

**Code:**
```csharp
private VoucherModel? GetAppliedVoucher()
{
    var voucherJson = HttpContext.Session.GetString("AppliedVoucher");
    if (string.IsNullOrEmpty(voucherJson)) return null;
    return JsonSerializer.Deserialize<VoucherModel>(voucherJson);
}
```

---

## PHẦN 3: THANH TOÁN ĐƠN HÀNG

### Q7: Có những phương thức thanh toán nào? Khác nhau như thế nào?

**Trả lời:**

**1. COD (Cash on Delivery - Thanh toán khi nhận hàng):**
- **Trạng thái đơn:** "Đang xử lý"
- **Luồng xử lý:**
  ```
  ProcessCheckout → Tạo Order → Xóa giỏ hàng → Redirect PaymentSuccess
  ```
- **Đánh dấu coupon:** Ngay sau khi tạo đơn
- **Giảm Stock:** Ngay sau khi tạo OrderDetails
- **Không cần thanh toán trước**

**2. BANK_TRANSFER (Chuyển khoản ngân hàng qua PayOS):**
- **Trạng thái đơn:** "Chờ thanh toán"
- **Luồng xử lý:**
  ```
  ProcessCheckout → Tạo Order → Lưu OrderId vào session 
  → Redirect PaymentController → Tạo PayOS link 
  → User thanh toán trên PayOS 
  → Callback /payment-success → Cập nhật trạng thái "Đã thanh toán"
  ```
- **Đánh dấu coupon:** Sau khi thanh toán thành công
- **Giảm Stock:** Ngay sau khi tạo OrderDetails (trước khi thanh toán)
- **Yêu cầu thanh toán trước**

**Code:**
```csharp
if (model.PaymentMethod == "BANK_TRANSFER")
{
    order.Status = "Chờ thanh toán";
    HttpContext.Session.SetString("PENDING_ORDER_ID", order.OrderId.ToString());
    return RedirectToAction("CreatePaymentProgress", "Payment");
}
else
{
    order.Status = "Đang xử lý";
    return RedirectToAction(nameof(PaymentSuccess));
}
```

---

### Q8: Quy trình tích hợp PayOS như thế nào?

**Trả lời:**

**Bước 1: Tạo payment link (CreatePaymentProgress)**
- Lấy OrderId và TotalAmount từ session
- Lấy chi tiết đơn hàng từ database
- Tạo danh sách items cho PayOS
- Tạo PaymentData với:
  - OrderId (dùng làm orderCode)
  - Amount (tổng tiền)
  - Description
  - Items (danh sách sản phẩm)
  - Cancel URL: `/cancel-payment`
  - Success URL: `/payment-success`
- Gọi `_payOS.createPaymentLink()` để tạo link
- Redirect user đến PayOS checkout URL

**Bước 2: User thanh toán trên PayOS**
- User điền thông tin thanh toán
- PayOS xử lý giao dịch

**Bước 3: Callback từ PayOS**
- **Thành công:** `/payment-success?orderCode=XXX`
  - Validate orderCode với OrderId trong session
  - Cập nhật Order.Status = "Đã thanh toán"
  - Đánh dấu coupon đã sử dụng
  - Xóa giỏ hàng
  - Hiển thị thông tin đơn hàng

- **Hủy:** `/cancel-payment`
  - Cập nhật Order.Status = "Đã hủy"
  - Xóa session
  - Hiển thị thông báo

**Code:**
```csharp
PaymentData paymentData = new PaymentData(
    orderId,                    // orderCode
    (int)amount,                // amount
    $"Thanh toan don hang #{orderId}",
    items,                      // danh sách sản phẩm
    $"{baseUrl}/cancel-payment",
    $"{baseUrl}/payment-success"
);

CreatePaymentResult result = await _payOS.createPaymentLink(paymentData);
return Redirect(result.checkoutUrl);
```

---

### Q9: Khi nào Stock được giảm? Có rủi ro gì không?

**Trả lời:**

**Thời điểm giảm Stock:**
- Stock được giảm **NGAY SAU KHI TẠO OrderDetails** trong `ProcessCheckout`
- Áp dụng cho cả COD và BANK_TRANSFER
- **KHÔNG** chờ đến khi thanh toán thành công

**Rủi ro:**
1. **Đơn BANK_TRANSFER bị hủy:**
   - Stock đã bị giảm nhưng đơn bị hủy
   - **Giải pháp:** Cần có cơ chế hoàn trả Stock khi hủy đơn (hiện tại chưa có)

2. **Race condition:**
   - Nhiều user cùng đặt sản phẩm cuối cùng
   - **Giải pháp:** Sử dụng transaction và kiểm tra Stock trước khi giảm

3. **Đơn "Chờ thanh toán" không thanh toán:**
   - Stock bị giảm nhưng không có tiền
   - **Giải pháp:** Có thể thêm timeout để tự động hủy đơn và hoàn trả Stock

**Code:**
```csharp
// Kiểm tra Stock trước
if (product.Stock < cartItem.Quantity)
{
    throw new InvalidOperationException("Sản phẩm chỉ còn X sản phẩm...");
}

// Giảm Stock
product.Stock -= cartItem.Quantity;
```

---

## PHẦN 4: QUẢN LÝ ĐƠN HÀNG

### Q10: Các trạng thái đơn hàng là gì? Luồng chuyển trạng thái như thế nào?

**Trả lời:**

**Các trạng thái:**
1. **"Chờ thanh toán":** Đơn BANK_TRANSFER chưa thanh toán
2. **"Đang xử lý":** Đơn COD hoặc đơn đã thanh toán, đang chờ xử lý
3. **"Đã thanh toán":** Đơn BANK_TRANSFER đã thanh toán thành công
4. **"Đã hủy":** Đơn bị hủy (từ PayOS hoặc admin)

**Luồng chuyển trạng thái:**

**COD:**
```
Tạo đơn → "Đang xử lý" → Admin xử lý → "Đã giao hàng"
```

**BANK_TRANSFER:**
```
Tạo đơn → "Chờ thanh toán" → Thanh toán thành công → "Đã thanh toán" 
→ Admin xử lý → "Đã giao hàng"
```

**Hủy đơn:**
```
"Chờ thanh toán" → User hủy trên PayOS → "Đã hủy"
```

**Code:**
```csharp
// Khi tạo đơn
order.Status = model.PaymentMethod == "BANK_TRANSFER" 
    ? "Chờ thanh toán" 
    : "Đang xử lý";

// Sau khi thanh toán thành công
order.Status = "Đã thanh toán";
```

---

### Q11: Đơn hàng được lưu trữ như thế nào trong database?

**Trả lời:**

**Cấu trúc database:**

1. **Order (Bảng đơn hàng):**
   - OrderId (PK)
   - CustomerId (FK → Customer)
   - AddressId (FK → ShippingAddress)
   - CouponId (FK → Coupon, nullable)
   - OrderDate
   - TotalAmount
   - PaymentMethod
   - Status
   - Notes

2. **OrderDetail (Chi tiết đơn hàng):**
   - OrderDetailId (PK)
   - OrderId (FK → Order)
   - ProductId (FK → Product)
   - Quantity
   - UnitPrice
   - TotalPrice

3. **ShippingAddress (Địa chỉ giao hàng):**
   - AddressId (PK)
   - CustomerId (FK → Customer)
   - RecipientName
   - Phone
   - Province, District, Ward
   - AddressLine
   - IsDefault

4. **Customer (Khách hàng):**
   - CustomerId (PK)
   - Name, Email, Phone
   - CreatedDate

5. **Coupon (Mã giảm giá):**
   - CouponId (PK)
   - Code
   - DiscountAmount
   - IsUsed
   - UsedDate

**Quan hệ:**
- Order 1 → N OrderDetails
- Order N → 1 Customer
- Order N → 1 ShippingAddress
- Order N → 1 Coupon (nullable)

**Code:**
```csharp
var order = await _context.Orders
    .Include(o => o.Customer)
    .Include(o => o.Address)
    .Include(o => o.Coupon)
    .Include(o => o.OrderDetails)
        .ThenInclude(od => od.Product)
    .FirstOrDefaultAsync(o => o.OrderId == orderId);
```

---

## PHẦN 5: BẢO MẬT

### Q12: Các biện pháp bảo mật nào được áp dụng cho chức năng đặt hàng?

**Trả lời:**

**1. Anti-Forgery Token (CSRF Protection):**
- Tất cả các action POST đều có `[ValidateAntiForgeryToken]`
- Bảo vệ khỏi tấn công CSRF
- Token được tạo tự động trong form và validate khi submit

**Code:**
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> ProcessCheckout(CheckoutViewModel model)
{
    // ...
}
```

**2. Session-based Order Validation:**
- OrderId được lưu trong session (`PENDING_ORDER_ID`)
- Khi callback từ PayOS, validate orderCode với OrderId trong session
- Ngăn chặn user giả mạo orderCode để xem đơn hàng của người khác

**Code:**
```csharp
// Lấy OrderId từ session (an toàn)
var orderIdStr = HttpContext.Session.GetString("PENDING_ORDER_ID");

// Validate với orderCode từ PayOS
if (payOSOrderId != orderId)
{
    _logger.LogWarning("SECURITY ALERT - OrderCode mismatch!");
    return RedirectToAction("Index", "Cart");
}
```

**3. Authentication Check:**
- Trang Checkout yêu cầu đăng nhập
- Tuy nhiên, ProcessCheckout cho phép guest checkout (có thể cải thiện)

**Code:**
```csharp
public IActionResult Checkout()
{
    if (!User.Identity.IsAuthenticated)
    {
        TempData["Error"] = "Vui lòng đăng nhập để tiếp tục đặt hàng";
        return RedirectToAction("Login", "Auth");
    }
    // ...
}
```

**4. Stock Validation:**
- Kiểm tra Stock trước khi thêm vào giỏ
- Kiểm tra Stock trước khi tạo đơn
- Ngăn chặn đặt hàng vượt quá tồn kho

**5. Input Validation:**
- ModelState validation cho form checkout
- Validate số lượng (1-10)
- Validate email, phone format (nếu có)

**6. Transaction (Database):**
- Sử dụng transaction khi tạo đơn hàng
- Đảm bảo tính toàn vẹn dữ liệu
- Rollback nếu có lỗi

**Code:**
```csharp
using var transaction = await _context.Database.BeginTransactionAsync();
try
{
    // Tạo đơn hàng
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

**7. Logging:**
- Log các sự kiện quan trọng (tạo đơn, thanh toán, lỗi)
- Log cảnh báo bảo mật (OrderCode mismatch)
- Giúp phát hiện và điều tra sự cố

**Code:**
```csharp
_logger.LogWarning($"SECURITY ALERT - OrderCode mismatch! Session OrderId: {orderId}, PayOS OrderCode: {payOSOrderId}");
```

---

### Q13: Có điểm yếu bảo mật nào cần lưu ý không?

**Trả lời:**

**1. Guest Checkout:**
- ProcessCheckout cho phép checkout mà không cần đăng nhập
- **Rủi ro:** Khó kiểm soát và xác thực người dùng
- **Khuyến nghị:** Nên yêu cầu đăng nhập hoặc xác thực email/SĐT

**2. Session Hijacking:**
- Giỏ hàng lưu trong session có thể bị đánh cắp
- **Rủi ro:** Người khác có thể truy cập giỏ hàng nếu có session ID
- **Khuyến nghị:** Sử dụng HTTPS, HttpOnly cookies, Secure cookies

**3. Stock Race Condition:**
- Nhiều request cùng lúc có thể vượt qua kiểm tra Stock
- **Rủi ro:** Bán quá số lượng tồn kho
- **Khuyến nghị:** Sử dụng database lock hoặc optimistic concurrency

**4. Order Manipulation:**
- User có thể thay đổi giá, số lượng qua request
- **Rủi ro:** Đặt hàng với giá/số lượng sai
- **Khuyến nghị:** Luôn lấy giá từ database, không tin tưởng client

**5. PayOS Callback Validation:**
- Hiện tại chỉ validate orderCode với session
- **Rủi ro:** Nếu session bị mất, không thể xác thực
- **Khuyến nghị:** Nên có webhook từ PayOS để xác thực chữ ký

**6. Coupon Abuse:**
- Coupon có thể bị sử dụng nhiều lần nếu không kiểm tra kỹ
- **Rủi ro:** Giảm giá không đúng
- **Khuyến nghị:** Kiểm tra IsUsed và ExpiryDate trước khi áp dụng

---

### Q14: Làm thế nào để đảm bảo tính toàn vẹn dữ liệu khi tạo đơn hàng?

**Trả lời:**

**1. Database Transaction:**
- Sử dụng transaction để đảm bảo tất cả thao tác thành công hoặc rollback
- Code:
```csharp
using var transaction = await _context.Database.BeginTransactionAsync();
try
{
    // Tạo Customer
    // Tạo ShippingAddress
    // Tạo Order
    // Tạo OrderDetails
    // Giảm Stock
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

**2. Kiểm tra Stock trước khi giảm:**
- Kiểm tra Stock trước khi tạo OrderDetails
- Nếu không đủ, throw exception và rollback

**3. Validate dữ liệu:**
- Validate ModelState trước khi xử lý
- Validate số lượng, giá, thông tin khách hàng

**4. Kiểm tra lại sau khi lưu:**
- Sau khi thanh toán thành công, kiểm tra lại từ database
- Đảm bảo dữ liệu đã được lưu đúng

**Code:**
```csharp
// Kiểm tra lại từ database
var verifiedOrder = await _context.Orders
    .Include(o => o.OrderDetails)
    .FirstOrDefaultAsync(o => o.OrderId == orderId);

if (verifiedOrder == null || verifiedOrder.Status != "Đã thanh toán")
{
    _logger.LogError("Verification failed");
    TempData["Error"] = "Xác thực đơn hàng không thành công";
    return RedirectToAction("Index", "Cart");
}
```

---

## PHẦN 6: CÂU HỎI TỔNG HỢP

### Q15: Nếu user đặt hàng nhưng không thanh toán (BANK_TRANSFER), hệ thống xử lý như thế nào?

**Trả lời:**

**Hiện tại:**
- Đơn hàng vẫn tồn tại với trạng thái "Chờ thanh toán"
- Stock đã bị giảm
- Coupon chưa được đánh dấu đã sử dụng

**Vấn đề:**
- Stock bị "khóa" nhưng không có tiền
- Có thể dẫn đến thiếu hàng cho khách hàng khác

**Giải pháp đề xuất:**
1. **Timeout tự động:**
   - Đặt timeout 24-48 giờ
   - Tự động hủy đơn và hoàn trả Stock

2. **Reserve Stock:**
   - Tách Stock thành Available và Reserved
   - Chỉ giảm Available khi thanh toán thành công

3. **Admin can thiệp:**
   - Admin có thể hủy đơn và hoàn trả Stock thủ công

---

### Q16: Làm thế nào để xử lý khi nhiều user cùng đặt sản phẩm cuối cùng?

**Trả lời:**

**Vấn đề (Race Condition):**
- User A và B cùng kiểm tra Stock = 1
- Cả hai đều pass kiểm tra
- Cả hai đều giảm Stock → Stock = -1 (sai)

**Giải pháp:**

**1. Database Lock (Pessimistic Locking):**
```csharp
var product = await _context.Products
    .FromSqlRaw("SELECT * FROM Products WHERE ProductId = {0} FOR UPDATE", productId)
    .FirstOrDefaultAsync();
```

**2. Optimistic Concurrency:**
- Thêm RowVersion vào Product
- Kiểm tra version trước khi update
- Nếu version khác, throw exception

**3. Transaction Isolation Level:**
```csharp
using var transaction = await _context.Database.BeginTransactionAsync(
    IsolationLevel.Serializable);
```

**4. Kiểm tra lại trong transaction:**
```csharp
// Trong transaction
var product = await _context.Products.FindAsync(productId);
if (product.Stock < quantity)
{
    throw new InvalidOperationException("Hết hàng");
}
product.Stock -= quantity;
```

---

### Q17: Nếu PayOS callback bị lỗi hoặc không gọi được, hệ thống xử lý như thế nào?

**Trả lời:**

**Vấn đề:**
- User đã thanh toán trên PayOS
- Nhưng callback `/payment-success` không được gọi
- Đơn hàng vẫn ở trạng thái "Chờ thanh toán"

**Giải pháp:**

**1. Webhook từ PayOS:**
- PayOS gửi webhook khi thanh toán thành công
- Xử lý webhook để cập nhật trạng thái
- Không phụ thuộc vào user callback

**2. Polling:**
- Định kỳ kiểm tra trạng thái thanh toán từ PayOS API
- Cập nhật đơn hàng nếu đã thanh toán

**3. User có thể kiểm tra:**
- Cho phép user kiểm tra trạng thái đơn hàng
- Nếu đã thanh toán nhưng đơn chưa cập nhật, có thể báo admin

**4. Logging và monitoring:**
- Log tất cả callback từ PayOS
- Cảnh báo nếu callback không đến

---

### Q18: Làm thế nào để test chức năng đặt hàng và thanh toán?

**Trả lời:**

**1. Test endpoints có sẵn:**
- `/Cart/AddTestProducts` - Thêm sản phẩm test vào giỏ
- `/Cart/TestFullCheckoutFlow` - Test toàn bộ luồng checkout
- `/Cart/TestDatabaseConnection` - Kiểm tra kết nối database

**2. Test thủ công:**
- Thêm sản phẩm vào giỏ
- Đi đến checkout
- Điền form và submit
- Kiểm tra đơn hàng trong database

**3. Test PayOS:**
- Sử dụng PayOS sandbox/test mode
- Test các trường hợp: thành công, hủy, lỗi

**4. Test bảo mật:**
- Test CSRF token
- Test session validation
- Test Stock validation
- Test input validation

**5. Test edge cases:**
- Giỏ hàng rỗng
- Stock = 0
- Số lượng > Stock
- Voucher hết hạn
- Đơn hàng bị hủy

---

## TÓM TẮT CÁC ĐIỂM QUAN TRỌNG

### ✅ Điểm mạnh:
1. ✅ Kiểm tra Stock ở nhiều bước (thêm vào giỏ, cập nhật, checkout)
2. ✅ Sử dụng transaction để đảm bảo tính toàn vẹn
3. ✅ Anti-forgery token cho tất cả POST requests
4. ✅ Session validation cho PayOS callback
5. ✅ Logging đầy đủ cho debugging và security

### ⚠️ Điểm cần cải thiện:
1. ⚠️ Guest checkout không yêu cầu xác thực
2. ⚠️ Stock bị giảm trước khi thanh toán (BANK_TRANSFER)
3. ⚠️ Chưa có cơ chế hoàn trả Stock khi hủy đơn
4. ⚠️ Chưa có webhook từ PayOS để xác thực thanh toán
5. ⚠️ Race condition có thể xảy ra khi nhiều user cùng đặt

### 🔒 Bảo mật:
1. ✅ CSRF Protection (Anti-forgery token)
2. ✅ Session-based validation
3. ✅ Stock validation
4. ✅ Input validation
5. ⚠️ Cần thêm HTTPS enforcement
6. ⚠️ Cần thêm rate limiting

---

**Chúc bạn vấn đáp thành công! 🎉**


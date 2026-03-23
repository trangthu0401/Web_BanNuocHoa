using Microsoft.EntityFrameworkCore;
using PerfumeStore.Models;
using PerfumeStore.Models.ViewModels;

namespace PerfumeStore.Services
{
    public class OrderService : IOrderService
    {
        private readonly PerfumeStoreContext _context;
        private readonly ILogger<OrderService> _logger;

        public OrderService(PerfumeStoreContext context, ILogger<OrderService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Order> CreateOrderAsync(CheckoutViewModel model, string customerEmail, List<CartItem> cartItems, VoucherModel? appliedVoucher)
        {
            Console.WriteLine($"OrderService: Starting order creation for email: {customerEmail}");
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Tìm hoặc tạo customer
                Console.WriteLine($"OrderService: Looking for customer with email: {customerEmail}");
                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.Email == customerEmail);

                if (customer == null)
                {
                    Console.WriteLine($"OrderService: Creating new customer: {model.CustomerName}");
                    customer = new Customer
                    {
                        Name = model.CustomerName,
                        Email = customerEmail,
                        Phone = model.CustomerPhone,
                        CreatedDate = DateTime.Now,
                        MembershipId = 1 // Default membership
                    };
                    _context.Customers.Add(customer);
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"OrderService: Customer created with ID: {customer.CustomerId}");
                }
                else
                {
                    Console.WriteLine($"OrderService: Found existing customer: {customer.CustomerId}");
                    // Cập nhật thông tin customer nếu cần
                    customer.Name = model.CustomerName;
                    customer.Phone = model.CustomerPhone;
                }

                // Tạo shipping address
                Console.WriteLine($"OrderService: Creating shipping address");
                var shippingAddress = new ShippingAddress
                {
                    CustomerId = customer.CustomerId,
                    RecipientName = model.CustomerName,
                    Phone = model.CustomerPhone,
                    Province = model.Province,
                    District = model.District,
                    Ward = model.Ward,
                    AddressLine = model.ShippingAddress,
                    IsDefault = model.SaveAsDefaultAddress
                };

                // Nếu đây là địa chỉ mặc định, bỏ default của các địa chỉ khác
                if (model.SaveAsDefaultAddress)
                {
                    var existingAddresses = await _context.ShippingAddresses
                        .Where(sa => sa.CustomerId == customer.CustomerId)
                        .ToListAsync();

                    foreach (var existingAddress in existingAddresses)
                    {
                        existingAddress.IsDefault = false;
                    }
                }

                _context.ShippingAddresses.Add(shippingAddress);
                await _context.SaveChangesAsync();
                Console.WriteLine($"OrderService: Shipping address created with ID: {shippingAddress.AddressId}");

                // Tìm coupon nếu có voucher
                Coupon? coupon = null;
                if (appliedVoucher != null)
                {
                    coupon = await _context.Coupons
                        .FirstOrDefaultAsync(c => c.Code == appliedVoucher.Code);

                    if (coupon == null)
                    {
                        // Tạo coupon mới nếu chưa tồn tại
                        coupon = new Coupon
                        {
                            Code = appliedVoucher.Code,
                            DiscountAmount = appliedVoucher.Type == "amount" ? appliedVoucher.Value : 0,
                            CreatedDate = DateTime.Now,
                            ExpiryDate = DateTime.Now.AddDays(30),
                            IsUsed = false
                        };
                        _context.Coupons.Add(coupon);
                        await _context.SaveChangesAsync();
                    }
                }

                // Tính toán tổng tiền
                var subtotal = cartItems.Sum(item => item.LineTotal);
                var discount = CalculateDiscount(subtotal, appliedVoucher);
                var shippingFee = CalculateShippingFee(subtotal, appliedVoucher);
                var vat = CalculateVAT(subtotal); // VAT tính trên giá gốc (trước khi trừ voucher)
                var total = subtotal - discount + shippingFee + vat;

                // Tạo order
                Console.WriteLine($"OrderService: Creating order with total: {total}");
                var order = new Order
                {
                    CustomerId = customer.CustomerId,
                    AddressId = shippingAddress.AddressId,
                    CouponId = coupon?.CouponId,
                    OrderDate = DateTime.Now,
                    TotalAmount = total,
                    PaymentMethod = "Thanh toán khi nhận hàng",
                    Status = "Chờ xử lý",
                    Notes = model.OrderNotes
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();
                Console.WriteLine($"OrderService: Order created with ID: {order.OrderId}");

                // Tạo order details và giảm Stock
                foreach (var cartItem in cartItems)
                {
                    Product? product = null;

                    // Ưu tiên tìm theo ProductId nếu có
                    if (cartItem.ProductId > 0)
                    {
                        product = await _context.Products.FindAsync(cartItem.ProductId);
                    }

                    // Nếu không tìm thấy, tìm theo tên
                    if (product == null)
                    {
                        product = await _context.Products
                            .FirstOrDefaultAsync(p => p.ProductName == cartItem.ProductName);
                    }

                    if (product != null)
                    {
                        // Kiểm tra Stock trước khi tạo đơn
                        if (product.Stock < cartItem.Quantity)
                        {
                            throw new InvalidOperationException($"Sản phẩm '{product.ProductName}' chỉ còn {product.Stock} sản phẩm trong kho. Vui lòng điều chỉnh số lượng trong giỏ hàng.");
                        }

                        // Giảm Stock
                        product.Stock -= cartItem.Quantity;

                        var orderDetail = new OrderDetail
                        {
                            OrderId = order.OrderId,
                            ProductId = product.ProductId,
                            Quantity = cartItem.Quantity,
                            UnitPrice = cartItem.UnitPrice,
                            TotalPrice = cartItem.LineTotal
                        };
                        _context.OrderDetails.Add(orderDetail);
                    }
                    else
                    {
                        // Tạo product tạm thời nếu không tìm thấy
                        var tempProduct = new Product
                        {
                            ProductName = cartItem.ProductName,
                            Price = cartItem.UnitPrice,
                            Stock = 0,
                            IsPublished = false,
                            BrandId = 1, // Default brand
                            Scent = "Unknown",
                            WarrantyPeriodMonths = 0,
                            TopNote = "Unknown",
                            HeartNote = "Unknown",
                            BaseNote = "Unknown",
                            Concentration = "EDP",
                            Origin = "Unknown",
                            Style = "Unknown",
                            UsingOccasion = "Unknown",
                            Introduction = "Temporary product",
                            DescriptionNo1 = "Temporary product description",
                            DescriptionNo2 = "Temporary product description",
                            DiscountPrice = 0,
                            DiscountId = null,
                            Craftsman = "Unknown",
                            SuggestionName = "Unknown"
                        };
                        _context.Products.Add(tempProduct);
                        await _context.SaveChangesAsync();

                        var orderDetail = new OrderDetail
                        {
                            OrderId = order.OrderId,
                            ProductId = tempProduct.ProductId,
                            Quantity = cartItem.Quantity,
                            UnitPrice = cartItem.UnitPrice,
                            TotalPrice = cartItem.LineTotal
                        };
                        _context.OrderDetails.Add(orderDetail);

                        _logger.LogWarning($"Created temporary product for: {cartItem.ProductName}");
                    }
                }

                await _context.SaveChangesAsync();

                // Đánh dấu coupon đã dùng (nếu có)
                if (coupon != null)
                {
                    coupon.IsUsed = true;
                    coupon.UsedDate = DateTime.Now;
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
                Console.WriteLine($"OrderService: Order creation completed successfully with ID: {order.OrderId}");

                return order;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OrderService: Error creating order: {ex.Message}");
                Console.WriteLine($"OrderService: Stack trace: {ex.StackTrace}");
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<Order?> GetOrderByIdAsync(int orderId)
        {
            return await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Address)
                .Include(o => o.Coupon)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);
        }

        public async Task<Order?> GetOrderByOrderIdAsync(string orderId)
        {
            // Tìm order theo một số cách khác nhau nếu cần
            return await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Address)
                .Include(o => o.Coupon)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                        .ThenInclude(p => p.ProductImages)
                .FirstOrDefaultAsync(o => o.OrderId.ToString() == orderId);
        }

        private decimal CalculateDiscount(decimal subtotal, VoucherModel? voucher)
        {
            if (voucher == null) return 0m;

            return voucher.Type switch
            {
                "percent" => subtotal * Math.Min(voucher.Value, 100) / 100,
                "amount" => Math.Min(voucher.Value, subtotal),
                "freeship" => 0m,
                _ => 0m
            };
        }

        private decimal CalculateShippingFee(decimal subtotal, VoucherModel? voucher)
        {
            // Nếu có voucher miễn phí ship
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
    }
}

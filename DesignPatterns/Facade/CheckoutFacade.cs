using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PerfumeStore.Models;
using PerfumeStore.Models.ViewModels;

namespace PerfumeStore.DesignPatterns.Facade
{
    /// <summary>
    /// DESIGN PATTERN: FACADE (Mặt tiền)
    /// - Ứng dụng tại: CartController.cs -> Hàm: ProcessCheckout()
    /// - Luồng hoạt động: 
    ///   1. Nhận dữ liệu thô từ Giỏ hàng và Form nhập của khách.
    ///   2. Mở một Transaction (Giao dịch an toàn).
    ///   3. Lần lượt ghi vào các bảng: Customers -> ShippingAddresses -> Coupons -> Orders -> OrderDetails (kèm trừ Stock).
    ///   4. Nếu 1 bước lỗi, Rollback toàn bộ để tránh rác dữ liệu. Nếu xong, Commit và trả về Order.
    /// - Lợi ích: Rút gọn >150 dòng code thao tác DB phức tạp trong Controller xuống chỉ còn 1 dòng gọi hàm Facade.
    /// </summary>
    public interface ICheckoutFacade
    {
        Task<Order> PlaceOrderAsync(CheckoutViewModel model, string customerEmail, List<CartItem> cart, VoucherModel appliedVoucher, decimal totalAmount);
    }

    public class CheckoutFacade : ICheckoutFacade
    {
        private readonly PerfumeStoreContext _context;

        public CheckoutFacade(PerfumeStoreContext context)
        {
            _context = context;
        }

        public async Task<Order> PlaceOrderAsync(CheckoutViewModel model, string customerEmail, List<CartItem> cart, VoucherModel appliedVoucher, decimal totalAmount)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Xử lý Customer
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Email == customerEmail);
                if (customer == null)
                {
                    customer = new Customer { Name = model.CustomerName, Email = customerEmail, Phone = model.CustomerPhone, CreatedDate = DateTime.Now, BirthYear = 1990, PasswordHash = "", SpinNumber = 3 };
                    _context.Customers.Add(customer);
                    await _context.SaveChangesAsync();
                }

                // 2. Xử lý Address
                var address = new ShippingAddress { CustomerId = customer.CustomerId, RecipientName = model.CustomerName, Phone = model.CustomerPhone, Province = model.Province, District = model.District, Ward = model.Ward, AddressLine = model.ShippingAddress, IsDefault = model.SaveAsDefaultAddress };
                _context.ShippingAddresses.Add(address);
                await _context.SaveChangesAsync();

                // 3. Xử lý Voucher (Coupon)
                Coupon coupon = null;
                if (appliedVoucher != null)
                {
                    var codeLower = appliedVoucher.Code.ToLower();
                    coupon = await _context.Coupons.FirstOrDefaultAsync(c => c.Code != null && c.Code.ToLower() == codeLower);
                    if (coupon == null)
                    {
                        coupon = new Coupon { Code = appliedVoucher.Code, DiscountAmount = appliedVoucher.Type == "amount" ? appliedVoucher.Value : 0, CreatedDate = DateTime.Now, ExpiryDate = DateTime.Now.AddDays(30), IsUsed = false };
                        _context.Coupons.Add(coupon);
                        await _context.SaveChangesAsync();
                    }
                }

                // 4. Tạo Đơn Hàng (Order)
                var order = new Order
                {
                    CustomerId = customer.CustomerId,
                    AddressId = address.AddressId,
                    CouponId = coupon?.CouponId,
                    OrderDate = DateTime.Now,
                    TotalAmount = totalAmount,
                    PaymentMethod = model.PaymentMethod == "BANK_TRANSFER" ? "Chuyển khoản ngân hàng" : "Thanh toán khi nhận hàng",
                    Status = model.PaymentMethod == "BANK_TRANSFER" ? "Chờ thanh toán" : "Đang xử lý",
                    Notes = model.OrderNotes
                };
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                // 5. Lưu Chi tiết & Trừ Tồn Kho
                foreach (var item in cart)
                {
                    var product = await _context.Products.FindAsync(item.ProductId) ?? await _context.Products.FirstOrDefaultAsync(p => p.ProductName == item.ProductName);
                    int productId = product?.ProductId ?? 0;

                    if (product != null)
                    {
                        if (product.Stock < item.Quantity) throw new Exception($"'{product.ProductName}' chỉ còn {product.Stock} cái.");
                        product.Stock -= item.Quantity; // Trừ tồn kho
                    }

                    _context.OrderDetails.Add(new OrderDetail { OrderId = order.OrderId, ProductId = productId, Quantity = item.Quantity, UnitPrice = item.UnitPrice, TotalPrice = item.LineTotal });
                }
                await _context.SaveChangesAsync();

                // 6. Cập nhật mã giảm giá (Nếu là COD)
                if (model.PaymentMethod != "BANK_TRANSFER" && coupon != null)
                {
                    coupon.IsUsed = true; coupon.UsedDate = DateTime.Now; await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
                return order;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
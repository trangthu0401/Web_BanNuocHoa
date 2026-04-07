using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using PerfumeStore.Models;
using PerfumeStore.Models.ViewModels;
using PerfumeStore.Services;

namespace PerfumeStore.DesignPatterns.Observer
{
    // 1. Giao diện chuẩn mực: Yêu cầu Observer nhận đủ Data để làm việc
    public interface IOrderObserver
    {
        Task UpdateAsync(Order order, List<CartItem> cart, CheckoutViewModel model, string customerEmail);
    }

    // 2. Chủ thể (Subject)
    public class OrderSubject
    {
        private List<IOrderObserver> _observers = new List<IOrderObserver>();

        public void Attach(IOrderObserver observer) => _observers.Add(observer);

        public async Task NotifyAsync(Order order, List<CartItem> cart, CheckoutViewModel model, string customerEmail)
        {
            foreach (var observer in _observers)
            {
                await observer.UpdateAsync(order, cart, model, customerEmail);
            }
        }
    }

    // 3. Observer 1: Chuyên lo gửi Email xác nhận
    public class EmailObserver : IOrderObserver
    {
        private readonly IEmailService _emailService;

        public EmailObserver(IEmailService emailService)
        {
            _emailService = emailService;
        }

        public async Task UpdateAsync(Order order, List<CartItem> cart, CheckoutViewModel model, string customerEmail)
        {
            // Vẫn in ra Console để UI bắt được log
            Console.WriteLine($"[EmailService] Đã kích hoạt lệnh gửi mail thực tế tới {customerEmail} cho đơn #{order.OrderId}.");

            if (!string.IsNullOrEmpty(customerEmail))
            {
                decimal total = order.TotalAmount ?? 0;
                string emailSub = $"Xac nhan don hang #{order.OrderId} tu PerfumeStore";
                string emailBody = $"Chao {model.CustomerName},\n\n" +
                                   $"Don hang cua ban da duoc he thong ghi nhan thanh cong. Chi tiet:\n\n" +
                                   $"Ma don hang: #{order.OrderId}\n" +
                                   $"Tong tien: {total:N0} VND\n" +
                                   $"Phuong thuc thanh toan: {model.PaymentMethod}\n\n" +
                                   $"Chung toi se som lien he va tien hanh giao hang cho ban.\n\n" +
                                   $"Tran trong,\nDoi ngu PerfumeStore";

                await _emailService.SendSimpleTextEmailAsync(customerEmail, emailSub, emailBody);
            }
        }
    }

    // 4. Observer 2: Chuyên lo trừ Tồn kho và Ghi Log cho Admin
    public class InventoryObserver : IOrderObserver
    {
        private readonly PerfumeStoreContext _context;
        private readonly ILogger _logger;

        public InventoryObserver(PerfumeStoreContext context, ILogger logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task UpdateAsync(Order order, List<CartItem> cart, CheckoutViewModel model, string customerEmail)
        {
            // Vẫn in ra Console để UI bắt được log
            Console.WriteLine($"[InventoryService] Đã xác nhận trừ kho cho các sản phẩm trong đơn #{order.OrderId}.");

            bool stockUpdated = false;
            string adminLogMessage = $"[HE THONG ADMIN] Thong bao tu dong : da cap nhat cho don hang #{order.OrderId}\nChi tiet:\n";

            foreach (var item in cart)
            {
                if (item.ProductId > 0)
                {
                    var prodInDb = await _context.Products.FindAsync(item.ProductId);
                    if (prodInDb != null)
                    {
                        prodInDb.Stock = prodInDb.Stock >= item.Quantity ? prodInDb.Stock - item.Quantity : 0;
                        adminLogMessage += $"- Tên SP: {prodInDb.ProductName} | Số lượng bán: {item.Quantity} | Tồn kho mới: {prodInDb.Stock}\n";
                        stockUpdated = true;
                    }
                }
            }

            if (stockUpdated)
            {
                _logger.LogInformation(adminLogMessage);
            }
        }
    }

    // 5. Observer 3: Chuyên lo Tích điểm và Gửi Mail điểm thưởng
    public class MembershipObserver : IOrderObserver
    {
        private readonly PerfumeStoreContext _context;
        private readonly IEmailService _emailService;

        public MembershipObserver(PerfumeStoreContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        public async Task UpdateAsync(Order order, List<CartItem> cart, CheckoutViewModel model, string customerEmail)
        {
            decimal total = order.TotalAmount ?? 0;
            int pointsEarned = (int)(total / 100000);

            // Vẫn in ra Console để UI bắt được log
            Console.WriteLine($"[MembershipService] Khách hàng ID {order.CustomerId} được cộng {pointsEarned} điểm thưởng.");

            var customerDb = await _context.Customers.FirstOrDefaultAsync(c => c.Email == customerEmail);
            if (customerDb != null && pointsEarned > 0)
            {
                customerDb.SpinNumber = (customerDb.SpinNumber ?? 0) + pointsEarned;

                if (!string.IsNullOrEmpty(customerEmail))
                {
                    string pointsSub = $"Thong bao cong diem thuong tu PerfumeStore";
                    string pointsBody = $"Chao {model.CustomerName},\n\n" +
                                        $"Ban vua duoc he thong cong them {pointsEarned} diem vao tai khoan sau khi hoan tat don hang #{order.OrderId}.\n\n" +
                                        $"Tong diem hien tai cua ban la: {customerDb.SpinNumber} diem.\n\n" +
                                        $"Hay dang nhap vao website de su dung diem thuong nay cho cac lan mua sam tiep theo.\n\n" +
                                        $"Cam on ban da ung ho PerfumeStore!";

                    await _emailService.SendSimpleTextEmailAsync(customerEmail, pointsSub, pointsBody);
                }
            }
        }
    }
}
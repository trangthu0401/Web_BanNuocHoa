using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PerfumeStore.Models;
using PerfumeStore.Services; // Thêm using này để nhận diện IEmailService

namespace PerfumeStore.DesignPatterns.Observer
{
    // 1. Interface Người quan sát
    public interface IOrderObserver
    {
        void Update(Order order);
    }

    // 2. Subject: Chủ thể (Nơi phát ra thông báo)
    public class OrderSubject
    {
        private List<IOrderObserver> _observers = new List<IOrderObserver>();

        // Đăng ký dịch vụ vào danh sách chờ
        public void Attach(IOrderObserver observer) => _observers.Add(observer);

        // Phát thông báo cho tất cả các dịch vụ trong danh sách
        public void Notify(Order order)
        {
            foreach (var observer in _observers)
            {
                observer.Update(order);
            }
        }
    }

    // --- 3. CÁC OBSERVER CỤ THỂ ---

    // 3.1. Observer Gửi Email xác nhận
    public class EmailObserver : IOrderObserver
    {
        private readonly IEmailService _emailService;

        // [SỬA Ở ĐÂY]: Inject IEmailService vào constructor
        public EmailObserver(IEmailService emailService)
        {
            _emailService = emailService;
        }

        public void Update(Order order)
        {
            // [SỬA Ở ĐÂY]: Lấy email của khách hàng (nếu có) hoặc dùng 1 email mặc định/test
            string toEmail = order.Customer?.Email ?? "test@example.com";
            string subject = $"Xác nhận đơn hàng #{order.OrderId}";
            string body = $"Cảm ơn bạn đã đặt hàng. Đơn hàng #{order.OrderId} của bạn với tổng tiền {order.TotalAmount:N0}đ đã được xác nhận.";

            // [SỬA Ở ĐÂY]: Gọi đúng tên hàm 'SendSimpleTextEmailAsync' có sẵn trong IEmailService.
            // Dùng dấu '_' (discard) vì Update() là hàm void đồng bộ (sync), còn gửi mail là bất đồng bộ (async).
            _ = _emailService.SendSimpleTextEmailAsync(toEmail, subject, body);

            Console.WriteLine($"[EmailService] Đã kích hoạt lệnh gửi mail thực tế tới {toEmail} cho đơn #{order.OrderId}.");
        }
    }

    // 3.2. Observer Cập nhật Tồn kho
    public class InventoryObserver : IOrderObserver
    {
        public void Update(Order order)
        {
            // Logic: Duyệt đơn hàng và trừ số lượng sản phẩm trong kho
            Console.WriteLine($"[InventoryService] Đã xác nhận trừ kho cho các sản phẩm trong đơn #{order.OrderId}.");
        }
    }

    // 3.3. Observer Tích điểm thành viên
    public class MembershipObserver : IOrderObserver
    {
        public void Update(Order order)
        {
            // Công thức: 100.000đ = 1 điểm
            decimal points = (order.TotalAmount ?? 0) / 100000;
            Console.WriteLine($"[MembershipService] Khách hàng ID {order.CustomerId} được cộng {(int)points} điểm thưởng.");
        }
    }
}
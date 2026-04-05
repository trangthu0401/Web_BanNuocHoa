using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PerfumeStore.Models;
using PerfumeStore.Services; // Thêm using này để nhận diện IEmailService

namespace PerfumeStore.DesignPatterns.Observer
{

    public interface IOrderObserver
    {
        void Update(Order order);
    }


    public class OrderSubject
    {
        private List<IOrderObserver> _observers = new List<IOrderObserver>();

        public void Attach(IOrderObserver observer) => _observers.Add(observer);

  
        public void Notify(Order order)
        {
            foreach (var observer in _observers)
            {
                observer.Update(order);
            }
        }
    }


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

 
    public class InventoryObserver : IOrderObserver
    {
        public void Update(Order order)
        {
           
            Console.WriteLine($"[InventoryService] Đã xác nhận trừ kho cho các sản phẩm trong đơn #{order.OrderId}.");
        }
    }


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
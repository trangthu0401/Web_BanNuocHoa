using System;
using System.Collections.Generic;
using PerfumeStore.Models;

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
        public void Update(Order order)
        {
            // Giả lập gửi mail qua EmailService của bạn
            Console.WriteLine($"[EmailService] Đơn hàng #{order.OrderId} thành công. Đã gửi mail xác nhận tới khách hàng.");
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
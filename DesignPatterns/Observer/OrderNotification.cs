using System;
using System.Collections.Generic;

namespace PerfumeStore.DesignPatterns.Observer
{
    // 1. Giao diện người quan sát (Observer)
    public interface IObserver
    {
        void Update(string message);
    }

    // 2. Chủ thể (Subject) - Quản lý danh sách người nhận tin
    public class OrderSubject
    {
        private List<IObserver> _observers = new List<IObserver>();
        public string OrderStatus { get; private set; }

        // Đăng ký nhận thông báo
        public void Attach(IObserver observer) => _observers.Add(observer);

        // Hủy đăng ký
        public void Detach(IObserver observer) => _observers.Remove(observer);

        // Gửi thông báo đến tất cả Observer
        public void Notify()
        {
            foreach (var observer in _observers)
            {
                observer.Update($"Đơn hàng cập nhật: {OrderStatus}");
            }
        }

        // Logic nghiệp vụ: Đổi trạng thái -> Tự động báo
        public void ChangeStatus(string newStatus)
        {
            OrderStatus = newStatus;
            Notify();
        }
    }

    // 3. Observer gửi Email
    public class EmailObserver : IObserver
    {
        public void Update(string message)
        {
            Console.WriteLine($"[Email Service]: Đã gửi thông báo '{message}' tới khách hàng.");
        }
    }

    // 4. Observer ghi Log hệ thống
    public class LoggerObserver : IObserver
    {
        public void Update(string message)
        {
            Console.WriteLine($"[System Log]: Ghi nhận sự kiện '{message}'.");
        }
    }
}
using System;
using System.Collections.Generic;
using PerfumeStore.Models;

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
        public void Update(Order order)
        {
          
            Console.WriteLine($"[EmailService] Đơn hàng #{order.OrderId} thành công. Đã gửi mail xác nhận tới khách hàng.");
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
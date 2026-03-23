using PerfumeStore.Models;

namespace PerfumeStore.DesignPatterns.Strategy
{
    public class PaymentRouteResult
    {
        public string ActionName { get; set; }
        public string ControllerName { get; set; }
    }

    /// <summary>
    /// DESIGN PATTERN: STRATEGY (Chiến lược)
    /// - Ứng dụng tại: CartController.cs -> Hàm: ProcessCheckout() (Sau khi Facade tạo xong Order)
    /// - Luồng hoạt động: 
    ///   Thay vì dùng các khối IF-ELSE khổng lồ để điều hướng khách hàng sau khi đặt đơn:
    ///   + Nếu khách chọn COD -> Dùng CodPaymentStrategy -> Trả về Route trỏ tới trang Thành công.
    ///   + Nếu khách chọn PAYOS -> Dùng PayOsPaymentStrategy -> Trả về Route trỏ sang PaymentController để tạo mã QR.
    /// - Lợi ích: Tuân thủ Open/Closed Principle. Sau này muốn thêm Momo, ZaloPay chỉ cần tạo thêm class Strategy mới, không cần sửa đổi CartController.
    /// </summary>
    public interface IPaymentStrategy
    {
        PaymentRouteResult ProcessRouting(Order order);
    }

    public class CodPaymentStrategy : IPaymentStrategy
    {
        public PaymentRouteResult ProcessRouting(Order order)
        {
            return new PaymentRouteResult { ActionName = "PaymentSuccess", ControllerName = "Cart" };
        }
    }

    public class PayOsPaymentStrategy : IPaymentStrategy
    {
        public PaymentRouteResult ProcessRouting(Order order)
        {
            return new PaymentRouteResult { ActionName = "CreatePaymentProgress", ControllerName = "Payment" };
        }
    }

    public class PaymentContext
    {
        private IPaymentStrategy _strategy;

        public void SetStrategy(IPaymentStrategy strategy)
        {
            _strategy = strategy;
        }

        public PaymentRouteResult ExecuteRouting(Order order)
        {
            return _strategy.ProcessRouting(order);
        }
    }
}
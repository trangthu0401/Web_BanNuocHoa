using System;

namespace PerfumeStore.DesignPatterns.Strategy
{
    // 1. Giao diện chiến lược (Strategy Interface)
    public interface IPaymentStrategy
    {
        // Mỗi phương thức thanh toán đều phải xử lý số tiền và mã đơn
        void ProcessPayment(decimal amount, int orderId);
    }

    // 2. Các chiến lược cụ thể (Concrete Strategies)

    // 2.1. Chiến lược thanh toán khi nhận hàng (COD)
    public class CodPaymentStrategy : IPaymentStrategy
    {
        public void ProcessPayment(decimal amount, int orderId)
        {
            // Logic: Cập nhật trạng thái đơn hàng là 'Chờ thanh toán'
            Console.WriteLine($"[COD] Đơn hàng #{orderId}: Thu tiền mặt {amount:N0} VNĐ khi giao hàng.");
        }
    }

    // 2.2. Chiến lược thanh toán Online qua PayOS
    public class PayOsPaymentStrategy : IPaymentStrategy
    {
        public void ProcessPayment(decimal amount, int orderId)
        {
            // Logic: Gọi API PayOS tạo mã QR
            Console.WriteLine($"[PayOS] Đơn hàng #{orderId}: Đang tạo mã QR thanh toán cho số tiền {amount:N0} VNĐ.");
        }
    }

    // 3. Lớp ngữ cảnh (Context) - Nơi Controller gọi vào
    public class PaymentContext
    {
        private IPaymentStrategy _strategy;

        // Cho phép thay đổi chiến lược thanh toán ngay lúc chạy (Runtime)
        public void SetStrategy(IPaymentStrategy strategy)
        {
            _strategy = strategy;
        }

        public void ExecutePayment(decimal amount, int orderId)
        {
            if (_strategy == null)
            {
                throw new InvalidOperationException("Lỗi: Chưa chọn phương thức thanh toán!");
            }
            // Ủy quyền xử lý cho chiến lược đã chọn
            _strategy.ProcessPayment(amount, orderId);
        }
    }
}
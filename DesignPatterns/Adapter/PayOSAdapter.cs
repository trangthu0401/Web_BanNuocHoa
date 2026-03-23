using System;

namespace PerfumeStore.DesignPatterns.Adapter
{
    // --- PHẦN HỖ TRỢ (MÔ PHỎNG DỮ LIỆU PAYOS VÀ DATABASE) ---

    // 1. Dữ liệu giả định từ PayOS gửi về (Adaptee)
    public class PayOSWebhookData
    {
        public string orderCode { get; set; }
        public string status { get; set; } // PayOS trả về: "PAID", "PENDING", "CANCELLED"
    }

    // 2. Định nghĩa trạng thái đơn hàng trong Database nội bộ (Target)
    public enum InternalOrderStatus
    {
        Pending = 0,    // Chờ xử lý
        Paid = 1,       // Đã thanh toán
        Failed = -1,    // Hủy bỏ
        Unknown = 99
    }

    // --- PHẦN CHÍNH: CODE MẪU ADAPTER (Copy vào báo cáo) ---

    // 3. Giao diện Adapter
    public interface IPaymentAdapter
    {
        InternalOrderStatus ConvertStatus(PayOSWebhookData payOSData);
    }

    // 4. Lớp Adapter thực hiện chuyển đổi
    public class PayOSAdapter : IPaymentAdapter
    {
        public InternalOrderStatus ConvertStatus(PayOSWebhookData payOSData)
        {
            if (payOSData == null || string.IsNullOrEmpty(payOSData.status))
            {
                return InternalOrderStatus.Unknown;
            }

            // Chuyển đổi từ String (PayOS) sang Enum/Int (Database)
            // Sử dụng Switch Expression của C# mới nhất
            return payOSData.status.ToUpper() switch
            {
                "PAID" => InternalOrderStatus.Paid,           // Map "PAID" -> 1
                "PENDING" => InternalOrderStatus.Pending,     // Map "PENDING" -> 0
                "PROCESSING" => InternalOrderStatus.Pending,
                "CANCELLED" => InternalOrderStatus.Failed,    // Map "CANCELLED" -> -1
                _ => InternalOrderStatus.Unknown
            };
        }
    }
}
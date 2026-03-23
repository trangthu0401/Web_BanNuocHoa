using System;
using System.Collections.Generic;

namespace PerfumeStore.DesignPatterns.Observer
{
    /// <summary>
    /// =========================================================================
    /// DESIGN PATTERN: OBSERVER (MẪU QUAN SÁT / XUẤT BẢN - ĐĂNG KÝ)
    /// =========================================================================
    /// - Ứng dụng tại: CartController (Hàm ProcessCheckout - Sau khi lưu đơn hàng thành công).
    /// - Luồng hoạt động: Khi đơn hàng được tạo (Subject thay đổi trạng thái), nó sẽ tự động 
    ///   "hét lên" (Notify) cho tất cả các dịch vụ đang quan tâm (Email, Logger...) biết để tự xử lý công việc của mình.
    /// 
    /// ⚠️ LƯU Ý SƯ PHẠM 1 (TẠI SAO KHÔNG ĐĂNG KÝ VÀO PROGRAM.CS?):
    /// - Mẫu này được thiết kế theo chuẩn Classic GoF (Gang of Four) sử dụng cơ chế Dynamic Subscription 
    ///   (Đăng ký động tại thời điểm chạy bằng lệnh Attach/Detach).
    /// - GIẢI THÍCH: Trong thực tế, có đơn hàng cần gửi Email, có đơn không cần. Việc khởi tạo động (dùng từ khóa `new`)
    ///   và `Attach()` trực tiếp trong Controller giúp hệ thống cực kỳ linh hoạt, thay vì tiêm cứng qua Dependency Injection (DI).
    ///
    /// ⚠️ LƯU Ý SƯ PHẠM 2 (LỢI ÍCH KIẾN TRÚC - OPEN/CLOSED PRINCIPLE):
    /// - Mẫu này giúp CartController được "giải phóng". Controller không cần quan tâm việc gửi Email diễn ra thế nào.
    /// - Nếu sau này Sếp yêu cầu: "Cần gửi thêm tin nhắn Zalo khi có đơn mới". Ta chỉ cần tạo thêm class `ZaloObserver` 
    ///   và `Attach()` nó vào, tuyệt đối không cần sửa đổi luồng code phức tạp bên trong CartController.
    /// =========================================================================
    /// </summary>

    // 1. Giao diện người quan sát (Observer)
    public interface IObserver
    {
        void Update(string message);
    }

    // 2. Chủ thể (Subject) - Quản lý danh sách các Observer đang theo dõi nó
    public class OrderSubject
    {
        private List<IObserver> _observers = new List<IObserver>();
        public string OrderStatus { get; private set; }

        // Đăng ký nhận thông báo (Subscribe)
        public void Attach(IObserver observer) => _observers.Add(observer);

        // Hủy đăng ký nhận thông báo (Unsubscribe)
        public void Detach(IObserver observer) => _observers.Remove(observer);

        // Phát loa thông báo đến tất cả Observer đang đăng ký
        public void Notify()
        {
            foreach (var observer in _observers)
            {
                observer.Update($"HỆ THỐNG THÔNG BÁO: {OrderStatus}");
            }
        }

        // Logic nghiệp vụ: Đổi trạng thái -> Tự động kích hoạt chuỗi gửi tin
        public void ChangeStatus(string newStatus)
        {
            OrderStatus = newStatus;
            Notify();
        }
    }

    // ==========================================
    // CÁC OBSERVER CỤ THỂ SẼ NHẬN THÔNG BÁO
    // ==========================================

    // 3. Observer gửi Email
    public class EmailObserver : IObserver
    {
        public void Update(string message)
        {
            // Trong dự án thực tế, chỗ này sẽ gọi _emailService.SendEmailAsync(...)
            Console.WriteLine($"[Email Service Observer]: Đang xử lý gửi Email... '{message}'");
        }
    }

    // 4. Observer ghi Log hệ thống
    public class LoggerObserver : IObserver
    {
        public void Update(string message)
        {
            // Trong dự án thực tế, chỗ này có thể lưu file .txt hoặc đẩy lên Kibana/ElasticSearch
            Console.WriteLine($"[System Log Observer]: Đã ghi nhận sự kiện '{message}' vào CSDL.");
        }
    }
}
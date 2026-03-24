using System;
using System.Collections.Concurrent;

namespace PerfumeStore.DesignPatterns.Singleton
{
    /// <summary>
    /// =========================================================================
    /// DESIGN PATTERN: SINGLETON (MẪU ĐƠN BẢN)
    /// =========================================================================
    /// - Ứng dụng tại: SpinWheelController (Tính năng Vòng quay may mắn).
    /// - Luồng hoạt động: Tạo ra một bộ đếm in-memory (trên RAM) duy nhất toàn hệ thống để theo dõi 
    ///   số lượt quay của người dùng. Giúp chống Spam-click hiệu quả mà không cần chọc xuống Database liên tục.
    /// 
    /// ⚠️ LƯU Ý SƯ PHẠM (TẠI SAO KHÔNG ĐĂNG KÝ VÀO PROGRAM.CS?):
    /// - Mẫu này được triển khai theo trường phái "Classic Singleton" (Gang of Four) sử dụng từ khóa `static`.
    /// - GIẢI THÍCH: Từ khóa `static Lazy<T>` tự động khởi tạo đối tượng và cấp phát một vùng nhớ dùng chung 
    ///   duy nhất (Global Access Point) trên máy chủ ngay khi ứng dụng chạy. Do class này tự quản lý vòng đời 
    ///   của chính nó, ta KHÔNG CẦN đăng ký qua hệ thống Dependency Injection (DI - AddSingleton) trong Program.cs. 
    ///   Ở bất kỳ Controller nào, ta chỉ việc gọi trực tiếp: `SpinWheelTrackerSingleton.Instance.TenHam()`.
    /// =========================================================================
    /// </summary>
    public sealed class SpinWheelTrackerSingleton
    {
        // Sử dụng Lazy<T> để đảm bảo an toàn luồng (Thread-safe) trong môi trường Web nhiều request đồng thời
        private static readonly Lazy<SpinWheelTrackerSingleton> _instance =
            new Lazy<SpinWheelTrackerSingleton>(() => new SpinWheelTrackerSingleton());

        // Dùng ConcurrentDictionary để tránh lỗi xung đột (Race Condition) khi nhiều user quay cùng lúc
        // Key: Tên tài khoản (Email) hoặc SessionId | Value: Số lần đã quay
        private readonly ConcurrentDictionary<string, int> _userSpins;

        // Constructor private là bắt buộc của Singleton để chặn việc dùng từ khóa 'new' ở bên ngoài
        private SpinWheelTrackerSingleton()
        {
            _userSpins = new ConcurrentDictionary<string, int>();
        }

        // Điểm truy cập duy nhất toàn hệ thống
        public static SpinWheelTrackerSingleton Instance => _instance.Value;

        /// <summary>
        /// Kiểm tra xem user có được quyền quay tiếp không (Tối đa 2 lần)
        /// </summary>
        public bool CanSpin(string userIdentifier)
        {
            if (_userSpins.TryGetValue(userIdentifier, out int spinCount))
            {
                return spinCount < 2; // Giới hạn 2 lần
            }
            return true; // Chưa quay lần nào
        }

        /// <summary>
        /// Ghi nhận 1 lần quay của user
        /// </summary>
        public void RecordSpin(string userIdentifier)
        {
            _userSpins.AddOrUpdate(userIdentifier, 1, (key, oldValue) => oldValue + 1);
        }

        /// <summary>
        /// Reset lượt quay (có thể gọi hàm này bằng Background Service vào lúc 12h đêm)
        /// </summary>
        public void ClearTracker(string userIdentifier)
        {
            _userSpins.TryRemove(userIdentifier, out _);
        }
    }
}
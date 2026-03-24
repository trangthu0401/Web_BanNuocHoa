using System;

namespace PerfumeStore.DesignPatterns.State
{
    /// <summary>
    /// =========================================================================
    /// DESIGN PATTERN: STATE (MẪU TRẠNG THÁI)
    /// =========================================================================
    /// - Ứng dụng tại: WarrantyController (Luồng duyệt và cập nhật trạng thái bảo hành)
    /// - Luồng hoạt động: Thay vì dùng các khối lệnh if-else/switch-case khổng lồ và gán chuỗi cứng 
    ///   ("Chờ xử lý", "Đang xử lý") có nguy cơ sai chính tả, Pattern này đóng gói mỗi trạng thái 
    ///   thành 1 Class độc lập. Mỗi Class tự quy định logic chuyển đổi hợp lệ của riêng nó.
    /// 
    /// ⚠️ LƯU Ý SƯ PHẠM 1 (TẠI SAO KHÔNG ĐĂNG KÝ VÀO PROGRAM.CS?):
    /// - Mẫu thiết kế này KHÔNG ĐƯỢC ĐĂNG KÝ (DI) trong Program.cs và KHÔNG Inject vào Constructor của Controller.
    /// - GIẢI THÍCH: Trong ASP.NET Core, Dependency Injection (DI) thường dùng để quản lý các 
    ///   "Dịch vụ không trạng thái" (Stateless Services) xuyên suốt ứng dụng (như EmailService). 
    ///   Ngược lại, State Pattern đại diện cho "Trạng thái Dữ liệu Động" của một thực thể cụ thể 
    ///   (ở đây là 1 đơn bảo hành). Vì mỗi đơn có một trạng thái khác nhau lấy từ Database lên, 
    ///   ta bắt buộc phải khởi tạo động thông qua từ khóa `new` (dynamic instantiation) bên trong Controller.
    ///
    /// ⚠️ LƯU Ý SƯ PHẠM 2 (KIẾN TRÚC TÁCH BIỆT - DECOUPLING):
    /// - Mẫu này đã được tái cấu trúc để KHÔNG phụ thuộc vào Entity Model của Database.
    /// - Việc chỉ truyền vào `string currentStatus` giúp Pattern tuân thủ tuyệt đối:
    ///   1. Single Responsibility Principle (SRP): Mẫu State chỉ quản lý trạng thái, không quan tâm CSDL.
    ///   2. Dependency Inversion (DIP): Tránh hoàn toàn lỗi xung đột Namespace giữa Admin.Models và Customer.Models.
    /// =========================================================================
    /// </summary>

    public interface IWarrantyState
    {
        string StateName { get; }
        void Approve(WarrantyContext context);
        void Complete(WarrantyContext context);
        void Reject(WarrantyContext context);
    }

    public class WarrantyContext
    {
        public IWarrantyState CurrentState { get; private set; }

        private string _statusString;

        public WarrantyContext(string currentStatus)
        {
            _statusString = currentStatus;

            // Ép kiểu rõ ràng (IWarrantyState) để dứt điểm lỗi CS0029 (Cannot implicitly convert type)
            CurrentState = currentStatus switch
            {
                "Chờ xử lý" => (IWarrantyState)new PendingState(),
                "Đang xử lý" => (IWarrantyState)new ProcessingState(),
                "Hoàn tất" => (IWarrantyState)new CompletedState(),
                "Từ chối" => (IWarrantyState)new RejectedState(),
                _ => (IWarrantyState)new PendingState()
            };
        }

        public void SetState(IWarrantyState state)
        {
            CurrentState = state;
            _statusString = state.StateName;
        }

        public string GetStatusString() => _statusString;

        public void Approve() => CurrentState.Approve(this);
        public void Complete() => CurrentState.Complete(this);
        public void Reject() => CurrentState.Reject(this);
    }

    // ==========================================
    // CÁC LỚP TRẠNG THÁI CỤ THỂ (CONCRETE STATES)
    // ==========================================

    public class PendingState : IWarrantyState
    {
        public string StateName => "Chờ xử lý";
        public void Approve(WarrantyContext context) => context.SetState(new ProcessingState());
        public void Complete(WarrantyContext context) => throw new Exception("Lỗi logic: Đơn đang chờ xử lý, không thể Hoàn tất!");
        public void Reject(WarrantyContext context) => context.SetState(new RejectedState());
    }

    public class ProcessingState : IWarrantyState
    {
        public string StateName => "Đang xử lý";
        public void Approve(WarrantyContext context) => throw new Exception("Lỗi logic: Đơn này đã được duyệt xử lý rồi.");
        public void Complete(WarrantyContext context) => context.SetState(new CompletedState());
        public void Reject(WarrantyContext context) => context.SetState(new RejectedState());
    }

    public class CompletedState : IWarrantyState
    {
        public string StateName => "Hoàn tất";
        public void Approve(WarrantyContext context) => throw new Exception("Từ chối: Bảo hành đã Hoàn tất, không thể lùi về Đang xử lý.");
        public void Complete(WarrantyContext context) => throw new Exception("Từ chối: Bảo hành đã Hoàn tất, không thể thay đổi.");
        public void Reject(WarrantyContext context) => throw new Exception("Từ chối: Bảo hành đã Hoàn tất, không thể Từ chối.");
    }

    public class RejectedState : IWarrantyState
    {
        public string StateName => "Từ chối";
        public void Approve(WarrantyContext context) => throw new Exception("Từ chối: Yêu cầu này đã bị Từ chối, không thể phục hồi.");
        public void Complete(WarrantyContext context) => throw new Exception("Từ chối: Yêu cầu này đã bị Từ chối, không thể Hoàn tất.");
        public void Reject(WarrantyContext context) => throw new Exception("Từ chối: Yêu cầu này đã bị Từ chối, không thể thay đổi.");
    }
}
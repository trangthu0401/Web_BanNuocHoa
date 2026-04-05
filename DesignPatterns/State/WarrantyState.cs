using System;

namespace PerfumeStore.DesignPatterns.State
{
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

        // Constructor 1: Dùng cho tạo mới
        public WarrantyContext(IWarrantyState initialState)
        {
            CurrentState = initialState;
            _statusString = initialState.StateName;
        }

        // Constructor 2: Dùng để phục dựng từ Database
        public WarrantyContext(string currentStatus)
        {
            _statusString = currentStatus;

            // ĐỒNG BỘ TIẾNG ANH TẠI ĐÂY
            CurrentState = currentStatus switch
            {
                "Pending" => (IWarrantyState)new PendingState(),
                "Processing" => (IWarrantyState)new ProcessingState(),
                "Completed" => (IWarrantyState)new CompletedState(),
                "Rejected" => (IWarrantyState)new RejectedState(),
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

    // ==========================
    // CÁC LỚP TRẠNG THÁI CỤ THỂ
    // ==========================

    public class PendingState : IWarrantyState
    {
        public string StateName => "Pending";
        public void Approve(WarrantyContext context) => context.SetState(new ProcessingState());
        public void Complete(WarrantyContext context) => throw new Exception("Lỗi logic: Đơn đang chờ xử lý, không thể Hoàn tất!");
        public void Reject(WarrantyContext context) => context.SetState(new RejectedState());
    }

    public class ProcessingState : IWarrantyState
    {
        public string StateName => "Processing";
        public void Approve(WarrantyContext context) => throw new Exception("Lỗi logic: Đơn này đã được duyệt xử lý rồi.");
        public void Complete(WarrantyContext context) => context.SetState(new CompletedState());
        public void Reject(WarrantyContext context) => context.SetState(new RejectedState());
    }

    public class CompletedState : IWarrantyState
    {
        public string StateName => "Completed";
        public void Approve(WarrantyContext context) => throw new Exception("Từ chối: Bảo hành đã Hoàn tất, không thể lùi về Đang xử lý.");
        public void Complete(WarrantyContext context) => throw new Exception("Từ chối: Bảo hành đã Hoàn tất, không thể thay đổi.");
        public void Reject(WarrantyContext context) => throw new Exception("Từ chối: Bảo hành đã Hoàn tất, không thể Từ chối.");
    }

    public class RejectedState : IWarrantyState
    {
        public string StateName => "Rejected";
        public void Approve(WarrantyContext context) => throw new Exception("Từ chối: Yêu cầu này đã bị Từ chối, không thể phục hồi.");
        public void Complete(WarrantyContext context) => throw new Exception("Từ chối: Yêu cầu này đã bị Từ chối, không thể Hoàn tất.");
        public void Reject(WarrantyContext context) => throw new Exception("Từ chối: Yêu cầu này đã bị Từ chối, không thể thay đổi.");
    }
}
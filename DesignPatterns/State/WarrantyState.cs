using System;

namespace PerfumeStore.DesignPatterns.State
{
    // 1. Giao diện trạng thái bảo hành
    public interface IWarrantyState
    {
        void HandleRequest(WarrantyContext context);
    }

    // 2. Lớp Context: Đại diện cho Phiếu bảo hành thực tế
    public class WarrantyContext
    {
        private IWarrantyState _currentState;

        public WarrantyContext(IWarrantyState initialState)
        {
            _currentState = initialState;
        }

        public void TransitionTo(IWarrantyState state)
        {
            _currentState = state;
        }

        // Hành vi của context thay đổi tùy thuộc vào trạng thái hiện tại
        public void RequestAction()
        {
            _currentState.HandleRequest(this);
        }
    }

    // 3. Trạng thái: Đang hiệu lực (Cho phép xử lý)
    public class ActiveState : IWarrantyState
    {
        public void HandleRequest(WarrantyContext context)
        {
            Console.WriteLine("Yêu cầu được chấp nhận. Đang chuyển sang quy trình sửa chữa.");
            // Tự động chuyển trạng thái
            context.TransitionTo(new ProcessingState());
        }
    }

    // 4. Trạng thái: Đã hoàn tất (Chặn mọi thao tác)
    public class FinishedState : IWarrantyState
    {
        public void HandleRequest(WarrantyContext context)
        {
            // Ném lỗi nếu cố tình thao tác trên phiếu đã đóng
            throw new InvalidOperationException("Bảo hành đã đóng, không thể thao tác.");
        }
    }

    // (Lớp ProcessingState được lược bỏ để ngắn gọn)
    public class ProcessingState : IWarrantyState
    {
        public void HandleRequest(WarrantyContext context) { /* Logic xử lý */ }
    }
}
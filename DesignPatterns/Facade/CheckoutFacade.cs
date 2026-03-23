using System;
using PerfumeStore.Models.ViewModels;

namespace PerfumeStore.DesignPatterns.Facade
{
    // 1. Giao diện các hệ thống con (Subsystems)
    public interface IOrderSubsystem
    {
        int CreateOrder(CheckoutViewModel model, int customerId);
    }

    public interface IEmailSubsystem
    {
        void SendConfirmation(string email, int orderId);
    }

    // 2. Lớp Facade: Mặt tiền đơn giản hóa quy trình thanh toán
    public class CheckoutFacade
    {
        private readonly IOrderSubsystem _orderSystem;
        private readonly IEmailSubsystem _emailSystem;

        public CheckoutFacade(IOrderSubsystem orderSystem, IEmailSubsystem emailSystem)
        {
            _orderSystem = orderSystem;
            _emailSystem = emailSystem;
        }

        public bool PlaceOrder(CheckoutViewModel model, int customerId)
        {
            try
            {
                // Lấy thông tin email an toàn
                dynamic data = model;
                string email = data.Email ?? "customer@example.com";

                // Bước 1: Tạo đơn hàng thông qua hệ thống con
                int orderId = _orderSystem.CreateOrder(model, customerId);

                if (orderId > 0)
                {
                    // Bước 2: Gửi email xác nhận khi tạo đơn thành công
                    _emailSystem.SendConfirmation(email, orderId);
                    return true;
                }
                return false;
            }
            catch (Exception)
            {
                return false; // Xử lý lỗi tập trung tại Facade
            }
        }
    }
}
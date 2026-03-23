namespace PerfumeStore.DesignPatterns.Adapter
{
    public enum InternalOrderStatus
    {
        Pending,
        PaidSuccess,
        Cancelled,
        Unknown
    }

    /// <summary>
    /// DESIGN PATTERN: ADAPTER (Bộ chuyển đổi)
    /// - Ứng dụng tại: PaymentController.cs
    /// - Luồng hoạt động: API của bên thứ 3 (PayOS) trả về các mã code (00) hoặc chuỗi trạng thái ("CANCELLED").
    ///   Adapter sẽ chuyển đổi các mã "ngoại lai" này thành Enum chuẩn nội bộ (InternalOrderStatus).
    /// </summary>
    public class PayOSAdapter
    {
        // Chuyển đổi mã trạng thái PayOS sang chuẩn nội bộ
        public static InternalOrderStatus ConvertExternalStatusToInternal(string payOsStatus, string payOsCode)
        {
            // Nếu PayOS trả về mã "00" tức là giao dịch thành công
            if (payOsCode == "00") return InternalOrderStatus.PaidSuccess;

            if (string.IsNullOrEmpty(payOsStatus)) return InternalOrderStatus.Unknown;

            return payOsStatus.ToUpper() switch
            {
                "PAID" => InternalOrderStatus.PaidSuccess,
                "CANCELLED" => InternalOrderStatus.Cancelled,
                "PENDING" => InternalOrderStatus.Pending,
                _ => InternalOrderStatus.Unknown
            };
        }
    }
}
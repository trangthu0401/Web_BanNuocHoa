namespace PerfumeStore.DesignPatterns.Factory
{
    /// <summary>
    /// Giao diện chuẩn mực cho tất cả các "Bộ xử lý sản phẩm" được sinh ra từ Nhà máy.
    /// Bất kỳ loại nước hoa nào (Nam, Nữ, Unisex) cũng đều phải tuân thủ việc có 
    /// hàm tính giá và hàm lấy ghi chú khuyến mãi.
    /// </summary>
    public interface IProductProcessor
    {
        decimal CalculateFinalPrice(decimal basePrice);
        string GetPromotionNote();
    }
}
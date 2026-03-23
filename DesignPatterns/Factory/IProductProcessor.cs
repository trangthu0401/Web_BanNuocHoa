namespace PerfumeStore.DesignPatterns.Factory
{
    // Interface định nghĩa các hành vi chung cho bộ xử lý sản phẩm
    public interface IProductProcessor
    {
        decimal CalculateFinalPrice(decimal basePrice);
        string GetPromotionNote();
    }
}
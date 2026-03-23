using System;

namespace PerfumeStore.DesignPatterns.Factory
{
    // 1. Bộ xử lý cho Nước hoa Nam (Category 1)
    public class MenPerfumeProcessor : IProductProcessor
    {
        public decimal CalculateFinalPrice(decimal basePrice)
        {
            // Nước hoa nam thường có phí bảo hiểm vận chuyển cao hơn
            return basePrice + 50000;
        }

        public string GetPromotionNote()
        {
            return "Ưu đãi: Tặng kèm mẫu thử sữa tắm mạnh mẽ cho quý ông.";
        }
    }

    // 2. Bộ xử lý cho Nước hoa Nữ (Category 2)
    public class WomenPerfumeProcessor : IProductProcessor
    {
        public decimal CalculateFinalPrice(decimal basePrice)
        {
            return basePrice; // Giữ nguyên giá gốc
        }

        public string GetPromotionNote()
        {
            return "Ưu đãi: Miễn phí gói quà cao cấp và thiệp chúc mừng.";
        }
    }

    // 3. Lớp Factory điều phối khởi tạo
    public class ProductProcessorFactory
    {
        public static IProductProcessor GetProcessor(int categoryId)
        {
            // Dựa vào CategoryID thực tế trong Database của nhóm để trả về đối tượng
            switch (categoryId)
            {
                case 1:
                    return new MenPerfumeProcessor();
                case 2:
                    return new WomenPerfumeProcessor();
                default:
                    // Mặc định trả về bộ xử lý chung cho các loại khác
                    return new MenPerfumeProcessor();
            }
        }
    }
}
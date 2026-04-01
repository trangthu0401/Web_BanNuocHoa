using System;

namespace PerfumeStore.DesignPatterns.Factory
{
    /// <summary>
    /// =========================================================================
    /// DESIGN PATTERN: FACTORY METHOD (MẪU NHÀ MÁY)
    /// =========================================================================
    /// - Ứng dụng tại: ProductController (Trang chi tiết sản phẩm) hoặc CartController.
    /// - Luồng hoạt động: Dựa vào Tên loại sản phẩm (Nam, Nữ, Unisex), "Nhà máy" sẽ tự động 
    ///   chế tạo và trả về "Bộ xử lý" (Processor) tương ứng chứa các chính sách khuyến mãi riêng.
    /// 
    /// ⚠️ LƯU Ý SƯ PHẠM 1 (TẠI SAO KHÔNG ĐĂNG KÝ VÀO PROGRAM.CS?):
    /// - Lớp Factory này sử dụng phương thức khởi tạo tĩnh `public static`. 
    /// - Tương tự như Singleton Cổ điển, vì nó tĩnh (static), ta không cần và không thể đăng ký 
    ///   vào hệ thống Dependency Injection (DI) trong Program.cs. Ở Controller, ta chỉ việc 
    ///   gọi trực tiếp: `ProductProcessorFactory.GetProcessor(categoryName)`.
    /// 
    /// ⚠️ LƯU Ý SƯ PHẠM 2 (KIẾN TRÚC TÁCH BIỆT - KHẮC PHỤC LỖI HARDCODE):
    /// - BẢN GỐC CỦA NHÓM: Dùng `switch (categoryId)` với case 1, case 2. Đây là một "Code Smell" 
    ///   (mùi code xấu) vì ID trong CSDL là tự tăng. Nếu Admin xóa danh mục đi tạo lại, ID thành 3, 4, 
    ///   Pattern sẽ hỏng hoàn toàn.
    /// - BẢN REFACTOR NÀY: Chuyển sang phân tích bằng `string categoryName` (chứa chữ "Nam", "Nữ"). 
    ///   Giải pháp này đảm bảo logic kiến trúc luôn chính xác bất chấp CSDL có thay đổi ID như thế nào.
    /// =========================================================================
    /// </summary>

    // 1. Bộ xử lý dành riêng cho Nước hoa Nam
    public class MenPerfumeProcessor : IProductProcessor
    {
        public decimal CalculateFinalPrice(decimal basePrice)
        {
            return basePrice; // Nước hoa nam giữ nguyên giá
        }

        public string GetPromotionNote()
        {
            return "🎁 Ưu đãi Nước hoa Nam: Tặng kèm mẫu thử sữa tắm mạnh mẽ cho quý ông.";
        }
    }

    // 2. Bộ xử lý dành riêng cho Nước hoa Nữ
    public class WomenPerfumeProcessor : IProductProcessor
    {
        public decimal CalculateFinalPrice(decimal basePrice)
        {
            return basePrice; // Nước hoa nữ giữ nguyên giá
        }

        public string GetPromotionNote()
        {
            return "🎁 Ưu đãi Nước hoa Nữ: Miễn phí gói quà cao cấp và thiệp chúc mừng mạ vàng.";
        }
    }

    // 3. Bộ xử lý Mặc định (Unisex hoặc các loại khác)
    public class DefaultPerfumeProcessor : IProductProcessor
    {
        public decimal CalculateFinalPrice(decimal basePrice)
        {
            return basePrice;
        }

        public string GetPromotionNote()
        {
            return "🎁 Ưu đãi chung: Tặng kèm voucher 50K cho lần mua hàng tiếp theo.";
        }
    }

    // ==========================================
    // 4. LỚP NHÀ MÁY (FACTORY) - Nơi quyết định tạo ra đối tượng nào
    // ==========================================
    public class ProductProcessorFactory
    {
        // Nhận vào Tên Danh Mục (Category Name) thay vì ID cứng
        public static IProductProcessor GetProcessor(string categoryName)
        {
            // Tránh lỗi Null Exception
            if (string.IsNullOrWhiteSpace(categoryName))
                return new DefaultPerfumeProcessor();

            var lowerName = categoryName.ToLower();

            // Nếu tên danh mục có chứa chữ "Nam" hoặc "Men"
            if (lowerName.Contains("nam") || lowerName.Contains("men"))
            {
                return new MenPerfumeProcessor();
            }
            // Nếu tên danh mục có chứa chữ "Nữ", "Nu" hoặc "Women"
            else if (lowerName.Contains("nữ") || lowerName.Contains("nu") || lowerName.Contains("women"))
            {
                return new WomenPerfumeProcessor();
            }
            // Mặc định trả về Unisex
            else
            {
                return new DefaultPerfumeProcessor();
            }
        }
    }
}
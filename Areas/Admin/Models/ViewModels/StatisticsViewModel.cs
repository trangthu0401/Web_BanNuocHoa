using System.ComponentModel.DataAnnotations;

namespace PerfumeStore.Areas.Admin.Models.ViewModels
{
    /// <summary>
    /// Model chứa thống kê sản phẩm theo danh mục
    /// </summary>
    public class ProductByCategory
    {
        /// <summary>Tên danh mục</summary>
        public string CategoryName { get; set; } = string.Empty;
        /// <summary>Số lượng sản phẩm trong danh mục</summary>
        public int ProductCount { get; set; }
        /// <summary>Phần trăm so với tổng số sản phẩm</summary>
        public decimal Percentage { get; set; }
    }

    /// <summary>
    /// Model chứa thống kê sản phẩm theo thương hiệu
    /// </summary>
    public class ProductByBrand
    {
        /// <summary>Tên thương hiệu</summary>
        public string BrandName { get; set; } = string.Empty;
        /// <summary>Số lượng sản phẩm của thương hiệu</summary>
        public int ProductCount { get; set; }
        /// <summary>Phần trăm so với tổng số sản phẩm</summary>
        public decimal Percentage { get; set; }
    }

    /// <summary>
    /// Model chứa thông tin tồn kho sản phẩm
    /// </summary>
    public class ProductStock
    {
        /// <summary>ID sản phẩm</summary>
        public int ProductId { get; set; }
        /// <summary>Tên sản phẩm</summary>
        public string ProductName { get; set; } = string.Empty;
        /// <summary>Tên thương hiệu</summary>
        public string BrandName { get; set; } = string.Empty;
        /// <summary>Số lượng tồn kho</summary>
        public int Stock { get; set; }
        /// <summary>Trạng thái tồn kho (Hết hàng, Sắp hết, Bình thường)</summary>
        public string StockStatus { get; set; } = string.Empty;
        /// <summary>Màu hiển thị cho trạng thái</summary>
        public string StatusColor { get; set; } = string.Empty;
    }

    /// <summary>
    /// Model chứa thông báo sản phẩm sắp hết hàng
    /// </summary>
    public class LowStockNotification
    {
        /// <summary>ID sản phẩm</summary>
        public int ProductId { get; set; }
        /// <summary>Tên sản phẩm</summary>
        public string ProductName { get; set; } = string.Empty;
        /// <summary>Tên thương hiệu</summary>
        public string BrandName { get; set; } = string.Empty;
        /// <summary>Số lượng tồn kho</summary>
        public int Stock { get; set; }
        /// <summary>Thời gian tạo thông báo</summary>
        public DateTime CreatedAt { get; set; }
        /// <summary>Mức độ ưu tiên (High, Medium, Low)</summary>
        public string Priority { get; set; } = string.Empty;
    }

    /// <summary>
    /// ViewModel chứa tất cả thống kê mới cho dashboard
    /// </summary>
    public class ExtendedDashboardViewModel : DashboardViewModel
    {
        // ========== THỐNG KÊ SẢN PHẨM ==========
        
        /// <summary>Tổng số sản phẩm trong CSDL</summary>
        [Display(Name = "Tổng số sản phẩm")]
        public int TotalProducts { get; set; }

        /// <summary>Số sản phẩm đang được bán (IsPublished = true)</summary>
        [Display(Name = "Sản phẩm đang bán")]
        public int PublishedProducts { get; set; }

        /// <summary>Số sản phẩm ngừng bán (IsPublished = false)</summary>
        [Display(Name = "Sản phẩm ngừng bán")]
        public int UnpublishedProducts { get; set; }

        /// <summary>Thống kê sản phẩm theo danh mục</summary>
        [Display(Name = "Sản phẩm theo danh mục")]
        public List<ProductByCategory> ProductsByCategory { get; set; } = new List<ProductByCategory>();

        /// <summary>Thống kê sản phẩm theo thương hiệu</summary>
        [Display(Name = "Sản phẩm theo thương hiệu")]
        public List<ProductByBrand> ProductsByBrand { get; set; } = new List<ProductByBrand>();

        // ========== THỐNG KÊ TỒN KHO ==========
        
        /// <summary>Tổng giá trị tồn kho (Stock * Price)</summary>
        [Display(Name = "Tổng giá trị tồn kho")]
        [DisplayFormat(DataFormatString = "{0:N0} VNĐ")]
        public decimal TotalStockValue { get; set; }

        /// <summary>Số sản phẩm hết hàng (Stock = 0)</summary>
        [Display(Name = "Sản phẩm hết hàng")]
        public int OutOfStockProducts { get; set; }

        /// <summary>Số sản phẩm sắp hết hàng (Stock <= 10)</summary>
        [Display(Name = "Sản phẩm sắp hết hàng")]
        public int LowStockProducts { get; set; }

        /// <summary>Danh sách chi tiết tồn kho các sản phẩm</summary>
        [Display(Name = "Chi tiết tồn kho")]
        public List<ProductStock> ProductStocks { get; set; } = new List<ProductStock>();

        // ========== THÔNG BÁO ==========
        
        /// <summary>Danh sách thông báo sản phẩm sắp hết hàng</summary>
        [Display(Name = "Thông báo sắp hết hàng")]
        public List<LowStockNotification> LowStockNotifications { get; set; } = new List<LowStockNotification>();

        /// <summary>Số lượng thông báo chưa đọc</summary>
        [Display(Name = "Thông báo chưa đọc")]
        public int UnreadNotifications { get; set; }
    }
}
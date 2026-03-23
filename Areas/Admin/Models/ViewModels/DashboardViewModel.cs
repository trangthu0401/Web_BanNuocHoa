using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PerfumeStore.Areas.Admin.Models
{
    /// <summary>
    /// Model chứa doanh thu theo ngày/tháng/năm
    /// Dùng để hiển thị biểu đồ doanh thu theo thời gian
    /// </summary>
    public class RevenueByDate
    {
        /// <summary>Ngày/tháng/năm</summary>
        public DateTime Ngay { get; set; }
        /// <summary>Tổng doanh thu trong ngày/tháng/năm đó</summary>
        public decimal DoanhThu { get; set; }
    }

    /// <summary>
    /// Model chứa thông tin doanh thu của sản phẩm
    /// Dùng để hiển thị top sản phẩm bán chạy
    /// </summary>
    public class RevenueByProduct
    {
        /// <summary>Tên sản phẩm</summary>
        public string ProductName { get; set; } = string.Empty;
        /// <summary>Tổng doanh thu từ sản phẩm này</summary>
        public decimal DoanhThu { get; set; }
        /// <summary>Tổng số lượng đã bán</summary>
        public int SoLuongBan { get; set; }
    }

    /// <summary>
    /// Model chứa doanh thu theo thương hiệu
    /// Dùng để hiển thị biểu đồ phân bố doanh thu theo thương hiệu
    /// </summary>
    public class RevenueByBrand
    {
        /// <summary>Tên thương hiệu</summary>
        public string BrandName { get; set; } = string.Empty;
        /// <summary>Tổng doanh thu từ thương hiệu này</summary>
        public decimal DoanhThu { get; set; }
    }

    /// <summary>
    /// ViewModel chứa các thông tin filter cho dashboard
    /// Bao gồm: thời gian (ngày/tháng/năm), thương hiệu
    /// </summary>
    public class DashboardFilterViewModel
    {
        /// <summary>Loại thời gian: "day" (theo ngày), "month" (theo tháng), "year" (theo năm)</summary>
        [Display(Name = "Loại thời gian")]
        public string TimeType { get; set; } = "month"; // day, month, year

        /// <summary>Từ ngày (dùng khi TimeType = "day")</summary>
        [Display(Name = "Từ ngày")]
        [DataType(DataType.Date)]
        public DateTime? FromDate { get; set; }

        /// <summary>Đến ngày (dùng khi TimeType = "day")</summary>
        [Display(Name = "Đến ngày")]
        [DataType(DataType.Date)]
        public DateTime? ToDate { get; set; }

        /// <summary>Tháng (1-12, dùng khi TimeType = "month")</summary>
        [Display(Name = "Tháng")]
        public int? Month { get; set; }

        /// <summary>Năm (dùng khi TimeType = "month" hoặc "year")</summary>
        [Display(Name = "Năm")]
        public int Year { get; set; } = DateTime.Now.Year;

        /// <summary>ID thương hiệu để lọc (null = tất cả thương hiệu)</summary>
        [Display(Name = "Thương hiệu")]
        public int? BrandId { get; set; }

        // Dropdown options - Được điền bởi Controller
        /// <summary>Danh sách thương hiệu cho dropdown</summary>
        public List<SelectListItem> BrandOptions { get; set; } = new List<SelectListItem>();
        /// <summary>Danh sách năm cho dropdown (5 năm gần nhất)</summary>
        public List<SelectListItem> YearOptions { get; set; } = new List<SelectListItem>();
        /// <summary>Danh sách tháng cho dropdown (1-12)</summary>
        public List<SelectListItem> MonthOptions { get; set; } = new List<SelectListItem>();
    }

    /// <summary>
    /// ViewModel chính chứa tất cả dữ liệu để hiển thị dashboard
    /// Bao gồm: thống kê tổng quan, biểu đồ, top sản phẩm, doanh thu theo thương hiệu
    /// </summary>
    public class DashboardViewModel
    {
        // ========== THỐNG KÊ TỔNG QUAN ==========
        
        /// <summary>Tổng doanh thu hôm nay (chỉ hiển thị khi không có filter)</summary>
        [Display(Name = "Tổng doanh thu hôm nay")]
        [DisplayFormat(DataFormatString = "{0:N0} VNĐ")]
        public decimal TongDoanhThuHomNay { get; set; }

        /// <summary>Tổng doanh thu tháng này (chỉ hiển thị khi không có filter)</summary>
        [Display(Name = "Tổng doanh thu tháng này")]
        [DisplayFormat(DataFormatString = "{0:N0} VNĐ")]
        public decimal TongDoanhThuThangNay { get; set; }

        /// <summary>Tổng doanh thu năm này (chỉ hiển thị khi không có filter)</summary>
        [Display(Name = "Tổng doanh thu năm này")]
        [DisplayFormat(DataFormatString = "{0:N0} VNĐ")]
        public decimal TongDoanhThuNamNay { get; set; }

        /// <summary>Tổng số đơn hàng (theo filter hoặc tháng này)</summary>
        [Display(Name = "Tổng số đơn hàng")]
        public int TongSoDonHang { get; set; }

        /// <summary>Số khách hàng mới (theo filter hoặc tổng số khách hàng)</summary>
        [Display(Name = "Số khách hàng mới")]
        public int SoKhachHangMoi { get; set; }

        // ========== DỮ LIỆU CHO BIỂU ĐỒ ==========
        
        /// <summary>Danh sách doanh thu theo ngày/tháng/năm (dùng cho biểu đồ đường)</summary>
        [Display(Name = "Doanh thu theo ngày")]
        public List<RevenueByDate> RevenueByDates { get; set; } = new List<RevenueByDate>();

        /// <summary>Top 5 sản phẩm bán chạy (dùng cho bảng top products)</summary>
        [Display(Name = "Top sản phẩm bán chạy")]
        public List<RevenueByProduct> TopProducts { get; set; } = new List<RevenueByProduct>();

        /// <summary>Doanh thu theo thương hiệu (dùng cho biểu đồ tròn)</summary>
        [Display(Name = "Doanh thu theo thương hiệu")]
        public List<RevenueByBrand> RevenueByBrands { get; set; } = new List<RevenueByBrand>();

        // ========== CÁC CHỈ SỐ BỔ SUNG ==========
        
        /// <summary>Tỷ lệ thành công đơn hàng (đơn giản hóa, hiện tại = 100%)</summary>
        public decimal TyLeThanhCong { get; set; }
        
        /// <summary>Giá trị đơn hàng trung bình (Average Order Value - AOV) = Tổng doanh thu / Số đơn hàng</summary>
        [Display(Name = "Giá trị đơn hàng trung bình")]
        [DisplayFormat(DataFormatString = "{0:N0} VNĐ")]
        public decimal GiaTriDonHangTrungBinh { get; set; } // Average Order Value (AOV)
        
        /// <summary>Phần trăm tăng trưởng tháng này so với tháng trước (chỉ hiển thị khi không có filter)</summary>
        public decimal TangTruongThang { get; set; }

        // ========== THÔNG TIN FILTER ==========
        
        /// <summary>Bộ lọc hiện tại (thời gian, thương hiệu)</summary>
        public DashboardFilterViewModel Filter { get; set; } = new DashboardFilterViewModel();
        
        /// <summary>Mô tả filter để hiển thị trên UI (ví dụ: "Tháng 10/2024 - Thương hiệu: Chanel")</summary>
        public string FilterDescription { get; set; } = string.Empty;
        
        /// <summary>Tổng doanh thu theo filter đã áp dụng (dùng khi có filter)</summary>
        public decimal TongDoanhThuLoc { get; set; } // Tổng doanh thu theo filter
    }
}
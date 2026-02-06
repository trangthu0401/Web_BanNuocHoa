using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PerfumeStore.Areas.Admin.Models;
using PerfumeStore.Areas.Admin.Models.ViewModels;
using PerfumeStore.Areas.Admin.Filters;
using OfficeOpenXml;

namespace PerfumeStore.Areas.Admin.Controllers
{
    
    [Area("Admin")]
    [AdminAuthorize] // Yêu cầu đăng nhập với quyền Admin
    public class DashboardController : Controller
    {
        private readonly PerfumeStore.Areas.Admin.Models.PerfumeStoreContext _context;

        /// <summary>
        /// Khởi tạo DashboardController với database context
        /// </summary>
        public DashboardController(PerfumeStore.Areas.Admin.Models.PerfumeStoreContext context)
        {
            _context = context;
        }

     
       
        [RequirePermission("View Dashboard")] // Yêu cầu quyền "View Dashboard"
        public async Task<IActionResult> Index(DashboardFilterViewModel filter)
        {
            // Tính toán các mốc thời gian để so sánh
            var today = DateTime.Today; // Ngày hôm nay
            var startOfMonth = new DateTime(today.Year, today.Month, 1); // Ngày đầu tháng hiện tại
            var startOfYear = new DateTime(today.Year, 1, 1); // Ngày đầu năm hiện tại
            var lastMonth = startOfMonth.AddMonths(-1); // Ngày đầu tháng trước
            var endOfLastMonth = startOfMonth.AddDays(-1); // Ngày cuối tháng trước

            // Khởi tạo ViewModel mở rộng để truyền dữ liệu ra view
            var viewModel = new ExtendedDashboardViewModel();
            viewModel.Filter = filter ?? new DashboardFilterViewModel(); // Sử dụng filter từ request hoặc tạo mới

            try
            {
               
                await SetupFilterOptions(viewModel.Filter);

                // Kiểm tra xem có bộ lọc nào được áp dụng không
                // Nếu có filter, sẽ hiển thị dữ liệu theo filter thay vì dữ liệu mặc định
                bool hasFilter = viewModel.Filter.FromDate.HasValue || // Có chọn từ ngày
                               viewModel.Filter.ToDate.HasValue || // Có chọn đến ngày
                               viewModel.Filter.Month.HasValue || // Có chọn tháng cụ thể
                               viewModel.Filter.BrandId.HasValue || // Có chọn thương hiệu
                               (!string.IsNullOrEmpty(viewModel.Filter.TimeType) && viewModel.Filter.TimeType != "month") || // TimeType khác "month"
                               (viewModel.Filter.TimeType == "year" && viewModel.Filter.Year != DateTime.Now.Year) || // Chọn năm khác năm hiện tại
                               (!string.IsNullOrEmpty(viewModel.Filter.TimeType)); // Có chọn TimeType

                // Debug logging để kiểm tra filter
                Console.WriteLine($"Filter Debug - TimeType: '{viewModel.Filter.TimeType}', Month: {viewModel.Filter.Month}, Year: {viewModel.Filter.Year}, BrandId: {viewModel.Filter.BrandId}, HasFilter: {hasFilter}");

                await LoadFilteredData(viewModel, viewModel.Filter);
                
                // Load thống kê mới (sản phẩm, tồn kho, thông báo)
                await LoadProductStatistics(viewModel);
                await LoadStockStatistics(viewModel);
                await LoadLowStockNotifications(viewModel);
                
                if (hasFilter)
                {
                    // Nếu có filter, hiển thị dữ liệu theo filter thay vì dữ liệu mặc định
                    viewModel.TongDoanhThuHomNay = 0; // Không hiển thị doanh thu hôm nay khi có filter
                    viewModel.TongDoanhThuThangNay = viewModel.TongDoanhThuLoc; // Dùng doanh thu đã lọc
                    viewModel.TongDoanhThuNamNay = viewModel.TongDoanhThuLoc; // Dùng doanh thu đã lọc
                    viewModel.TangTruongThang = 0; // Không so sánh tăng trưởng khi có filter
                    
                    // Đếm số đơn hàng trong khoảng thời gian đã lọc
                    DateTime startDate, endDate;
                    GetDateRange(viewModel.Filter, out startDate, out endDate); // Lấy khoảng thời gian từ filter
                    
                    // Query đơn hàng trong khoảng thời gian
                    var filteredQuery = _context.Orders.Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate);
                    
                    // Nếu có lọc theo thương hiệu, chỉ lấy đơn hàng có sản phẩm của thương hiệu đó
                    if (viewModel.Filter.BrandId.HasValue)
                    {
                        filteredQuery = filteredQuery.Where(o => o.OrderDetails.Any(od => od.Product.BrandId == viewModel.Filter.BrandId.Value));
                    }
                    
                    viewModel.TongSoDonHang = await filteredQuery.CountAsync(); // Đếm số đơn hàng
                    
                    // Đếm số khách hàng mới trong khoảng thời gian
                    viewModel.SoKhachHangMoi = await _context.Customers
                        .Where(c => c.CreatedDate >= startDate && c.CreatedDate <= endDate)
                        .CountAsync();
                    
                    viewModel.TyLeThanhCong = 100; // Tỷ lệ thành công (đơn giản hóa)
                    
                    // Tính giá trị đơn hàng trung bình = Tổng doanh thu / Số đơn hàng
                    viewModel.GiaTriDonHangTrungBinh = viewModel.TongSoDonHang > 0 ? 
                        viewModel.TongDoanhThuLoc / viewModel.TongSoDonHang : 0;
                }
                else
                {
                    // Load dữ liệu mặc định (không có filter) - hiển thị thống kê tổng quan
                    
                    // Tổng doanh thu hôm nay: Lấy tất cả đơn hàng có OrderDate = hôm nay
                    viewModel.TongDoanhThuHomNay = await _context.Orders
                        .Where(o => o.OrderDate.HasValue && o.OrderDate.Value.Date == today)
                        .SumAsync(o => o.TotalAmount ?? 0);

                    // Tổng doanh thu tháng này: Lấy tất cả đơn hàng từ đầu tháng đến nay
                    viewModel.TongDoanhThuThangNay = await _context.Orders
                        .Where(o => o.OrderDate >= startOfMonth)
                        .SumAsync(o => o.TotalAmount ?? 0m);

                    // Tổng doanh thu năm này: Lấy tất cả đơn hàng từ đầu năm đến nay
                    viewModel.TongDoanhThuNamNay = await _context.Orders
                        .Where(o => o.OrderDate >= startOfYear)
                        .SumAsync(o => o.TotalAmount ?? 0m);

                    // Doanh thu tháng trước để tính tăng trưởng
                    var doanhThuThangTruoc = await _context.Orders
                        .Where(o => o.OrderDate >= lastMonth && o.OrderDate <= endOfLastMonth)
                        .SumAsync(o => o.TotalAmount ?? 0m);

                    // Tính phần trăm tăng trưởng: ((Tháng này - Tháng trước) / Tháng trước) * 100
                    viewModel.TangTruongThang = doanhThuThangTruoc > 0 ? 
                        ((viewModel.TongDoanhThuThangNay - doanhThuThangTruoc) / doanhThuThangTruoc) * 100 : 0;

                    // Tổng số đơn hàng tháng này
                    viewModel.TongSoDonHang = await _context.Orders
                        .Where(o => o.OrderDate >= startOfMonth)
                        .CountAsync();

                    // Số khách hàng mới: Đếm tất cả khách hàng (có thể cải thiện để chỉ đếm khách hàng mới tháng này)
                    viewModel.SoKhachHangMoi = await _context.Customers.CountAsync();

                    // Tính toán các metrics bổ sung
                    var totalOrders = await _context.Orders
                        .Where(o => o.OrderDate >= startOfMonth)
                        .CountAsync();

                    viewModel.TyLeThanhCong = totalOrders > 0 ? 100 : 0; // Tỷ lệ thành công (đơn giản hóa)
                    
                    // Giá trị đơn hàng trung bình (Average Order Value - AOV)
                    viewModel.GiaTriDonHangTrungBinh = totalOrders > 0 ? 
                        viewModel.TongDoanhThuThangNay / totalOrders : 0;
                }

            }
            catch (Exception ex)
            {
                // Xử lý lỗi: Log lỗi và load dữ liệu fallback (dữ liệu cơ bản)
                Console.WriteLine($"Dashboard error: {ex.Message}");
                
                // Fallback: Load dữ liệu cơ bản nếu có lỗi với các query phức tạp
                await LoadFallbackData(viewModel, today, startOfMonth, startOfYear);
            }

            return View(viewModel);
        }

     
        private async Task LoadFallbackDataOriginalOld(DashboardViewModel viewModel, DateTime today, DateTime startOfMonth, DateTime startOfYear)
        {
            try
            {
                // Dữ liệu doanh thu theo ngày (30 ngày gần nhất)
                // Group by ngày và tính tổng doanh thu mỗi ngày
                viewModel.RevenueByDates = await _context.Orders
                    .Where(o => o.OrderDate.HasValue && o.OrderDate >= today.AddDays(-30))
                    .GroupBy(o => o.OrderDate.Value.Date) // Nhóm theo ngày
                    .Select(g => new RevenueByDate
                    {
                        Ngay = g.Key, // Ngày
                        DoanhThu = g.Sum(o => o.TotalAmount ?? 0m) // Tổng doanh thu ngày đó
                    })
                    .OrderBy(r => r.Ngay) // Sắp xếp theo ngày
                    .ToListAsync();

                // Điền các ngày thiếu để biểu đồ hiển thị đầy đủ (ngày không có đơn hàng = 0)
                FillMissingDates(viewModel.RevenueByDates, today.AddDays(-30), today);

                // Top 5 sản phẩm bán chạy tháng này
                // Group by ProductId và tính tổng số lượng bán + doanh thu
                viewModel.TopProducts = await _context.OrderDetails
                    .Include(od => od.Product) // Load thông tin sản phẩm
                    .Include(od => od.Order) // Load thông tin đơn hàng
                    .Where(od => od.Order.OrderDate >= startOfMonth) // Chỉ lấy đơn hàng tháng này
                    .GroupBy(od => new { od.ProductId, od.Product.ProductName }) // Nhóm theo sản phẩm
                    .Select(g => new RevenueByProduct
                    {
                        ProductName = g.Key.ProductName, // Tên sản phẩm
                        SoLuongBan = (g.Sum(od => od.Quantity) ?? 0), // Tổng số lượng bán
                        DoanhThu = g.Sum(od => od.TotalPrice) // Tổng doanh thu
                    })
                    .OrderByDescending(p => p.SoLuongBan) // Sắp xếp theo số lượng bán giảm dần
                    .Take(5) // Chỉ lấy top 5
                    .ToListAsync();

                // Dữ liệu doanh thu theo thương hiệu tháng này
                // Group by Brand và tính tổng doanh thu
                viewModel.RevenueByBrands = await _context.OrderDetails
                    .Include(od => od.Product) // Load sản phẩm
                    .ThenInclude(p => p.Brand) // Load thương hiệu
                    .Include(od => od.Order) // Load đơn hàng
                    .Where(od => od.Order.OrderDate >= startOfMonth) // Chỉ lấy đơn hàng tháng này
                    .GroupBy(od => od.Product.Brand.BrandName) // Nhóm theo tên thương hiệu
                    .Select(g => new RevenueByBrand
                    {
                        BrandName = g.Key, // Tên thương hiệu
                        DoanhThu = g.Sum(od => od.TotalPrice) // Tổng doanh thu
                    })
                    .OrderByDescending(b => b.DoanhThu) // Sắp xếp theo doanh thu giảm dần
                    .Take(10) // Chỉ lấy top 10
                    .ToListAsync();

                // Đặt mô tả filter cho view mặc định (không có filter)
                viewModel.FilterDescription = "";
                viewModel.TongDoanhThuLoc = 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fallback data error: {ex.Message}");
            }
        }

        private async Task SetupFilterOptions(DashboardFilterViewModel filter)
        {
            // Tạo danh sách thương hiệu cho dropdown
            var brands = await _context.Brands.OrderBy(b => b.BrandName).ToListAsync(); // Lấy tất cả thương hiệu, sắp xếp theo tên
            filter.BrandOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "-- Tất cả thương hiệu --" } // Option mặc định
            };
            // Thêm các thương hiệu vào danh sách
            filter.BrandOptions.AddRange(brands.Select(b => new SelectListItem 
            { 
                Value = b.BrandId.ToString(), // Giá trị = BrandId
                Text = b.BrandName // Hiển thị = Tên thương hiệu
            }));

            // Tạo danh sách năm (5 năm gần nhất: năm hiện tại và 4 năm trước)
            var currentYear = DateTime.Now.Year;
            filter.YearOptions = Enumerable.Range(currentYear - 4, 5) // Tạo mảng [currentYear-4, currentYear-3, ..., currentYear]
                .Select(y => new SelectListItem 
                { 
                    Value = y.ToString(), // Giá trị = năm
                    Text = y.ToString(), // Hiển thị = năm
                    Selected = y == filter.Year // Đánh dấu selected nếu trùng với filter.Year
                })
                .OrderByDescending(x => x.Value) // Sắp xếp giảm dần (năm mới nhất trước)
                .ToList();

            // Tạo danh sách tháng (1-12)
            filter.MonthOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "-- Tất cả tháng --" } // Option mặc định
            };
            for (int i = 1; i <= 12; i++)
            {
                filter.MonthOptions.Add(new SelectListItem 
                { 
                    Value = i.ToString(), // Giá trị = số tháng (1-12)
                    Text = $"Tháng {i}", // Hiển thị = "Tháng 1", "Tháng 2", ...
                    Selected = i == filter.Month // Đánh dấu selected nếu trùng với filter.Month
                });
            }
        }

      
        private async Task LoadFilteredData(DashboardViewModel viewModel, DashboardFilterViewModel filter)
        {
            try
            {
                // Lấy khoảng thời gian từ filter (startDate, endDate)
                DateTime startDate, endDate;
                
                // Luôn sử dụng GetDateRange để đảm bảo tính nhất quán
                GetDateRange(filter, out startDate, out endDate);

                Console.WriteLine($"LoadFilteredData - StartDate: {startDate}, EndDate: {endDate}, TimeType: {filter.TimeType}");

                // Query cơ bản: Lấy tất cả đơn hàng trong khoảng thời gian
                var ordersQuery = _context.Orders.Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate);
                
                // Query chi tiết đơn hàng: Load kèm Product và Brand để filter theo thương hiệu
                var orderDetailsQuery = _context.OrderDetails
                    .Include(od => od.Product) // Load thông tin sản phẩm
                    .ThenInclude(p => p.Brand) // Load thông tin thương hiệu
                    .Include(od => od.Order) // Load thông tin đơn hàng
                    .Where(od => od.Order.OrderDate >= startDate && od.Order.OrderDate <= endDate); // Lọc theo thời gian

                // Áp dụng filter thương hiệu nếu có
                if (filter.BrandId.HasValue)
                {
                    // Chỉ lấy OrderDetails của sản phẩm thuộc thương hiệu được chọn
                    orderDetailsQuery = orderDetailsQuery.Where(od => od.Product.BrandId == filter.BrandId.Value);
                    // Tính tổng doanh thu từ OrderDetails (chính xác hơn khi filter theo brand)
                    viewModel.TongDoanhThuLoc = await orderDetailsQuery.SumAsync(od => od.TotalPrice);
                }
                else
                {
                    // Không có filter brand, tính tổng doanh thu từ Orders
                    viewModel.TongDoanhThuLoc = await ordersQuery.SumAsync(o => o.TotalAmount ?? 0m);
                }

                // Tạo mô tả filter để hiển thị trên UI (ví dụ: "Tháng 10/2024 - Thương hiệu: Chanel")
                viewModel.FilterDescription = GetFilterDescription(filter, startDate, endDate);

                // Load doanh thu theo thời gian dựa trên TimeType (day/month/year)
                switch (filter.TimeType)
                {
                    case "day": // Hiển thị theo ngày
                        if (filter.BrandId.HasValue)
                        {
                            // Có filter brand: Group OrderDetails theo ngày
                            viewModel.RevenueByDates = await orderDetailsQuery
                                .Where(od => od.Order.OrderDate.HasValue)
                                .GroupBy(od => od.Order.OrderDate.Value.Date) // Nhóm theo ngày
                                .Select(g => new RevenueByDate { Ngay = g.Key, DoanhThu = g.Sum(od => od.TotalPrice) })
                                .OrderBy(r => r.Ngay)
                                .ToListAsync();
                        }
                        else
                        {
                            // Không có filter brand: Group Orders theo ngày
                            viewModel.RevenueByDates = await ordersQuery
                                .Where(o => o.OrderDate.HasValue)
                                .GroupBy(o => o.OrderDate.Value.Date) // Nhóm theo ngày
                                .Select(g => new RevenueByDate { Ngay = g.Key, DoanhThu = g.Sum(o => o.TotalAmount ?? 0m) })
                                .OrderBy(r => r.Ngay)
                                .ToListAsync();
                        }
                        // Điền các ngày thiếu (ngày không có đơn hàng = 0)
                        FillMissingDates(viewModel.RevenueByDates, startDate, endDate);
                        break;

                    case "month": // Hiển thị theo tháng
                        if (filter.Month.HasValue && filter.Month.Value > 0)
                        {
                            // Chọn tháng cụ thể: Hiển thị theo ngày trong tháng đó
                            goto case "day"; // Chuyển sang xử lý như "day"
                        }
                        else
                        {
                            // Không chọn tháng cụ thể: Hiển thị tất cả các tháng trong năm
                            if (filter.BrandId.HasValue)
                            {
                                // Có filter brand: Group OrderDetails theo năm + tháng
                                viewModel.RevenueByDates = await orderDetailsQuery
                                    .Where(od => od.Order.OrderDate.HasValue)
                                    .GroupBy(od => new { od.Order.OrderDate.Value.Year, od.Order.OrderDate.Value.Month }) // Nhóm theo năm + tháng
                                .Select(g => new RevenueByDate 
                                { 
                                    Ngay = new DateTime(g.Key.Year, g.Key.Month, 1), // Ngày = ngày đầu tháng
                                    DoanhThu = g.Sum(od => od.TotalPrice) 
                                })
                                    .OrderBy(r => r.Ngay)
                                    .ToListAsync();
                            }
                            else
                            {
                                // Không có filter brand: Group Orders theo năm + tháng
                                viewModel.RevenueByDates = await ordersQuery
                                    .Where(o => o.OrderDate.HasValue)
                                    .GroupBy(o => new { o.OrderDate.Value.Year, o.OrderDate.Value.Month }) // Nhóm theo năm + tháng
                                    .Select(g => new RevenueByDate 
                                    { 
                                        Ngay = new DateTime(g.Key.Year, g.Key.Month, 1), // Ngày = ngày đầu tháng
                                        DoanhThu = g.Sum(o => o.TotalAmount ?? 0m) 
                                    })
                                    .OrderBy(r => r.Ngay)
                                    .ToListAsync();
                            }
                            
                            // Điền các tháng thiếu (tháng không có đơn hàng = 0)
                            FillMissingMonths(viewModel.RevenueByDates, filter.Year);
                        }
                        break;

                    case "year": // Hiển thị theo năm
                        if (filter.BrandId.HasValue)
                        {
                            // Có filter brand: Group OrderDetails theo năm
                            viewModel.RevenueByDates = await orderDetailsQuery
                                .Where(od => od.Order.OrderDate.HasValue)
                                .GroupBy(od => od.Order.OrderDate.Value.Year) // Nhóm theo năm
                                .Select(g => new RevenueByDate 
                                { 
                                    Ngay = new DateTime(g.Key, 1, 1), // Ngày = ngày đầu năm
                                    DoanhThu = g.Sum(od => od.TotalPrice) 
                                })
                                .OrderBy(r => r.Ngay)
                                .ToListAsync();
                        }
                        else
                        {
                            // Không có filter brand: Group Orders theo năm
                            viewModel.RevenueByDates = await ordersQuery
                                .Where(o => o.OrderDate.HasValue)
                                .GroupBy(o => o.OrderDate.Value.Year) // Nhóm theo năm
                                .Select(g => new RevenueByDate 
                                { 
                                    Ngay = new DateTime(g.Key, 1, 1), // Ngày = ngày đầu năm
                                    DoanhThu = g.Sum(o => o.TotalAmount ?? 0m) 
                                })
                                .OrderBy(r => r.Ngay)
                                .ToListAsync();
                        }
                        break;
                }

                // Top 5 sản phẩm bán chạy trong khoảng thời gian đã lọc
                viewModel.TopProducts = await orderDetailsQuery
                    .GroupBy(od => new { od.ProductId, od.Product.ProductName }) // Nhóm theo sản phẩm
                    .Select(g => new RevenueByProduct
                    {
                        ProductName = g.Key.ProductName, // Tên sản phẩm
                        SoLuongBan = (g.Sum(od => od.Quantity) ?? 0), // Tổng số lượng bán
                        DoanhThu = g.Sum(od => od.TotalPrice) // Tổng doanh thu
                    })
                    .OrderByDescending(p => p.SoLuongBan) // Sắp xếp theo số lượng bán giảm dần
                    .Take(5) // Chỉ lấy top 5
                    .ToListAsync();

                // Doanh thu theo thương hiệu
                if (filter.BrandId.HasValue)
                {
                    // Nếu đã filter theo 1 thương hiệu cụ thể, chỉ hiển thị thương hiệu đó
                    var brand = await _context.Brands.FindAsync(filter.BrandId.Value);
                    if (brand != null)
                    {
                        viewModel.RevenueByBrands = new List<RevenueByBrand>
                        {
                            new RevenueByBrand { BrandName = brand.BrandName, DoanhThu = viewModel.TongDoanhThuLoc }
                        };
                    }
                }
                else
                {
                    // Không có filter brand: Hiển thị top 10 thương hiệu có doanh thu cao nhất
                    viewModel.RevenueByBrands = await orderDetailsQuery
                        .GroupBy(od => od.Product.Brand.BrandName) // Nhóm theo tên thương hiệu
                        .Select(g => new RevenueByBrand { BrandName = g.Key, DoanhThu = g.Sum(od => od.TotalPrice) })
                        .OrderByDescending(b => b.DoanhThu) // Sắp xếp theo doanh thu giảm dần
                        .Take(10) // Chỉ lấy top 10
                        .ToListAsync();
                }

                Console.WriteLine($"LoadFilteredData completed - Revenue: {viewModel.TongDoanhThuLoc}, Products: {viewModel.TopProducts.Count}, Brands: {viewModel.RevenueByBrands.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LoadFilteredData error: {ex.Message}");
                viewModel.RevenueByDates = new List<RevenueByDate>();
                viewModel.TopProducts = new List<RevenueByProduct>();
                viewModel.RevenueByBrands = new List<RevenueByBrand>();
            }
        }

    
        private void GetDateRange(DashboardFilterViewModel filter, out DateTime startDate, out DateTime endDate)
        {
            var today = DateTime.Today;

            // Đặt giá trị mặc định nếu không được cung cấp
            if (string.IsNullOrEmpty(filter.TimeType))
            {
                filter.TimeType = "day"; // Mặc định: theo ngày
            }
            
            if (filter.Year == 0)
            {
                filter.Year = today.Year; // Mặc định: năm hiện tại
            }

            // Tính toán khoảng thời gian dựa trên TimeType
            switch (filter.TimeType)
            {
                case "day": // Lọc theo ngày
                    if (filter.FromDate.HasValue && filter.ToDate.HasValue)
                    {
                        // Có chọn từ ngày và đến ngày: Dùng khoảng thời gian đã chọn
                        startDate = filter.FromDate.Value.Date; // Bắt đầu từ 00:00:00 của FromDate
                        endDate = filter.ToDate.Value.Date.AddDays(1).AddSeconds(-1); // Kết thúc lúc 23:59:59 của ToDate
                    }
                    else
                    {
                        // Không chọn ngày: Mặc định 30 ngày gần nhất
                        startDate = today.AddDays(-30);
                        endDate = today.AddDays(1).AddSeconds(-1); // Đến hết ngày hôm nay
                    }
                    break;

                case "month": // Lọc theo tháng
                    if (filter.Month.HasValue && filter.Month.Value > 0)
                    {
                        // Có chọn tháng cụ thể: Lấy toàn bộ tháng đó
                        startDate = new DateTime(filter.Year, filter.Month.Value, 1); // Ngày đầu tháng
                        endDate = startDate.AddMonths(1).AddSeconds(-1); // 23:59:59 ngày cuối tháng
                    }
                    else
                    {
                        // Không chọn tháng: Lấy toàn bộ năm
                        startDate = new DateTime(filter.Year, 1, 1); // Ngày đầu năm
                        endDate = new DateTime(filter.Year, 12, 31, 23, 59, 59); // Ngày cuối năm
                    }
                    break;

                case "year": // Lọc theo năm
                    // Lấy toàn bộ năm
                    startDate = new DateTime(filter.Year, 1, 1); // Ngày đầu năm
                    endDate = new DateTime(filter.Year, 12, 31, 23, 59, 59); // Ngày cuối năm
                    break;

                default:
                    // Mặc định: 30 ngày gần nhất
                    startDate = today.AddDays(-30);
                    endDate = today.AddDays(1).AddSeconds(-1);
                    filter.TimeType = "day"; // Đặt lại TimeType về "day"
                    break;
            }

            // Debug logging
            Console.WriteLine($"GetDateRange - TimeType: {filter.TimeType}, Year: {filter.Year}, Month: {filter.Month}, StartDate: {startDate}, EndDate: {endDate}");
        }

       
        private string GetFilterDescription(DashboardFilterViewModel filter, DateTime startDate, DateTime endDate)
        {
            var description = "";

            // Tạo mô tả theo TimeType
            switch (filter.TimeType)
            {
                case "day":
                    description = $"Từ {startDate:dd/MM/yyyy} đến {endDate:dd/MM/yyyy}";
                    break;
                case "month":
                    if (filter.Month.HasValue)
                        description = $"Tháng {filter.Month}/{filter.Year}"; // Ví dụ: "Tháng 10/2024"
                    else
                        description = $"Năm {filter.Year}"; // Ví dụ: "Năm 2024"
                    break;
                case "year":
                    description = $"Năm {filter.Year}";
                    break;
            }

            // Thêm thông tin thương hiệu nếu có filter
            if (filter.BrandId.HasValue)
            {
                var brand = _context.Brands.Find(filter.BrandId.Value);
                if (brand != null)
                    description += $" - Thương hiệu: {brand.BrandName}";
            }

            return description;
        }

     
        private void FillMissingDates(List<RevenueByDate> revenueData, DateTime startDate, DateTime endDate)
        {
            // Lấy danh sách các ngày đã có dữ liệu
            var existingDates = revenueData.Select(r => r.Ngay.Date).ToHashSet();
            
            // Duyệt từ startDate đến endDate, thêm các ngày thiếu với DoanhThu = 0
            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                if (!existingDates.Contains(date))
                {
                    revenueData.Add(new RevenueByDate { Ngay = date, DoanhThu = 0 }); // Ngày không có đơn hàng = 0
                }
            }
            
            // Sắp xếp lại theo ngày
            revenueData.Sort((a, b) => a.Ngay.CompareTo(b.Ngay));
        }

   
      
       
        private void FillMissingMonths(List<RevenueByDate> revenueData, int year)
        {
            // Lấy danh sách các tháng đã có dữ liệu
            var existingMonths = revenueData.Select(r => r.Ngay.Month).ToHashSet();
            
            // Duyệt từ tháng 1 đến 12, thêm các tháng thiếu với DoanhThu = 0
            for (int month = 1; month <= 12; month++)
            {
                if (!existingMonths.Contains(month))
                {
                    revenueData.Add(new RevenueByDate { Ngay = new DateTime(year, month, 1), DoanhThu = 0 });
                }
            }
            
            // Sắp xếp lại theo tháng
            revenueData.Sort((a, b) => a.Ngay.CompareTo(b.Ngay));
        }

        // ========== CÁC METHOD MỚI CHO THỐNG KÊ ==========

        /// <summary>
        /// Load thống kê sản phẩm (tổng số, theo danh mục, theo thương hiệu)
        /// </summary>
        private async Task LoadProductStatistics(ExtendedDashboardViewModel viewModel)
        {
            try
            {
                // Tổng số sản phẩm
                viewModel.TotalProducts = await _context.Products.CountAsync();
                
                // Sản phẩm đang bán và ngừng bán
                viewModel.PublishedProducts = await _context.Products.CountAsync(p => p.IsPublished == true);
                viewModel.UnpublishedProducts = await _context.Products.CountAsync(p => p.IsPublished == false);

                // Thống kê sản phẩm theo danh mục
                var categoryStats = await _context.Products
                    .Include(p => p.Categories)
                    .SelectMany(p => p.Categories)
                    .GroupBy(c => c.CategoryName)
                    .Select(g => new ProductByCategory
                    {
                        CategoryName = g.Key,
                        ProductCount = g.Count()
                    })
                    .OrderByDescending(x => x.ProductCount)
                    .ToListAsync();

                // Tính phần trăm cho danh mục
                foreach (var item in categoryStats)
                {
                    item.Percentage = viewModel.TotalProducts > 0 ? 
                        Math.Round((decimal)item.ProductCount / viewModel.TotalProducts * 100, 1) : 0;
                }
                viewModel.ProductsByCategory = categoryStats;

                // Thống kê sản phẩm theo thương hiệu
                var brandStats = await _context.Products
                    .Include(p => p.Brand)
                    .GroupBy(p => p.Brand.BrandName)
                    .Select(g => new ProductByBrand
                    {
                        BrandName = g.Key,
                        ProductCount = g.Count()
                    })
                    .OrderByDescending(x => x.ProductCount)
                    .ToListAsync();

                // Tính phần trăm cho thương hiệu
                foreach (var item in brandStats)
                {
                    item.Percentage = viewModel.TotalProducts > 0 ? 
                        Math.Round((decimal)item.ProductCount / viewModel.TotalProducts * 100, 1) : 0;
                }
                viewModel.ProductsByBrand = brandStats;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LoadProductStatistics error: {ex.Message}");
            }
        }

        /// <summary>
        /// Load thống kê tồn kho
        /// </summary>
        private async Task LoadStockStatistics(ExtendedDashboardViewModel viewModel)
        {
            try
            {
                // Tổng giá trị tồn kho
                viewModel.TotalStockValue = await _context.Products
                    .SumAsync(p => p.Stock * p.Price);

                // Số sản phẩm hết hàng và sắp hết hàng
                viewModel.OutOfStockProducts = await _context.Products.CountAsync(p => p.Stock == 0);
                viewModel.LowStockProducts = await _context.Products.CountAsync(p => p.Stock > 0 && p.Stock <= 10);

                // Chi tiết tồn kho các sản phẩm
                var stockDetails = await _context.Products
                    .Include(p => p.Brand)
                    .Select(p => new ProductStock
                    {
                        ProductId = p.ProductId,
                        ProductName = p.ProductName,
                        BrandName = p.Brand.BrandName,
                        Stock = p.Stock,
                        StockStatus = p.Stock == 0 ? "Hết hàng" : 
                                    p.Stock <= 5 ? "Sắp hết" : 
                                    p.Stock <= 10 ? "Ít hàng" : "Bình thường",
                        StatusColor = p.Stock == 0 ? "danger" : 
                                    p.Stock <= 5 ? "warning" : 
                                    p.Stock <= 10 ? "info" : "success"
                    })
                    .OrderBy(p => p.Stock)
                    .ThenBy(p => p.ProductName)
                    .ToListAsync();

                viewModel.ProductStocks = stockDetails;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LoadStockStatistics error: {ex.Message}");
            }
        }

        /// <summary>
        /// Load thông báo sản phẩm sắp hết hàng
        /// </summary>
        private async Task LoadLowStockNotifications(ExtendedDashboardViewModel viewModel)
        {
            try
            {
                // Lấy sản phẩm sắp hết hàng (Stock <= 10)
                var lowStockProducts = await _context.Products
                    .Include(p => p.Brand)
                    .Where(p => p.Stock <= 10)
                    .Select(p => new LowStockNotification
                    {
                        ProductId = p.ProductId,
                        ProductName = p.ProductName,
                        BrandName = p.Brand.BrandName,
                        Stock = p.Stock,
                        CreatedAt = DateTime.Now,
                        Priority = p.Stock == 0 ? "High" : 
                                 p.Stock <= 5 ? "Medium" : "Low"
                    })
                    .OrderBy(n => n.Stock)
                    .ThenBy(n => n.ProductName)
                    .ToListAsync();

                viewModel.LowStockNotifications = lowStockProducts;
                viewModel.UnreadNotifications = lowStockProducts.Count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LoadLowStockNotifications error: {ex.Message}");
            }
        }

        /// <summary>
        /// API để lấy thông báo sắp hết hàng (dùng cho notification icon)
        /// </summary>
        [HttpGet]
        [RequirePermission("View Dashboard")]
        public async Task<IActionResult> GetLowStockNotifications()
        {
            try
            {
                var notifications = await _context.Products
                    .Include(p => p.Brand)
                    .Where(p => p.Stock <= 10)
                    .Select(p => new
                    {
                        productId = p.ProductId,
                        productName = p.ProductName,
                        brandName = p.Brand.BrandName,
                        stock = p.Stock,
                        priority = p.Stock == 0 ? "High" : 
                                 p.Stock <= 5 ? "Medium" : "Low",
                        message = p.Stock == 0 ? $"{p.ProductName} đã hết hàng" :
                                $"{p.ProductName} chỉ còn {p.Stock} sản phẩm"
                    })
                    .OrderBy(n => n.stock)
                    .ToListAsync();

                return Json(new { 
                    success = true, 
                    count = notifications.Count,
                    notifications = notifications 
                });
            }
            catch (Exception ex)
            {
                return Json(new { 
                    success = false, 
                    message = ex.Message 
                });
            }
        }

        /// <summary>
        /// Trang chi tiết quản lý tồn kho
        /// </summary>
        [RequirePermission("View Dashboard")]
        public async Task<IActionResult> StockManagement()
        {
            try
            {
                var viewModel = new ExtendedDashboardViewModel();
                await LoadStockStatistics(viewModel);
                
                return View(viewModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"StockManagement error: {ex.Message}");
                return View(new ExtendedDashboardViewModel());
            }
        }

        /// <summary>
        /// Export báo cáo tồn kho ra Excel
        /// </summary>
        [RequirePermission("View Dashboard")]
        public async Task<IActionResult> ExportStockReport()
        {
            try
            {
                var products = await _context.Products
                    .Include(p => p.Brand)
                    .Include(p => p.Categories)
                    .Select(p => new
                    {
                        p.ProductId,
                        p.ProductName,
                        BrandName = p.Brand.BrandName,
                        Categories = string.Join(", ", p.Categories.Select(c => c.CategoryName)),
                        p.Stock,
                        p.Price,
                        StockValue = p.Stock * p.Price,
                        Status = p.Stock == 0 ? "Hết hàng" : 
                               p.Stock <= 5 ? "Sắp hết" : 
                               p.Stock <= 10 ? "Ít hàng" : "Bình thường"
                    })
                    .OrderBy(p => p.Stock)
                    .ToListAsync();

                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Báo cáo tồn kho");
                    
                    // Headers
                    worksheet.Cells[1, 1].Value = "Mã SP";
                    worksheet.Cells[1, 2].Value = "Tên sản phẩm";
                    worksheet.Cells[1, 3].Value = "Thương hiệu";
                    worksheet.Cells[1, 4].Value = "Danh mục";
                    worksheet.Cells[1, 5].Value = "Tồn kho";
                    worksheet.Cells[1, 6].Value = "Giá";
                    worksheet.Cells[1, 7].Value = "Giá trị tồn";
                    worksheet.Cells[1, 8].Value = "Trạng thái";

                    // Data
                    for (int i = 0; i < products.Count; i++)
                    {
                        var product = products[i];
                        worksheet.Cells[i + 2, 1].Value = product.ProductId;
                        worksheet.Cells[i + 2, 2].Value = product.ProductName;
                        worksheet.Cells[i + 2, 3].Value = product.BrandName;
                        worksheet.Cells[i + 2, 4].Value = product.Categories;
                        worksheet.Cells[i + 2, 5].Value = product.Stock;
                        worksheet.Cells[i + 2, 6].Value = product.Price;
                        worksheet.Cells[i + 2, 7].Value = product.StockValue;
                        worksheet.Cells[i + 2, 8].Value = product.Status;
                    }

                    // Auto fit columns
                    worksheet.Cells.AutoFitColumns();

                    var stream = new MemoryStream();
                    package.SaveAs(stream);
                    stream.Position = 0;

                    var fileName = $"BaoCaoTonKho_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                    return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ExportStockReport error: {ex.Message}");
                return RedirectToAction("Index");
            }
        }

        private async Task LoadFallbackData(ExtendedDashboardViewModel viewModel, DateTime today, DateTime startOfMonth, DateTime startOfYear)
        {
            try
            {
                // Load dữ liệu cũ
                await LoadFallbackDataOriginal(viewModel, today, startOfMonth, startOfYear);
                
                // Load thống kê mới
                await LoadProductStatistics(viewModel);
                await LoadStockStatistics(viewModel);
                await LoadLowStockNotifications(viewModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LoadFallbackData error: {ex.Message}");
            }
        }

        private async Task LoadFallbackDataOriginal(DashboardViewModel viewModel, DateTime today, DateTime startOfMonth, DateTime startOfYear)
        {
            try
            {
                // Dữ liệu doanh thu theo ngày (30 ngày gần nhất)
                // Group by ngày và tính tổng doanh thu mỗi ngày
                viewModel.RevenueByDates = await _context.Orders
                    .Where(o => o.OrderDate.HasValue && o.OrderDate >= today.AddDays(-30))
                    .GroupBy(o => o.OrderDate.Value.Date) // Nhóm theo ngày
                    .Select(g => new RevenueByDate
                    {
                        Ngay = g.Key, // Ngày
                        DoanhThu = g.Sum(o => o.TotalAmount ?? 0m) // Tổng doanh thu ngày đó
                    })
                    .OrderBy(r => r.Ngay) // Sắp xếp theo ngày
                    .ToListAsync();

                // Điền các ngày thiếu để biểu đồ hiển thị đầy đủ (ngày không có đơn hàng = 0)
                FillMissingDates(viewModel.RevenueByDates, today.AddDays(-30), today);

                // Top 5 sản phẩm bán chạy tháng này
                // Group by ProductId và tính tổng số lượng bán + doanh thu
                viewModel.TopProducts = await _context.OrderDetails
                    .Include(od => od.Product) // Load thông tin sản phẩm
                    .Include(od => od.Order) // Load thông tin đơn hàng
                    .Where(od => od.Order.OrderDate >= startOfMonth) // Chỉ lấy đơn hàng tháng này
                    .GroupBy(od => new { od.ProductId, od.Product.ProductName }) // Nhóm theo sản phẩm
                    .Select(g => new RevenueByProduct
                    {
                        ProductName = g.Key.ProductName, // Tên sản phẩm
                        SoLuongBan = (g.Sum(od => od.Quantity) ?? 0), // Tổng số lượng bán
                        DoanhThu = g.Sum(od => od.TotalPrice) // Tổng doanh thu
                    })
                    .OrderByDescending(p => p.SoLuongBan) // Sắp xếp theo số lượng bán giảm dần
                    .Take(5) // Chỉ lấy top 5
                    .ToListAsync();

                // Dữ liệu doanh thu theo thương hiệu tháng này
                // Group by Brand và tính tổng doanh thu
                viewModel.RevenueByBrands = await _context.OrderDetails
                    .Include(od => od.Product) // Load sản phẩm
                    .ThenInclude(p => p.Brand) // Load thương hiệu
                    .Include(od => od.Order) // Load đơn hàng
                    .Where(od => od.Order.OrderDate >= startOfMonth) // Chỉ lấy đơn hàng tháng này
                    .GroupBy(od => od.Product.Brand.BrandName) // Nhóm theo tên thương hiệu
                    .Select(g => new RevenueByBrand
                    {
                        BrandName = g.Key, // Tên thương hiệu
                        DoanhThu = g.Sum(od => od.TotalPrice) // Tổng doanh thu
                    })
                    .OrderByDescending(b => b.DoanhThu) // Sắp xếp theo doanh thu giảm dần
                    .Take(10) // Chỉ lấy top 10
                    .ToListAsync();

                // Đặt mô tả filter cho view mặc định (không có filter)
                viewModel.FilterDescription = "";
                viewModel.TongDoanhThuLoc = 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fallback data error: {ex.Message}");
            }
        }

        [HttpGet]
        [RequirePermission("Export Dashboard")] // Yêu cầu quyền "Export Dashboard"
        public async Task<IActionResult> ExportExcel(string timeType = "month", int year = 0, int? month = null, int? brandId = null)
        {
            try
            {
                // Tạo filter từ parameters
                var filter = new DashboardFilterViewModel
                {
                    TimeType = timeType,
                    Year = year == 0 ? DateTime.Now.Year : year, // Mặc định: năm hiện tại
                    Month = month,
                    BrandId = brandId
                };

                // Load dữ liệu theo filter
                var viewModel = new DashboardViewModel { Filter = filter };
                await LoadFilteredData(viewModel, filter);

                // Tạo HTML mà Excel có thể mở (Excel có thể mở file HTML)
                var html = new System.Text.StringBuilder();
                
                html.AppendLine("<!DOCTYPE html>");
                html.AppendLine("<html>");
                html.AppendLine("<head>");
                html.AppendLine("<meta charset='utf-8'>");
                html.AppendLine("<style>");
                html.AppendLine("table { border-collapse: collapse; width: 100%; margin-bottom: 20px; }");
                html.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
                html.AppendLine("th { background-color: #f2f2f2; font-weight: bold; }");
                html.AppendLine(".number { text-align: right; }");
                html.AppendLine("h1 { color: #333; }");
                html.AppendLine("h2 { color: #666; margin-top: 30px; }");
                html.AppendLine("</style>");
                html.AppendLine("</head>");
                html.AppendLine("<body>");
                
                // Header
                html.AppendLine("<h1>BÁO CÁO DOANH THU - PERFUME STORE</h1>");
                html.AppendLine($"<p><strong>Thời gian xuất:</strong> {DateTime.Now:dd/MM/yyyy HH:mm:ss}</p>");
                html.AppendLine($"<p><strong>Bộ lọc:</strong> {viewModel.FilterDescription ?? "Tất cả"}</p>");

                // Summary
                html.AppendLine("<h2>TỔNG QUAN</h2>");
                html.AppendLine("<table>");
                html.AppendLine("<tr><th>Chỉ số</th><th>Giá trị</th></tr>");
                html.AppendLine($"<tr><td>Tổng doanh thu</td><td class='number'>{viewModel.TongDoanhThuLoc:N0} VNĐ</td></tr>");
                html.AppendLine("</table>");

                // Revenue by dates
                html.AppendLine("<h2>DOANH THU THEO THỜI GIAN</h2>");
                html.AppendLine("<table>");
                html.AppendLine("<tr><th>Ngày</th><th>Doanh thu (VNĐ)</th></tr>");
                if (viewModel.RevenueByDates != null && viewModel.RevenueByDates.Any())
                {
                    foreach (var item in viewModel.RevenueByDates.OrderBy(r => r.Ngay))
                    {
                        html.AppendLine($"<tr><td>{item.Ngay:dd/MM/yyyy}</td><td class='number'>{item.DoanhThu:N0}</td></tr>");
                    }
                }
                html.AppendLine("</table>");

                // Top products
                html.AppendLine("<h2>TOP SẢN PHẨM BÁN CHẠY</h2>");
                html.AppendLine("<table>");
                html.AppendLine("<tr><th>STT</th><th>Tên sản phẩm</th><th>Doanh thu (VNĐ)</th></tr>");
                if (viewModel.TopProducts != null && viewModel.TopProducts.Any())
                {
                    for (int i = 0; i < viewModel.TopProducts.Count; i++)
                    {
                        var product = viewModel.TopProducts[i];
                        html.AppendLine($"<tr><td>{i + 1}</td><td>{product.ProductName}</td><td class='number'>{product.DoanhThu:N0}</td></tr>");
                    }
                }
                html.AppendLine("</table>");
                
                html.AppendLine("</body>");
                html.AppendLine("</html>");

                var fileName = $"BaoCaoDoanhThu_{DateTime.Now:yyyyMMdd_HHmmss}.xls";
                var bytes = System.Text.Encoding.UTF8.GetBytes(html.ToString());
                
                return File(bytes, "application/vnd.ms-excel", fileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Export Excel error: {ex.Message}");
                return BadRequest($"Lỗi khi xuất Excel: {ex.Message}");
            }
        }





        private string GenerateExcelReport(DashboardViewModel viewModel, DateTime startDate, DateTime endDate)
        {
            var html = new System.Text.StringBuilder();
            
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset='utf-8'>");
            html.AppendLine("<style>");
            html.AppendLine("table { border-collapse: collapse; width: 100%; }");
            html.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
            html.AppendLine("th { background-color: #f2f2f2; font-weight: bold; }");
            html.AppendLine(".number { text-align: right; }");
            html.AppendLine(".header { font-size: 18px; font-weight: bold; margin: 20px 0; }");
            html.AppendLine(".section { margin: 20px 0; }");
            html.AppendLine("</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");

            // Header
            html.AppendLine("<div class='header'>BÁO CÁO DASHBOARD - PERFUME STORE</div>");
            html.AppendLine($"<p><strong>Thời gian xuất:</strong> {DateTime.Now:dd/MM/yyyy HH:mm:ss}</p>");
            html.AppendLine($"<p><strong>Khoảng thời gian:</strong> {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}</p>");
            html.AppendLine($"<p><strong>Bộ lọc:</strong> {viewModel.FilterDescription}</p>");

            // Summary
            html.AppendLine("<div class='section'>");
            html.AppendLine("<h3>TỔNG QUAN</h3>");
            html.AppendLine("<table>");
            html.AppendLine("<tr><th>Chỉ số</th><th>Giá trị</th></tr>");
            html.AppendLine($"<tr><td>Tổng doanh thu</td><td class='number'>{viewModel.TongDoanhThuLoc:N0} VNĐ</td></tr>");
            html.AppendLine($"<tr><td>Số đơn hàng</td><td class='number'>{viewModel.TongSoDonHang}</td></tr>");
            html.AppendLine($"<tr><td>Khách hàng mới</td><td class='number'>{viewModel.SoKhachHangMoi}</td></tr>");
            html.AppendLine($"<tr><td>Giá trị đơn hàng TB</td><td class='number'>{viewModel.GiaTriDonHangTrungBinh:N0} VNĐ</td></tr>");
            html.AppendLine("</table>");
            html.AppendLine("</div>");

            // Revenue by dates
            html.AppendLine("<div class='section'>");
            html.AppendLine("<h3>DOANH THU THEO THỜI GIAN</h3>");
            html.AppendLine("<table>");
            html.AppendLine("<tr><th>Thời gian</th><th>Doanh thu (VNĐ)</th></tr>");
            foreach (var item in viewModel.RevenueByDates.OrderBy(r => r.Ngay))
            {
                var dateFormat = viewModel.Filter.TimeType == "month" && !viewModel.Filter.Month.HasValue ? "MM/yyyy" :
                               viewModel.Filter.TimeType == "year" ? "yyyy" : "dd/MM/yyyy";
                html.AppendLine($"<tr><td>{item.Ngay.ToString(dateFormat)}</td><td class='number'>{item.DoanhThu:N0}</td></tr>");
            }
            html.AppendLine("</table>");
            html.AppendLine("</div>");

            // Top products
            html.AppendLine("<div class='section'>");
            html.AppendLine("<h3>TOP SẢN PHẨM BÁN CHẠY</h3>");
            html.AppendLine("<table>");
            html.AppendLine("<tr><th>STT</th><th>Tên sản phẩm</th><th>Số lượng bán</th><th>Doanh thu (VNĐ)</th></tr>");
            for (int i = 0; i < viewModel.TopProducts.Count; i++)
            {
                var product = viewModel.TopProducts[i];
                html.AppendLine($"<tr><td>{i + 1}</td><td>{product.ProductName}</td><td class='number'>{product.SoLuongBan}</td><td class='number'>{product.DoanhThu:N0}</td></tr>");
            }
            html.AppendLine("</table>");
            html.AppendLine("</div>");

            // Revenue by brands
            html.AppendLine("<div class='section'>");
            html.AppendLine("<h3>DOANH THU THEO THƯƠNG HIỆU</h3>");
            html.AppendLine("<table>");
            html.AppendLine("<tr><th>STT</th><th>Thương hiệu</th><th>Doanh thu (VNĐ)</th></tr>");
            for (int i = 0; i < viewModel.RevenueByBrands.Count; i++)
            {
                var brand = viewModel.RevenueByBrands[i];
                html.AppendLine($"<tr><td>{i + 1}</td><td>{brand.BrandName}</td><td class='number'>{brand.DoanhThu:N0}</td></tr>");
            }
            html.AppendLine("</table>");
            html.AppendLine("</div>");

            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
        }
    }
}
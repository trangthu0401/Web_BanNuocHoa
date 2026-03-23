using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PerfumeStore.Models;

namespace PerfumeStore.DesignPatterns.Proxy
{
    /// <summary>
    /// =========================================================================
    /// DESIGN PATTERN: PROXY (MẪU ĐẠI DIỆN - Dạng Caching Proxy)
    /// =========================================================================
    /// - Ứng dụng tại: HomeController (Khu vực tải danh sách sản phẩm nổi bật ngoài trang chủ).
    /// - Luồng hoạt động: Đứng làm "rào chắn" trước cơ sở dữ liệu. Nếu dữ liệu trang chủ đã được tải 
    ///   và chưa quá 10 phút, Proxy trả về dữ liệu từ RAM (tốc độ 0.001s). Nếu chưa có, nó mới cho phép 
    ///   truy vấn xuống Real Service (Database). Giải quyết bài toán quá tải DB khi nhiều người vào trang chủ.
    /// 
    /// ⚠️ LƯU Ý SƯ PHẠM (TẠI SAO CẦN ĐĂNG KÝ VÀO PROGRAM.CS?):
    /// - Trái ngược với Singleton kinh điển, mẫu Proxy tuân thủ mạnh mẽ Dependency Inversion Principle (DIP).
    /// - GIẢI THÍCH: Controller không được phép biết nó đang dùng Proxy hay RealService. Nó chỉ nhận vào 
    ///   interface `IProductQueryService`. Ta BẮT BUỘC phải đăng ký DI trong Program.cs:
    ///   `builder.Services.AddScoped<IProductQueryService, ProductCacheProxy>();`
    ///   Việc này giúp ta dễ dàng tháo/lắp Proxy mà không cần sửa một dòng code nào bên trong HomeController.
    /// =========================================================================
    /// </summary>

    // 1. Giao diện chung (Subject)
    public interface IProductQueryService
    {
        Task<List<Product>> GetFeaturedProductsAsync();
    }

    // 2. Dịch vụ thật - Thực hiện việc chọc xuống Database (Real Subject)
    public class RealProductQueryService : IProductQueryService
    {
        private readonly PerfumeStoreContext _context;

        public RealProductQueryService(PerfumeStoreContext context)
        {
            _context = context;
        }

        public async Task<List<Product>> GetFeaturedProductsAsync()
        {
            Console.WriteLine("[DB] Đang truy vấn Database để lấy danh sách sản phẩm...");
            return await _context.Products
                .Include(p => p.ProductImages)
                .Include(p => p.Brand)
                .Include(p => p.Categories)
                .Where(p => p.IsPublished == true)
                .OrderByDescending(p => p.ProductId)
                .Take(10)
                .ToListAsync();
        }
    }

    // 3. Lớp Proxy - Đứng rào chắn trước Dịch vụ thật (Proxy)
    public class ProductCacheProxy : IProductQueryService
    {
        private readonly RealProductQueryService _realService;

        // Vùng nhớ Cache
        private List<Product> _cachedProducts;
        private DateTime _lastFetchTime;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(10);

        public ProductCacheProxy(RealProductQueryService realService)
        {
            _realService = realService;
        }

        public async Task<List<Product>> GetFeaturedProductsAsync()
        {
            // Kiểm tra: Trống hoặc đã hết hạn 10 phút thì mới gọi Database
            if (_cachedProducts == null || DateTime.Now - _lastFetchTime > _cacheDuration)
            {
                Console.WriteLine("[Proxy] Dữ liệu chưa có hoặc đã cũ. Chuyển tiếp yêu cầu xuống Real Service...");
                _cachedProducts = await _realService.GetFeaturedProductsAsync();
                _lastFetchTime = DateTime.Now;
            }
            else
            {
                Console.WriteLine("[Proxy] Cache Hit! Lấy dữ liệu trực tiếp từ RAM, không cần chạm DB.");
            }

            return _cachedProducts;
        }
    }
}
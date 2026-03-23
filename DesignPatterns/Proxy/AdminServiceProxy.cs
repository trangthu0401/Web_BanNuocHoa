using System;

namespace PerfumeStore.DesignPatterns.Proxy
{
    // 1. Giao diện định nghĩa hành động quản trị
    public interface IAdminService
    {
        void DeleteProduct(int productId);
        void DeleteBrand(int brandId);
    }

    // 2. Real Subject: Lớp thực thi nghiệp vụ xóa dữ liệu thực tế
    public class RealAdminService : IAdminService
    {
        public void DeleteProduct(int productId)
        {
            // Logic kết nối Entity Framework để xóa sản phẩm
            Console.WriteLine($"Database: Đã xóa sản phẩm {productId}.");
        }

        public void DeleteBrand(int brandId)
        {
            Console.WriteLine($"Database: Đã xóa thương hiệu {brandId}.");
        }
    }

    // 3. Proxy: Lớp đại diện kiểm tra bảo mật
    public class AdminServiceProxy : IAdminService
    {
        private RealAdminService _realService;
        private string _currentUserRole;

        public AdminServiceProxy(string role)
        {
            _currentUserRole = role;
        }

        public void DeleteProduct(int productId)
        {
            // Kiểm tra quyền (Protection Logic)
            if (_currentUserRole != "SuperAdmin")
            {
                throw new UnauthorizedAccessException("Lỗi: Bạn không có quyền xóa dữ liệu này.");
            }

            // Lazy Initialization: Chỉ khởi tạo đối tượng thật khi quyền hợp lệ
            if (_realService == null) _realService = new RealAdminService();

            _realService.DeleteProduct(productId);
        }

        public void DeleteBrand(int brandId)
        {
            if (_currentUserRole != "SuperAdmin")
            {
                throw new UnauthorizedAccessException("Lỗi: Bạn không có quyền xóa thương hiệu.");
            }

            if (_realService == null) _realService = new RealAdminService();

            _realService.DeleteBrand(brandId);
        }
    }
}
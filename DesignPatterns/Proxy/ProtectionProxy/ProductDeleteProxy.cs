using System;
using System.Threading.Tasks;

namespace PerfumeStore.DesignPatterns.Proxy.ProtectionProxy
{
    public class ProductDeleteProxy : IProductDeleteService
    {
        private readonly RealProductDeleteService _realDeleteService;

        public ProductDeleteProxy(RealProductDeleteService realDeleteService)
        {
            _realDeleteService = realDeleteService;
        }

        public async Task<bool> DeleteProductAsync(int productId, string userRole)
        {
            // Kiểm tra phân quyền
            if (string.IsNullOrEmpty(userRole) || (userRole != "Admin" && userRole != "SuperAdmin"))
            {
                throw new UnauthorizedAccessException("Bạn không có quyền xóa sản phẩm này! Hành động bị chặn bởi Protection Proxy.");
            }

            // Nếu hợp lệ, chuyển tiếp cho đối tượng thật xử lý
            return await _realDeleteService.DeleteProductAsync(productId, userRole);
        }
    }
}

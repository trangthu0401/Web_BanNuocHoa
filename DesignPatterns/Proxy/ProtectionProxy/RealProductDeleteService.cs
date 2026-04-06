using Microsoft.EntityFrameworkCore;
using PerfumeStore.Areas.Admin.Models;

namespace PerfumeStore.DesignPatterns.Proxy.ProtectionProxy
{
    public class RealProductDeleteService : IProductDeleteService
    {
        private readonly PerfumeStoreContext _db;

        public RealProductDeleteService(PerfumeStoreContext db)
        {
            _db = db;
        }

        public async Task<bool> DeleteProductAsync(int productId, string userRole)
        {
            var product = await _db.Products
                .Include(p => p.Categories)
                .Include(p => p.Liters)
                .FirstOrDefaultAsync(p => p.ProductId == productId);

            if (product == null)
                return false;

            // Bước 1: Xóa ProductImages trước (để tránh FK constraint)
            var productImages = await _db.ProductImages
                .Where(pi => pi.ProductId == productId)
                .ToListAsync();

            if (productImages.Any())
            {
                 _db.ProductImages.RemoveRange(productImages);
            }

            // Bước 2: Xóa các quan hệ với Categories
            product.Categories.Clear();

            // Bước 3: Xóa các quan hệ với Liters nếu có
            product.Liters.Clear();

            // Bước 4: Xóa Product
            _db.Products.Remove(product);
            
            await _db.SaveChangesAsync();

            return true;
        }
    }
}

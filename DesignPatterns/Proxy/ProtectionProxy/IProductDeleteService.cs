namespace PerfumeStore.DesignPatterns.Proxy.ProtectionProxy
{
    public interface IProductDeleteService
    {
        Task<bool> DeleteProductAsync(int productId, string userRole);
    }
}

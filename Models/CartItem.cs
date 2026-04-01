namespace PerfumeStore.Models
{
    public class CartItem
    {
        public int ProductId { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public bool IsFavorite { get; set; }
        
        // Decorator flags
        public bool HasGiftWrap { get; set; }
        public bool HasEngraveName { get; set; }

        public decimal LineTotal => UnitPrice * Quantity;
    }
} 
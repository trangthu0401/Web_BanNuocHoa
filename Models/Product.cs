using System;
using System.Collections.Generic;

namespace PerfumeStore.Models
{
    public partial class Product
    {
        public Product()
        {
            Comments = new HashSet<Comment>();
            OrderDetails = new HashSet<OrderDetail>();
            ProductImages = new HashSet<ProductImage>();
            Categories = new HashSet<Category>();
            Customers = new HashSet<Customer>();
            Liters = new HashSet<Liter>();
        }

        public int ProductId { get; set; }
        public string ProductName { get; set; } = null!;
        public string SuggestionName { get; set; } = null!;
        public decimal Price { get; set; }
        public string? Origin { get; set; }
        public int? ReleaseYear { get; set; }
        public string? Concentration { get; set; }
        public string? Craftsman { get; set; }
        public string? Style { get; set; }
        public string? UsingOccasion { get; set; }
        public int Stock { get; set; }
        public string? TopNote { get; set; }
        public string? HeartNote { get; set; }
        public string? BaseNote { get; set; }
        public decimal? DiscountPrice { get; set; }
        public bool? IsPublished { get; set; }
        public int WarrantyPeriodMonths { get; set; }
        public string Scent { get; set; } = null!;
        public int BrandId { get; set; }
        public int? DiscountId { get; set; }
        public string? Introduction { get; set; }
        public string? DescriptionNo1 { get; set; }
        public string? DescriptionNo2 { get; set; }

        public virtual Brand Brand { get; set; } = null!;
        public virtual DiscountProgram? Discount { get; set; }
        public virtual ICollection<Comment> Comments { get; set; }
        public virtual ICollection<OrderDetail> OrderDetails { get; set; }
        public virtual ICollection<ProductImage> ProductImages { get; set; }

        public virtual ICollection<Category> Categories { get; set; }
        public virtual ICollection<Customer> Customers { get; set; }
        public virtual ICollection<Liter> Liters { get; set; }
    }
}

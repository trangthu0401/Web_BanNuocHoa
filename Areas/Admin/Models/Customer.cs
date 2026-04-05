using System;
using System.Collections.Generic;

namespace PerfumeStore.Areas.Admin.Models
{
    public partial class Customer
    {
        public Customer()
        {
            Comments = new HashSet<Comment>();
            Coupons = new HashSet<Coupon>();
            Orders = new HashSet<Order>();
            ShippingAddresses = new HashSet<ShippingAddress>();
            Products = new HashSet<Product>();
        }

        public int CustomerId { get; set; }
        public string? Name { get; set; }
        public string? Phone { get; set; }
        public string Email { get; set; } = null!;
        public int? BirthYear { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string? PasswordHash { get; set; }
        public int? SpinNumber { get; set; }
        public int? MembershipId { get; set; }

        public virtual Membership? Membership { get; set; }
        public virtual ICollection<Comment> Comments { get; set; }
        public virtual ICollection<Coupon> Coupons { get; set; }
        public virtual ICollection<Order> Orders { get; set; }
        public virtual ICollection<ShippingAddress> ShippingAddresses { get; set; }

        public virtual ICollection<Product> Products { get; set; }
    }
}

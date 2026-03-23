using System;
using System.Collections.Generic;

namespace PerfumeStore.Areas.Admin.Models
{
    public partial class Coupon
    {
        public Coupon()
        {
            Orders = new HashSet<Order>();
        }

        public int CouponId { get; set; }
        public string? Code { get; set; }
        public bool? IsUsed { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public DateTime? UsedDate { get; set; }
        public decimal? DiscountAmount { get; set; }
        public int? CustomerId { get; set; }

        public virtual Customer? Customer { get; set; }
        public virtual ICollection<Order> Orders { get; set; }
    }
}

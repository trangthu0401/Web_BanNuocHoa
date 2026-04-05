using System;
using System.Collections.Generic;

namespace PerfumeStore.Areas.Admin.Models
{
    public partial class Coupon : ICloneable
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

        public object Clone()
        {
            return this.MemberwiseClone();
        }

        public Coupon DuplicateForNewSeason()
        {
            var clone = (Coupon)this.Clone();
            clone.CouponId = 0; // Đặt ID = 0 để Entity Framework hiểu là bản ghi mới
            clone.IsUsed = false;
            clone.UsedDate = null;
            clone.CreatedDate = DateTime.Now;
            // Xoá Code cũ đi để nhận Code mới ngẫu nhiên (30 ký tự) từ Controller
            clone.Code = string.Empty; 
            
            // Tránh copy các liên kết (đơn hàng)
            clone.Orders = new HashSet<Order>(); 
            return clone;
        }
    }
}

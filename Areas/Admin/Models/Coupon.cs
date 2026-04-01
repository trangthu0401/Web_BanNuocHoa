using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PerfumeStore.Areas.Admin.Models
{
    public partial class Coupon : ICloneable
    {
        public Coupon()
        {
            Orders = new HashSet<Order>();
        }

        public int CouponId { get; set; }
        [Required(ErrorMessage = "Code là bắt buộc.")]
        [StringLength(30, MinimumLength = 30, ErrorMessage = "Code phải gồm chính xác 30 ký tự.")]
        public string? Code { get; set; }
        public bool? IsUsed { get; set; }
        public DateTime? CreatedDate { get; set; }
        [Required(ErrorMessage = "Ngày hết hạn là bắt buộc.")]
        public DateTime? ExpiryDate { get; set; }
        public DateTime? UsedDate { get; set; }
        [Required(ErrorMessage = "Số tiền giảm là bắt buộc.")]
        [Range(typeof(decimal), "0.01", "9999999999", ErrorMessage = "Số tiền giảm phải lớn hơn 0.")]
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

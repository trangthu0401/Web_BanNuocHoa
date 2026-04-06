using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System;

namespace PerfumeStore.Models.ViewModels
{
    public class CustomerAccountVM : IValidatableObject
    {
        public int CustomerId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ và tên")]
        [Display(Name = "Họ và Tên")]
        public string? Name { get; set; }

        [Phone(ErrorMessage = "Định dạng số điện thoại chưa đúng")]
        [Display(Name = "Số điện thoại")]
        public string? Phone { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = null!;

        [Display(Name = "Năm sinh")]
        public int? BirthYear { get; set; }
        [Display(Name = "Ngày tham gia")]
        public DateTime? CreatedDate { get; set; }

        [Display(Name = "Hạng thành viên")]
        public string? MembershipName { get; set; }
        public int RewardPoints { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (BirthYear.HasValue)
            {
                var minYear = 1900;
                var maxYear = DateTime.Now.Year;
                if (BirthYear.Value < minYear || BirthYear.Value > maxYear)
                {
                    yield return new ValidationResult(
                        $"Năm sinh chỉ trong khoảng 1900 đến {maxYear}",
                        new[] { nameof(BirthYear) }
                    );
                }
            }
        }
    }
}



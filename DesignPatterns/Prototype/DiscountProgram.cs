using System;

namespace PerfumeStore.DesignPatterns.Prototype
{
    // Sử dụng partial class để mở rộng Model DiscountProgram thực tế của dự án
    public class DiscountProgram : ICloneable
    {
        public int ProgramId { get; set; }
        public string DiscountName { get; set; }
        public decimal DiscountRate { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        /// <summary>
        /// Phương thức Clone - Trái tim của mẫu Prototype
        /// </summary>
        public object Clone()
        {
            // MemberwiseClone tạo một bản sao cạn (Shallow Copy) 
            // Giúp sao chép nhanh các thuộc tính kiểu giá trị (int, string, decimal...)
            return this.MemberwiseClone();
        }

        /// <summary>
        /// Phương thức hỗ trợ nhân bản và tùy chỉnh nhanh cho Admin
        /// </summary>
        public DiscountProgram DuplicateForNewSeason(string newName, DateTime newStart, DateTime newEnd)
        {
            // 1. Nhân bản đối tượng hiện tại
            var newCampaign = (DiscountProgram)this.Clone();

            // 2. Cập nhật thông tin cho đợt khuyến mãi mới
            newCampaign.DiscountName = newName;
            newCampaign.StartDate = newStart;
            newCampaign.EndDate = newEnd;

            // 3. Quan trọng: Reset ID về 0 để Database hiểu đây là bản ghi mới khi lưu
            newCampaign.ProgramId = 0;

            return newCampaign;
        }
    }
}
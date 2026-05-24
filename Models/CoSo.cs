using System.ComponentModel.DataAnnotations;

namespace HeThongQuanLyPhongTro.Models
{
    // Bảng cơ sở: quản lý các cơ sở (chi nhánh) của chủ trọ
    public class CoSo
    {
        [Key]
        public int MaCoSo { get; set; }

        [Required]
        public string TenCoSo { get; set; } = string.Empty;

        public string? DiaChi { get; set; }

        public string? MoTa { get; set; }
        // Thuộc tính này giúp hệ thống hiểu 1 Cơ sở chứa 1 danh sách các Phòng
        public virtual ICollection<Phong> Phongs { get; set; } = new List<Phong>();
    }
}
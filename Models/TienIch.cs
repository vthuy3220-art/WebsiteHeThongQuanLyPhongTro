using System.ComponentModel.DataAnnotations;

namespace HeThongQuanLyPhongTro.Models
{
    public class TienIch
    {
        [Key]
        public int MaTienIch { get; set; }

        [Required]
        public int MaPhong { get; set; }

        [Required(ErrorMessage = "Tên tiện ích không được để trống")]
        public string TenTienIch { get; set; } = string.Empty;

        public string? MoTa { get; set; }

        public int? SoLuong { get; set; } = 1;

        public string? TrangThai { get; set; } = "Hoạt động";

        // Navigation property
        public virtual Phong? Phong { get; set; }
    }
}
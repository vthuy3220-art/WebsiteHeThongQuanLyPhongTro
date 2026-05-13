using System.ComponentModel.DataAnnotations;

namespace HeThongQuanLyPhongTro.Models
{
    public class LichSuThuePhong
    {
        [Key]
        public int MaLichSu { get; set; }

        [Required]
        public int MaPhong { get; set; }

        public int? MaHopDong { get; set; }

        public string? TenKhachHang { get; set; }

        public DateTime NgayBatDau { get; set; }

        public DateTime NgayKetThuc { get; set; }

        public decimal GiaPhong { get; set; }

        public string? TrangThai { get; set; }

        // Navigation property
        public virtual Phong? Phong { get; set; }
    }
}
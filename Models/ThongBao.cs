using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HeThongQuanLyPhongTro.Models
{
    public class ThongBao
    {
        [Key]
        public int MaThongBao { get; set; }

        [Required]
        [MaxLength(200)]
        public string? TieuDe { get; set; }

        [Required]
        public string? NoiDung { get; set; }

        [MaxLength(20)]
        public string? Loai { get; set; } = "info";

        [MaxLength(500)]
        public string? DuongDan { get; set; }

        public int? NguoiNhan { get; set; } // NULL = Admin, khác = MaKhachHang

        public bool DaXem { get; set; } = false;

        public DateTime NgayTao { get; set; } = DateTime.Now;
    }
}

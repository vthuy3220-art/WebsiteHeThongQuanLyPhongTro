using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HeThongQuanLyPhongTro.Models
{
    public class HoaDon
    {
        [Key]
        public int MaHoaDon { get; set; }

        public int MaHopDong { get; set; }

        public int Thang { get; set; }

        public int Nam { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? TongTien { get; set; }

        public string? TrangThai { get; set; } = "Chưa thanh toán";
        public DateTime? NgayTao { get; set; } = DateTime.Now;
        public bool? KhachXacNhan { get; set; } = false;
        public DateTime? NgayKhachXacNhan { get; set; }
        public bool? ChuXacNhan { get; set; } = false;
        public DateTime? NgayChuXacNhan { get; set; }

        [ForeignKey("MaHopDong")]
        public virtual HopDong? HopDongNavigation { get; set; }
    }
}
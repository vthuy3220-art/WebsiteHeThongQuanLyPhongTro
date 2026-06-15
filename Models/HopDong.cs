using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HeThongQuanLyPhongTro.Models
{
    // Bảng hợp đồng: lưu thông tin hợp đồng thuê phòng
    public class HopDong
    {
        [Key]
        public int MaHopDong { get; set; }

        public int MaPhong { get; set; }

        public int MaKhachHang { get; set; }

        // 👇 THÊM THUỘC TÍNH NÀY (đã có trong SQL)
        public int MaChuTro { get; set; }

        public DateTime? NgayBatDau { get; set; }

        public DateTime? NgayKetThuc { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? TienCoc { get; set; }

        public string? FileHopDong { get; set; }

        public string? TrangThai { get; set; } = "Hiệu lực";

        [ForeignKey("MaPhong")]
        public virtual Phong? PhongNavigation { get; set; }

        [ForeignKey("MaKhachHang")]
        public virtual KhachHang? KhachHangNavigation { get; set; }

        // 👇 THÊM NAVIGATION NÀY
        [ForeignKey("MaChuTro")]
        public virtual TaiKhoan? ChuTroNavigation { get; set; }
    }
}
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HeThongQuanLyPhongTro.Models
{
    // Bảng thanh toán: lưu lịch sử thanh toán của khách
    public class ThanhToan
    {
        [Key]
        public int MaThanhToan { get; set; }

        public int MaHoaDon { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? SoTien { get; set; }

        public DateTime? NgayThanhToan { get; set; } = DateTime.Now;

        public string? NoiDungChuyenKhoan { get; set; }

        public string? TrangThai { get; set; } = "Thành công";

        [ForeignKey("MaHoaDon")]
        public virtual HoaDon? HoaDonNavigation { get; set; }
    }
}
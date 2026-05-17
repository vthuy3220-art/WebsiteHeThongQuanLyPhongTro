using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HeThongQuanLyPhongTro.Models
{
    public class YeuCauSuaChua
    {
        [Key]
        public int MaYeuCau { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn phòng")]
        public int MaPhong { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn khách hàng")]
        public int MaKhachHang { get; set; }

        [Required(ErrorMessage = "Tiêu đề không được để trống")]
        [StringLength(200)]
        public string TieuDe { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mô tả không được để trống")]
        public string NoiDung { get; set; } = string.Empty;

        public string? HinhAnh { get; set; }

        public string? TrangThai { get; set; } = "Chờ xử lý"; // Chờ xử lý, Đã tiếp nhận, Đã hoàn thành

        public DateTime NgayTao { get; set; } = DateTime.Now;

        public DateTime? NgayXuLy { get; set; }

        public string? GhiChuXuLy { get; set; }

        public decimal? ChiPhiPhatSinh { get; set; } // Tiền sửa chữa (cộng vào hóa đơn)

        [ForeignKey("MaPhong")]
        public virtual Phong? PhongNavigation { get; set; }

        [ForeignKey("MaKhachHang")]
        public virtual KhachHang? KhachHangNavigation { get; set; }
    }
}
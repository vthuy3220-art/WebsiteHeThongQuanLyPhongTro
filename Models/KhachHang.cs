using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HeThongQuanLyPhongTro.Models
{
    // Bảng khách hàng: lưu thông tin người thuê (người đại diện)
    public class KhachHang
    {
        [Key]
        public int MaKhachHang { get; set; }

        // Liên kết với tài khoản đăng nhập (1-1)
        public int? MaTaiKhoan { get; set; }

        [Required(ErrorMessage = "Họ tên không được để trống")]
        public string HoTen { get; set; } = string.Empty;

        public string? CCCD { get; set; }

        public string? SoDienThoai { get; set; }

        public string? Email { get; set; }

        public string? DiaChi { get; set; }

        public DateTime? NgaySinh { get; set; }

        // Navigation property
        [ForeignKey("MaTaiKhoan")]
        public virtual TaiKhoan? TaiKhoanNavigation { get; set; }
    }
}
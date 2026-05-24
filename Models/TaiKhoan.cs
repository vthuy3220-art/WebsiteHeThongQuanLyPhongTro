using System.ComponentModel.DataAnnotations;

namespace HeThongQuanLyPhongTro.Models
{
    // Bảng tài khoản: lưu thông tin đăng nhập và phân quyền
    public class TaiKhoan
    {
        [Key] // Khóa chính, tự động tăng
        public int MaTaiKhoan { get; set; }

        [Required(ErrorMessage = "Tên đăng nhập không được để trống")]
        [StringLength(50)]
        public string TenDangNhap { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu không được để trống")]
        [StringLength(255)]
        public string MatKhau { get; set; } = string.Empty;
        [StringLength(100)]
        public string? Email { get; set; }

        [Required]
        public string VaiTro { get; set; } = "Khach"; // Admin hoặc Khach

        public string? TrangThai { get; set; } = "Hoạt động"; // Hoạt động / Khóa
    }
}
using System.ComponentModel.DataAnnotations;

namespace HeThongQuanLyPhongTro.Models
{
    public class TaiKhoan
    {
        [Key]
        public int MaTaiKhoan { get; set; }

        [Required(ErrorMessage = "Tên đăng nhập không được để trống")]
        [StringLength(50)]
        public string TenDangNhap { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu không được để trống")]
        [StringLength(255)]
        public string MatKhau { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Email { get; set; }

        // ===== THÊM 2 DÒNG NÀY =====
        [StringLength(20)]
        public string? SoDienThoai { get; set; }

        [StringLength(255)]
        public string? DiaChi { get; set; }
        // ===== KẾT THÚC =====

        [Required]
        public string VaiTro { get; set; } = "Khach"; // SuperAdmin, ChuTro, Khach

        public string? TrangThai { get; set; } = "Hoạt động"; // Hoạt động / Khóa

        // ===== Thông tin ngân hàng (dùng cho QR thanh toán) =====
        [StringLength(100)]
        public string? TenNganHang { get; set; }

        [StringLength(50)]
        public string? SoTaiKhoan { get; set; }

        [StringLength(100)]

        public string? ChuTaiKhoan { get; set; }
        [StringLength(50)]
        public string? MaNganHang { get; set; }

        // 1 Chủ trọ quản lý nhiều Tòa nhà
        public virtual ICollection<ToaNha> ToaNhas { get; set; } = new List<ToaNha>();
    }
}
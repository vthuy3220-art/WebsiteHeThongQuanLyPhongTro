using System.ComponentModel.DataAnnotations;

namespace HeThongQuanLyPhongTro.Models
{
    public class HopDongViewModel
    {
        public int? MaHopDong { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn phòng")]
        public int MaPhong { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn khách hàng")]
        public int MaKhachHang { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập ngày bắt đầu")]
        [DataType(DataType.Date)]
        public DateTime NgayBatDau { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập ngày kết thúc")]
        [DataType(DataType.Date)]
        public DateTime NgayKetThuc { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tiền cọc")]
        [Range(0, double.MaxValue, ErrorMessage = "Tiền cọc phải lớn hơn hoặc bằng 0")]
        public decimal TienCoc { get; set; }

        public string? FileHopDong { get; set; }
        public string? TrangThai { get; set; } = "Hiệu lực";

        // Hiển thị thông tin
        public string? TenPhong { get; set; }
        public string? TenKhachHang { get; set; }
        public decimal GiaPhong { get; set; }
        public double DienTich { get; set; }
        public string? TenCoSo { get; set; }
    }
}
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HeThongQuanLyPhongTro.Models
{
    // Bảng phòng: quản lý các phòng trọ
    public class Phong
    {
        [Key]
        public int MaPhong { get; set; }

        // Khóa ngoại đến bảng CoSo
        public int MaCoSo { get; set; }

        [Required(ErrorMessage = "Tên phòng không được để trống")]
        public string TenPhong { get; set; } = string.Empty;

        [Required(ErrorMessage = "Giá phòng không được để trống")]
        [Range(0, double.MaxValue)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal GiaPhong { get; set; }

        public double? DienTich { get; set; }

        public string? TrangThai { get; set; } = "Trống"; // Trống / Đã thuê

        // Số người ở (để tính phí dịch vụ 200k/người)
        public int? SoLuongNguoiO { get; set; } = 1;

        [ForeignKey("MaCoSo")]
        public virtual CoSo? CoSo { get; set; }

        // THÊM: Mối quan hệ đến bảng CoSoVatChat để sửa lỗi CS1061
        public virtual ICollection<CoSoVatChat> CoSoVatChats { get; set; } = new List<CoSoVatChat>();
    }
}
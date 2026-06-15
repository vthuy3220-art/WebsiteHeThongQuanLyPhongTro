using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HeThongQuanLyPhongTro.Models
{
    public class Phong
    {
        [Key]
        public int MaPhong { get; set; }

        [Required(ErrorMessage = "Tên phòng không được để trống")]
        public string TenPhong { get; set; } = string.Empty;

        [Required(ErrorMessage = "Giá phòng không được để trống")]
        [Range(0, double.MaxValue)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal GiaPhong { get; set; }

        public double? DienTich { get; set; }

        public string? TrangThai { get; set; } = "Trống";

        public int? SoLuongNguoiO { get; set; } = 1;

        // Khóa ngoại
        public int MaToaNha { get; set; }
        public int MaChuTro { get; set; }

        // Navigation properties
        [ForeignKey("MaToaNha")]
        public virtual ToaNha? ToaNha { get; set; }

        [ForeignKey("MaChuTro")]
        public virtual TaiKhoan? ChuTro { get; set; }

        // Danh sách cơ sở vật chất
        public virtual ICollection<CoSoVatChat> CoSoVatChats { get; set; } = new List<CoSoVatChat>();
    }
}
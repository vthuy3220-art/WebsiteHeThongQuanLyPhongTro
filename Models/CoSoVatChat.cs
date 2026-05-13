using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HeThongQuanLyPhongTro.Models
{
    // Bảng cơ sở vật chất: quản lý trang thiết bị trong từng phòng
    public class CoSoVatChat
    {
        [Key]
        public int MaCSVC { get; set; }

        // Khóa ngoại đến bảng Phong
        [Required(ErrorMessage = "Vui lòng chọn phòng")]
        public int MaPhong { get; set; }

        [Required(ErrorMessage = "Tên thiết bị không được để trống")]
        [StringLength(100)]
        public string? TenThietBi { get; set; }

        [Range(1, 100, ErrorMessage = "Số lượng từ 1 đến 100")]
        public int? SoLuong { get; set; } = 1;

        [StringLength(100)]
        public string? TinhTrang { get; set; } = "Tốt"; // Tốt / Hư / Cần bảo trì

        // Navigation property
        [ForeignKey("MaPhong")]
        public virtual Phong? PhongNavigation { get; set; }
    }
}
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HeThongQuanLyPhongTro.Models
{
    // Bảng bài đăng: đăng tin cho thuê phòng lên website
    public class BaiDang
    {
        [Key]
        public int MaBaiDang { get; set; }

        public int MaPhong { get; set; }

        // 👇 THÊM THUỘC TÍNH NÀY (đã có trong SQL)
        public int MaChuTro { get; set; }

        public string? TieuDe { get; set; }

        public string? MoTa { get; set; }

        public string? HinhAnh { get; set; }

        public DateTime? NgayDang { get; set; } = DateTime.Now;

        public string? TrangThai { get; set; } = "Hiển thị"; // Hiển thị / Ẩn

        [ForeignKey("MaPhong")]
        public virtual Phong? PhongNavigation { get; set; }

        // 👇 THÊM NAVIGATION NÀY
        [ForeignKey("MaChuTro")]
        public virtual TaiKhoan? ChuTroNavigation { get; set; }
    }
}
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HeThongQuanLyPhongTro.Models
{
    // Bảng lưu nhiều ảnh cho mỗi phòng
    public class PhongImage
    {
        [Key]
        public int MaImage { get; set; }

        // Khóa ngoại đến bảng Phong
        public int MaPhong { get; set; }

        // Đường dẫn file ảnh (VD: /images/phongs/phong101_1.jpg)
        public string ImagePath { get; set; } = string.Empty;

        // Ảnh chính (chỉ có 1 ảnh IsMain = true)
        public bool IsMain { get; set; } = false;

        // Ngày upload ảnh
        public DateTime NgayUpload { get; set; } = DateTime.Now;

        // Navigation property
        [ForeignKey("MaPhong")]
        public virtual Phong? Phong { get; set; }
    }
}
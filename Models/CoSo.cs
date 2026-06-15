using System.ComponentModel.DataAnnotations;
using HeThongQuanLyPhongTro.Models;  // 👈 SỬA LẠI DÒNG NÀY

namespace HeThongQuanLyPhongTro.Models
{
    // Bảng cơ sở: quản lý các cơ sở (chi nhánh) của chủ trọ
    public class CoSo
    {
        [Key]
        public int MaCoSo { get; set; }

        [Required]
        public string TenCoSo { get; set; } = string.Empty;

        public string? DiaChi { get; set; }

        public string? MoTa { get; set; }

        public virtual ICollection<ToaNha> ToaNhas { get; set; } = new List<ToaNha>();
    }
}
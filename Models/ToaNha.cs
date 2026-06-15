using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HeThongQuanLyPhongTro.Models
{
    [Table("ToaNha")] // Khớp với tên bảng trong SQL Server của bạn
    public class ToaNha
    {
        [Key]
        [Column("MaToaNha")] // Khóa chính trong SQL
        public int MaToaNha { get; set; }

        public int MaCoSo { get; set; }

        public int MaChuTro { get; set; }

        [Required(ErrorMessage = "Tên tòa nhà không được để trống")]
        [StringLength(100)]
        public string TenToaNha { get; set; }

        [Required(ErrorMessage = "Địa chỉ không được để trống")]
        [StringLength(255)]
        public string DiaChi { get; set; }

        [StringLength(500)]
        public string? MoTa { get; set; }

        [Required]
        [StringLength(50)]
        public string TrangThai { get; set; } = "Pending"; // Mặc định Chờ duyệt (Pending / Approved / Rejected)

        // =================================================================
        // 🛠️ THÊM DÒNG NÀY ĐỂ FIX TRIỆT ĐỂ TOÀN BỘ LỖI 'CoSo' TRONG ẢNH
        // =================================================================
        [ForeignKey("MaCoSo")]
        public virtual CoSo? CoSo { get; set; } // Khóa ngoại trỏ sang bảng CoSo (MaCoSo)

        [ForeignKey("MaChuTro")]
        public virtual TaiKhoan? ChuTro { get; set; } // Khóa ngoại trỏ sang bảng TaiKhoan (MaTaiKhoan)

        // Một tòa nhà chứa nhiều phòng
        public virtual ICollection<Phong> Phongs { get; set; } = new List<Phong>();
    }
}
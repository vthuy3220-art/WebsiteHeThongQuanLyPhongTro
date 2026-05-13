using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HeThongQuanLyPhongTro.Models
{
    public class LichSuChiSoDienNuoc
    {
        [Key]
        public int MaLichSu { get; set; }
        public int MaPhong { get; set; }
        public int Thang { get; set; }
        public int Nam { get; set; }
        public decimal ChiSoDienCu { get; set; }
        public decimal ChiSoDienMoi { get; set; }
        public decimal ChiSoNuocCu { get; set; }
        public decimal ChiSoNuocMoi { get; set; }
        public DateTime NgayGhi { get; set; } = DateTime.Now;

        [ForeignKey("MaPhong")]
        public virtual Phong? PhongNavigation { get; set; }
    }
}
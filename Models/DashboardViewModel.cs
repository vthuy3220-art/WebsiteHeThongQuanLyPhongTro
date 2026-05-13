namespace HeThongQuanLyPhongTro.Models
{
    // ViewModel: tập hợp dữ liệu để hiển thị trên Dashboard
    public class DashboardViewModel
    {
        // Thống kê phòng
        public int TongSoPhong { get; set; }
        public int SoPhongDaThue { get; set; }
        public int SoPhongTrong { get; set; }
        public int TongSoKhachThue { get; set; }
        public decimal DoanhThuThangNay { get; set; }
        public int SoHopDongSapHetHan { get; set; }
        public List<HopDongSapHetHan> HopDongSapHetHanList { get; set; } = new();

        // Thống kê bài đăng
        public int TongSoBaiDang { get; set; }
        public int SoBaiDangHienThi { get; set; }
        public int SoBaiDangAn { get; set; }
        public int SoBaiDangThangNay { get; set; }
        public List<BaiDangGanDay> BaiDangGanDayList { get; set; } = new();
    }

    public class HopDongSapHetHan
    {
        public string TenPhong { get; set; } = string.Empty;
        public string TenKhachHang { get; set; } = string.Empty;
        public DateTime NgayKetThuc { get; set; }
        public int SoNgayConLai { get; set; }
    }

    public class BaiDangGanDay
    {
        public int MaBaiDang { get; set; }
        public string TieuDe { get; set; } = string.Empty;
        public string TenPhong { get; set; } = string.Empty;
        public DateTime NgayDang { get; set; }
        public string TrangThai { get; set; } = string.Empty;
        public int LuotXem { get; set; }
    }
}
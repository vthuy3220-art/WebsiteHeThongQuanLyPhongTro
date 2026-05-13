namespace HeThongQuanLyPhongTro.Models
{
    public class KhachHangDashboardViewModel
    {
        public KhachHang? ThongTinKhachHang { get; set; }
        public HopDong? HopDongHienTai { get; set; }
        public List<HopDong>? LichSuHopDong { get; set; }
        public List<HoaDon>? HoaDonChuaThanhToan { get; set; }
        public List<HoaDon>? HoaDonDaThanhToan { get; set; }
        public decimal TongNo { get; set; }
        public int SoNgayConLai { get; set; }
    }
}
using System;

namespace HeThongQuanLyPhongTro.Models
{
    // Class khuôn cho trang Chi tiết doanh thu
    public class ChiTietDoanhThuViewModel
    {
        public string MaHoaDon { get; set; }
        public string TenPhong { get; set; }
        public DateTime? NgayThanhToan { get; set; }
        public decimal TongTien { get; set; }
    }

    // Class khuôn cho trang Chi tiết lấp đầy phòng
    public class ThongKeCoSoViewModel
    {
        public string TenCoSo { get; set; }
        public int TongSoPhong { get; set; }
        public int SoPhongDaThue { get; set; }
        public int SoPhongTrong { get; set; }

        // Chỉ số cũ: Tỷ lệ lấp đầy (Tại thời điểm hiện tại)
        public double TyLeLapDay => TongSoPhong == 0 ? 0 : Math.Round((double)SoPhongDaThue / TongSoPhong * 100, 2);

        // MỚI: Tần suất / Tỷ lệ sử dụng thực tế (Tính dựa trên dữ liệu hợp đồng/hóa đơn)
        public double TyLeSuDung { get; set; }
    }
    // Class khuôn cho trang Chi tiết hợp đồng
    public class ChiTietHopDongViewModel
    {
        public string MaHopDong { get; set; }
        public string TenPhong { get; set; }
        public string TenKhachThue { get; set; }
        public DateTime? NgayBatDau { get; set; }
        public DateTime? NgayKetThuc { get; set; }
        public string TrangThai { get; set; }
    }
    // Class khuôn cho trang Thống kê Hóa đơn
    public class ThongKeHoaDonViewModel
    {
        public string MaHoaDon { get; set; }
        public string TenPhong { get; set; }
        public string KhachHang { get; set; }
        public decimal TongTien { get; set; }
        public string TrangThai { get; set; }
        public DateTime? NgayTao { get; set; }
    }
}
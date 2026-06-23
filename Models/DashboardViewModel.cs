using System;
using System.Collections.Generic;

namespace HeThongQuanLyPhongTro.Models
{
    public class DashboardViewModel
    {
        // Thống kê phòng

        public int TongSoPhong { get; set; }
        public int SoPhongDaThue { get; set; }
        public int SoPhongTrong { get; set; }
        public int TongSoKhachHang { get; set; }

        // Thống kê hợp đồng
        public int SoHopDongHieuLuc { get; set; }
        public int SoHopDongHetHan { get; set; }
        public int SoHopDongSapHetHan { get; set; }
        public List<HopDongSapHetHan> HopDongSapHetHanList { get; set; } = new();

        // Doanh thu
        public decimal DoanhThuHomNay { get; set; }
        public decimal DoanhThuThangNay { get; set; }
        public decimal DoanhThuNamNay { get; set; }
        public decimal DoanhThuTatCa { get; set; }
        public List<LapDayTheoThang> LapDayTheoThangList { get; set; } = new();

        // Công nợ
        public decimal TongNoHienTai { get; set; }
        public int SoHoaDonChuaThanhToan { get; set; }

        // Biểu đồ
        public List<DoanhThuTheoThang> DoanhThuTheoThangList { get; set; } = new();
        public List<TrangThaiPhong> TrangThaiPhongList { get; set; } = new();

        // ✅ THÊM MỚI: Doanh thu theo tuần (Giải quyết lỗi DoanhThuTheoTuanList)
        public List<DoanhThuTheoTuan> DoanhThuTheoTuanList { get; set; } = new();
        // ✅ THÊM MỚI: Top 5 phòng doanh thu (Giải quyết lỗi Top5PhongDoanhThuList)
        public List<Top5PhongDoanhThu> Top5PhongDoanhThuList { get; set; } = new();

        // Top khách hàng
        public List<TopKhachHang> TopKhachHangList { get; set; } = new();

        // Bài đăng
        public int TongSoBaiDang { get; set; }
        public int SoBaiDangHienThi { get; set; }
        public int SoBaiDangAn { get; set; }
        public int SoBaiDangThangNay { get; set; }
        public List<BaiDangGanDay> BaiDangGanDayList { get; set; } = new();

        public List<TopPhongSuDung> TopPhongList { get; set; } = new();
        public List<ThongBao> ThongBaoGanDayList { get; set; } = new();

        // Hóa đơn gần đây thực tế
        public List<HoaDonGanDay> HoaDonGanDayList { get; set; } = new();
    }

    // ==================== HỢP ĐỒNG SẮP HẾT HẠN ====================
    public class HopDongSapHetHan
    {
        public int MaHopDong { get; set; }
        public string TenPhong { get; set; } = string.Empty;
        public string TenKhachHang { get; set; } = string.Empty;
        public DateTime NgayKetThuc { get; set; }
        public int SoNgayConLai { get; set; }
    }

    // ==================== HÓA ĐƠN GẦN ĐÂY ====================
    public class HoaDonGanDay
    {
        public int MaHoaDon { get; set; }
        public string TenPhong { get; set; } = string.Empty;
        public decimal TongTien { get; set; }
        public string TrangThai { get; set; } = string.Empty;
    }

    // ==================== DOANH THU THEO THÁNG ====================
    public class DoanhThuTheoThang
    {
        public int Thang { get; set; }
        public int Nam { get; set; }
        public decimal DoanhThu { get; set; }
        public string TenThang => $"Tháng {Thang}/{Nam}";
    }

    // ✅ THÊM MỚI: CLASS DOANH THU THEO TUẦN
    public class DoanhThuTheoTuan
    {
        public string Tuan { get; set; } = string.Empty; // Ví dụ: "Tuần 1", "Tuần 2" hoặc "15/06 - 21/06"
        public decimal DoanhThu { get; set; }
    }

    // ✅ THÊM MỚI: CLASS TOP 5 PHÒNG DOANH THU
    public class Top5PhongDoanhThu
    {
        public int MaPhong { get; set; }
        public string TenPhong { get; set; } = string.Empty;
        public decimal TongDoanhThu { get; set; }
    }

    // ==================== TRẠNG THÁI PHÒNG ====================
    public class TrangThaiPhong
    {
        public string TrangThai { get; set; } = string.Empty;
        public int SoLuong { get; set; }
        public string MauSac => TrangThai == "Đã thuê" ? "#28a745" : "#ffc107";
    }

    // ==================== TOP KHÁCH HÀNG ====================
    public class TopKhachHang
    {
        public int MaKhachHang { get; set; }
        public string HoTen { get; set; } = string.Empty;
        public string SoDienThoai { get; set; } = string.Empty;
        public decimal TongTienDaThanhToan { get; set; }
        public int SoHoaDonDaThanhToan { get; set; }
    }

    // ==================== BÀI ĐĂNG GẦN ĐÂY ====================
    public class BaiDangGanDay
    {
        public int MaBaiDang { get; set; }
        public string TieuDe { get; set; } = string.Empty;
        public string TenPhong { get; set; } = string.Empty;
        public DateTime NgayDang { get; set; }
        public string TrangThai { get; set; } = string.Empty;
        public int LuotXem { get; set; }
    }

    // ==================== TOP PHÒNG SỬ DỤNG ====================
    public class TopPhongSuDung
    {
        public int MaPhong { get; set; }
        public string TenPhong { get; set; } = string.Empty;
        public string TenCoSo { get; set; } = string.Empty;
        public int SoHoaDon { get; set; }
        public decimal TongDoanhThu { get; set; }
    }

    // ==================== THỐNG KÊ LẤP ĐẦY THEO THÁNG ====================
    public class LapDayTheoThang
    {
        public int Thang { get; set; }
        public int Nam { get; set; }
        public double TyLeLapDay { get; set; }
    }

    // Đảm bảo có thêm class ThongBao nếu dự án của bạn chưa khai báo ở file khác
}
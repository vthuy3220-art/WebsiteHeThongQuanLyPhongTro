using System;
using System.Collections.Generic;

namespace HeThongQuanLyPhongTro.Models
{
    // ==================== DASHBOARD ADMIN ====================
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

        // Công nợ
        public decimal TongNoHienTai { get; set; }
        public int SoHoaDonChuaThanhToan { get; set; }

        // Biểu đồ
        public List<DoanhThuTheoThang> DoanhThuTheoThangList { get; set; } = new();
        public List<TrangThaiPhong> TrangThaiPhongList { get; set; } = new();

        // Top khách hàng
        public List<TopKhachHang> TopKhachHangList { get; set; } = new();

        // Bài đăng
        public int TongSoBaiDang { get; set; }
        public int SoBaiDangHienThi { get; set; }
        public int SoBaiDangAn { get; set; }
        public int SoBaiDangThangNay { get; set; }
        public List<BaiDangGanDay> BaiDangGanDayList { get; set; } = new();
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

    // ==================== DOANH THU THEO THÁNG ====================
    public class DoanhThuTheoThang
    {
        public int Thang { get; set; }
        public int Nam { get; set; }
        public decimal DoanhThu { get; set; }
        public string TenThang => $"Tháng {Thang}/{Nam}";
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
}
using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongQuanLyPhongTro.Controllers
{
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Trang tổng quan (phân biệt Admin và Khách)
        public async Task<IActionResult> Index()
        {
            // Kiểm tra đăng nhập
            if (HttpContext.Session.GetInt32("UserId") == null)
                return RedirectToAction("Index", "Login");

            var role = HttpContext.Session.GetString("Role");

            if (role == "Admin")
                return await AdminDashboard();
            else
                return await KhachDashboard();
        }

        // ==================== DASHBOARD ADMIN ====================
        private async Task<IActionResult> AdminDashboard()
        {
            var role = "Admin";
            var model = new DashboardViewModel();

            // ========== CÁC THỐNG KÊ HIỆN CÓ ==========
            model.TongSoPhong = await _context.Phong.CountAsync();
            model.SoPhongDaThue = await _context.Phong.CountAsync(p => p.TrangThai == "Đã thuê");
            model.SoPhongTrong = await _context.Phong.CountAsync(p => p.TrangThai == "Trống");

            var now = DateTime.Now;
            model.DoanhThuThangNay = await _context.HoaDon
                .Where(h => h.Thang == now.Month && h.Nam == now.Year && h.TrangThai == "Đã thanh toán")
                .SumAsync(h => h.TongTien) ?? 0;

            // Hợp đồng sắp hết hạn
            var ngayHetHan = DateTime.Now.AddDays(7);
            var hopDongSapHetHan = await _context.HopDong
                .Include(h => h.PhongNavigation)
                .Include(h => h.KhachHangNavigation)
                .Where(h => h.TrangThai == "Hiệu lực" && h.NgayKetThuc <= ngayHetHan && h.NgayKetThuc >= DateTime.Now)
                .ToListAsync();

            model.SoHopDongSapHetHan = hopDongSapHetHan.Count;
            model.HopDongSapHetHanList = hopDongSapHetHan.Select(h => new HopDongSapHetHan
            {
                TenPhong = h.PhongNavigation?.TenPhong ?? "N/A",
                TenKhachHang = h.KhachHangNavigation?.HoTen ?? "N/A",
                NgayKetThuc = h.NgayKetThuc ?? DateTime.Now,
                SoNgayConLai = (h.NgayKetThuc - DateTime.Now)?.Days ?? 0
            }).ToList();

            // ========== LẤY DANH SÁCH CHO TOOLTIP ==========

            // 1. Danh sách TẤT CẢ phòng (cho tooltip Tổng số phòng)
            var danhSachTatCaPhong = await _context.Phong
                .Select(p => new { p.TenPhong, p.GiaPhong, p.TrangThai })
                .ToListAsync();

            // 2. Danh sách phòng TRỐNG (cho tooltip Phòng trống)
            var danhSachPhongTrong = await _context.Phong
                .Where(p => p.TrangThai == "Trống")
                .Select(p => new { p.TenPhong, p.GiaPhong })
                .ToListAsync();

            // 3. Danh sách phòng ĐÃ THUÊ kèm tên khách (cho tooltip Phòng đã thuê)
            var danhSachPhongDaThue = await _context.HopDong
                .Include(h => h.PhongNavigation)
                .Include(h => h.KhachHangNavigation)
                .Where(h => h.TrangThai == "Hiệu lực")
                .Select(h => new
                {
                    TenPhong = h.PhongNavigation != null ? h.PhongNavigation.TenPhong : "N/A",
                    TenKhachHang = h.KhachHangNavigation != null ? h.KhachHangNavigation.HoTen : "N/A"
                })
                .ToListAsync();

            // Gán vào ViewBag
            ViewBag.DanhSachPhong = danhSachTatCaPhong;
            ViewBag.DanhSachPhongTrong = danhSachPhongTrong;
            ViewBag.DanhSachPhongDaThue = danhSachPhongDaThue;

            // ========== THỐNG KÊ BÀI ĐĂNG ==========
            model.TongSoBaiDang = await _context.BaiDang.CountAsync();
            model.SoBaiDangHienThi = await _context.BaiDang.CountAsync(b => b.TrangThai == "Hiển thị");
            model.SoBaiDangAn = await _context.BaiDang.CountAsync(b => b.TrangThai == "Ẩn");
            model.SoBaiDangThangNay = await _context.BaiDang.CountAsync(b =>
                b.NgayDang.HasValue && b.NgayDang.Value.Month == now.Month && b.NgayDang.Value.Year == now.Year);

            var baiDangGanDay = await _context.BaiDang
                .Include(b => b.PhongNavigation)
                .OrderByDescending(b => b.NgayDang)
                .Take(5)
                .Select(b => new BaiDangGanDay
                {
                    MaBaiDang = b.MaBaiDang,
                    TieuDe = b.TieuDe ?? "Không có tiêu đề",
                    TenPhong = b.PhongNavigation != null ? b.PhongNavigation.TenPhong : "N/A",
                    NgayDang = b.NgayDang ?? DateTime.Now,
                    TrangThai = b.TrangThai ?? "Ẩn",
                    LuotXem = 0
                })
                .ToListAsync();

            model.BaiDangGanDayList = baiDangGanDay;

            ViewBag.Role = role;
            ViewBag.Username = HttpContext.Session.GetString("Username");

            return View("AdminDashboard", model);
        }

        // ==================== DASHBOARD KHÁCH HÀNG ====================
        private async Task<IActionResult> KhachDashboard()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var khachHang = await _context.KhachHang
                .FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);

            if (khachHang == null)
            {
                // Nếu chưa có, tạo mới
                khachHang = new KhachHang
                {
                    MaTaiKhoan = userId,
                    HoTen = HttpContext.Session.GetString("Username") ?? "",
                    SoDienThoai = "",
                    DiaChi = ""
                };
                _context.KhachHang.Add(khachHang);
                await _context.SaveChangesAsync();
            }

            var hopDongHienTai = await _context.HopDong
                .Include(h => h.PhongNavigation)
                .FirstOrDefaultAsync(h => h.MaKhachHang == khachHang.MaKhachHang && h.TrangThai == "Hiệu lực");

            ViewBag.KhachHang = khachHang;
            ViewBag.HopDong = hopDongHienTai;
            ViewBag.Username = HttpContext.Session.GetString("Username");

            return View();
        }
    }
}
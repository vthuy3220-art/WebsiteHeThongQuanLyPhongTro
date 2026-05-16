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

        public async Task<IActionResult> Index()
        {
            var role = HttpContext.Session.GetString("Role");
            var username = HttpContext.Session.GetString("Username");
            var userId = HttpContext.Session.GetInt32("UserId");

            if (string.IsNullOrEmpty(role))
            {
                return RedirectToAction("Index", "Login");
            }

            ViewBag.Username = username;
            ViewBag.Role = role;

            if (role == "Admin")
            {
                // ========== DỮ LIỆU CHO ADMIN DASHBOARD ==========

                // Thống kê phòng
                var tongSoPhong = await _context.Phong.CountAsync();
                var soPhongDaThue = await _context.Phong.CountAsync(p => p.TrangThai == "Đã thuê");
                var soPhongTrong = tongSoPhong - soPhongDaThue;
                var tongSoKhachThue = await _context.KhachHang.CountAsync();

                // Doanh thu tháng này
                var doanhThuThangNay = await _context.HoaDon
                    .Where(h => h.TrangThai == "Đã thanh toán"
                        && h.Nam == DateTime.Now.Year
                        && h.Thang == DateTime.Now.Month)
                    .SumAsync(h => h.TongTien) ?? 0;

                // Hợp đồng sắp hết hạn (còn dưới 30 ngày)
                var hopDongSapHetHanList = await _context.HopDong
                    .Include(h => h.KhachHangNavigation)
                    .Include(h => h.PhongNavigation)
                    .Where(h => h.TrangThai == "Hiệu lực"
                        && h.NgayKetThuc <= DateTime.Now.AddDays(30)
                        && h.NgayKetThuc >= DateTime.Now)
                    .Select(h => new HopDongSapHetHan
                    {
                        TenPhong = h.PhongNavigation != null ? h.PhongNavigation.TenPhong : "N/A",
                        TenKhachHang = h.KhachHangNavigation != null ? h.KhachHangNavigation.HoTen : "N/A",
                        NgayKetThuc = h.NgayKetThuc ?? DateTime.Now,
                        SoNgayConLai = (h.NgayKetThuc.Value - DateTime.Now).Days
                    })
                    .ToListAsync();

                var soHopDongSapHetHan = hopDongSapHetHanList.Count;

                // Thống kê bài đăng
                var tongSoBaiDang = await _context.BaiDang.CountAsync();
                var soBaiDangHienThi = await _context.BaiDang.CountAsync(b => b.TrangThai == "Hiển thị");
                var soBaiDangAn = tongSoBaiDang - soBaiDangHienThi;
                var soBaiDangThangNay = await _context.BaiDang
                    .CountAsync(b => b.NgayDang != null && b.NgayDang.Value.Month == DateTime.Now.Month);

                var baiDangGanDayList = await _context.BaiDang
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

                var model = new DashboardViewModel
                {
                    TongSoPhong = tongSoPhong,
                    SoPhongDaThue = soPhongDaThue,
                    SoPhongTrong = soPhongTrong,
                    TongSoKhachThue = tongSoKhachThue,
                    DoanhThuThangNay = doanhThuThangNay,
                    SoHopDongSapHetHan = soHopDongSapHetHan,
                    HopDongSapHetHanList = hopDongSapHetHanList,
                    TongSoBaiDang = tongSoBaiDang,
                    SoBaiDangHienThi = soBaiDangHienThi,
                    SoBaiDangAn = soBaiDangAn,
                    SoBaiDangThangNay = soBaiDangThangNay,
                    BaiDangGanDayList = baiDangGanDayList
                };

                return View("AdminDashboard", model);
            }
            else if (role == "Khach")
            {
                // ========== DỮ LIỆU CHO KHÁCH DASHBOARD ==========

                // Tìm khách hàng theo userId
                var khachHang = await _context.KhachHang
                    .FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);

                if (khachHang == null)
                {
                    khachHang = await _context.KhachHang
                        .FirstOrDefaultAsync(k => k.Email == username || k.SoDienThoai == username);
                }

                // Tìm hợp đồng hiện tại
                var hopDong = await _context.HopDong
                    .Include(h => h.PhongNavigation)
                    .FirstOrDefaultAsync(h => h.MaKhachHang == khachHang.MaKhachHang && h.TrangThai == "Hiệu lực");

                // Tìm hóa đơn chưa thanh toán
                var hoaDonChuaThanhToan = new List<HoaDon>();
                if (hopDong != null)
                {
                    hoaDonChuaThanhToan = await _context.HoaDon
                        .Where(h => h.MaHopDong == hopDong.MaHopDong && h.TrangThai == "Chưa thanh toán")
                        .OrderBy(h => h.Nam)
                        .ThenBy(h => h.Thang)
                        .ToListAsync();
                }

                ViewBag.KhachHang = khachHang;
                ViewBag.HopDong = hopDong;
                ViewBag.HoaDonChuaThanhToan = hoaDonChuaThanhToan;

                return View("KhachDashboard");
            }

            return RedirectToAction("Index", "Login");
        }
    }
}
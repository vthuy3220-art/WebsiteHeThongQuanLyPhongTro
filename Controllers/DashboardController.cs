using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

                // 1. Thống kê phòng
                var tongSoPhong = await _context.Phong.CountAsync();
                var soPhongDaThue = await _context.Phong.CountAsync(p => p.TrangThai == "Đã thuê");
                var soPhongTrong = tongSoPhong - soPhongDaThue;
                var tongSoKhachThue = await _context.KhachHang.CountAsync();

                // 2. Thống kê hợp đồng động chuẩn theo danh sách dữ liệu thực tế của ông
                var soHopDongHieuLuc = await _context.HopDong.CountAsync(h => h.TrangThai == "Hiệu lực");
                var soHopDongDaKetThuc = await _context.HopDong.CountAsync(h => h.TrangThai != "Hiệu lực");

                // 3. Doanh thu tháng này từ hóa đơn đã thanh toán
                var doanhThuThangNay = await _context.HoaDon
                    .Where(h => h.TrangThai == "Đã thanh toán"
                        && h.Nam == DateTime.Now.Year
                        && h.Thang == DateTime.Now.Month)
                    .SumAsync(h => h.TongTien) ?? 0;

                // 4. Tính toán công nợ hiện tại từ các hóa đơn chưa thanh toán
                var hoaDonChuaThanhToan = await _context.HoaDon
                    .Where(h => h.TrangThai == "Chưa thanh toán")
                    .ToListAsync();

                var tongNoHienTai = hoaDonChuaThanhToan.Sum(h => h.TongTien ?? 0);
                var soHoaDonChuaThanhToan = hoaDonChuaThanhToan.Count;

                // 5. Hợp đồng sắp hết hạn (còn dưới 30 ngày)
                var hopDongSapHetHanList = await _context.HopDong
                    .Include(h => h.PhongNavigation)
                    .Include(h => h.KhachHangNavigation)
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

                // 6. Thống kê bài đăng
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

                // 7. Lấy danh sách doanh thu 6 tháng gần nhất gửi sang cho biểu đồ đường
                var doanhThuTheoThang = new List<DoanhThuTheoThang>();
                for (int i = 5; i >= 0; i--)
                {
                    var mThang = DateTime.Now.AddMonths(-i);
                    var tienHoaDon = await _context.HoaDon
                        .Where(h => h.Thang == mThang.Month && h.Nam == mThang.Year && h.TrangThai == "Đã thanh toán")
                        .SumAsync(h => h.TongTien ?? 0);

                    doanhThuTheoThang.Add(new DoanhThuTheoThang
                    {
                        Thang = mThang.Month,
                        Nam = mThang.Year,
                        DoanhThu = tienHoaDon
                    });
                }

                // 8. Lấy top phòng dựa trên doanh thu của THÁNG HIỆN TẠI (Đảm bảo thực tế và tự nhảy dữ liệu)
                var thangHienTai = DateTime.Now.Month;
                var namHienTai = DateTime.Now.Year;

                var topPhongList = await (from p in _context.Phong
                                          join hd in _context.HopDong on p.MaPhong equals hd.MaPhong into hdGroup
                                          from hd in hdGroup.DefaultIfEmpty()
                                          join h in _context.HoaDon.Where(x => x.Thang == thangHienTai && x.Nam == namHienTai && x.TrangThai == "Đã thanh toán")
                                          on (hd != null ? hd.MaHopDong : -1) equals h.MaHopDong into hGroup
                                          from h in hGroup.DefaultIfEmpty()
                                          group h by new { p.MaPhong, p.TenPhong } into g
                                          select new TopPhongSuDung
                                          {
                                              MaPhong = g.Key.MaPhong,
                                              TenPhong = g.Key.TenPhong,
                                              TongDoanhThu = g.Sum(x => x != null ? (x.TongTien ?? 0) : 0),
                                              SoHoaDon = g.Count(x => x != null)
                                          })
                                          .OrderByDescending(x => x.TongDoanhThu)
                                          .Take(5)
                                          .ToListAsync();

                var model = new DashboardViewModel
                {
                    TongSoPhong = tongSoPhong,
                    SoPhongDaThue = soPhongDaThue,
                    SoPhongTrong = soPhongTrong,
                    TongSoKhachHang = tongSoKhachThue,
                    DoanhThuThangNay = doanhThuThangNay,
                    TongNoHienTai = tongNoHienTai,
                    SoHoaDonChuaThanhToan = soHoaDonChuaThanhToan,
                    SoHopDongHieuLuc = soHopDongHieuLuc,
                    SoHopDongHetHan = soHopDongDaKetThuc,
                    SoHopDongSapHetHan = soHopDongSapHetHan,
                    HopDongSapHetHanList = hopDongSapHetHanList,
                    TongSoBaiDang = tongSoBaiDang,
                    SoBaiDangHienThi = soBaiDangHienThi,
                    SoBaiDangAn = soBaiDangAn,
                    SoBaiDangThangNay = soBaiDangThangNay,
                    BaiDangGanDayList = baiDangGanDayList,
                    DoanhThuTheoThangList = doanhThuTheoThang,
                    TopPhongList = topPhongList
                };

                return View("AdminDashboard", model);
            }
            else if (role == "Khach")
            {
                var khachHang = await _context.KhachHang.FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);
                if (khachHang == null)
                {
                    khachHang = await _context.KhachHang.FirstOrDefaultAsync(k => k.Email == username || k.SoDienThoai == username);
                }

                var hopDong = await _context.HopDong
                    .Include(h => h.PhongNavigation)
                    .FirstOrDefaultAsync(h => h.MaKhachHang == khachHang.MaKhachHang && h.TrangThai == "Hiệu lực");

                var hoaDonChuaThanhToan = new List<HoaDon>();
                if (hopDong != null)
                {
                    hoaDonChuaThanhToan = await _context.HoaDon
                        .Where(h => h.MaHopDong == hopDong.MaHopDong && h.TrangThai == "Chưa thanh toán")
                        .OrderBy(h => h.Nam).ThenBy(h => h.Thang).ToListAsync();
                }

                ViewBag.KhachHang = khachHang;
                ViewBag.HopDong = hopDong;
                ViewBag.HoaDonChuaThanhToan = hoaDonChuaThanhToan;

                return View("KhachDashboard");
            }

            return RedirectToAction("Index", "Login");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuiThongBaoNhacNo()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin") return RedirectToAction("Index", "Login");

            var hoaDonChuaThanhToan = await _context.HoaDon
                .Include(h => h.HopDongNavigation)
                .Where(h => h.TrangThai == "Chưa thanh toán")
                .ToListAsync();

            if (!hoaDonChuaThanhToan.Any())
            {
                TempData["Info"] = "Hiện tại không có hóa đơn quá hạn nào để nhắc nhở!";
                return RedirectToAction("Index");
            }

            int soThongBaoDaGui = hoaDonChuaThanhToan.Count;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã xử lý tác vụ và gửi thông báo nhắc nợ thành công tới {soThongBaoDaGui} phòng chưa đóng tiền!";
            return RedirectToAction("Index");
        }
    }
}
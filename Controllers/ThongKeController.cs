using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongQuanLyPhongTro.Controllers
{
    public class ThongKeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ThongKeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
            {
                return RedirectToAction("Index", "Login");
            }

            var model = new DashboardViewModel();

            // ========== TỔNG QUAN ==========
            model.TongSoPhong = await _context.Phong.CountAsync();
            model.SoPhongDaThue = await _context.Phong.CountAsync(p => p.TrangThai == "Đã thuê");
            model.SoPhongTrong = model.TongSoPhong - model.SoPhongDaThue;
            model.TongSoKhachHang = await _context.KhachHang.CountAsync();

            model.SoHopDongHieuLuc = await _context.HopDong.CountAsync(h => h.TrangThai == "Hiệu lực");
            model.SoHopDongHetHan = await _context.HopDong.CountAsync(h => h.TrangThai == "Đã hủy" || h.TrangThai == "Hết hạn");

            // ========== DOANH THU ==========
            var homNay = DateTime.Now.Date;
            var ngayMai = homNay.AddDays(1);

            var thanhToanHomNay = await _context.ThanhToan
                .Where(t => t.NgayThanhToan >= homNay && t.NgayThanhToan < ngayMai)
                .ToListAsync();
            model.DoanhThuHomNay = thanhToanHomNay.Sum(t => t.SoTien ?? 0);

            var thanhToanThangNay = await _context.ThanhToan
                .Where(t => t.NgayThanhToan.HasValue
                    && t.NgayThanhToan.Value.Year == DateTime.Now.Year
                    && t.NgayThanhToan.Value.Month == DateTime.Now.Month)
                .ToListAsync();
            model.DoanhThuThangNay = thanhToanThangNay.Sum(t => t.SoTien ?? 0);

            var thanhToanNamNay = await _context.ThanhToan
                .Where(t => t.NgayThanhToan.HasValue && t.NgayThanhToan.Value.Year == DateTime.Now.Year)
                .ToListAsync();
            model.DoanhThuNamNay = thanhToanNamNay.Sum(t => t.SoTien ?? 0);

            var tatCaThanhToan = await _context.ThanhToan.ToListAsync();
            model.DoanhThuTatCa = tatCaThanhToan.Sum(t => t.SoTien ?? 0);

            // ========== CÔNG NỢ ==========
            var hoaDonChuaThanhToan = await _context.HoaDon
                .Where(h => h.TrangThai == "Chưa thanh toán")
                .ToListAsync();
            model.SoHoaDonChuaThanhToan = hoaDonChuaThanhToan.Count;
            model.TongNoHienTai = hoaDonChuaThanhToan.Sum(h => h.TongTien ?? 0);

            // ========== BIỂU ĐỒ DOANH THU THEO THÁNG ==========
            var doanhThuTheoThang = new List<DoanhThuTheoThang>();
            for (int i = 11; i >= 0; i--)
            {
                var thang = DateTime.Now.AddMonths(-i);
                var thanhToan = await _context.ThanhToan
                    .Where(t => t.NgayThanhToan.HasValue
                        && t.NgayThanhToan.Value.Year == thang.Year
                        && t.NgayThanhToan.Value.Month == thang.Month)
                    .ToListAsync();
                var doanhThu = thanhToan.Sum(t => t.SoTien ?? 0);

                doanhThuTheoThang.Add(new DoanhThuTheoThang
                {
                    Thang = thang.Month,
                    Nam = thang.Year,
                    DoanhThu = doanhThu
                });
            }
            model.DoanhThuTheoThangList = doanhThuTheoThang;

            // ========== TRẠNG THÁI PHÒNG ==========
            model.TrangThaiPhongList = new List<TrangThaiPhong>
            {
                new TrangThaiPhong { TrangThai = "Đã thuê", SoLuong = model.SoPhongDaThue },
                new TrangThaiPhong { TrangThai = "Trống", SoLuong = model.SoPhongTrong }
            };

            // ========== TOP KHÁCH HÀNG ==========
            var topKhachQuery = from hd in _context.HoaDon
                                join hd2 in _context.HopDong on hd.MaHopDong equals hd2.MaHopDong
                                join kh in _context.KhachHang on hd2.MaKhachHang equals kh.MaKhachHang
                                where hd.TrangThai == "Đã thanh toán"
                                group hd by new { kh.MaKhachHang, kh.HoTen, kh.SoDienThoai } into g
                                select new TopKhachHang
                                {
                                    MaKhachHang = g.Key.MaKhachHang,
                                    HoTen = g.Key.HoTen,
                                    SoDienThoai = g.Key.SoDienThoai,
                                    TongTienDaThanhToan = g.Sum(x => x.TongTien ?? 0),
                                    SoHoaDonDaThanhToan = g.Count()
                                };

            model.TopKhachHangList = await topKhachQuery
                .OrderByDescending(x => x.TongTienDaThanhToan)
                .Take(10)
                .ToListAsync();

            // ========== HỢP ĐỒNG SẮP HẾT HẠN ==========
            var hopDongList = await _context.HopDong
                .Include(h => h.PhongNavigation)
                .Include(h => h.KhachHangNavigation)
                .Where(h => h.TrangThai == "Hiệu lực")
                .ToListAsync();

            var hopDongSapHetHan = hopDongList
                .Where(h => h.NgayKetThuc.HasValue && h.NgayKetThuc.Value <= DateTime.Now.AddDays(30))
                .Select(h => new HopDongSapHetHan
                {
                    MaHopDong = h.MaHopDong,
                    TenPhong = h.PhongNavigation?.TenPhong ?? "N/A",
                    TenKhachHang = h.KhachHangNavigation?.HoTen ?? "N/A",
                    NgayKetThuc = h.NgayKetThuc ?? DateTime.Now,
                    SoNgayConLai = (h.NgayKetThuc.Value - DateTime.Now).Days
                })
                .OrderBy(h => h.SoNgayConLai)
                .Take(10)
                .ToList();

            model.HopDongSapHetHanList = hopDongSapHetHan;

            // ========== BÀI ĐĂNG GẦN ĐÂY ==========
            var baiDangList = await _context.BaiDang
                .Include(b => b.PhongNavigation)
                .OrderByDescending(b => b.NgayDang)
                .Take(5)
                .ToListAsync();

            model.BaiDangGanDayList = baiDangList.Select(b => new BaiDangGanDay
            {
                MaBaiDang = b.MaBaiDang,
                TieuDe = b.TieuDe ?? "Không có tiêu đề",
                TenPhong = b.PhongNavigation?.TenPhong ?? "N/A",
                NgayDang = b.NgayDang ?? DateTime.Now,
                TrangThai = b.TrangThai ?? "Ẩn",
                LuotXem = 0
            }).ToList();

            model.TongSoBaiDang = await _context.BaiDang.CountAsync();
            model.SoBaiDangHienThi = await _context.BaiDang.CountAsync(b => b.TrangThai == "Hiển thị");
            model.SoBaiDangAn = model.TongSoBaiDang - model.SoBaiDangHienThi;
            model.SoBaiDangThangNay = await _context.BaiDang
                .CountAsync(b => b.NgayDang != null && b.NgayDang.Value.Month == DateTime.Now.Month);

            return View(model);
        }
    }
}
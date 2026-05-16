using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System.Security.Cryptography;

namespace HeThongQuanLyPhongTro.Controllers
{
    public class KhachHangController : Controller
    {
        private readonly ApplicationDbContext _context;

        public KhachHangController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==================== DASHBOARD CHO KHÁCH HÀNG ====================
        public async Task<IActionResult> Dashboard()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var role = HttpContext.Session.GetString("Role");

            if (userId == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (role != "Khach")
            {
                return RedirectToAction("Index", "Login");
            }

            // Tìm khách hàng theo tài khoản
            var khachHang = await _context.KhachHang
                .FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);

            if (khachHang == null)
            {
                return RedirectToAction("Index", "Login");
            }

            // Lấy hợp đồng hiện tại
            var hopDongHienTai = await _context.HopDong
                .FirstOrDefaultAsync(h => h.MaKhachHang == khachHang.MaKhachHang && h.TrangThai == "Hiệu lực");

            // Lấy lịch sử hợp đồng
            var lichSuHopDong = await _context.HopDong
                .Where(h => h.MaKhachHang == khachHang.MaKhachHang && h.TrangThai != "Hiệu lực")
                .OrderByDescending(h => h.NgayBatDau)
                .ToListAsync();

            List<HoaDon> hoaDonChuaThanhToan = new List<HoaDon>();
            List<HoaDon> hoaDonDaThanhToan = new List<HoaDon>();
            decimal tongNo = 0;
            int soNgayConLai = 0;

            if (hopDongHienTai != null)
            {
                hoaDonChuaThanhToan = await _context.HoaDon
                    .Where(h => h.MaHopDong == hopDongHienTai.MaHopDong && h.TrangThai == "Chưa thanh toán")
                    .ToListAsync();

                hoaDonDaThanhToan = await _context.HoaDon
                    .Where(h => h.MaHopDong == hopDongHienTai.MaHopDong && h.TrangThai == "Đã thanh toán")
                    .ToListAsync();

                if (hoaDonChuaThanhToan != null && hoaDonChuaThanhToan.Any())
                {
                    tongNo = hoaDonChuaThanhToan.Sum(h => h.TongTien ?? 0);
                }

                if (hopDongHienTai.NgayKetThuc.HasValue)
                {
                    soNgayConLai = (hopDongHienTai.NgayKetThuc.Value - DateTime.Now).Days;
                    if (soNgayConLai < 0) soNgayConLai = 0;
                }
            }

            var viewModel = new KhachHangDashboardViewModel
            {
                ThongTinKhachHang = khachHang,
                HopDongHienTai = hopDongHienTai,
                LichSuHopDong = lichSuHopDong ?? new List<HopDong>(),
                HoaDonChuaThanhToan = hoaDonChuaThanhToan ?? new List<HoaDon>(),
                HoaDonDaThanhToan = hoaDonDaThanhToan ?? new List<HoaDon>(),
                TongNo = tongNo,
                SoNgayConLai = soNgayConLai
            };

            return View(viewModel);
        }

        // ==================== HỢP ĐỒNG CỦA TÔI ====================
        public async Task<IActionResult> HopDongCuaToi()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Index", "Login");

            var khachHang = await _context.KhachHang
                .FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);

            if (khachHang == null) return RedirectToAction("Index", "Login");

            var hopDongs = await _context.HopDong
                .Where(h => h.MaKhachHang == khachHang.MaKhachHang)
                .OrderByDescending(h => h.NgayBatDau)
                .ToListAsync();

            return View(hopDongs);
        }

        // ==================== HÓA ĐƠN CỦA TÔI ====================
        public async Task<IActionResult> HoaDonCuaToi()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Index", "Login");

            var khachHang = await _context.KhachHang
                .FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);

            if (khachHang == null) return RedirectToAction("Index", "Login");

            var hopDongIds = await _context.HopDong
                .Where(h => h.MaKhachHang == khachHang.MaKhachHang)
                .Select(h => h.MaHopDong)
                .ToListAsync();

            var hoaDons = await _context.HoaDon
                .Where(h => hopDongIds.Contains(h.MaHopDong))
                .OrderByDescending(h => h.Nam)
                .ThenByDescending(h => h.Thang)
                .ToListAsync();

            return View(hoaDons);
        }

        // ==================== CHI TIẾT HÓA ĐƠN ====================
        public async Task<IActionResult> HoaDonChiTiet(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Index", "Login");

            var hoaDon = await _context.HoaDon
                .FirstOrDefaultAsync(h => h.MaHoaDon == id);

            if (hoaDon == null) return NotFound();

            var chiTietHoaDons = await _context.ChiTietHoaDon
                .Where(ct => ct.MaHoaDon == id)
                .ToListAsync();

            ViewBag.ChiTietHoaDons = chiTietHoaDons;
            return View(hoaDon);
        }

        // ==================== LỊCH SỬ THANH TOÁN ====================
        public async Task<IActionResult> LichSuThanhToan()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Index", "Login");

            var khachHang = await _context.KhachHang
                .FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);

            if (khachHang == null) return RedirectToAction("Index", "Login");

            var hopDongIds = await _context.HopDong
                .Where(h => h.MaKhachHang == khachHang.MaKhachHang)
                .Select(h => h.MaHopDong)
                .ToListAsync();

            var hoaDonIds = await _context.HoaDon
                .Where(h => hopDongIds.Contains(h.MaHopDong))
                .Select(h => h.MaHoaDon)
                .ToListAsync();

            var thanhToans = await _context.ThanhToan
                .Where(t => hoaDonIds.Contains(t.MaHoaDon))
                .OrderByDescending(t => t.NgayThanhToan)
                .ToListAsync();

            return View(thanhToans);
        }

        // ==================== ĐỔI MẬT KHẨU ====================
        public IActionResult DoiMatKhau()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> DoiMatKhau(string matKhauCu, string matKhauMoi, string xacNhanMatKhau)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Index", "Login");

            var taiKhoan = await _context.TaiKhoan.FindAsync(userId);
            if (taiKhoan == null) return RedirectToAction("Index", "Login");

            if (taiKhoan.MatKhau != matKhauCu)
            {
                TempData["Error"] = "Mật khẩu cũ không đúng!";
                return View();
            }

            if (matKhauMoi != xacNhanMatKhau)
            {
                TempData["Error"] = "Mật khẩu mới và xác nhận không khớp!";
                return View();
            }

            if (matKhauMoi.Length < 6)
            {
                TempData["Error"] = "Mật khẩu mới phải có ít nhất 6 ký tự!";
                return View();
            }

            taiKhoan.MatKhau = matKhauMoi;
            _context.Update(taiKhoan);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đổi mật khẩu thành công!";
            return RedirectToAction("Dashboard");
        }

        // ==================== QUẢN LÝ KHÁCH HÀNG (CHO CHỦ TRỌ) ====================
        public async Task<IActionResult> QuanLy(string searchString)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin") return RedirectToAction("Index", "Login");

            var khachHangs = _context.KhachHang.AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                khachHangs = khachHangs.Where(k =>
                    k.HoTen.Contains(searchString) ||
                    (k.SoDienThoai != null && k.SoDienThoai.Contains(searchString)) ||
                    (k.CCCD != null && k.CCCD.Contains(searchString)));
            }

            ViewBag.SearchString = searchString;
            return View(await khachHangs.ToListAsync());
        }

        // GET: Thêm khách hàng mới (có tạo tài khoản)
        public IActionResult Create()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin") return RedirectToAction("Index", "Login");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(KhachHang khachHang, string tenDangNhap, string matKhau)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin") return RedirectToAction("Index", "Login");

            if (ModelState.IsValid)
            {
                // Kiểm tra tên đăng nhập đã tồn tại
                var existingAccount = await _context.TaiKhoan
                    .FirstOrDefaultAsync(t => t.TenDangNhap == tenDangNhap);

                if (existingAccount != null)
                {
                    ModelState.AddModelError("", "Tên đăng nhập đã tồn tại!");
                    return View(khachHang);
                }

                // Tạo tài khoản mới
                var taiKhoan = new TaiKhoan
                {
                    TenDangNhap = tenDangNhap,
                    MatKhau = matKhau,
                    VaiTro = "Khach",
                    TrangThai = "Hoạt động"
                };

                _context.TaiKhoan.Add(taiKhoan);
                await _context.SaveChangesAsync();

                // Gán MaTaiKhoan cho khách hàng
                khachHang.MaTaiKhoan = taiKhoan.MaTaiKhoan;
                _context.KhachHang.Add(khachHang);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Thêm khách hàng thành công! Tài khoản: {tenDangNhap} / {matKhau}";
                return RedirectToAction(nameof(QuanLy));
            }
            return View(khachHang);
        }
    }
}
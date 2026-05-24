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
        // ==================== TRANG CHỦ MẶC ĐỊNH CỦA KHÁCH HÀNG ====================
        public IActionResult Index()
        {
            var role = HttpContext.Session.GetString("Role");

            // Phân luồng tùy theo vai trò
            if (role == "Admin")
            {
                return RedirectToAction("QuanLy");
            }
            else if (role == "Khach")
            {
                return RedirectToAction("Dashboard");
            }

            // Nếu chưa đăng nhập hoặc mất Session thì đuổi về trang Login
            return RedirectToAction("Index", "Login");
        }
        // ==================== DASHBOARD CHO KHÁCH HÀNG ====================
        public async Task<IActionResult> Dashboard()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var role = HttpContext.Session.GetString("Role");
            var username = HttpContext.Session.GetString("Username");

            if (userId == null || role != "Khach")
            {
                return RedirectToAction("Index", "Login");
            }

            // Tìm khách hàng
            var khachHang = await _context.KhachHang
                .FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);

            if (khachHang == null)
            {
                khachHang = await _context.KhachHang
                    .FirstOrDefaultAsync(k => k.Email == username || k.SoDienThoai == username);
            }

            if (khachHang == null)
            {
                return RedirectToAction("Index", "Login");
            }

            // Lấy hợp đồng hiện tại
            var hopDong = await _context.HopDong
                .Include(h => h.PhongNavigation)
                .FirstOrDefaultAsync(h => h.MaKhachHang == khachHang.MaKhachHang && h.TrangThai == "Hiệu lực");

            // Dùng ViewBag
            ViewBag.KhachHang = khachHang;
            ViewBag.HopDong = hopDong;

            return View("Index");

        }
        // ==================== HỢP ĐỒNG CỦA TÔI ====================
        public async Task<IActionResult> HopDongCuaToi()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Index", "Login");

            var khachHang = await _context.KhachHang
                .FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);

            if (khachHang == null) return RedirectToAction("Index", "Login");

            // CHỈ LẤY HỢP ĐỒNG CỦA KHÁCH NÀY
            var hopDongs = await _context.HopDong
                .Include(h => h.PhongNavigation)
                .Where(h => h.MaKhachHang == khachHang.MaKhachHang)  // Quan trọng!
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
        // ==================== CHI TIẾT HÓA ĐƠN (CHO KHÁCH) ====================
        public async Task<IActionResult> HoaDonChiTiet(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Index", "Login");

            var khachHang = await _context.KhachHang
                .FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);

            if (khachHang == null) return RedirectToAction("Index", "Login");

            // Lấy hóa đơn và kiểm tra quyền sở hữu
            var hoaDon = await _context.HoaDon
                .Include(h => h.HopDongNavigation)
                    .ThenInclude(h => h.PhongNavigation)
                .Include(h => h.HopDongNavigation)
                    .ThenInclude(h => h.KhachHangNavigation)
                .FirstOrDefaultAsync(h => h.MaHoaDon == id);

            if (hoaDon == null) return NotFound();

            // Kiểm tra hóa đơn có thuộc về khách này không
            if (hoaDon.HopDongNavigation?.MaKhachHang != khachHang.MaKhachHang)
            {
                TempData["Error"] = "Bạn không có quyền xem hóa đơn này!";
                return RedirectToAction("HoaDonCuaToi");
            }

            var chiTietHoaDons = await _context.ChiTietHoaDon
                .Where(ct => ct.MaHoaDon == id)
                .ToListAsync();

            // Lấy số người ở
            int soNguoiO = await _context.NguoiOHopDong
                .CountAsync(n => n.MaHopDong == hoaDon.MaHopDong);
            int soNguoi = soNguoiO + 1;

            // Lấy chỉ số từ chi tiết hóa đơn
            var chiSoDienCu = chiTietHoaDons.FirstOrDefault(c => c.LoaiKhoanThu == "Chỉ số điện cũ")?.SoLuong ?? 0;
            var chiSoDienMoi = chiTietHoaDons.FirstOrDefault(c => c.LoaiKhoanThu == "Chỉ số điện mới")?.SoLuong ?? 0;
            var chiSoNuocCu = chiTietHoaDons.FirstOrDefault(c => c.LoaiKhoanThu == "Chỉ số nước cũ")?.SoLuong ?? 0;
            var chiSoNuocMoi = chiTietHoaDons.FirstOrDefault(c => c.LoaiKhoanThu == "Chỉ số nước mới")?.SoLuong ?? 0;
            var giaDien = chiTietHoaDons.FirstOrDefault(c => c.LoaiKhoanThu == "Đơn giá điện")?.DonGia ?? 3500;
            var giaNuoc = chiTietHoaDons.FirstOrDefault(c => c.LoaiKhoanThu == "Đơn giá nước")?.DonGia ?? 30000;
            var tienPhatSinh = chiTietHoaDons.FirstOrDefault(c => c.LoaiKhoanThu == "Phí phát sinh")?.ThanhTien ?? 0;

            // Tính toán
            var soDien = Math.Max(0, chiSoDienMoi - chiSoDienCu);
            var soNuoc = Math.Max(0, chiSoNuocMoi - chiSoNuocCu);
            var tienDien = soDien * giaDien;
            var tienNuoc = soNuoc * giaNuoc;
            var tienDichVu = soNguoi * 200000;
            var tongTien = (hoaDon.HopDongNavigation?.PhongNavigation?.GiaPhong ?? 0) + tienDien + tienNuoc + tienDichVu + tienPhatSinh;

            ViewBag.HopDong = hoaDon.HopDongNavigation;
            ViewBag.ChiTietHoaDons = chiTietHoaDons;
            ViewBag.SoNguoi = soNguoi;
            ViewBag.ChiSoDienCu = chiSoDienCu;
            ViewBag.ChiSoDienMoi = chiSoDienMoi;
            ViewBag.ChiSoNuocCu = chiSoNuocCu;
            ViewBag.ChiSoNuocMoi = chiSoNuocMoi;
            ViewBag.GiaDien = giaDien;
            ViewBag.GiaNuoc = giaNuoc;
            ViewBag.TienPhatSinh = tienPhatSinh;
            ViewBag.TongTien = tongTien;

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
        // ==================== CHI TIẾT HỢP ĐỒNG (CHO KHÁCH) ====================
        public async Task<IActionResult> HopDongChiTiet(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var role = HttpContext.Session.GetString("Role");

            if (userId == null || role != "Khach")
            {
                return RedirectToAction("Index", "Login");
            }

            // Tìm khách hàng
            var khachHang = await _context.KhachHang
                .FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);

            if (khachHang == null)
            {
                return RedirectToAction("Index", "Login");
            }

            // Lấy hợp đồng và kiểm tra quyền sở hữu
            var hopDong = await _context.HopDong
                .Include(h => h.PhongNavigation)
                .ThenInclude(p => p.CoSo)
                .FirstOrDefaultAsync(h => h.MaHopDong == id && h.MaKhachHang == khachHang.MaKhachHang);

            if (hopDong == null)
            {
                TempData["Error"] = "Không tìm thấy hợp đồng hoặc bạn không có quyền xem!";
                return RedirectToAction("HopDongCuaToi");
            }

            // Lấy danh sách người ở
            var nguoiOList = await _context.NguoiOHopDong
                .Where(n => n.MaHopDong == id)
                .ToListAsync();

            // Lấy danh sách hóa đơn
            var hoaDons = await _context.HoaDon
                .Where(h => h.MaHopDong == id)
                .OrderByDescending(h => h.Nam)
                .ThenByDescending(h => h.Thang)
                .ToListAsync();

            ViewBag.NguoiOList = nguoiOList;
            ViewBag.HoaDons = hoaDons;

            return View(hopDong);
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


        // ==================== XEM CHI TIẾT KHÁCH HÀNG (CHO ADMIN) ====================
        public async Task<IActionResult> Details(int? id)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin") return RedirectToAction("Index", "Login");

            if (id == null) return NotFound();

            var khachHang = await _context.KhachHang
                .FirstOrDefaultAsync(m => m.MaKhachHang == id);

            if (khachHang == null) return NotFound();

            return View(khachHang);
        }

        // ==================== SỬA KHÁCH HÀNG (CHO ADMIN) ====================
        public async Task<IActionResult> Edit(int? id)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin") return RedirectToAction("Index", "Login");

            if (id == null) return NotFound();

            var khachHang = await _context.KhachHang.FindAsync(id);
            if (khachHang == null) return NotFound();

            return View(khachHang);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, KhachHang khachHang)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin") return RedirectToAction("Index", "Login");

            if (id != khachHang.MaKhachHang) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(khachHang);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật thông tin khách hàng thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.KhachHang.Any(e => e.MaKhachHang == khachHang.MaKhachHang))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(QuanLy));
            }
            return View(khachHang);
        }

        // ==================== XÓA KHÁCH HÀNG (CHO ADMIN) ====================
        public async Task<IActionResult> Delete(int? id)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin") return RedirectToAction("Index", "Login");

            if (id == null) return NotFound();

            var khachHang = await _context.KhachHang
                .FirstOrDefaultAsync(m => m.MaKhachHang == id);

            if (khachHang == null) return NotFound();

            return View(khachHang);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin") return RedirectToAction("Index", "Login");

            var khachHang = await _context.KhachHang.FindAsync(id);
            if (khachHang != null)
            {
                // Xóa luôn tài khoản đăng nhập liên kết với khách hàng này (nếu có)
                if (khachHang.MaTaiKhoan != null)
                {
                    var taiKhoan = await _context.TaiKhoan.FindAsync(khachHang.MaTaiKhoan);
                    if (taiKhoan != null) _context.TaiKhoan.Remove(taiKhoan);
                }

                _context.KhachHang.Remove(khachHang);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa khách hàng và tài khoản liên kết thành công!";
            }

            return RedirectToAction(nameof(QuanLy));
        }
    }
}
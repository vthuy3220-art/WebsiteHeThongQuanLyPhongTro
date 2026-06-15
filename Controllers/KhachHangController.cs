using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using QRCoder;
using System.Drawing;
using System.IO;

namespace HeThongQuanLyPhongTro.Controllers
{
    public class KhachHangController : Controller
    {
        private readonly ApplicationDbContext _context;

        public KhachHangController(ApplicationDbContext context)
        {
            _context = context;
        }
        public IActionResult GenerateQR(string data)
        {
            using (QRCodeGenerator generator = new QRCodeGenerator())
            {
                QRCodeData qrData = generator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
                using (QRCode qrCode = new QRCode(qrData))
                {
                    using (Bitmap bitmap = qrCode.GetGraphic(20))
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                            return File(ms.ToArray(), "image/png");
                        }
                    }
                }
            }
        }
        // Helper lấy thông tin user
        private int GetCurrentUserId()
        {
            return HttpContext.Session.GetInt32("UserId") ?? 0;
        }

        private string GetCurrentRole()
        {
            return HttpContext.Session.GetString("Role") ?? "";
        }

        private int GetCurrentMaChuTro()
        {
            return HttpContext.Session.GetInt32("MaChuTro") ?? 0;
        }

        // ==================== TRANG CHỦ MẶC ĐỊNH ====================
        public IActionResult Index()
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0)
            {
                return RedirectToAction("Index", "Login");
            }

            // Phân luồng tùy theo vai trò
            if (role == "Admin" || role == "SuperAdmin")
            {
                return RedirectToAction("QuanLy");
            }
            else if (role == "ChuTro")
            {
                return RedirectToAction("QuanLy");
            }
            else if (role == "Khach")
            {
                return RedirectToAction("Dashboard");
            }

            return RedirectToAction("Index", "Login");
        }

        // ==================== DASHBOARD CHO KHÁCH HÀNG ====================
        public async Task<IActionResult> Dashboard()
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0 || role != "Khach")
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

            // Lấy hợp đồng hiện tại
            var hopDong = await _context.HopDong
                .Include(h => h.PhongNavigation)
                    .ThenInclude(p => p.ToaNha)
                .FirstOrDefaultAsync(h => h.MaKhachHang == khachHang.MaKhachHang && h.TrangThai == "Hiệu lực");

            ViewBag.KhachHang = khachHang;
            ViewBag.HopDong = hopDong;

            return View("Index");
        }

        // ==================== QUẢN LÝ KHÁCH HÀNG (CHO ADMIN & CHỦ TRỌ) ====================
        public async Task<IActionResult> QuanLy(string searchString)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0)
            {
                return RedirectToAction("Index", "Login");
            }

            if (role != "Admin" && role != "SuperAdmin" && role != "ChuTro")
            {
                return RedirectToAction("Index", "Login");
            }
            var khachHangs = _context.KhachHang.AsQueryable();  // Bỏ .Include
                                                                // 👇 PHÂN QUYỀN: Chủ trọ chỉ thấy khách hàng đã thuê phòng của mình
            if (role == "ChuTro")
            {
                var maChuTro = GetCurrentMaChuTro();

                // Lấy danh sách phòng của chủ trọ này
                var phongIds = await _context.Phong
                    .Where(p => p.MaChuTro == maChuTro)
                    .Select(p => p.MaPhong)
                    .ToListAsync();

                // Lấy danh sách khách hàng có hợp đồng với các phòng đó
                var khachHangIds = await _context.HopDong
                    .Where(h => phongIds.Contains(h.MaPhong))
                    .Select(h => h.MaKhachHang)
                    .Distinct()
                    .ToListAsync();

                khachHangs = khachHangs.Where(k => khachHangIds.Contains(k.MaKhachHang));
            }

            // Tìm kiếm
            if (!string.IsNullOrEmpty(searchString))
            {
                khachHangs = khachHangs.Where(k =>
                    k.HoTen.Contains(searchString) ||
                    (k.SoDienThoai != null && k.SoDienThoai.Contains(searchString)) ||
                    (k.CCCD != null && k.CCCD.Contains(searchString)));
            }

            ViewBag.SearchString = searchString;
            ViewBag.Role = role;
            return View(await khachHangs.ToListAsync());
        }

        // ==================== HỢP ĐỒNG CỦA TÔI (CHO KHÁCH) ====================
        public async Task<IActionResult> HopDongCuaToi()
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0) return RedirectToAction("Index", "Login");

            if (role != "Khach")
            {
                return RedirectToAction("Index", "Login");
            }

            var khachHang = await _context.KhachHang
                .FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);

            if (khachHang == null) return RedirectToAction("Index", "Login");

            var hopDongs = await _context.HopDong
                .Include(h => h.PhongNavigation)
                    .ThenInclude(p => p.ToaNha)
                .Where(h => h.MaKhachHang == khachHang.MaKhachHang)
                .OrderByDescending(h => h.NgayBatDau)
                .ToListAsync();

            return View(hopDongs);
        }

        // ==================== CHI TIẾT HỢP ĐỒNG (CHO KHÁCH) ====================
        public async Task<IActionResult> HopDongChiTiet(int id)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0 || role != "Khach")
            {
                return RedirectToAction("Index", "Login");
            }

            var khachHang = await _context.KhachHang
                .FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);

            if (khachHang == null)
            {
                return RedirectToAction("Index", "Login");
            }

            var hopDong = await _context.HopDong
                .Include(h => h.PhongNavigation)
                    .ThenInclude(p => p.ToaNha)
                .FirstOrDefaultAsync(h => h.MaHopDong == id && h.MaKhachHang == khachHang.MaKhachHang);

            if (hopDong == null)
            {
                TempData["Error"] = "Không tìm thấy hợp đồng hoặc bạn không có quyền xem!";
                return RedirectToAction("HopDongCuaToi");
            }

            var nguoiOList = await _context.NguoiOHopDong
                .Where(n => n.MaHopDong == id)
                .ToListAsync();

            var hoaDons = await _context.HoaDon
                .Where(h => h.MaHopDong == id)
                .OrderByDescending(h => h.Nam)
                .ThenByDescending(h => h.Thang)
                .ToListAsync();

            ViewBag.NguoiOList = nguoiOList;
            ViewBag.HoaDons = hoaDons;

            return View(hopDong);
        }

        // ==================== HÓA ĐƠN CỦA TÔI (CHO KHÁCH) ====================
        public async Task<IActionResult> HoaDonCuaToi()
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0 || role != "Khach")
            {
                return RedirectToAction("Index", "Login");
            }

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

        // ==================== CHI TIẾT HÓA ĐƠN (CHO KHÁCH) ====================
        public async Task<IActionResult> HoaDonChiTiet(int id)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0) return RedirectToAction("Index", "Login");

            var khachHang = await _context.KhachHang
                .FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);

            if (khachHang == null) return RedirectToAction("Index", "Login");

            var hoaDon = await _context.HoaDon
                .Include(h => h.HopDongNavigation)
                    .ThenInclude(h => h.PhongNavigation)
                        .ThenInclude(p => p.ToaNha)
                .Include(h => h.HopDongNavigation)
                    .ThenInclude(h => h.KhachHangNavigation)
                .FirstOrDefaultAsync(h => h.MaHoaDon == id);

            if (hoaDon == null) return NotFound();

            // Kiểm tra quyền sở hữu
            if (role == "Khach" && hoaDon.HopDongNavigation?.MaKhachHang != khachHang.MaKhachHang)
            {
                TempData["Error"] = "Bạn không có quyền xem hóa đơn này!";
                return RedirectToAction("HoaDonCuaToi");
            }

            var chiTietHoaDons = await _context.ChiTietHoaDon
                .Where(ct => ct.MaHoaDon == id)
                .ToListAsync();

            int soNguoiO = await _context.NguoiOHopDong
                .CountAsync(n => n.MaHopDong == hoaDon.MaHopDong);
            int soNguoi = soNguoiO + 1;

            var chiSoDienCu = chiTietHoaDons.FirstOrDefault(c => c.LoaiKhoanThu == "Chỉ số điện cũ")?.SoLuong ?? 0;
            var chiSoDienMoi = chiTietHoaDons.FirstOrDefault(c => c.LoaiKhoanThu == "Chỉ số điện mới")?.SoLuong ?? 0;
            var chiSoNuocCu = chiTietHoaDons.FirstOrDefault(c => c.LoaiKhoanThu == "Chỉ số nước cũ")?.SoLuong ?? 0;
            var chiSoNuocMoi = chiTietHoaDons.FirstOrDefault(c => c.LoaiKhoanThu == "Chỉ số nước mới")?.SoLuong ?? 0;
            var giaDien = chiTietHoaDons.FirstOrDefault(c => c.LoaiKhoanThu == "Đơn giá điện")?.DonGia ?? 3500;
            var giaNuoc = chiTietHoaDons.FirstOrDefault(c => c.LoaiKhoanThu == "Đơn giá nước")?.DonGia ?? 30000;
            var tienPhatSinh = chiTietHoaDons.FirstOrDefault(c => c.LoaiKhoanThu == "Phí phát sinh")?.ThanhTien ?? 0;

            var soDien = Math.Max(0, (chiSoDienMoi - chiSoDienCu));
            var soNuoc = Math.Max(0, (chiSoNuocMoi - chiSoNuocCu));
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

        // ==================== LỊCH SỬ THANH TOÁN (CHO KHÁCH) ====================
        public async Task<IActionResult> LichSuThanhToan()
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0 || role != "Khach")
            {
                return RedirectToAction("Index", "Login");
            }

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
            var userId = GetCurrentUserId();
            if (userId == 0)
            {
                return RedirectToAction("Index", "Login");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> DoiMatKhau(string matKhauCu, string matKhauMoi, string xacNhanMatKhau)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Index", "Login");

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

        // ==================== CHI TIẾT KHÁCH HÀNG ====================
        public async Task<IActionResult> Details(int? id)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0)
            {
                return RedirectToAction("Index", "Login");
            }

            if (id == null) return NotFound();

            var khachHang = await _context.KhachHang
        // .Include(k => k.TaiKhoan)  // COMMENT DÒNG NÀY
        .FirstOrDefaultAsync(m => m.MaKhachHang == id);
            if (khachHang == null) return NotFound();

            // 👇 PHÂN QUYỀN: Chủ trọ chỉ xem được khách hàng của mình
            if (role == "ChuTro")
            {
                var maChuTro = GetCurrentMaChuTro();
                var phongIds = await _context.Phong
                    .Where(p => p.MaChuTro == maChuTro)
                    .Select(p => p.MaPhong)
                    .ToListAsync();

                var hopDongExists = await _context.HopDong
                    .AnyAsync(h => h.MaKhachHang == id && phongIds.Contains(h.MaPhong));

                if (!hopDongExists)
                {
                    TempData["Error"] = "Bạn không có quyền xem khách hàng này!";
                    return RedirectToAction("QuanLy");
                }
            }

            return View(khachHang);
        }

        // ==================== THÊM KHÁCH HÀNG ====================
        public IActionResult Create()
        {
            var role = GetCurrentRole();
            if (role != "Admin" && role != "SuperAdmin")
            {
                return RedirectToAction("Index", "Login");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(KhachHang khachHang, string tenDangNhap, string matKhau)
        {
            var role = GetCurrentRole();
            if (role != "Admin" && role != "SuperAdmin")
            {
                return RedirectToAction("Index", "Login");
            }

            if (ModelState.IsValid)
            {
                var existingAccount = await _context.TaiKhoan
                    .FirstOrDefaultAsync(t => t.TenDangNhap == tenDangNhap);

                if (existingAccount != null)
                {
                    ModelState.AddModelError("", "Tên đăng nhập đã tồn tại!");
                    return View(khachHang);
                }

                var taiKhoan = new TaiKhoan
                {
                    TenDangNhap = tenDangNhap,
                    MatKhau = matKhau,
                    VaiTro = "Khach",
                    TrangThai = "Hoạt động"
                };

                _context.TaiKhoan.Add(taiKhoan);
                await _context.SaveChangesAsync();

                khachHang.MaTaiKhoan = taiKhoan.MaTaiKhoan;
                _context.KhachHang.Add(khachHang);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Thêm khách hàng thành công! Tài khoản: {tenDangNhap} / {matKhau}";
                return RedirectToAction(nameof(QuanLy));
            }
            return View(khachHang);
        }

        // ==================== SỬA KHÁCH HÀNG ====================
        public async Task<IActionResult> Edit(int? id)
        {
            var role = GetCurrentRole();
            if (role != "Admin" && role != "SuperAdmin")
            {
                return RedirectToAction("Index", "Login");
            }

            if (id == null) return NotFound();

            var khachHang = await _context.KhachHang.FindAsync(id);
            if (khachHang == null) return NotFound();

            return View(khachHang);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, KhachHang khachHang)
        {
            var role = GetCurrentRole();
            if (role != "Admin" && role != "SuperAdmin")
            {
                return RedirectToAction("Index", "Login");
            }

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

        // ==================== XÓA KHÁCH HÀNG ====================
        public async Task<IActionResult> Delete(int? id)
        {
            var role = GetCurrentRole();
            if (role != "Admin" && role != "SuperAdmin")
            {
                return RedirectToAction("Index", "Login");
            }

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
            var role = GetCurrentRole();
            if (role != "Admin" && role != "SuperAdmin")
            {
                return RedirectToAction("Index", "Login");
            }

            var khachHang = await _context.KhachHang.FindAsync(id);
            if (khachHang != null)
            {
                var coHopDong = await _context.HopDong.AnyAsync(h => h.MaKhachHang == id);
                if (coHopDong)
                {
                    TempData["Error"] = "Không thể xóa khách hàng này vì đã có hợp đồng!";
                    return RedirectToAction(nameof(QuanLy));
                }

                _context.KhachHang.Remove(khachHang);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa khách hàng thành công!";
            }

            return RedirectToAction(nameof(QuanLy));
        }
    }
}
using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;
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

            var khachHang = await _context.KhachHang
                .FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);

            if (khachHang == null)
            {
                return RedirectToAction("Index", "Login");
            }

            var hopDong = await _context.HopDong
                .Include(h => h.PhongNavigation)
                    .ThenInclude(p => p.ToaNha)
                .FirstOrDefaultAsync(h => h.MaKhachHang == khachHang.MaKhachHang && h.TrangThai == "Hiệu lực");

            var hopDongIds = await _context.HopDong
                .Where(h => h.MaKhachHang == khachHang.MaKhachHang)
                .Select(h => h.MaHopDong)
                .ToListAsync();

            var hoaDonsGanDay = await _context.HoaDon
                .Where(h => hopDongIds.Contains(h.MaHopDong))
                .OrderByDescending(h => h.Nam)
                .ThenByDescending(h => h.Thang)
                .Take(5)
                .ToListAsync();

            var maHoaDonIds = hoaDonsGanDay.Select(h => h.MaHoaDon).ToList();
            var thanhToans = await _context.ThanhToan
                .Where(t => maHoaDonIds.Contains(t.MaHoaDon))
                .ToListAsync();

            ViewBag.KhachHang = khachHang;
            ViewBag.HopDong = hopDong;
            ViewBag.HoaDonsGanDay = hoaDonsGanDay;
            ViewBag.ThanhToansMap = thanhToans;

            return View("Index");
        }

        // ==================== QUẢN LÝ KHÁCH HÀNG ====================
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

            var khachHangs = _context.KhachHang.AsQueryable();

            if (role == "ChuTro")
            {
                var maChuTro = GetCurrentMaChuTro();
                var phongIds = await _context.Phong
                    .Where(p => p.MaChuTro == maChuTro)
                    .Select(p => p.MaPhong)
                    .ToListAsync();

                var khachHangIds = await _context.HopDong
                    .Where(h => phongIds.Contains(h.MaPhong))
                    .Select(h => h.MaKhachHang)
                    .Distinct()
                    .ToListAsync();

                khachHangs = khachHangs.Where(k => khachHangIds.Contains(k.MaKhachHang));
            }

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

        // ==================== HỢP ĐỒNG CỦA TÔI ====================
        public async Task<IActionResult> HopDongCuaToi()
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0) return RedirectToAction("Index", "Login");
            if (role != "Khach") return RedirectToAction("Index", "Login");

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

        // ==================== CHI TIẾT HỢP ĐỒNG ====================
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

        // ==================== HÓA ĐƠN CỦA TÔI ====================
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
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Index", "Login");

            var hoaDon = await _context.HoaDon
                .Include(h => h.HopDongNavigation)
                    .ThenInclude(h => h.PhongNavigation)
                        .ThenInclude(p => p.ToaNha)
                .Include(h => h.HopDongNavigation)
                    .ThenInclude(h => h.KhachHangNavigation)
                .FirstOrDefaultAsync(h => h.MaHoaDon == id);

            if (hoaDon == null) return NotFound();

            // Kiểm tra quyền
            var khachHang = await _context.KhachHang
                .FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);

            if (khachHang == null ||
                hoaDon.HopDongNavigation?.MaKhachHang != khachHang.MaKhachHang)
            {
                TempData["Error"] = "Bạn không có quyền xem hóa đơn này!";
                return RedirectToAction("Index");
            }

            // ✅ Lấy chi tiết hóa đơn
            var chiTietHoaDons = await _context.ChiTietHoaDon
                .Where(c => c.MaHoaDon == id)
                .ToListAsync();

            // ✅ Lấy chỉ số từ chi tiết hóa đơn
            var chiSoDienCu = chiTietHoaDons
                .FirstOrDefault(c => c.LoaiKhoanThu == "Chỉ số điện cũ")?.SoLuong ?? 0;
            var chiSoDienMoi = chiTietHoaDons
                .FirstOrDefault(c => c.LoaiKhoanThu == "Chỉ số điện mới")?.SoLuong ?? 0;
            var chiSoNuocCu = chiTietHoaDons
                .FirstOrDefault(c => c.LoaiKhoanThu == "Chỉ số nước cũ")?.SoLuong ?? 0;
            var chiSoNuocMoi = chiTietHoaDons
                .FirstOrDefault(c => c.LoaiKhoanThu == "Chỉ số nước mới")?.SoLuong ?? 0;
            var giaDien = chiTietHoaDons
                .FirstOrDefault(c => c.LoaiKhoanThu == "Đơn giá điện")?.DonGia ?? 3500;
            var giaNuoc = chiTietHoaDons
                .FirstOrDefault(c => c.LoaiKhoanThu == "Đơn giá nước")?.DonGia ?? 30000;
            var tienPhatSinh = chiTietHoaDons
                .FirstOrDefault(c => c.LoaiKhoanThu == "Phí phát sinh")?.ThanhTien ?? 0;

            int soNguoiO = await _context.NguoiOHopDong
                .CountAsync(n => n.MaHopDong == hoaDon.MaHopDong);
            int soNguoi = soNguoiO + 1;

            // ✅ TRUYỀN TẤT CẢ VÀO ViewBag
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
            ViewBag.TongTien = hoaDon.TongTien;

            // ✅ Lấy thông tin ngân hàng
            string tenNganHang = "Techcombank";
            string soTaiKhoan = "19072789933016";
            string chuTaiKhoan = "Vu Thi Thanh Thuy";

            if (hoaDon.MaChuTro != null && hoaDon.MaChuTro > 0)
            {
                var chuTro = await _context.TaiKhoan
                    .FirstOrDefaultAsync(t => t.MaTaiKhoan == hoaDon.MaChuTro && t.VaiTro == "ChuTro");

                if (chuTro != null && !string.IsNullOrEmpty(chuTro.SoTaiKhoan))
                {
                    tenNganHang = chuTro.TenNganHang ?? "Techcombank";
                    soTaiKhoan = chuTro.SoTaiKhoan;
                    chuTaiKhoan = chuTro.ChuTaiKhoan ?? "Unknown";
                }
            }

            // Chuẩn hóa tên ngân hàng
            string bankId = tenNganHang.ToUpper();
            var bankMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "MBBANK", "MB" },
                { "MB BANK", "MB" },
                { "MB", "MB" },
                { "TECHCOMBANK", "TCB" },
                { "TCB", "TCB" },
                { "VIETCOMBANK", "VCB" },
                { "VCB", "VCB" },
                { "VIETINBANK", "VTB" },
                { "VTB", "VTB" },
                { "BIDV", "BIDV" },
                { "ACB", "ACB" },
                { "SACOMBANK", "STB" },
                { "STB", "STB" },
                { "VPBANK", "VPB" },
                { "VPB", "VPB" },
                { "TPBANK", "TPB" },
                { "TPB", "TPB" }
            };

            if (bankMap.ContainsKey(bankId))
            {
                bankId = bankMap[bankId];
            }

            // Tạo QR
            string amount = hoaDon.TongTien?.ToString("0") ?? "0";
            string content = $"TT_HD_{hoaDon.MaHoaDon}_{hoaDon.Thang}{hoaDon.Nam}";
            string accountNameEncoded = System.Web.HttpUtility.UrlEncode(chuTaiKhoan);
            string qrUrl = $"https://img.vietqr.io/image/{bankId}-{soTaiKhoan}-compact.png?amount={amount}&addInfo={content}&accountName={accountNameEncoded}";

            ViewBag.QRUrl = qrUrl;
            ViewBag.TenNganHang = tenNganHang;
            ViewBag.SoTaiKhoan = soTaiKhoan;
            ViewBag.ChuTaiKhoan = chuTaiKhoan;

            return View(hoaDon);
        }

        // ==================== LỊCH SỬ THANH TOÁN ====================
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
                .FirstOrDefaultAsync(m => m.MaKhachHang == id);

            if (khachHang == null) return NotFound();

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
            if (role != "Admin" && role != "SuperAdmin" && role != "ChuTro")
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
            if (role != "Admin" && role != "SuperAdmin" && role != "ChuTro")
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

        // ==================== XÓA TÀI KHOẢN KHÁCH  ====================
        public async Task<IActionResult> Delete(int? id)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();
            if (userId == 0) return RedirectToAction("Index", "Login");

            if (id == null) return NotFound();

            var taiKhoan = await _context.TaiKhoan.FindAsync(id);
            if (taiKhoan == null) return NotFound();

            // Thực hiện xóa liên kết với khách hàng trước
            if (taiKhoan.VaiTro == "Khach")
            {
                var khachHang = await _context.KhachHang.FirstOrDefaultAsync(k => k.MaTaiKhoan == id);
                if (khachHang != null)
                {
                    khachHang.MaTaiKhoan = null;
                    _context.Update(khachHang);
                    await _context.SaveChangesAsync();
                }
            }

            // Xóa tài khoản khỏi DB
            _context.TaiKhoan.Remove(taiKhoan);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Xóa tài khoản người dùng thành công!";

            // Tự động nhận diện vai trò để điều hướng về đúng trang danh sách đang xem
            if (role == "ChuTro")
            {
                return RedirectToAction("DanhSachKhachHang");
            }
            return RedirectToAction("Index");
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
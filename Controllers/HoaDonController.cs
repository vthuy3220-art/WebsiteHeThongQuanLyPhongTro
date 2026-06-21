using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using HeThongQuanLyPhongTro.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using System.IO;

namespace HeThongQuanLyPhongTro.Controllers
{
    public class HoaDonController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ThongBaoService _thongBaoService;

        public HoaDonController(ApplicationDbContext context, ThongBaoService thongBaoService)
        {
            _context = context;
            _thongBaoService = thongBaoService;
        }

        // ==================== HELPER ====================
        private int GetCurrentUserId()
        {
            return HttpContext.Session.GetInt32("UserId") ?? 0;
        }

        private string GetCurrentRole()
        {
            return HttpContext.Session.GetString("Role") ?? "";
        }

        // ==================== DANH SÁCH HÓA ĐƠN ====================
        public async Task<IActionResult> Index(string searchString, int? thang, int? nam, string trangThai)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return RedirectToAction("Index", "Login");

            var role = GetCurrentRole();
            var userId = GetCurrentUserId();

            var hoaDons = _context.HoaDon
                .Include(h => h.HopDongNavigation)
                    .ThenInclude(h => h.PhongNavigation)
                .Include(h => h.HopDongNavigation)
                    .ThenInclude(h => h.KhachHangNavigation)
                .Where(h => h.HopDongNavigation.TrangThai == "Hiệu lực")
                .AsQueryable();

            // ✅ PHÂN QUYỀN: Chủ trọ chỉ thấy hóa đơn của mình
            if (role == "ChuTro")
            {
                hoaDons = hoaDons.Where(h => h.MaChuTro == userId);
            }
            // ✅ KHÁCH HÀNG CHỈ THẤY HÓA ĐƠN CỦA MÌNH
            else if (role == "Khach")
            {
                var khachHangCurrent = await _context.KhachHang
                    .FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);
                if (khachHangCurrent != null)
                {
                    var listHopDongIds = await _context.HopDong
                        .Where(h => h.MaKhachHang == khachHangCurrent.MaKhachHang)
                        .Select(h => h.MaHopDong)
                        .ToListAsync();
                    hoaDons = hoaDons.Where(h => listHopDongIds.Contains(h.MaHopDong));
                }
                else
                {
                    hoaDons = hoaDons.Where(h => false);
                }
            }
            // ✅ ADMIN KHÔNG ĐƯỢC XEM (CHỈ XEM QUA TatCaHoaDon)
            else if (role == "Admin")
            {
                TempData["Error"] = "Admin vui lòng sử dụng chức năng 'Tất cả hóa đơn'!";
                return RedirectToAction("TatCaHoaDon");
            }

            if (!string.IsNullOrEmpty(searchString))
            {
                hoaDons = hoaDons.Where(h =>
                    (h.HopDongNavigation != null && h.HopDongNavigation.KhachHangNavigation != null &&
                     h.HopDongNavigation.KhachHangNavigation.HoTen.Contains(searchString)) ||
                    (h.HopDongNavigation != null && h.HopDongNavigation.PhongNavigation != null &&
                     h.HopDongNavigation.PhongNavigation.TenPhong.Contains(searchString)));
            }

            if (thang.HasValue) hoaDons = hoaDons.Where(h => h.Thang == thang.Value);
            if (nam.HasValue) hoaDons = hoaDons.Where(h => h.Nam == nam.Value);
            if (!string.IsNullOrEmpty(trangThai) && trangThai != "Tất cả")
                hoaDons = hoaDons.Where(h => h.TrangThai == trangThai);

            ViewBag.SearchString = searchString;
            ViewBag.Thang = thang;
            ViewBag.Nam = nam;
            ViewBag.TrangThai = trangThai;
            ViewBag.TrangThaiList = new List<string> { "Tất cả", "Chưa thanh toán", "Đã thanh toán" };

            var namList = await hoaDons
                .Select(h => h.Nam)
                .Distinct()
                .OrderByDescending(n => n)
                .ToListAsync();
            ViewBag.NamList = namList;

            return View(await hoaDons.ToListAsync());
        }

        // ==================== DANH SÁCH TẤT CẢ HÓA ĐƠN (ADMIN) ====================
        public async Task<IActionResult> TatCaHoaDon(string searchString, int? thang, int? nam, string trangThai)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return RedirectToAction("Index", "Login");

            var role = GetCurrentRole();

            if (role != "Admin")
            {
                TempData["Error"] = "Bạn không có quyền truy cập!";
                return RedirectToAction("Index", "Home");
            }

            var hoaDons = _context.HoaDon
                .Include(h => h.HopDongNavigation)
                    .ThenInclude(h => h.PhongNavigation)
                .Include(h => h.HopDongNavigation)
                    .ThenInclude(h => h.KhachHangNavigation)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                hoaDons = hoaDons.Where(h =>
                    (h.HopDongNavigation != null && h.HopDongNavigation.KhachHangNavigation != null &&
                     h.HopDongNavigation.KhachHangNavigation.HoTen.Contains(searchString)) ||
                    (h.HopDongNavigation != null && h.HopDongNavigation.PhongNavigation != null &&
                     h.HopDongNavigation.PhongNavigation.TenPhong.Contains(searchString)));
            }

            if (thang.HasValue) hoaDons = hoaDons.Where(h => h.Thang == thang.Value);
            if (nam.HasValue) hoaDons = hoaDons.Where(h => h.Nam == nam.Value);
            if (!string.IsNullOrEmpty(trangThai) && trangThai != "Tất cả")
                hoaDons = hoaDons.Where(h => h.TrangThai == trangThai);

            ViewBag.SearchString = searchString;
            ViewBag.Thang = thang;
            ViewBag.Nam = nam;
            ViewBag.TrangThai = trangThai;
            ViewBag.TrangThaiList = new List<string> { "Tất cả", "Chưa thanh toán", "Đã thanh toán" };
            ViewBag.NamList = await _context.HoaDon.Select(h => h.Nam).Distinct().OrderByDescending(n => n).ToListAsync();

            return View(await hoaDons.ToListAsync());
        }

        // ==================== CHI TIẾT HÓA ĐƠN ====================
        public async Task<IActionResult> Details(int? id)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return RedirectToAction("Index", "Login");

            if (id == null) return NotFound();

            var hoaDon = await _context.HoaDon
                .FirstOrDefaultAsync(m => m.MaHoaDon == id);
            if (hoaDon == null) return NotFound();

            // ✅ PHÂN QUYỀN: Kiểm tra quyền xem
            var role = GetCurrentRole();
            var userId = GetCurrentUserId();
            var hasAccess = false;

            if (role == "ChuTro" && hoaDon.MaChuTro == userId)
            {
                hasAccess = true;
            }
            else if (role == "Khach")
            {
                var khachHangCurrent = await _context.KhachHang
                    .FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);
                if (khachHangCurrent != null)
                {
                    var hopDongCurrent = await _context.HopDong
                        .FirstOrDefaultAsync(h => h.MaHopDong == hoaDon.MaHopDong);
                    if (hopDongCurrent != null && hopDongCurrent.MaKhachHang == khachHangCurrent.MaKhachHang)
                    {
                        hasAccess = true;
                    }
                }
            }
            else if (role == "Admin")
            {
                TempData["Error"] = "Admin không có quyền xem chi tiết hóa đơn!";
                return RedirectToAction("Index", "Home");
            }

            if (!hasAccess)
            {
                TempData["Error"] = "Bạn không có quyền xem hóa đơn này!";
                return RedirectToAction("Index");
            }

            // ========== CODE GỐC - KHÔNG THAY ĐỔI ==========
            var hopDong = await _context.HopDong
                .Include(h => h.PhongNavigation)
                .Include(h => h.KhachHangNavigation)
                .FirstOrDefaultAsync(h => h.MaHopDong == hoaDon.MaHopDong);

            var chiTietHoaDons = await _context.ChiTietHoaDon
                .Where(c => c.MaHoaDon == id)
                .ToListAsync();

            // Lấy số người từ bảng NguoiOHopDong
            int soNguoi = 1;
            if (hopDong != null)
            {
                int soNguoiO = await _context.NguoiOHopDong
                    .CountAsync(n => n.MaHopDong == hopDong.MaHopDong);
                soNguoi = soNguoiO + 1;
            }

            // ========== TỰ ĐỘNG LẤY CHỈ SỐ CŨ TỪ THÁNG TRƯỚC ==========
            decimal chiSoDienCu = 0;
            decimal chiSoNuocCu = 0;

            if (hopDong != null && hoaDon.Thang > 1)
            {
                int thangTruoc = hoaDon.Thang - 1;
                int namTruoc = hoaDon.Nam;
                if (thangTruoc == 0)
                {
                    thangTruoc = 12;
                    namTruoc = hoaDon.Nam - 1;
                }

                var lichSuThangTruoc = await _context.LichSuChiSoDienNuoc
                    .FirstOrDefaultAsync(l => l.MaPhong == hopDong.MaPhong && l.Thang == thangTruoc && l.Nam == namTruoc);

                if (lichSuThangTruoc != null)
                {
                    chiSoDienCu = lichSuThangTruoc.ChiSoDienMoi;
                    chiSoNuocCu = lichSuThangTruoc.ChiSoNuocMoi;
                }
            }

            // Lấy chỉ số mới từ chi tiết hóa đơn hiện tại
            var chiSoDienMoi = chiTietHoaDons?.FirstOrDefault(c => c.LoaiKhoanThu == "Chỉ số điện mới")?.SoLuong ?? chiSoDienCu;
            var chiSoNuocMoi = chiTietHoaDons?.FirstOrDefault(c => c.LoaiKhoanThu == "Chỉ số nước mới")?.SoLuong ?? chiSoNuocCu;
            var giaDien = chiTietHoaDons?.FirstOrDefault(c => c.LoaiKhoanThu == "Đơn giá điện")?.DonGia ?? 3500;
            var giaNuoc = chiTietHoaDons?.FirstOrDefault(c => c.LoaiKhoanThu == "Đơn giá nước")?.DonGia ?? 30000;
            var tienPhatSinh = chiTietHoaDons?.FirstOrDefault(c => c.LoaiKhoanThu == "Phí phát sinh")?.ThanhTien ?? 0;

            // ========== KIỂM TRA QR CODE ==========
            string qrImagePath = "/images/qr_code.jpg";
            string physicalPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "qr_code.jpg");
            if (System.IO.File.Exists(physicalPath))
            {
                ViewBag.QRImagePath = qrImagePath;
                byte[] imageBytes = await System.IO.File.ReadAllBytesAsync(physicalPath);
                ViewBag.QRImageBase64 = Convert.ToBase64String(imageBytes);
            }
            else
            {
                ViewBag.QRImagePath = null;
                ViewBag.QRImageBase64 = "";
            }

            ViewBag.HopDong = hopDong;
            ViewBag.ChiTietHoaDons = chiTietHoaDons;
            ViewBag.SoNguoi = soNguoi;
            ViewBag.ChiSoDienCu = chiSoDienCu;
            ViewBag.ChiSoDienMoi = chiSoDienMoi;
            ViewBag.ChiSoNuocCu = chiSoNuocCu;
            ViewBag.ChiSoNuocMoi = chiSoNuocMoi;
            ViewBag.GiaDien = giaDien;
            ViewBag.GiaNuoc = giaNuoc;
            ViewBag.TienPhatSinh = tienPhatSinh;
            // Lấy thông tin ngân hàng của chủ trọ
            var chuTroInfo = await _context.TaiKhoan
                .FirstOrDefaultAsync(u => u.MaTaiKhoan == hoaDon.MaChuTro);

            ViewBag.TenNganHang = chuTroInfo?.TenNganHang ?? "Techcombank";
            ViewBag.SoTaiKhoan = chuTroInfo?.SoTaiKhoan ?? "19072789933016";
            ViewBag.ChuTaiKhoan = chuTroInfo?.ChuTaiKhoan ?? "N/A";
            return View(hoaDon);
        }

        // ==================== CẬP NHẬT CHI TIẾT HÓA ĐƠN ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CapNhatChiTietHoaDon(
            int maHoaDon, int soNguoi,
            decimal chiSoDienMoi, decimal chiSoNuocMoi,
            decimal giaDien, decimal giaNuoc,
            decimal tienPhatSinh)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0)
                return RedirectToAction("Index", "Login");

            var hoaDon = await _context.HoaDon.FindAsync(maHoaDon);
            if (hoaDon == null) return RedirectToAction(nameof(Index));

            // ✅ PHÂN QUYỀN: Chỉ Chủ trọ mới cập nhật và chỉ hóa đơn của mình
            if (role != "ChuTro")
            {
                TempData["Error"] = "Chỉ chủ trọ mới được cập nhật hóa đơn!";
                return RedirectToAction("Index");
            }

            if (hoaDon.MaChuTro != userId)
            {
                TempData["Error"] = "Bạn không có quyền cập nhật hóa đơn này!";
                return RedirectToAction("Index");
            }

            if (hoaDon.TrangThai == "Đã thanh toán")
            {
                TempData["Error"] = "Hóa đơn đã thanh toán, không thể cập nhật!";
                return RedirectToAction(nameof(Details), new { id = maHoaDon });
            }

            // ========== CODE GỐC - KHÔNG THAY ĐỔI ==========
            var hopDong = await _context.HopDong
                .Include(h => h.PhongNavigation)
                .FirstOrDefaultAsync(h => h.MaHopDong == hoaDon.MaHopDong);

            var maPhong = hopDong?.MaPhong ?? 0;
            var giaPhong = hopDong?.PhongNavigation?.GiaPhong ?? 0;
            var thangHienTai = hoaDon.Thang;
            var namHienTai = hoaDon.Nam;

            // Lấy chỉ số cũ từ tháng trước
            decimal chiSoDienCu = 0;
            decimal chiSoNuocCu = 0;

            int thangTruoc = thangHienTai == 1 ? 12 : thangHienTai - 1;
            int namTruoc = thangHienTai == 1 ? namHienTai - 1 : namHienTai;

            var lichSuThangTruoc = await _context.LichSuChiSoDienNuoc
                .FirstOrDefaultAsync(l => l.MaPhong == maPhong && l.Thang == thangTruoc && l.Nam == namTruoc);

            if (lichSuThangTruoc != null)
            {
                chiSoDienCu = lichSuThangTruoc.ChiSoDienMoi;
                chiSoNuocCu = lichSuThangTruoc.ChiSoNuocMoi;
            }

            // 🔴 BẮT LỖI LOGIC: Chặn trường hợp chỉ số mới thấp hơn chỉ số cũ
            if (chiSoDienMoi < chiSoDienCu)
            {
                TempData["Error"] = $"Cập nhật thất bại! Chỉ số ĐIỆN mới ({chiSoDienMoi}) không được thấp hơn chỉ số cũ ({chiSoDienCu}).";
                return RedirectToAction(nameof(Details), new { id = maHoaDon });
            }

            if (chiSoNuocMoi < chiSoNuocCu)
            {
                TempData["Error"] = $"Cập nhật thất bại! Chỉ số NƯỚC mới ({chiSoNuocMoi}) không được thấp hơn chỉ số cũ ({chiSoNuocCu}).";
                return RedirectToAction(nameof(Details), new { id = maHoaDon });
            }

            // ✅ Tính toán (Loại bỏ hoàn toàn Math.Max vì dữ liệu đã được đảm bảo >= 0)
            decimal soDien = chiSoDienMoi - chiSoDienCu;
            decimal soNuoc = chiSoNuocMoi - chiSoNuocCu;

            decimal tienDien = soDien * giaDien;
            decimal tienNuoc = soNuoc * giaNuoc;
            decimal tienDichVu = soNguoi * 200000;
            decimal tongCong = giaPhong + tienDien + tienNuoc + tienDichVu + tienPhatSinh;

            // Lưu lịch sử
            var lichSu = await _context.LichSuChiSoDienNuoc
                .FirstOrDefaultAsync(l => l.MaPhong == maPhong && l.Thang == thangHienTai && l.Nam == namHienTai);

            if (lichSu == null)
            {
                lichSu = new LichSuChiSoDienNuoc
                {
                    MaPhong = maPhong,
                    Thang = thangHienTai,
                    Nam = namHienTai,
                    ChiSoDienCu = chiSoDienCu,
                    ChiSoDienMoi = chiSoDienMoi,
                    ChiSoNuocCu = chiSoNuocCu,
                    ChiSoNuocMoi = chiSoNuocMoi,
                    NgayGhi = DateTime.Now
                };
                _context.LichSuChiSoDienNuoc.Add(lichSu);
            }
            else
            {
                lichSu.ChiSoDienMoi = chiSoDienMoi;
                lichSu.ChiSoNuocMoi = chiSoNuocMoi;
                _context.Update(lichSu);
            }

            // Xóa chi tiết cũ và thêm mới
            var oldDetails = await _context.ChiTietHoaDon
                .Where(c => c.MaHoaDon == maHoaDon)
                .ToListAsync();
            _context.ChiTietHoaDon.RemoveRange(oldDetails);

            var chiTietList = new List<ChiTietHoaDon>
            {
                new ChiTietHoaDon { MaHoaDon = maHoaDon, LoaiKhoanThu = "Tiền phòng", SoLuong = 1, DonGia = giaPhong, ThanhTien = giaPhong },
                new ChiTietHoaDon { MaHoaDon = maHoaDon, LoaiKhoanThu = "Chỉ số điện cũ", SoLuong = chiSoDienCu },
                new ChiTietHoaDon { MaHoaDon = maHoaDon, LoaiKhoanThu = "Chỉ số điện mới", SoLuong = chiSoDienMoi },
                new ChiTietHoaDon { MaHoaDon = maHoaDon, LoaiKhoanThu = "Đơn giá điện", DonGia = giaDien },
                new ChiTietHoaDon { MaHoaDon = maHoaDon, LoaiKhoanThu = "Chỉ số nước cũ", SoLuong = chiSoNuocCu },
                new ChiTietHoaDon { MaHoaDon = maHoaDon, LoaiKhoanThu = "Chỉ số nước mới", SoLuong = chiSoNuocMoi },
                new ChiTietHoaDon { MaHoaDon = maHoaDon, LoaiKhoanThu = "Đơn giá nước", DonGia = giaNuoc },
                new ChiTietHoaDon { MaHoaDon = maHoaDon, LoaiKhoanThu = "Tiền điện", ThanhTien = tienDien },
                new ChiTietHoaDon { MaHoaDon = maHoaDon, LoaiKhoanThu = "Tiền nước", ThanhTien = tienNuoc },
                new ChiTietHoaDon { MaHoaDon = maHoaDon, LoaiKhoanThu = "Phí dịch vụ", ThanhTien = tienDichVu, GhiChu = $"{soNguoi} người" }
            };
            if (tienPhatSinh > 0)
            {
                chiTietList.Add(new ChiTietHoaDon { MaHoaDon = maHoaDon, LoaiKhoanThu = "Phí phát sinh", ThanhTien = tienPhatSinh });
            }

            _context.ChiTietHoaDon.AddRange(chiTietList);
            hoaDon.TongTien = tongCong;
            _context.Update(hoaDon);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Cập nhật thành công! Điện cũ: {chiSoDienCu}, Nước cũ: {chiSoNuocCu}";
            return RedirectToAction(nameof(Details), new { id = maHoaDon });
        }        // ==================== KHÁCH XÁC NHẬN ĐÃ CHUYỂN KHOẢN ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KhachXacNhan(int id)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0)
                return RedirectToAction("Index", "Login");

            // ✅ PHÂN QUYỀN: Chỉ Khách hàng mới xác nhận
            if (role != "Khach")
            {
                TempData["Error"] = "Chỉ khách hàng mới có thể xác nhận chuyển khoản!";
                return RedirectToAction("Index", "Home");
            }

            var hoaDon = await _context.HoaDon
                .Include(h => h.HopDongNavigation)
                    .ThenInclude(h => h.KhachHangNavigation)
                .Include(h => h.HopDongNavigation)
                    .ThenInclude(h => h.PhongNavigation)
                .FirstOrDefaultAsync(h => h.MaHoaDon == id);

            if (hoaDon == null) return NotFound();

            // ✅ Kiểm tra khách hàng có quyền với hóa đơn này không
            var khachHangCurrent = await _context.KhachHang
                .FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);
            if (khachHangCurrent == null || hoaDon.HopDongNavigation?.MaKhachHang != khachHangCurrent.MaKhachHang)
            {
                TempData["Error"] = "Bạn không có quyền xác nhận hóa đơn này!";
                return RedirectToAction("Index", "KhachHang");
            }

            if (hoaDon.TrangThai == "Đã thanh toán")
            {
                TempData["Error"] = "Hóa đơn đã được thanh toán!";
                return RedirectToAction("Details", new { id });
            }

            // ========== CODE GỐC - KHÔNG THAY ĐỔI ==========
            hoaDon.KhachXacNhan = true;
            hoaDon.NgayKhachXacNhan = DateTime.Now;
            _context.Update(hoaDon);
            await _context.SaveChangesAsync();

            // Gửi thông báo cho Admin
            var khachHang = hoaDon.HopDongNavigation?.KhachHangNavigation;
            var phong = hoaDon.HopDongNavigation?.PhongNavigation;

            if (_thongBaoService != null && khachHang != null)
            {
                await _thongBaoService.GuiAdmin(
                    "💰 Khách hàng xác nhận thanh toán",
                    $"Khách {khachHang.HoTen} - Phòng {phong?.TenPhong} đã xác nhận chuyển khoản cho hóa đơn tháng {hoaDon.Thang}/{hoaDon.Nam}. Vui lòng kiểm tra và xác nhận!",
                    "warning",
                    $"/HoaDon/Details/{hoaDon.MaHoaDon}"
                );
            }

            TempData["Success"] = "Đã gửi xác nhận! Chủ trọ sẽ kiểm tra và xác nhận thanh toán.";
            return RedirectToAction("HoaDonChiTiet", "KhachHang", new { id });
        }

        // ==================== CHỦ TRỌ XÁC NHẬN ĐÃ NHẬN TIỀN ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChuXacNhan(int id)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0)
                return RedirectToAction("Index", "Login");

            // ✅ PHÂN QUYỀN: Chỉ Chủ trọ mới xác nhận
            if (role != "ChuTro")
            {
                TempData["Error"] = "Chỉ chủ trọ mới có thể xác nhận thanh toán!";
                return RedirectToAction("Index", "Home");
            }

            var hoaDon = await _context.HoaDon
                .Include(h => h.HopDongNavigation)
                    .ThenInclude(h => h.KhachHangNavigation)
                .Include(h => h.HopDongNavigation)
                    .ThenInclude(h => h.PhongNavigation)
                .FirstOrDefaultAsync(h => h.MaHoaDon == id);

            if (hoaDon == null)
            {
                TempData["Error"] = "Không tìm thấy hóa đơn!";
                return RedirectToAction("Index");
            }

            // ✅ Chủ trọ chỉ xác nhận hóa đơn của mình
            if (hoaDon.MaChuTro != userId)
            {
                TempData["Error"] = "Bạn không có quyền xác nhận hóa đơn này!";
                return RedirectToAction("Index");
            }

            if (hoaDon.TrangThai == "Đã thanh toán")
            {
                TempData["Error"] = "Hóa đơn đã được thanh toán!";
                return RedirectToAction("Details", new { id });
            }

            // ========== CODE GỐC - KHÔNG THAY ĐỔI ==========
            hoaDon.ChuXacNhan = true;
            hoaDon.NgayChuXacNhan = DateTime.Now;
            hoaDon.TrangThai = "Đã thanh toán";

            var thanhToan = new ThanhToan
            {
                MaHoaDon = id,
                SoTien = hoaDon.TongTien,
                NgayThanhToan = DateTime.Now,
                NoiDungChuyenKhoan = $"Khách xác nhận ngày {hoaDon.NgayKhachXacNhan?.ToString("dd/MM/yyyy HH:mm")}",
                TrangThai = "Thành công"
            };
            _context.ThanhToan.Add(thanhToan);
            _context.Update(hoaDon);
            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ Đã xác nhận thanh toán thành công!";

            // Gửi email
            try
            {
                var khachHang = hoaDon.HopDongNavigation?.KhachHangNavigation;
                if (khachHang != null && !string.IsNullOrEmpty(khachHang.Email))
                {
                    var pdfBytes = await TaoPdfHoaDon(id);
                    if (pdfBytes != null && pdfBytes.Length > 0)
                    {
                        var emailService = HttpContext.RequestServices.GetRequiredService<EmailService>();
                        var result = await emailService.GuiEmailHoaDon(
                            khachHang.Email,
                            khachHang.HoTen,
                            hoaDon.MaHoaDon.ToString("00000"),
                            pdfBytes
                        );

                        if (result)
                        {
                            TempData["EmailSent"] = $"Đã gửi hóa đơn qua email {khachHang.Email}";
                        }
                        else
                        {
                            TempData["Warning"] = "Xác nhận thành công nhưng gửi email thất bại!";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["Warning"] = $"Lỗi gửi email: {ex.Message}";
            }

            return RedirectToAction("Details", new { id });
        }

        // ==================== THANH TOÁN HÓA ĐƠN ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ThanhToan(int id, decimal soTien, string noiDung)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0)
                return RedirectToAction("Index", "Login");

            // ✅ PHÂN QUYỀN: Chỉ Chủ trọ mới thanh toán
            if (role != "ChuTro")
            {
                TempData["Error"] = "Chỉ chủ trọ mới có thể thanh toán hóa đơn!";
                return RedirectToAction("Index", "Home");
            }

            var hoaDon = await _context.HoaDon
                .Include(h => h.HopDongNavigation)
                    .ThenInclude(h => h.KhachHangNavigation)
                .FirstOrDefaultAsync(h => h.MaHoaDon == id);

            if (hoaDon == null) return NotFound();

            // ✅ Chủ trọ chỉ thanh toán hóa đơn của mình
            if (hoaDon.MaChuTro != userId)
            {
                TempData["Error"] = "Bạn không có quyền thanh toán hóa đơn này!";
                return RedirectToAction("Index");
            }

            if (hoaDon.TrangThai == "Đã thanh toán")
            {
                TempData["Error"] = "Hóa đơn này đã được thanh toán!";
                return RedirectToAction(nameof(Details), new { id });
            }

            // ========== CODE GỐC - KHÔNG THAY ĐỔI ==========
            var thanhToan = new ThanhToan
            {
                MaHoaDon = id,
                SoTien = soTien,
                NgayThanhToan = DateTime.Now,
                NoiDungChuyenKhoan = noiDung,
                TrangThai = "Thành công"
            };
            _context.ThanhToan.Add(thanhToan);

            hoaDon.TrangThai = "Đã thanh toán";
            hoaDon.KhachXacNhan = true;
            hoaDon.ChuXacNhan = true;

            _context.Update(hoaDon);
            await _context.SaveChangesAsync();

            var khachHang = hoaDon.HopDongNavigation?.KhachHangNavigation;
            if (khachHang != null && _thongBaoService != null)
            {
                await _thongBaoService.GuiKhach(
                    khachHang.MaKhachHang,
                    "✅ Thanh toán thành công",
                    $"Hóa đơn tháng {hoaDon.Thang}/{hoaDon.Nam} đã được thanh toán {soTien:N0} đ",
                    "success",
                    $"/KhachHang/HoaDonChiTiet/{hoaDon.MaHoaDon}"
                );
            }

            TempData["Success"] = $"Thanh toán thành công {soTien:N0} đ!";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ==================== TẠO HÓA ĐƠN HÀNG LOẠT ====================
        public async Task<IActionResult> TaoHangLoat()
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0)
                return RedirectToAction("Index", "Login");

            // ✅ PHÂN QUYỀN: Chỉ Chủ trọ mới tạo hóa đơn
            if (role != "ChuTro")
            {
                TempData["Error"] = "Bạn không có quyền tạo hóa đơn hàng loạt!";
                return RedirectToAction("Index", "Home");
            }

            // ========== CODE GỐC - KHÔNG THAY ĐỔI ==========
            var thangHienTai = DateTime.Now.Month;
            var namHienTai = DateTime.Now.Year;

            // ✅ Chỉ lấy phòng của chủ trọ đang đăng nhập
            var phongIds = await _context.Phong
                .Where(p => p.MaChuTro == userId && p.TrangThai == "Đã thuê")
                .Select(p => p.MaPhong)
                .ToListAsync();

            var listHopDong = await _context.HopDong
                .Include(h => h.PhongNavigation)
                .Where(h => phongIds.Contains(h.MaPhong) && h.TrangThai == "Hiệu lực")
                .ToListAsync();

            int dem = 0;
            foreach (var hopDongItem in listHopDong)
            {
                var exists = await _context.HoaDon
                    .AnyAsync(h => h.MaHopDong == hopDongItem.MaHopDong && h.Thang == thangHienTai && h.Nam == namHienTai);

                if (!exists && hopDongItem.PhongNavigation != null)
                {
                    var hoaDon = new HoaDon
                    {
                        MaHopDong = hopDongItem.MaHopDong,
                        MaChuTro = userId,
                        Thang = thangHienTai,
                        Nam = namHienTai,
                        TongTien = hopDongItem.PhongNavigation.GiaPhong,
                        TrangThai = "Chưa thanh toán",
                        NgayTao = DateTime.Now
                    };
                    _context.HoaDon.Add(hoaDon);
                    dem++;
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã tạo {dem} hóa đơn cho tháng {thangHienTai}/{namHienTai}!";
            return RedirectToAction(nameof(Index));
        }

        // ==================== XÓA HÓA ĐƠN ====================
        public async Task<IActionResult> Delete(int? id)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0)
                return RedirectToAction("Index", "Login");

            if (id == null) return NotFound();

            var hoaDon = await _context.HoaDon
                .Include(h => h.HopDongNavigation)
                .FirstOrDefaultAsync(m => m.MaHoaDon == id);

            if (hoaDon == null) return NotFound();

            // ✅ PHÂN QUYỀN: Chỉ Chủ trọ mới xóa và chỉ hóa đơn của mình
            if (role != "ChuTro")
            {
                TempData["Error"] = "Chỉ chủ trọ mới có quyền xóa hóa đơn!";
                return RedirectToAction("Index");
            }

            if (hoaDon.MaChuTro != userId)
            {
                TempData["Error"] = "Bạn không có quyền xóa hóa đơn này!";
                return RedirectToAction("Index");
            }

            if (hoaDon.TrangThai == "Đã thanh toán")
            {
                TempData["Error"] = "Hóa đơn đã thanh toán không thể xóa!";
                return RedirectToAction(nameof(Index));
            }

            return View(hoaDon);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0)
                return RedirectToAction("Index", "Login");

            // ========== CODE GỐC - KHÔNG THAY ĐỔI ==========
            var hoaDon = await _context.HoaDon.FindAsync(id);
            if (hoaDon != null)
            {
                // ✅ PHÂN QUYỀN: Chỉ Chủ trọ mới xóa và chỉ hóa đơn của mình
                if (role != "ChuTro")
                {
                    TempData["Error"] = "Chỉ chủ trọ mới có quyền xóa hóa đơn!";
                    return RedirectToAction("Index");
                }

                if (hoaDon.MaChuTro != userId)
                {
                    TempData["Error"] = "Bạn không có quyền xóa hóa đơn này!";
                    return RedirectToAction("Index");
                }

                if (hoaDon.TrangThai == "Đã thanh toán")
                {
                    TempData["Error"] = "Không thể xóa hóa đơn đã thanh toán!";
                    return RedirectToAction(nameof(Index));
                }

                var chiTiets = await _context.ChiTietHoaDon.Where(c => c.MaHoaDon == id).ToListAsync();
                _context.ChiTietHoaDon.RemoveRange(chiTiets);
                _context.HoaDon.Remove(hoaDon);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa hóa đơn thành công!";
            }
            return RedirectToAction(nameof(Index));
        }
        // ==================== TẠO HÓA ĐƠN THÁNG ĐỒNG BỘ NGÀY ====================
        [HttpPost]
        public async Task<IActionResult> TaoHoaDonThang(int thang, int nam, int ngayChotCoDinh = 25)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0 || role != "ChuTro")
                return RedirectToAction("Index", "Login");

            // Lấy danh sách các hợp đồng còn hiệu lực của riêng chủ trọ này
            var hopDongs = await _context.HopDong
                .Where(h => h.TrangThai == "DangHieuLuc" && h.MaChuTro == userId)
                .ToListAsync();

            foreach (var hd in hopDongs)
            {
                // Kiểm tra xem tháng đó phòng này đã có hóa đơn chưa để tránh tạo trùng
                bool daCoHoaDon = await _context.HoaDon
                    .AnyAsync(h => h.MaHopDong == hd.MaHopDong && h.Thang == thang && h.Nam == nam);

                if (daCoHoaDon) continue;

                // Ép ngày lập phiếu đồng bộ về ngày chốt cố định của tháng/năm đó (Ví dụ: ngày 25)
                DateTime ngayLapMoi = new DateTime(nam, thang, ngayChotCoDinh);

                var hoaDonMoi = new HoaDon
                {
                    MaHopDong = hd.MaHopDong,
                    Thang = thang,
                    Nam = nam,
                    NgayTao = ngayLapMoi,         // Sử dụng duy nhất trường NgayTao theo đúng DB của bạn
                    TongTien = 0,                // Khởi tạo ban đầu tổng tiền = 0
                    TrangThai = "Chưa thanh toán"  // Trạng thái mặc định ban đầu
                };

                _context.HoaDon.Add(hoaDonMoi);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã khởi tạo loạt hóa đơn đồng bộ ngày {ngayChotCoDinh} cho tháng {thang}/{nam}!";
            return RedirectToAction(nameof(Index));
                }        // ==================== XUẤT PDF HÓA ĐƠN ====================
        public async Task<IActionResult> XuatPdf(int id)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0)
                return RedirectToAction("Index", "Login");

            // ✅ PHÂN QUYỀN XUẤT PDF
            var hasAccess = false;

            if (role == "ChuTro")
            {
                var hoaDonCheck = await _context.HoaDon
                    .FirstOrDefaultAsync(h => h.MaHoaDon == id);
                if (hoaDonCheck != null && hoaDonCheck.MaChuTro == userId)
                {
                    hasAccess = true;
                }
            }
            else if (role == "Khach")
            {
                var khachHangCurrent = await _context.KhachHang
                    .FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);
                if (khachHangCurrent != null)
                {
                    var hoaDonCheck = await _context.HoaDon
                        .Include(h => h.HopDongNavigation)
                        .FirstOrDefaultAsync(h => h.MaHoaDon == id);
                    if (hoaDonCheck != null)
                    {
                        var hopDongCheck = await _context.HopDong
                            .FirstOrDefaultAsync(h => h.MaHopDong == hoaDonCheck.MaHopDong);
                        if (hopDongCheck != null && hopDongCheck.MaKhachHang == khachHangCurrent.MaKhachHang)
                        {
                            hasAccess = true;
                        }
                    }
                }
            }
            else if (role == "Admin")
            {
                TempData["Error"] = "Admin không có quyền xuất hóa đơn!";
                return RedirectToAction("Index", "Home");
            }

            if (!hasAccess)
            {
                TempData["Error"] = "Bạn không có quyền xuất hóa đơn này!";
                return RedirectToAction("Index");
            }

            // ========== CODE GỐC - KHÔNG THAY ĐỔI ==========
            var hoaDon = await _context.HoaDon
                .Include(h => h.HopDongNavigation)
                    .ThenInclude(h => h.PhongNavigation)
                .Include(h => h.HopDongNavigation)
                    .ThenInclude(h => h.KhachHangNavigation)
                .FirstOrDefaultAsync(h => h.MaHoaDon == id);

            if (hoaDon == null) return NotFound();

            var chiTietHoaDons = await _context.ChiTietHoaDon
                .Where(c => c.MaHoaDon == id)
                .ToListAsync();

            var phong = hoaDon.HopDongNavigation?.PhongNavigation;
            var khachHang = hoaDon.HopDongNavigation?.KhachHangNavigation;

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
            var tongTien = (phong?.GiaPhong ?? 0) + tienDien + tienNuoc + tienDichVu + tienPhatSinh;
            var ngayThanhToan = DateTime.Now.AddDays(7);

            // Tạo nội dung HTML cho PDF
            string dsChiTiet = $@"
        <tr>
            <td>Tiền phòng</td>
            <td>{phong?.TenPhong ?? "N/A"} - tháng {hoaDon.Thang}/{hoaDon.Nam}</td>
            <td class='text-right'>{((decimal)(phong?.GiaPhong ?? 0)):N0} đ</td>
            <td class='text-right'>{((decimal)(phong?.GiaPhong ?? 0)):N0} đ</td>
        </tr>
        <tr>
            <td>Tiền điện</td>
            <td>Số điện: {soDien:N0} kWh × {giaDien:N0} đ</td>
            <td class='text-right'>{tienDien:N0} đ</td>
            <td class='text-right'>{tienDien:N0} đ</td>
        </tr>
        <tr>
            <td>Tiền nước</td>
            <td>Số nước: {soNuoc:N0} m³ × {giaNuoc:N0} đ</td>
            <td class='text-right'>{tienNuoc:N0} đ</td>
            <td class='text-right'>{tienNuoc:N0} đ</td>
        </tr>
        <tr>
            <td>Phí dịch vụ</td>
            <td>{soNguoi} người × 200.000đ</td>
            <td class='text-right'>{tienDichVu:N0} đ</td>
            <td class='text-right'>{tienDichVu:N0} đ</td>
        </tr>";

            if (tienPhatSinh > 0)
            {
                dsChiTiet += $@"
        <tr>
            <td>Phí phát sinh</td>
            <td>Sửa chữa/phát sinh</td>
            <td class='text-right'>{tienPhatSinh:N0} đ</td>
            <td class='text-right'>{tienPhatSinh:N0} đ</td>
        </tr>";
            }
            // Lấy thông tin ngân hàng của chủ trọ
            var chuTroInfo = await _context.TaiKhoan
                .FirstOrDefaultAsync(u => u.MaTaiKhoan == hoaDon.MaChuTro);

            var maNganHang = chuTroInfo?.MaNganHang ?? "";
            var soTaiKhoan = chuTroInfo?.SoTaiKhoan ?? "";
            var chuTaiKhoan = chuTroInfo?.ChuTaiKhoan ?? "N/A";

            string amountStr = ((long)Math.Round(tongTien)).ToString();
            string qrUrl = $"https://img.vietqr.io/image/{maNganHang}-{soTaiKhoan}-compact2.png" +
                           $"?amount={amountStr}" +
                           $"&addInfo=TT_HD{hoaDon.MaHoaDon:D5}_{hoaDon.Thang}{hoaDon.Nam}" +
                           $"&accountName={Uri.EscapeDataString(chuTaiKhoan)}"; string htmlContent = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <title>HÓA ĐƠN THANH TOÁN</title>
    <style>
        body {{ font-family: 'Times New Roman', Times, serif; font-size: 14px; margin: 0; padding: 20px; background: white; }}
        .invoice-box {{ max-width: 800px; margin: 0 auto; padding: 20px; background: white; }}
        .header {{ text-align: center; margin-bottom: 25px; border-bottom: 2px solid #2563eb; padding-bottom: 15px; }}
        .title {{ font-size: 22px; font-weight: bold; color: #1e3a8a; text-transform: uppercase; }}
        .subtitle {{ font-size: 13px; color: #666; margin-top: 5px; }}
        .info-box {{ background: #f8fafc; padding: 12px; border-radius: 8px; margin-bottom: 20px; border: 1px solid #e2e8f0; }}
        .info-row {{ display: flex; justify-content: space-between; margin-bottom: 8px; padding: 5px 0; }}
        .info-label {{ font-weight: bold; min-width: 100px; display: inline-block; }}
        table {{ width: 100%; border-collapse: collapse; margin: 15px 0; }}
        th {{ background: #1e3a8a; color: white; padding: 10px; text-align: left; }}
        td {{ padding: 10px; border-bottom: 1px solid #e2e8f0; }}
        .text-right {{ text-align: right; }}
        .total-row {{ background: #fef3c7; font-weight: bold; border-top: 2px solid #f59e0b; }}
        .bank-box {{ background: #ecfdf5; border: 1px solid #10b981; border-radius: 8px; padding: 15px; margin-top: 20px; }}
        .bank-title {{ font-weight: bold; color: #065f46; margin-bottom: 10px; font-size: 16px; }}
        .qr-section {{ text-align: center; margin-top: 20px; padding-top: 15px; border-top: 1px solid #ddd; }}
        .qr-code img {{ width: 140px; height: 140px; }}
        .footer {{ text-align: center; font-size: 11px; color: #888; margin-top: 20px; border-top: 1px solid #ddd; padding-top: 15px; }}
        .amount {{ font-size: 18px; font-weight: bold; color: #dc2626; }}
    </style>
</head>
<body>
    <div class='invoice-box'>
        <div class='header'>
            <div class='title'>HÓA ĐƠN THANH TOÁN</div>
            <div class='subtitle'>Phòng Trọ Xinh - Hệ thống quản lý phòng trọ</div>
        </div>
        <div class='info-box'>
            <div class='info-row'>
                <span><span class='info-label'>Mã hóa đơn:</span> {hoaDon.MaHoaDon:D5}</span>
                <span><span class='info-label'>Ngày lập:</span> {DateTime.Now:dd/MM/yyyy}</span>
            </div>
            <div class='info-row'>
                <span><span class='info-label'>Phòng:</span> {phong?.TenPhong ?? "N/A"}</span>
                <span><span class='info-label'>Hạn thanh toán:</span> {ngayThanhToan:dd/MM/yyyy}</span>
            </div>
            <div class='info-row'>
                <span><span class='info-label'>Khách hàng:</span> {khachHang?.HoTen ?? "N/A"}</span>
                <span><span class='info-label'>Số điện thoại:</span> {khachHang?.SoDienThoai ?? "N/A"}</span>
            </div>
            <div class='info-row'>
                <span><span class='info-label'>Email:</span> {khachHang?.Email ?? "N/A"}</span>
            </div>
        </div>
        <table>
            <thead>
                <tr><th>Tiêu đề</th><th>Mô tả</th><th class='text-right'>Giá</th><th class='text-right'>Tổng</th></tr>
            </thead>
            <tbody>
                {dsChiTiet}
                <tr class='total-row'>
                    <td colspan='3' class='text-right'><strong>Tổng cộng:</strong></td>
                    <td class='text-right amount'>{tongTien:N0} đ</td>
                </tr>
            </tbody>
        </table>
<div class='bank-box'>
    <div class='bank-title'>THÔNG TIN CHUYỂN KHOẢN</div>
    <div><strong>Ngân hàng:</strong> {maNganHang}</div>
    <div><strong>Số tài khoản:</strong> {soTaiKhoan}</div>
    <div><strong>Chủ tài khoản:</strong> {chuTaiKhoan}</div>
    <div><strong>Nội dung:</strong> TT_HD{hoaDon.MaHoaDon:D5}_{hoaDon.Thang}{hoaDon.Nam}</div>
    <div><strong>Số tiền:</strong> <span class='amount'>{tongTien:N0} đ</span></div>
</div>
<div class='qr-section'>
            <div class='qr-code'>
                <img src='{qrUrl}' style='width:140px; height:140px;' />
                <p>Quét mã VietQR để thanh toán</p>
            </div>
        </div>
        <div class='footer'>
            <p>Cảm ơn quý khách đã sử dụng dịch vụ!</p>
            <p>Mọi thắc mắc vui lòng liên hệ: 0869189018</p>
        </div>
    </div>
</body>
</html>";

            await new BrowserFetcher().DownloadAsync();
            using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
            using var page = await browser.NewPageAsync();
            await page.SetContentAsync(htmlContent);

            var pdfBytes = await page.PdfDataAsync(new PdfOptions
            {
                Format = PaperFormat.A4,
                MarginOptions = new MarginOptions { Top = "15mm", Bottom = "15mm", Left = "12mm", Right = "12mm" }
            });

            return File(pdfBytes, "application/pdf", $"HoaDon_{hoaDon.MaHoaDon:D5}.pdf");
        }

        // ==================== TẠO PDF HÓA ĐƠN (HÀM PHỤ) ====================
        private async Task<byte[]> TaoPdfHoaDon(int id)
        {
            var hoaDon = await _context.HoaDon
                .Include(h => h.HopDongNavigation)
                    .ThenInclude(h => h.PhongNavigation)
                .Include(h => h.HopDongNavigation)
                    .ThenInclude(h => h.KhachHangNavigation)
                .FirstOrDefaultAsync(h => h.MaHoaDon == id);

            if (hoaDon == null) return null;

            var chiTietHoaDons = await _context.ChiTietHoaDon
                .Where(c => c.MaHoaDon == id)
                .ToListAsync();

            var phong = hoaDon.HopDongNavigation?.PhongNavigation;
            var khachHang = hoaDon.HopDongNavigation?.KhachHangNavigation;

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
            var tongTien = (phong?.GiaPhong ?? 0) + tienDien + tienNuoc + tienDichVu + tienPhatSinh;
            // Lấy thông tin ngân hàng của chủ trọ
            var chuTro = await _context.TaiKhoan
                .FirstOrDefaultAsync(u => u.MaTaiKhoan == hoaDon.MaChuTro);

            var chuTroInfo = await _context.TaiKhoan
                .FirstOrDefaultAsync(u => u.MaTaiKhoan == hoaDon.MaChuTro);

            var maNganHang = chuTroInfo?.MaNganHang ?? "";
            var soTaiKhoan = chuTroInfo?.SoTaiKhoan ?? "";
            var chuTaiKhoan = chuTroInfo?.ChuTaiKhoan ?? "N/A";

            string amountStr = ((long)Math.Round(tongTien)).ToString();
            string qrUrl = $"https://img.vietqr.io/image/{maNganHang}-{soTaiKhoan}-compact2.png" +
                           $"?amount={amountStr}" +
                           $"&addInfo=TT_HD{hoaDon.MaHoaDon:D5}_{hoaDon.Thang}{hoaDon.Nam}" +
                           $"&accountName={Uri.EscapeDataString(chuTaiKhoan)}"; string dsChiTiet = $@"
        <tr><td>Tiền phòng</td><td>{phong?.TenPhong ?? "N/A"} - tháng {hoaDon.Thang}/{hoaDon.Nam}</td><td class='text-right'>{(phong?.GiaPhong ?? 0):N0} đ</td><td class='text-right'>{(phong?.GiaPhong ?? 0):N0} đ</td></tr>
        <tr><td>Tiền điện</td><td>Số điện: {soDien:N0} kWh × {giaDien:N0} đ</td><td class='text-right'>{tienDien:N0} đ</td><td class='text-right'>{tienDien:N0} đ</td></tr>
        <tr><td>Tiền nước</td><td>Số nước: {soNuoc:N0} m³ × {giaNuoc:N0} đ</td><td class='text-right'>{tienNuoc:N0} đ</td><td class='text-right'>{tienNuoc:N0} đ</td></tr>
        <tr><td>Phí dịch vụ</td><td>{soNguoi} người × 200.000đ</td><td class='text-right'>{tienDichVu:N0} đ</td><td class='text-right'>{tienDichVu:N0} đ</td></tr>";

            if (tienPhatSinh > 0)
            {
                dsChiTiet += $@"<tr><td>Phí phát sinh</td><td>Sửa chữa/phát sinh</td><td class='text-right'>{tienPhatSinh:N0} đ</td><td class='text-right'>{tienPhatSinh:N0} đ</td></tr>";
            }

            string htmlContent = $@"
<!DOCTYPE html>
<html>
<head><meta charset='UTF-8'><title>HÓA ĐƠN THANH TOÁN</title>
<style>
    body {{ font-family: 'Times New Roman', Times, serif; font-size: 14px; margin: 0; padding: 20px; background: white; }}
    .invoice-box {{ max-width: 800px; margin: 0 auto; padding: 20px; background: white; }}
    .header {{ text-align: center; margin-bottom: 25px; border-bottom: 2px solid #2563eb; padding-bottom: 15px; }}
    .title {{ font-size: 22px; font-weight: bold; color: #1e3a8a; text-transform: uppercase; }}
    .subtitle {{ font-size: 13px; color: #666; margin-top: 5px; }}
    .info-box {{ background: #f8fafc; padding: 12px; border-radius: 8px; margin-bottom: 20px; border: 1px solid #e2e8f0; }}
    .info-row {{ display: flex; justify-content: space-between; margin-bottom: 8px; padding: 5px 0; }}
    .info-label {{ font-weight: bold; min-width: 100px; display: inline-block; }}
    table {{ width: 100%; border-collapse: collapse; margin: 15px 0; }}
    th {{ background: #1e3a8a; color: white; padding: 10px; text-align: left; }}
    td {{ padding: 10px; border-bottom: 1px solid #e2e8f0; }}
    .text-right {{ text-align: right; }}
    .total-row {{ background: #fef3c7; font-weight: bold; border-top: 2px solid #f59e0b; }}
    .bank-box {{ background: #ecfdf5; border: 1px solid #10b981; border-radius: 8px; padding: 15px; margin-top: 20px; }}
    .amount {{ font-size: 18px; font-weight: bold; color: #dc2626; }}
    .qr-section {{ text-align: center; margin-top: 20px; padding-top: 15px; border-top: 1px solid #ddd; }}
    .qr-code img {{ width: 140px; height: 140px; }}
    .footer {{ text-align: center; font-size: 11px; color: #888; margin-top: 20px; border-top: 1px solid #ddd; padding-top: 15px; }}
</style>
</head>
<body>
    <div class='invoice-box'>
        <div class='header'>
            <div class='title'>HÓA ĐƠN THANH TOÁN</div>
            <div class='subtitle'>Phòng Trọ Xinh - Hệ thống quản lý phòng trọ</div>
        </div>
        <div class='info-box'>
            <div class='info-row'><span><span class='info-label'>Mã hóa đơn:</span> {hoaDon.MaHoaDon:D5}</span><span><span class='info-label'>Ngày lập:</span> {DateTime.Now:dd/MM/yyyy}</span></div>
            <div class='info-row'><span><span class='info-label'>Phòng:</span> {phong?.TenPhong ?? "N/A"}</span><span><span class='info-label'>Hạn thanh toán:</span> {DateTime.Now.AddDays(7):dd/MM/yyyy}</span></div>
            <div class='info-row'><span><span class='info-label'>Khách hàng:</span> {khachHang?.HoTen ?? "N/A"}</span><span><span class='info-label'>Số điện thoại:</span> {khachHang?.SoDienThoai ?? "N/A"}</span></div>
            <div class='info-row'><span><span class='info-label'>Email:</span> {khachHang?.Email ?? "N/A"}</span></div>
        </div>
        <table>
            <thead><tr><th>Tiêu đề</th><th>Mô tả</th><th class='text-right'>Giá</th><th class='text-right'>Tổng</th></tr></thead>
            <tbody>
                {dsChiTiet}
            </tbody>
            <tfoot><tr class='total-row'><td colspan='3' class='text-right'><strong>Tổng cộng:</strong></td><td class='text-right amount'>{tongTien:N0} đ</td></tr></tfoot>
        </table>
        <div class='bank-box'>
            <div><strong>Ngân hàng:</strong> Techcombank</div>
            <div><strong>Số tài khoản:</strong> 19072789933016</div>
            <div><strong>Chủ tài khoản:</strong> Vũ Thị Thanh Thúy</div>
            <div><strong>Nội dung:</strong> TT_HD{hoaDon.MaHoaDon:D5}_{hoaDon.Thang}{hoaDon.Nam}</div>
            <div><strong>Số tiền:</strong> <span class='amount'>{tongTien:N0} đ</span></div>
        </div>
        <div class='qr-section'>
            <div class='qr-code'>
                <img src='{qrUrl}' style='width:140px; height:140px;' />
                <p>Quét mã VietQR để thanh toán</p>
            </div>
        </div>
        <div class='footer'>
            <p>Cảm ơn quý khách đã sử dụng dịch vụ!</p>
            <p>Mọi thắc mắc vui lòng liên hệ: 0869189018</p>
        </div>
    </div>
</body>
</html>";

            await new BrowserFetcher().DownloadAsync();
            using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
            using var page = await browser.NewPageAsync();
            await page.SetContentAsync(htmlContent);

            return await page.PdfDataAsync(new PdfOptions
            {
                Format = PaperFormat.A4,
                MarginOptions = new MarginOptions { Top = "15mm", Bottom = "15mm", Left = "12mm", Right = "12mm" }
            });
        }
    }
}
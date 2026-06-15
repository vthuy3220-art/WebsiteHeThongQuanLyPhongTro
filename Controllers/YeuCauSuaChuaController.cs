using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using HeThongQuanLyPhongTro.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System;
using System.Collections.Generic;

namespace HeThongQuanLyPhongTro.Controllers
{
    public class YeuCauSuaChuaController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ThongBaoService _thongBaoService;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public YeuCauSuaChuaController(ApplicationDbContext context, ThongBaoService thongBaoService, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _thongBaoService = thongBaoService;
            _webHostEnvironment = webHostEnvironment;
        }

        // Helper methods
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

        // ==================== ADMIN & CHỦ TRỌ: DANH SÁCH YÊU CẦU ====================
        public async Task<IActionResult> Index(string trangThai)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0) return RedirectToAction("Index", "Login");
            if (role != "Admin" && role != "SuperAdmin" && role != "ChuTro")
                return RedirectToAction("Index", "Login");

            var query = _context.YeuCauSuaChua
                .Include(y => y.PhongNavigation)
                    .ThenInclude(p => p.ToaNha)
                .Include(y => y.KhachHangNavigation)
                .AsQueryable();

            // PHÂN QUYỀN: Chủ trọ chỉ thấy yêu cầu của phòng mình
            if (role == "ChuTro")
            {
                var maChuTro = GetCurrentMaChuTro();
                query = query.Where(y => y.PhongNavigation.ToaNha.MaChuTro == maChuTro);
            }

            if (!string.IsNullOrEmpty(trangThai) && trangThai != "Tất cả")
                query = query.Where(y => y.TrangThai == trangThai);

            ViewBag.TrangThai = trangThai;
            ViewBag.TrangThaiList = new List<string> { "Tất cả", "Chờ xử lý", "Đã tiếp nhận", "Đã hoàn thành" };
            ViewBag.Role = role;

            return View(await query.OrderByDescending(y => y.NgayTao).ToListAsync());
        }

        // ==================== ADMIN & CHỦ TRỌ: CHI TIẾT YÊU CẦU ====================
        public async Task<IActionResult> Details(int id)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0) return RedirectToAction("Index", "Login");
            if (role != "Admin" && role != "SuperAdmin" && role != "ChuTro")
                return RedirectToAction("Index", "Login");

            var yeuCau = await _context.YeuCauSuaChua
                .Include(y => y.PhongNavigation)
                .ThenInclude(p => p.ToaNha)
                .Include(y => y.KhachHangNavigation)
                .FirstOrDefaultAsync(y => y.MaYeuCau == id);

            if (yeuCau == null) return NotFound();

            // Kiểm tra quyền: Chủ trọ chỉ xem yêu cầu của phòng mình
            if (role == "ChuTro")
            {
                var maChuTro = GetCurrentMaChuTro();
                if (yeuCau.PhongNavigation?.ToaNha?.MaChuTro != maChuTro)
                {
                    TempData["Error"] = "Bạn không có quyền xem yêu cầu này!";
                    return RedirectToAction(nameof(Index));
                }
            }

            return View(yeuCau);
        }

        // ==================== ADMIN & CHỦ TRỌ: XỬ LÝ YÊU CẦU ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XuLy(int id, string trangThai, string ghiChu, decimal chiPhiPhatSinh)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0) return RedirectToAction("Index", "Login");
            if (role != "Admin" && role != "SuperAdmin" && role != "ChuTro")
                return RedirectToAction("Index", "Login");

            var yeuCau = await _context.YeuCauSuaChua
                .Include(y => y.KhachHangNavigation)
                .Include(y => y.PhongNavigation)
                    .ThenInclude(p => p.ToaNha)
                .FirstOrDefaultAsync(y => y.MaYeuCau == id);

            if (yeuCau == null) return NotFound();

            // Kiểm tra quyền: Chủ trọ chỉ xử lý yêu cầu của phòng mình
            if (role == "ChuTro")
            {
                var maChuTro = GetCurrentMaChuTro();
                if (yeuCau.PhongNavigation?.ToaNha?.MaChuTro != maChuTro)
                {
                    TempData["Error"] = "Bạn không có quyền xử lý yêu cầu này!";
                    return RedirectToAction(nameof(Index));
                }
            }

            // Cập nhật yêu cầu
            yeuCau.TrangThai = trangThai;
            yeuCau.GhiChuXuLy = ghiChu;
            yeuCau.NgayXuLy = DateTime.Now;

            if (chiPhiPhatSinh > 0)
            {
                yeuCau.ChiPhiPhatSinh = chiPhiPhatSinh;
            }

            _context.Update(yeuCau);
            await _context.SaveChangesAsync();

            // Nếu có chi phí phát sinh, cộng vào hóa đơn tháng hiện tại
            if (chiPhiPhatSinh > 0 && trangThai == "Đã hoàn thành")
            {
                await CongVaoHoaDonThangHienTai(yeuCau.MaPhong, chiPhiPhatSinh, yeuCau.TieuDe);
            }

            // Gửi thông báo cho khách hàng
            string tieuDeThongBao = trangThai == "Đã hoàn thành"
                ? "✅ Yêu cầu sửa chữa đã hoàn thành"
                : "📝 Yêu cầu sửa chữa đã được tiếp nhận";

            string noiDungThongBao = trangThai == "Đã hoàn thành"
                ? $"Yêu cầu '{yeuCau.TieuDe}' đã được xử lý xong. Ghi chú: {ghiChu}"
                : $"Yêu cầu '{yeuCau.TieuDe}' đã được tiếp nhận. Chủ trọ sẽ xử lý sớm.";

            if (chiPhiPhatSinh > 0 && trangThai == "Đã hoàn thành")
            {
                noiDungThongBao += $" Phí sửa chữa: {chiPhiPhatSinh:N0} đ sẽ được cộng vào hóa đơn tháng này.";
            }

            await _thongBaoService.GuiKhach(
                yeuCau.MaKhachHang,
                tieuDeThongBao,
                noiDungThongBao,
                trangThai == "Đã hoàn thành" ? "success" : "info",
                $"/YeuCauSuaChua/KhachDetails/{yeuCau.MaYeuCau}"
            );

            TempData["Success"] = "Đã cập nhật trạng thái yêu cầu!";
            return RedirectToAction("Details", new { id });
        }

        // ==================== KHÁCH HÀNG: TẠO YÊU CẦU MỚI ====================
        [HttpGet]
        public async Task<IActionResult> TaoYeuCau()
        {
            var role = GetCurrentRole();
            if (role != "Khach")
                return RedirectToAction("Index", "Login");

            var userId = GetCurrentUserId();
            var khachHang = await _context.KhachHang
                .FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);

            if (khachHang == null)
            {
                TempData["Error"] = "Không tìm thấy thông tin khách hàng!";
                return RedirectToAction("Index", "Dashboard");
            }

            // Lấy phòng đang thuê của khách
            var hopDong = await _context.HopDong
                .Include(h => h.PhongNavigation)
                .ThenInclude(p => p.ToaNha)
                .FirstOrDefaultAsync(h => h.MaKhachHang == khachHang.MaKhachHang && h.TrangThai == "Hiệu lực");

            if (hopDong == null)
            {
                TempData["Error"] = "Bạn chưa có hợp đồng thuê phòng!";
                return RedirectToAction("Index", "Dashboard");
            }

            ViewBag.Phong = hopDong.PhongNavigation;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TaoYeuCau(string tieuDe, string noiDung, IFormFile? fileAnh)
        {
            var role = GetCurrentRole();
            if (role != "Khach")
                return RedirectToAction("Index", "Login");

            var userId = GetCurrentUserId();
            var khachHang = await _context.KhachHang
                .FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);

            if (khachHang == null)
                return RedirectToAction("Index", "Login");

            // Lấy phòng đang thuê
            var hopDong = await _context.HopDong
                .Include(h => h.PhongNavigation)
                .FirstOrDefaultAsync(h => h.MaKhachHang == khachHang.MaKhachHang && h.TrangThai == "Hiệu lực");

            if (hopDong == null)
            {
                TempData["Error"] = "Bạn chưa có hợp đồng thuê phòng!";
                return RedirectToAction("Index", "Dashboard");
            }

            // Xử lý upload ảnh
            string imagePath = null;
            if (fileAnh != null && fileAnh.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var extension = Path.GetExtension(fileAnh.FileName).ToLower();

                if (allowedExtensions.Contains(extension))
                {
                    var uploadFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "yeucau");
                    if (!Directory.Exists(uploadFolder))
                        Directory.CreateDirectory(uploadFolder);

                    var fileName = $"yc_{DateTime.Now.Ticks}{extension}";
                    var filePath = Path.Combine(uploadFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await fileAnh.CopyToAsync(stream);
                    }
                    imagePath = $"/images/yeucau/{fileName}";
                }
            }

            // Tạo yêu cầu mới
            var yeuCau = new YeuCauSuaChua
            {
                MaPhong = hopDong.MaPhong,
                MaKhachHang = khachHang.MaKhachHang,
                TieuDe = tieuDe,
                NoiDung = noiDung,
                HinhAnh = imagePath,
                TrangThai = "Chờ xử lý",
                NgayTao = DateTime.Now
            };

            _context.YeuCauSuaChua.Add(yeuCau);
            await _context.SaveChangesAsync();

            // Gửi thông báo cho Admin và Chủ trọ
            await _thongBaoService.GuiAdmin(
                "🔧 Yêu cầu sửa chữa mới",
                $"Khách {khachHang.HoTen} - Phòng {hopDong.PhongNavigation?.TenPhong} vừa gửi yêu cầu: {tieuDe}",
                "warning",
                $"/YeuCauSuaChua/Details/{yeuCau.MaYeuCau}"
            );

            TempData["Success"] = "Đã gửi yêu cầu sửa chữa! Chủ trọ sẽ xử lý sớm.";
            return RedirectToAction("Index", "Dashboard");
        }

        // ==================== KHÁCH HÀNG: XEM YÊU CẦU CỦA MÌNH ====================
        public async Task<IActionResult> YeuCauCuaToi()
        {
            var role = GetCurrentRole();
            if (role != "Khach")
                return RedirectToAction("Index", "Login");

            var userId = GetCurrentUserId();
            var khachHang = await _context.KhachHang
                .FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);

            if (khachHang == null)
            {
                return RedirectToAction("Index", "Dashboard");
            }

            var yeuCaus = await _context.YeuCauSuaChua
                .Include(y => y.PhongNavigation)
                .Where(y => y.MaKhachHang == khachHang.MaKhachHang)
                .OrderByDescending(y => y.NgayTao)
                .ToListAsync();

            return View(yeuCaus);
        }

        // ==================== KHÁCH HÀNG: XEM CHI TIẾT YÊU CẦU ====================
        public async Task<IActionResult> KhachDetails(int id)
        {
            var role = GetCurrentRole();
            if (role != "Khach")
                return RedirectToAction("Index", "Login");

            var userId = GetCurrentUserId();
            var khachHang = await _context.KhachHang
                .FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);

            if (khachHang == null)
            {
                TempData["Error"] = "Không tìm thấy thông tin khách hàng!";
                return RedirectToAction("Index", "Dashboard");
            }

            var yeuCau = await _context.YeuCauSuaChua
                .Include(y => y.PhongNavigation)
                .Include(y => y.KhachHangNavigation)
                .FirstOrDefaultAsync(y => y.MaYeuCau == id && y.MaKhachHang == khachHang.MaKhachHang);

            if (yeuCau == null) return NotFound();

            return View(yeuCau);
        }

        // ==================== Hàm cộng chi phí phát sinh vào hóa đơn ====================
        private async Task CongVaoHoaDonThangHienTai(int maPhong, decimal chiPhi, string noiDung)
        {
            var thangHienTai = DateTime.Now.Month;
            var namHienTai = DateTime.Now.Year;

            // Tìm hợp đồng đang hiệu lực của phòng
            var hopDong = await _context.HopDong
                .FirstOrDefaultAsync(h => h.MaPhong == maPhong && h.TrangThai == "Hiệu lực");

            if (hopDong == null) return;

            // Tìm hóa đơn tháng hiện tại
            var hoaDon = await _context.HoaDon
                .FirstOrDefaultAsync(h => h.MaHopDong == hopDong.MaHopDong && h.Thang == thangHienTai && h.Nam == namHienTai);

            if (hoaDon == null)
            {
                // Tạo hóa đơn mới nếu chưa có
                var phong = await _context.Phong.FindAsync(maPhong);
                hoaDon = new HoaDon
                {
                    MaHopDong = hopDong.MaHopDong,
                    Thang = thangHienTai,
                    Nam = namHienTai,
                    TongTien = phong?.GiaPhong ?? 0,
                    TrangThai = "Chưa thanh toán",
                    NgayTao = DateTime.Now,
                    MaChuTro = hopDong.MaChuTro
                };
                _context.HoaDon.Add(hoaDon);
                await _context.SaveChangesAsync();
            }

            // Thêm chi phí phát sinh vào chi tiết hóa đơn
            var chiTiet = new ChiTietHoaDon
            {
                MaHoaDon = hoaDon.MaHoaDon,
                LoaiKhoanThu = $"Phí sửa chữa: {noiDung}",
                SoLuong = 1,
                DonGia = chiPhi,
                ThanhTien = chiPhi,
                GhiChu = "Phát sinh từ yêu cầu sửa chữa"
            };
            _context.ChiTietHoaDon.Add(chiTiet);

            // Cập nhật tổng tiền hóa đơn
            hoaDon.TongTien = (hoaDon.TongTien ?? 0) + chiPhi;
            _context.Update(hoaDon);
            await _context.SaveChangesAsync();
        }
    }
}
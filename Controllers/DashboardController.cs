using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace HeThongQuanLyPhongTro.Controllers
{
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public DashboardController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<IActionResult> Index()
        {
            var role = HttpContext.Session.GetString("Role");
            var username = HttpContext.Session.GetString("Username");
            var userId = HttpContext.Session.GetInt32("UserId");

            if (string.IsNullOrEmpty(role) || userId == null)
                return RedirectToAction("Index", "Login");

            ViewBag.Username = username;
            ViewBag.Role = role;
            bool isSuperAdmin = (role == "SuperAdmin" || role == "Admin");

            if (role == "SuperAdmin" || role == "Admin" || role == "ChuTro")
            {
                var queryPhong = _context.Phong.AsQueryable();
                var queryHopDong = _context.HopDong.AsQueryable();
                var queryHoaDon = _context.HoaDon.AsQueryable();
                var queryBaiDang = _context.BaiDang.AsQueryable();

                // Lọc phạm vi dữ liệu dựa theo Chủ trọ (Lấy từ tư duy tối ưu File 1)
                if (!isSuperAdmin && role == "ChuTro")
                {
                    var toaNhaIds = _context.ToaNha
                        .Where(t => t.MaChuTro == userId)
                        .Select(t => t.MaToaNha);

                    queryPhong = queryPhong.Where(p => toaNhaIds.Contains(p.MaToaNha));
                    queryHopDong = queryHopDong.Where(h => queryPhong.Select(p => p.MaPhong).Contains(h.MaPhong));
                    queryHoaDon = queryHoaDon.Where(hd => queryHopDong.Select(h => h.MaHopDong).Contains(hd.MaHopDong));
                    queryBaiDang = queryBaiDang.Where(b => queryPhong.Select(p => p.MaPhong).Contains(b.MaPhong));
                }

                // ========== TÍNH TOÁN CÁC CHỈ SỐ TRỰC TIẾP TỪ DATABASE (Tối ưu RAM File 1) ==========
                int tongSoPhong = await queryPhong.CountAsync();
                int soPhongDaThue = await queryPhong.CountAsync(p => p.TrangThai == "Đã thuê");
                int soPhongTrong = tongSoPhong - soPhongDaThue;

                int tongSoKhachHang = 0;
                if (isSuperAdmin)
                {
                    tongSoKhachHang = await _context.KhachHang.CountAsync();
                }
                else
                {
                    tongSoKhachHang = await queryHopDong
                        .Where(h => h.TrangThai == "Hiệu lực" && h.MaKhachHang != null)
                        .Select(h => h.MaKhachHang)
                        .Distinct()
                        .CountAsync();
                }

                int soHopDongHieuLuc = await queryHopDong.CountAsync(h => h.TrangThai == "Hiệu lực");
                int soHopDongHetHan = await queryHopDong.CountAsync(h => h.TrangThai != "Hiệu lực");

                decimal doanhThuThangNay = await queryHoaDon
                    .Where(h => h.TrangThai != null && h.TrangThai.Trim() == "Đã thanh toán" && h.Nam == DateTime.Now.Year && h.Thang == DateTime.Now.Month)
                    .SumAsync(h => h.TongTien ?? 0);

                var queryHoaDonChuaThanhToan = queryHoaDon.Where(h => h.TrangThai != null && h.TrangThai.Trim() == "Chưa thanh toán");
                decimal tongNoHienTai = await queryHoaDonChuaThanhToan.SumAsync(h => h.TongTien ?? 0);
                int soHoaDonChuaThanhToan = await queryHoaDonChuaThanhToan.CountAsync();

                double tyLeLapDay = tongSoPhong > 0 ? Math.Round(((double)soPhongDaThue / tongSoPhong) * 100, 1) : 0;
                double tyLeTrong = tongSoPhong > 0 ? Math.Round(((double)soPhongTrong / tongSoPhong) * 100, 1) : 0;

                ViewBag.TyLeLapDay = tyLeLapDay;
                ViewBag.TyLeTrong = tyLeTrong;

                // BÙ ĐẮP TỪ FILE 2: Tính toán dữ liệu doanh thu theo 4 tuần phục vụ vẽ biểu đồ
                var hoaDonThangNayChoBieuDo = await queryHoaDon
                    .Where(h => h.Thang == DateTime.Now.Month && h.Nam == DateTime.Now.Year && h.TrangThai != null && h.TrangThai.Trim() == "Đã thanh toán" && h.NgayChuXacNhan.HasValue)
                    .ToListAsync();

                decimal tuan1 = hoaDonThangNayChoBieuDo.Where(h => h.NgayChuXacNhan.Value.Day >= 1 && h.NgayChuXacNhan.Value.Day <= 7).Sum(h => h.TongTien ?? 0);
                decimal tuan2 = hoaDonThangNayChoBieuDo.Where(h => h.NgayChuXacNhan.Value.Day >= 8 && h.NgayChuXacNhan.Value.Day <= 14).Sum(h => h.TongTien ?? 0);
                decimal tuan3 = hoaDonThangNayChoBieuDo.Where(h => h.NgayChuXacNhan.Value.Day >= 15 && h.NgayChuXacNhan.Value.Day <= 21).Sum(h => h.TongTien ?? 0);
                decimal tuan4 = hoaDonThangNayChoBieuDo.Where(h => h.NgayChuXacNhan.Value.Day >= 22).Sum(h => h.TongTien ?? 0);
                ViewBag.DoanhThuTuanData = new List<decimal> { tuan1, tuan2, tuan3, tuan4 };

                // Lấy danh sách giới hạn phục vụ UI
                var hopDongSapHetHanList = await queryHopDong
                    .Where(h => h.TrangThai == "Hiệu lực" && h.NgayKetThuc.HasValue
                        && h.NgayKetThuc.Value <= DateTime.Now.AddDays(30)
                        && h.NgayKetThuc.Value >= DateTime.Now)
                    .Select(h => new HopDongSapHetHan
                    {
                        MaHopDong = h.MaHopDong,
                        TenPhong = h.PhongNavigation != null ? h.PhongNavigation.TenPhong : "N/A",
                        TenKhachHang = h.KhachHangNavigation != null ? h.KhachHangNavigation.HoTen : "N/A",
                        NgayKetThuc = h.NgayKetThuc.Value,
                        SoNgayConLai = (int)EF.Functions.DateDiffDay(DateTime.Now, h.NgayKetThuc.Value)
                    })
                    .OrderBy(h => h.SoNgayConLai)
                    .ToListAsync();

                var hoaDonGanDayList = await queryHoaDon
                    .Where(h => h.TrangThai != null && h.TrangThai.Trim() == "Đã thanh toán")
                    .OrderByDescending(h => h.NgayChuXacNhan)
                    .Take(5)
                    .Select(h => new HoaDonGanDay
                    {
                        MaHoaDon = h.MaHoaDon,
                        TenPhong = h.HopDongNavigation != null && h.HopDongNavigation.PhongNavigation != null ? h.HopDongNavigation.PhongNavigation.TenPhong : "N/A",
                        TongTien = h.TongTien ?? 0,
                        TrangThai = h.TrangThai
                    })
                    .ToListAsync();

                int tongSoBaiDang = await queryBaiDang.CountAsync();
                int soBaiDangHienThi = await queryBaiDang.CountAsync(b => b.TrangThai == "Hiển thị" || b.TrangThai == "Hoạt động");
                int soBaiDangAn = tongSoBaiDang - soBaiDangHienThi;
                int soBaiDangThangNay = await queryBaiDang.CountAsync(b => b.NgayDang.HasValue && b.NgayDang.Value.Month == DateTime.Now.Month && b.NgayDang.Value.Year == DateTime.Now.Year);

                var baiDangGanDayList = await queryBaiDang
                    .OrderByDescending(b => b.NgayDang)
                    .Take(5)
                    .Select(b => new BaiDangGanDay
                    {
                        MaBaiDang = b.MaBaiDang,
                        TieuDe = b.TieuDe ?? "Không có tiêu đề",
                        TenPhong = b.PhongNavigation != null ? b.PhongNavigation.TenPhong : "N/A",
                        NgayDang = b.NgayDang ?? DateTime.Now,
                        TrangThai = b.TrangThai ?? "Ẩn"
                    })
                    .ToListAsync();

                // Doanh thu 6 tháng
                var resultDoanhThuThang = new List<DoanhThuTheoThang>();
                for (int i = 5; i >= 0; i--)
                {
                    var mThang = DateTime.Now.AddMonths(-i);
                    var tienHoaDon = await queryHoaDon
                        .Where(h => h.Thang == mThang.Month && h.Nam == mThang.Year && h.TrangThai != null && h.TrangThai.Trim() == "Đã thanh toán")
                        .SumAsync(h => h.TongTien ?? 0);

                    resultDoanhThuThang.Add(new DoanhThuTheoThang
                    {
                        Thang = mThang.Month,
                        Nam = mThang.Year,
                        DoanhThu = tienHoaDon
                    });
                }

                // Top 5 phòng doanh thu cao nhất tháng
                var thangHienTai = DateTime.Now.Month;
                var namHienTai = DateTime.Now.Year;

                var topPhongList = await queryPhong
                    .Select(p => new TopPhongSuDung
                    {
                        MaPhong = p.MaPhong,
                        TenPhong = p.TenPhong,
                        TongDoanhThu = _context.HoaDon.Where(h => h.HopDongNavigation.MaPhong == p.MaPhong
                                            && h.Thang == thangHienTai && h.Nam == namHienTai
                                            && h.TrangThai != null && h.TrangThai.Trim() == "Đã thanh toán")
                                            .Sum(h => h.TongTien ?? 0),
                        SoHoaDon = _context.HoaDon.Count(h => h.HopDongNavigation.MaPhong == p.MaPhong
                                            && h.Thang == thangHienTai && h.Nam == namHienTai
                                            && h.TrangThai != null && h.TrangThai.Trim() == "Đã thanh toán")
                    })
                    .OrderByDescending(x => x.TongDoanhThu)
                    .Take(5)
                    .ToListAsync();

                var model = new DashboardViewModel
                {
                    TongSoPhong = tongSoPhong,
                    SoPhongDaThue = soPhongDaThue,
                    SoPhongTrong = soPhongTrong,
                    TongSoKhachHang = tongSoKhachHang,
                    DoanhThuThangNay = doanhThuThangNay,
                    TongNoHienTai = tongNoHienTai,
                    SoHoaDonChuaThanhToan = soHoaDonChuaThanhToan,
                    SoHopDongHieuLuc = soHopDongHieuLuc,
                    SoHopDongHetHan = soHopDongHetHan,
                    SoHopDongSapHetHan = hopDongSapHetHanList.Count,
                    HopDongSapHetHanList = hopDongSapHetHanList,
                    HoaDonGanDayList = hoaDonGanDayList,
                    TongSoBaiDang = tongSoBaiDang,
                    SoBaiDangHienThi = soBaiDangHienThi,
                    SoBaiDangAn = soBaiDangAn,
                    SoBaiDangThangNay = soBaiDangThangNay,
                    BaiDangGanDayList = baiDangGanDayList,
                    DoanhThuTheoThangList = resultDoanhThuThang,
                    TopPhongList = topPhongList
                };

                if (isSuperAdmin) return RedirectToAction("Index", "Admin");
                return View("ChuTroDashboard", model);
            }
            else if (role == "Khach")
            {
                var khachHang = await _context.KhachHang.FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);
                if (khachHang == null)
                    khachHang = await _context.KhachHang.FirstOrDefaultAsync(k => k.Email == username || k.SoDienThoai == username);

                if (khachHang == null) return RedirectToAction("Index", "Login");

                var hopDong = await _context.HopDong
                    .Include(h => h.PhongNavigation)
                    .FirstOrDefaultAsync(h => h.MaKhachHang == khachHang.MaKhachHang && h.TrangThai == "Hiệu lực");

                var hoaDonChuaThanhToan = new List<HoaDon>();
                var hoaDonDaThanhToan = new List<HoaDon>();

                if (hopDong != null)
                {
                    hoaDonChuaThanhToan = await _context.HoaDon
                        .Where(h => h.MaHopDong == hopDong.MaHopDong && h.TrangThai == "Chưa thanh toán")
                        .OrderBy(h => h.Nam).ThenBy(h => h.Thang).ToListAsync();

                    hoaDonDaThanhToan = await _context.HoaDon
                        .Where(h => h.MaHopDong == hopDong.MaHopDong && h.TrangThai == "Đã thanh toán")
                        .OrderByDescending(h => h.Nam).ThenByDescending(h => h.Thang)
                        .Take(5)
                        .ToListAsync();
                }

                ViewBag.KhachHang = khachHang;
                ViewBag.HopDong = hopDong;
                ViewBag.HoaDonChuaThanhToan = hoaDonChuaThanhToan;
                ViewBag.HoaDonDaThanhToan = hoaDonDaThanhToan;

                return View("KhachDashboard");
            }

            return RedirectToAction("Index", "Login");
        }

        // ==================== VÁ LỖI BẢO MẬT IDOR TẠI CÁC API CHI TIẾT (Lấy từ File 1) ====================
        [HttpGet]
        public async Task<IActionResult> GetInvoiceDetails(int id)
        {
            var role = HttpContext.Session.GetString("Role");
            var userId = HttpContext.Session.GetInt32("UserId");
            if (role != "Admin" && role != "SuperAdmin" && role != "ChuTro") return Forbid();

            var query = _context.HoaDon.AsQueryable();
            if (role == "ChuTro")
            {
                query = query.Where(h => h.HopDongNavigation.MaChuTro == userId);
            }

            var hoaDon = await query
                .Include(h => h.HopDongNavigation).ThenInclude(hd => hd.KhachHangNavigation)
                .Include(h => h.HopDongNavigation).ThenInclude(hd => hd.PhongNavigation)
                .FirstOrDefaultAsync(h => h.MaHoaDon == id);

            if (hoaDon == null) return NotFound("Hóa đơn không tồn tại hoặc bạn không có quyền xem.");
            var chiTiet = await _context.ChiTietHoaDon.Where(ct => ct.MaHoaDon == id).ToListAsync();

            return Json(new
            {
                maHoaDon = hoaDon.MaHoaDon,
                tenPhong = hoaDon.HopDongNavigation?.PhongNavigation?.TenPhong ?? "N/A",
                tenKhachHang = hoaDon.HopDongNavigation?.KhachHangNavigation?.HoTen ?? "N/A",
                thang = hoaDon.Thang,
                nam = hoaDon.Nam,
                tongTien = hoaDon.TongTien ?? 0,
                ngayThanhToan = hoaDon.NgayChuXacNhan?.ToString("dd/MM/yyyy HH:mm") ?? "N/A",
                chiTietList = chiTiet.Select(ct => new { ct.LoaiKhoanThu, ct.SoLuong, donGia = ct.DonGia ?? 0, thanhTien = ct.ThanhTien ?? 0 })
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetContractDetails(int id)
        {
            var role = HttpContext.Session.GetString("Role");
            var userId = HttpContext.Session.GetInt32("UserId");
            if (role != "Admin" && role != "SuperAdmin" && role != "ChuTro") return Forbid();

            var query = _context.HopDong.AsQueryable();
            if (role == "ChuTro")
            {
                query = query.Where(h => h.MaChuTro == userId);
            }

            var hopDong = await query
                .Include(h => h.PhongNavigation)
                .Include(h => h.KhachHangNavigation)
                .FirstOrDefaultAsync(h => h.MaHopDong == id);

            if (hopDong == null) return NotFound("Hợp đồng không tồn tại hoặc bạn không có quyền xem.");

            return Json(new
            {
                maHopDong = hopDong.MaHopDong,
                tenPhong = hopDong.PhongNavigation?.TenPhong ?? "N/A",
                tenKhachHang = hopDong.KhachHangNavigation?.HoTen ?? "N/A",
                sdtKhachHang = hopDong.KhachHangNavigation?.SoDienThoai ?? "N/A",
                ngayBatDau = hopDong.NgayBatDau?.ToString("dd/MM/yyyy") ?? "N/A",
                ngayKetThuc = hopDong.NgayKetThuc?.ToString("dd/MM/yyyy") ?? "N/A",
                tienCoc = hopDong.TienCoc ?? 0,
                giaThue = hopDong.PhongNavigation?.GiaPhong ?? 0,
                trangThai = hopDong.TrangThai ?? "N/A",
                soNgayConLai = hopDong.NgayKetThuc.HasValue ? (int)(hopDong.NgayKetThuc.Value.Date - DateTime.Now.Date).TotalDays : 0
            });
        }

        [HttpGet]
        public async Task<IActionResult> ThongKe(int? maToaNha)
        {
            var role = HttpContext.Session.GetString("Role");
            var userId = HttpContext.Session.GetInt32("UserId");
            if (role != "ChuTro" || userId == null) return RedirectToAction("Index", "Login");

            var danhSachToaNha = await _context.ToaNha.Where(t => t.MaChuTro == userId.Value).ToListAsync();
            ViewBag.DanhSachToaNha = danhSachToaNha;
            ViewBag.SelectedToaNha = maToaNha;

            var targetToaNhaIds = maToaNha.HasValue ? new List<int> { maToaNha.Value } : danhSachToaNha.Select(t => t.MaToaNha).ToList();
            var phongs = await _context.Phong.Where(p => targetToaNhaIds.Contains(p.MaToaNha)).ToListAsync();
            var phongIds = phongs.Select(p => p.MaPhong).ToList();

            var hoaDonsDaThanhToan = await _context.HoaDon
                .Where(hd => hd.HopDongNavigation != null && phongIds.Contains(hd.HopDongNavigation.MaPhong) && hd.TrangThai != null && hd.TrangThai.Trim() == "Đã thanh toán")
                .Select(hd => new { hd.TongTien, MaPhong = hd.HopDongNavigation.MaPhong, MaToaNha = hd.HopDongNavigation.PhongNavigation != null ? hd.HopDongNavigation.PhongNavigation.MaToaNha : 0 })
                .ToListAsync();

            decimal tongDoanhThuTatCa = hoaDonsDaThanhToan.Sum(h => h.TongTien ?? 0);

            var thongKeToaNha = danhSachToaNha.Select(t => {
                decimal doanhThuToa = hoaDonsDaThanhToan.Where(hd => hd.MaToaNha == t.MaToaNha).Sum(hd => hd.TongTien ?? 0);
                return new { TenToaNha = t.TenToaNha, DoanhThu = doanhThuToa, PhanTram = tongDoanhThuTatCa > 0 ? Math.Round((double)doanhThuToa / (double)tongDoanhThuTatCa * 100, 1) : 0 };
            }).OrderByDescending(x => x.DoanhThu).ToList();
            ViewBag.ThongKeToaNha = thongKeToaNha;

            var thongKePhong = phongs.Select(p => {
                decimal doanhThuPhong = hoaDonsDaThanhToan.Where(hd => hd.MaPhong == p.MaPhong).Sum(hd => hd.TongTien ?? 0);
                return new { TenPhong = p.TenPhong, DoanhThu = doanhThuPhong, PhanTram = tongDoanhThuTatCa > 0 ? Math.Round((double)doanhThuPhong / (double)tongDoanhThuTatCa * 100, 1) : 0 };
            }).Where(x => x.DoanhThu > 0).OrderByDescending(x => x.DoanhThu).Take(10).ToList();
            ViewBag.ThongKePhong = thongKePhong;

            ViewBag.TongDoanhThuMụcTieu = tongDoanhThuTatCa;
            ViewBag.TongSoPhongMụcTieu = phongs.Count;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuiThongBaoNhacNo()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin" && role != "SuperAdmin" && role != "ChuTro") return RedirectToAction("Index", "Login");

            var userId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var isSuperAdmin = (role == "Admin" || role == "SuperAdmin");
            var ngayHienTai = DateTime.Now;

            var queryHoaDon = _context.HoaDon
                .Include(h => h.HopDongNavigation).ThenInclude(hd => hd.KhachHangNavigation)
                .Include(h => h.HopDongNavigation).ThenInclude(hd => hd.PhongNavigation)
                .Where(h => h.TrangThai != null && h.TrangThai.Trim() == "Chưa thanh toán" && (h.Nam < ngayHienTai.Year || (h.Nam == ngayHienTai.Year && h.Thang <= ngayHienTai.Month)));

            if (!isSuperAdmin && role == "ChuTro")
            {
                var toaNhaIds = await _context.ToaNha.Where(t => t.MaChuTro == userId).Select(t => t.MaToaNha).ToListAsync();
                var phongIds = await _context.Phong.Where(p => toaNhaIds.Contains(p.MaToaNha)).Select(p => p.MaPhong).ToListAsync();
                var hopDongIds = await _context.HopDong.Where(p => phongIds.Contains(p.MaPhong)).Select(h => h.MaHopDong).ToListAsync();
                queryHoaDon = queryHoaDon.Where(h => hopDongIds.Contains(h.MaHopDong));
            }

            var hoaDonChuaThanhToan = await queryHoaDon.ToListAsync();
            if (!hoaDonChuaThanhToan.Any()) return RedirectToAction("Index");

            var smtpServer = _configuration["EmailSettings:SmtpServer"];
            var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
            var senderEmail = _configuration["EmailSettings:SenderEmail"];
            var senderPassword = _configuration["EmailSettings:SenderPassword"];

            foreach (var hoaDon in hoaDonChuaThanhToan)
            {
                var khachHang = hoaDon.HopDongNavigation?.KhachHangNavigation;
                var tenPhong = hoaDon.HopDongNavigation?.PhongNavigation?.TenPhong ?? "N/A";
                if (khachHang == null) continue;

                _context.ThongBao.Add(new ThongBao
                {
                    TieuDe = "Nhắc nhở thanh toán hóa đơn",
                    NoiDung = $"Bạn có hóa đơn tháng {hoaDon.Thang}/{hoaDon.Nam} phòng {tenPhong} trị giá {hoaDon.TongTien?.ToString("N0")} đ chưa thanh toán.",
                    Loai = "warning",
                    DuongDan = $"/HoaDon/Details/{hoaDon.MaHoaDon}",
                    NgayTao = DateTime.Now,
                    NguoiNhan = khachHang.MaKhachHang
                });

                if (!string.IsNullOrWhiteSpace(khachHang.Email) && !string.IsNullOrEmpty(smtpServer))
                {
                    try
                    {
                        using var smtpClient = new SmtpClient(smtpServer) { Port = smtpPort, Credentials = new NetworkCredential(senderEmail, senderPassword), EnableSsl = true };
                        var mailMessage = new MailMessage { From = new MailAddress(senderEmail, "Phòng Trọ Xinh"), Subject = $"[Nhắc nhở] Hóa đơn tháng {hoaDon.Thang}/{hoaDon.Nam} chưa thanh toán", IsBodyHtml = true };
                        mailMessage.Body = $"Xin chào {khachHang.HoTen}, hóa đơn phòng {tenPhong} tháng {hoaDon.Thang}/{hoaDon.Nam} trị giá {hoaDon.TongTien?.ToString("N0")} đ đang quá hạn.";
                        mailMessage.To.Add(khachHang.Email);
                        await smtpClient.SendMailAsync(mailMessage);
                    }
                    catch { }
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> QuanLyNguoiO()
        {
            var role = HttpContext.Session.GetString("Role");
            var userId = HttpContext.Session.GetInt32("UserId");
            if (role != "ChuTro" || userId == null) return RedirectToAction("Index", "Login");

            var danhSachNguoiO = await (from no in _context.NguoiOHopDong
                                        join hd in _context.HopDong on no.MaHopDong equals hd.MaHopDong
                                        join p in _context.Phong on hd.MaPhong equals p.MaPhong
                                        join kh in _context.KhachHang on hd.MaKhachHang equals kh.MaKhachHang into khGroup
                                        from kh in khGroup.DefaultIfEmpty()
                                        where hd.MaChuTro == userId.Value
                                        orderby no.MaNguoiO descending
                                        select new NguoiOHopDong
                                        {
                                            MaNguoiO = no.MaNguoiO,
                                            MaHopDong = no.MaHopDong,
                                            HoTen = no.HoTen,
                                            CCCD = no.CCCD,
                                            SoDienThoai = no.SoDienThoai,
                                            HopDongNavigation = new HopDong
                                            {
                                                MaHopDong = hd.MaHopDong,
                                                PhongNavigation = new Phong { TenPhong = p.TenPhong },
                                                KhachHangNavigation = kh != null ? new KhachHang { HoTen = kh.HoTen } : null
                                            }
                                        }).ToListAsync();

            return View("~/Views/NguoiOhopDongs/Index.cshtml", danhSachNguoiO);
        }

        // ==================== 🎯 ĐÃ SỬA: HÀM GỬI KHẢO SÁT CHUẨN JSON + GỬI MAIL (Từ File 2) ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuiKhaoSatGiaHan(int id)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "ChuTro")
                return Json(new { success = false, message = "Bạn không có quyền thực hiện hành động này!" });

            var hopDong = await _context.HopDong
                .Include(h => h.KhachHangNavigation)
                .Include(h => h.PhongNavigation)
                .FirstOrDefaultAsync(h => h.MaHopDong == id);

            if (hopDong == null || hopDong.KhachHangNavigation == null)
                return Json(new { success = false, message = "Không tìm thấy dữ liệu hợp đồng!" });

            var khachHang = hopDong.KhachHangNavigation;
            var tenPhong = hopDong.PhongNavigation?.TenPhong ?? "N/A";

            _context.ThongBao.Add(new ThongBao
            {
                TieuDe = "Khảo sát nhu cầu gia hạn hợp đồng",
                NoiDung = $"Hợp đồng phòng {tenPhong} của bạn sắp hết hạn vào ngày {hopDong.NgayKetThuc?.ToString("dd/MM/yyyy")}. Vui lòng phản hồi lại với chủ trọ.",
                Loai = "info",
                DuongDan = $"/HopDong/Details/{hopDong.MaHopDong}",
                NgayTao = DateTime.Now,
                NguoiNhan = khachHang.MaKhachHang
            });
            await _context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(khachHang.Email))
            {
                try
                {
                    var smtpServer = _configuration["EmailSettings:SmtpServer"];
                    var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
                    var senderEmail = _configuration["EmailSettings:SenderEmail"];
                    var senderPassword = _configuration["EmailSettings:SenderPassword"];

                    using var smtpClient = new SmtpClient(smtpServer) { Port = smtpPort, Credentials = new NetworkCredential(senderEmail, senderPassword), EnableSsl = true };
                    var mailMessage = new MailMessage { From = new MailAddress(senderEmail, "Hệ Thống Quản Lý"), Subject = $"[Khảo Sát] Nhu cầu gia hạn thuê phòng {tenPhong}", IsBodyHtml = true };
                    mailMessage.Body = $@"
                        <div style='max-width: 500px; margin: 0 auto; font-family: sans-serif; border: 1px solid #e2e8f0; border-radius: 12px; overflow: hidden;'>
                            <div style='background-color: #f97316; padding: 24px; text-align: center;'>
                                <h2 style='color: white; margin: 0; font-size: 22px;'>🏠 Hệ thống Quản Lý Phòng Trọ</h2>
                                <p style='color: #ffedd5; margin: 4px 0 0 0; font-size: 14px;'>Khảo sát gia hạn hợp đồng</p>
                            </div>
                            <div style='padding: 24px; color: #334155; line-height: 1.6;'>
                                <p>Xin chào <b>{khachHang.HoTen}</b>,</p>
                                <p>Hợp đồng thuê phòng <b>{tenPhong}</b> của bạn sắp hết hạn hiệu lực vào ngày <b>{hopDong.NgayKetThuc?.ToString("dd/MM/yyyy")}</b>.</p>
                                <p>Vui lòng đăng nhập ứng dụng để phản hồi lại nhu cầu cho chủ nhà nhé.</p>
                                <hr style='border: none; border-top: 1px solid #f1f5f9; margin: 20px 0;'>
                                <p style='font-size: 12px; color: #94a3b8; text-align: center; margin: 0;'>Trân trọng,<br>Ban quản lý hệ thống</p>
                            </div>
                        </div>";

                    mailMessage.To.Add(khachHang.Email);
                    await smtpClient.SendMailAsync(mailMessage);
                }
                catch (Exception ex)
                {
                    return Json(new { success = true, message = "Đã lưu khảo sát lên hệ thống, nhưng gửi mail lỗi: " + ex.Message });
                }
            }

            return Json(new { success = true, message = "Đã gửi khảo sát đến khách hàng thành công!" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KhachPhanHoiGiaHan(int maHopDong, string luaChon)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Khach") return Forbid();

            var hopDong = await _context.HopDong.Include(h => h.PhongNavigation).Include(h => h.KhachHangNavigation).FirstOrDefaultAsync(h => h.MaHopDong == maHopDong);
            if (hopDong == null) return RedirectToAction("Index");

            string tenPhong = hopDong.PhongNavigation?.TenPhong ?? "N/A";
            string textPhanHoi = luaChon == "GiaHan" ? "SẼ GIA HẠN TIẾP" : "SẼ TRẢ PHÒNG";

            _context.ThongBao.Add(new ThongBao
            {
                TieuDe = $"Phản hồi khảo sát phòng {tenPhong}",
                NoiDung = $"Khách hàng {hopDong.KhachHangNavigation?.HoTen ?? "Khách thuê"} ở phòng {tenPhong} đã phản hồi khảo sát: {textPhanHoi}.",
                Loai = luaChon == "GiaHan" ? "success" : "danger",
                DuongDan = $"/Dashboard/Index?tab=Dashboard",
                NgayTao = DateTime.Now,
                NguoiNhan = hopDong.MaChuTro
            });

            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }
    }
}
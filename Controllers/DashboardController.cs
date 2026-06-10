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

            if (string.IsNullOrEmpty(role))
                return RedirectToAction("Index", "Login");

            ViewBag.Username = username;
            ViewBag.Role = role;

            if (role == "Admin")
            {
                var tongSoPhong = await _context.Phong.CountAsync();
                var soPhongDaThue = await _context.Phong.CountAsync(p => p.TrangThai == "Đã thuê");
                var soPhongTrong = tongSoPhong - soPhongDaThue;
                var tongSoKhachThue = await _context.KhachHang.CountAsync();

                var soHopDongHieuLuc = await _context.HopDong.CountAsync(h => h.TrangThai == "Hiệu lực");
                var soHopDongDaKetThuc = await _context.HopDong.CountAsync(h => h.TrangThai != "Hiệu lực");

                var doanhThuThangNay = await _context.HoaDon
                    .Where(h => h.TrangThai == "Đã thanh toán" && h.Nam == DateTime.Now.Year && h.Thang == DateTime.Now.Month)
                    .SumAsync(h => h.TongTien) ?? 0;

                var ngayHienTai = DateTime.Now;

                var hoaDonChuaThanhToan = await _context.HoaDon
                    .Where(h => h.TrangThai == "Chưa thanh toán"
                             && (h.Nam < ngayHienTai.Year || (h.Nam == ngayHienTai.Year && h.Thang <= ngayHienTai.Month)))
                    .ToListAsync();

                var tongNoHienTai = hoaDonChuaThanhToan.Sum(h => h.TongTien ?? 0);
                var soHoaDonChuaThanhToan = hoaDonChuaThanhToan.Count;

                var hopDongSapHetHanList = await _context.HopDong
                    .Include(h => h.PhongNavigation)
                    .Include(h => h.KhachHangNavigation)
                    .Where(h => h.TrangThai == "Hiệu lực"
                        && h.NgayKetThuc <= DateTime.Now.AddDays(30)
                        && h.NgayKetThuc >= DateTime.Now)
                    .Select(h => new HopDongSapHetHan
                    {
                        MaHopDong = h.MaHopDong,
                        TenPhong = h.PhongNavigation != null ? h.PhongNavigation.TenPhong : "N/A",
                        TenKhachHang = h.KhachHangNavigation != null ? h.KhachHangNavigation.HoTen : "N/A",
                        NgayKetThuc = h.NgayKetThuc ?? DateTime.Now,
                        SoNgayConLai = EF.Functions.DateDiffDay(DateTime.Now, h.NgayKetThuc.Value)
                    })
                    .OrderBy(h => h.SoNgayConLai)
                    .ToListAsync();

                var hoaDonGanDayList = await _context.HoaDon
                    .Include(h => h.HopDongNavigation)
                        .ThenInclude(hd => hd.PhongNavigation)
                    .Where(h => h.TrangThai == "Đã thanh toán")
                    .OrderByDescending(h => h.NgayChuXacNhan)
                    .Take(5)
                    .Select(h => new HoaDonGanDay
                    {
                        MaHoaDon = h.MaHoaDon,
                        TenPhong = h.HopDongNavigation != null && h.HopDongNavigation.PhongNavigation != null
                            ? h.HopDongNavigation.PhongNavigation.TenPhong : "N/A",
                        TongTien = h.TongTien ?? 0,
                        TrangThai = h.TrangThai ?? "N/A"
                    })
                    .ToListAsync();

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

                var doanhThuTheoThang = new List<DoanhThuTheoThang>();
                for (int i = 5; i >= 0; i--)
                {
                    var mThang = DateTime.Now.AddMonths(-i);
                    var tienHoaDon = await _context.HoaDon
                        .Where(h => h.Thang == mThang.Month && h.Nam == mThang.Year && h.TrangThai == "Đã thanh toán")
                        .SumAsync(h => h.TongTien ?? 0);
                    doanhThuTheoThang.Add(new DoanhThuTheoThang { Thang = mThang.Month, Nam = mThang.Year, DoanhThu = tienHoaDon });
                }

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
                    SoHopDongSapHetHan = hopDongSapHetHanList.Count,
                    HopDongSapHetHanList = hopDongSapHetHanList,
                    HoaDonGanDayList = hoaDonGanDayList,
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
                    khachHang = await _context.KhachHang.FirstOrDefaultAsync(k => k.Email == username || k.SoDienThoai == username);

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

        [HttpGet]
        public async Task<IActionResult> GetInvoiceDetails(int id)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin") return Forbid();

            var hoaDon = await _context.HoaDon
                .Include(h => h.HopDongNavigation)
                    .ThenInclude(hd => hd.KhachHangNavigation)
                .Include(h => h.HopDongNavigation)
                    .ThenInclude(hd => hd.PhongNavigation)
                .FirstOrDefaultAsync(h => h.MaHoaDon == id);

            if (hoaDon == null) return NotFound();

            var chiTiet = await _context.ChiTietHoaDon
                .Where(ct => ct.MaHoaDon == id)
                .ToListAsync();

            return Json(new
            {
                maHoaDon = hoaDon.MaHoaDon,
                tenPhong = hoaDon.HopDongNavigation?.PhongNavigation?.TenPhong ?? "N/A",
                tenKhachHang = hoaDon.HopDongNavigation?.KhachHangNavigation?.HoTen ?? "N/A",
                thang = hoaDon.Thang,
                nam = hoaDon.Nam,
                tongTien = hoaDon.TongTien ?? 0,
                ngayThanhToan = hoaDon.NgayChuXacNhan?.ToString("dd/MM/yyyy HH:mm") ?? "N/A",
                chiTietList = chiTiet.Select(ct => new {
                    khoanThu = ct.LoaiKhoanThu,
                    soLuong = ct.SoLuong,
                    donGia = ct.DonGia ?? 0,
                    thanhTien = ct.ThanhTien ?? 0
                })
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetContractDetails(int id)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin") return Forbid();

            var hopDong = await _context.HopDong
                .Include(h => h.PhongNavigation)
                .Include(h => h.KhachHangNavigation)
                .FirstOrDefaultAsync(h => h.MaHopDong == id);

            if (hopDong == null) return NotFound();

            // Đã sửa: Loại bỏ toán tử ?? 0 thừa lỗi để biên dịch mượt mà kiểu decimal không null
            return Json(new
            {
                maHopDong = hopDong.MaHopDong,
                tenPhong = hopDong.PhongNavigation?.TenPhong ?? "N/A",
                tenKhachHang = hopDong.KhachHangNavigation?.HoTen ?? "N/A",
                sdtKhachHang = hopDong.KhachHangNavigation?.SoDienThoai ?? "N/A",
                ngayBatDau = hopDong.NgayBatDau?.ToString("dd/MM/yyyy") ?? "N/A",
                ngayKetThuc = hopDong.NgayKetThuc?.ToString("dd/MM/yyyy") ?? "N/A",
                tienCoc = hopDong.TienCoc ?? 0,
                giaThue = hopDong.PhongNavigation != null ? hopDong.PhongNavigation.GiaPhong : 0,
                trangThai = hopDong.TrangThai ?? "N/A",
                soNgayConLai = (hopDong.NgayKetThuc.Value.Date - DateTime.Now.Date).Days
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuiThongBaoNhacNo()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin") return RedirectToAction("Index", "Login");

            // Lấy thời gian thực tại thời điểm bấm nút
            var ngayHienTai = DateTime.Now;
            var namHienTai = ngayHienTai.Year;
            var thangHienTai = ngayHienTai.Month;

            // Lọc hóa đơn: Chưa thanh toán VÀ (Năm nhỏ hơn năm hiện tại HOẶC (Năm bằng năm hiện tại VÀ Tháng nhỏ hơn hoặc bằng tháng hiện tại))
            var hoaDonChuaThanhToan = await _context.HoaDon
                .Include(h => h.HopDongNavigation)
                    .ThenInclude(hd => hd.KhachHangNavigation)
                .Include(h => h.HopDongNavigation)
                    .ThenInclude(hd => hd.PhongNavigation)
                .Where(h => h.TrangThai == "Chưa thanh toán"
                         && (h.Nam < namHienTai || (h.Nam == namHienTai && h.Thang <= thangHienTai)))
                .ToListAsync();

            if (!hoaDonChuaThanhToan.Any())
            {
                TempData["Info"] = $"Hiện tại không có hóa đơn quá hạn hoặc chưa thanh toán nào tính đến tháng {thangHienTai}/{namHienTai} để nhắc nhở!";
                return RedirectToAction("Index");
            }

            var smtpServer = _configuration["EmailSettings:SmtpServer"];
            var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"]);
            var senderEmail = _configuration["EmailSettings:SenderEmail"];
            var senderPassword = _configuration["EmailSettings:SenderPassword"];

            int soGuiThanhCong = 0;
            int soGuiThatBai = 0;

            foreach (var hoaDon in hoaDonChuaThanhToan)
            {
                var khachHang = hoaDon.HopDongNavigation?.KhachHangNavigation;
                var tenPhong = hoaDon.HopDongNavigation?.PhongNavigation?.TenPhong ?? "N/A";

                if (khachHang == null) continue;

                // 1. Tạo thông báo trên hệ thống web (Hệ thống nội bộ)
                var thongBao = new ThongBao
                {
                    TieuDe = "Nhắc nhở thanh toán hóa đơn",
                    NoiDung = $"Bạn có hóa đơn tháng {hoaDon.Thang}/{hoaDon.Nam} phòng {tenPhong} trị giá {hoaDon.TongTien?.ToString("N0")} đ chưa thanh toán. Vui lòng thanh toán sớm.",
                    Loai = "warning",
                    DuongDan = $"/HoaDon/Details/{hoaDon.MaHoaDon}",
                    NgayTao = DateTime.Now,
                    DaXem = false,
                    NguoiNhan = khachHang.MaKhachHang
                };
                _context.ThongBao.Add(thongBao);

                // 2. Gửi Email nhắc nợ trực tiếp cho khách hàng
                if (!string.IsNullOrWhiteSpace(khachHang.Email))
                {
                    try
                    {
                        var smtpClient = new SmtpClient(smtpServer)
                        {
                            Port = smtpPort,
                            Credentials = new NetworkCredential(senderEmail, senderPassword),
                            EnableSsl = true
                        };

                        var mailMessage = new MailMessage
                        {
                            From = new MailAddress(senderEmail, "Phòng Trọ Xinh"),
                            Subject = $"[Nhắc nhở] Hóa đơn tháng {hoaDon.Thang}/{hoaDon.Nam} chưa thanh toán",
                            IsBodyHtml = true,
                            Body = $@"
                        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e0e0e0; border-radius: 8px; overflow: hidden;'>
                            <div style='background: #1890ff; padding: 24px; text-align: center;'>
                                <h2 style='color: white; margin: 0;'>🏠 Phòng Trọ Xinh</h2>
                                <p style='color: #e0f0ff; margin: 4px 0 0 0; font-size: 14px;'>Thông báo thanh toán</p>
                            </div>
                            <div style='padding: 24px;'>
                                <p style='color: #333;'>Xin chào <strong>{khachHang.HoTen}</strong>,</p>
                                <p style='color: #555;'>Bạn hiện có hóa đơn chưa thanh toán với thông tin như sau:</p>
                                <table style='width: 100%; border-collapse: collapse; margin: 16px 0;'>
                                    <tr style='background: #f5f5f5;'>
                                        <td style='padding: 10px 14px; font-weight: bold; color: #333;'>Phòng</td>
                                        <td style='padding: 10px 14px; color: #555;'>{tenPhong}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 10px 14px; font-weight: bold; color: #333;'>Tháng/Năm</td>
                                        <td style='padding: 10px 14px; color: #555;'>{hoaDon.Thang}/{hoaDon.Nam}</td>
                                    </tr>
                                    <tr style='background: #f5f5f5;'>
                                        <td style='padding: 10px 14px; font-weight: bold; color: #333;'>Số tiền</td>
                                        <td style='padding: 10px 14px; color: #e53935; font-weight: bold; font-size: 16px;'>{hoaDon.TongTien?.ToString("N0")} đ</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 10px 14px; font-weight: bold; color: #333;'>Trạng thái</td>
                                        <td style='padding: 10px 14px;'><span style='background: #fff3e0; color: #e65100; padding: 3px 10px; border-radius: 4px; font-size: 13px;'>⏳ Chưa thanh toán</span></td>
                                    </tr>
                                </table>
                                <p style='color: #555;'>Vui lòng thanh toán sớm để tránh phát sinh thêm chi phí. Nếu bạn đã thanh toán, xin bỏ qua email này.</p>
                                <p style='color: #888; font-size: 13px; margin-top: 24px;'>Trân trọng,<br/><strong>Ban quản lý Phòng Trọ Xinh</strong></p>
                            </div>
                        </div>"
                        };
                        mailMessage.To.Add(khachHang.Email);

                        await smtpClient.SendMailAsync(mailMessage);
                        soGuiThanhCong++;
                    }
                    catch
                    {
                        soGuiThatBai++;
                    }
                }
            }

            await _context.SaveChangesAsync();

            if (soGuiThatBai == 0)
                TempData["Success"] = $"Hệ thống đã xử lý nhắc nợ cho toàn bộ {hoaDonChuaThanhToan.Count} hóa đơn chưa thanh toán (tính đến tháng {thangHienTai}/{namHienTai})!";
            else
                TempData["Success"] = $"Tìm thấy {hoaDonChuaThanhToan.Count} hóa đơn quá hạn. Gửi thành công {soGuiThanhCong} email, thất bại {soGuiThatBai} trường hợp (do thiếu email hoặc lỗi kết nối).";

            return RedirectToAction("Index");
        }
    }
}
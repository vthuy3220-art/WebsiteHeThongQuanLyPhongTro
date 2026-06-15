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

                // Lọc cho Chủ trọ
                if (!isSuperAdmin && role == "ChuTro")
                {
                    var toaNhaIds = await _context.ToaNha
                        .Where(t => t.MaChuTro == userId)
                        .Select(t => t.MaToaNha)
                        .ToListAsync();

                    queryPhong = queryPhong.Where(p => toaNhaIds.Contains(p.MaToaNha));
                    var phongIds = await queryPhong.Select(p => p.MaPhong).ToListAsync();

                    queryHopDong = queryHopDong.Where(h => phongIds.Contains(h.MaPhong));
                    var hopDongIds = await queryHopDong.Select(h => h.MaHopDong).ToListAsync();

                    queryHoaDon = queryHoaDon.Where(hd => hopDongIds.Contains(hd.MaHopDong));
                    queryBaiDang = queryBaiDang.Where(b => phongIds.Contains(b.MaPhong));
                }

                // Lấy dữ liệu (dùng ToListAsync trước khi tính toán)
                var danhSachPhong = await queryPhong.ToListAsync();
                var danhSachHopDong = await queryHopDong.ToListAsync();
                var danhSachHoaDon = await queryHoaDon.ToListAsync();
                var danhSachBaiDang = await queryBaiDang.ToListAsync();

                // ========== THỐNG KÊ ==========
                int tongSoPhong = danhSachPhong.Count;
                int soPhongDaThue = danhSachPhong.Count(p => p.TrangThai == "Đã thuê");
                int soPhongTrong = tongSoPhong - soPhongDaThue;
                int tongSoKhachHang = await _context.KhachHang.CountAsync();

                int soHopDongHieuLuc = danhSachHopDong.Count(h => h.TrangThai == "Hiệu lực");
                int soHopDongHetHan = danhSachHopDong.Count(h => h.TrangThai != "Hiệu lực");

                decimal doanhThuThangNay = danhSachHoaDon
                    .Where(h => h.TrangThai == "Đã thanh toán" && h.Nam == DateTime.Now.Year && h.Thang == DateTime.Now.Month)
                    .Sum(h => h.TongTien ?? 0);

                var hoaDonChuaThanhToanList = danhSachHoaDon
                    .Where(h => h.TrangThai == "Chưa thanh toán")
                    .ToList();
                decimal tongNoHienTai = hoaDonChuaThanhToanList.Sum(h => h.TongTien ?? 0);
                int soHoaDonChuaThanhToan = hoaDonChuaThanhToanList.Count;

                // Hợp đồng sắp hết hạn
                var hopDongSapHetHanList = danhSachHopDong
                    .Where(h => h.TrangThai == "Hiệu lực" && h.NgayKetThuc.HasValue
                        && h.NgayKetThuc.Value <= DateTime.Now.AddDays(30)
                        && h.NgayKetThuc.Value >= DateTime.Now)
                    .Select(h => new HopDongSapHetHan
                    {
                        MaHopDong = h.MaHopDong,
                        TenPhong = h.PhongNavigation?.TenPhong ?? "N/A",
                        TenKhachHang = h.KhachHangNavigation?.HoTen ?? "N/A",
                        NgayKetThuc = h.NgayKetThuc ?? DateTime.Now,
                        SoNgayConLai = (int)(h.NgayKetThuc.Value - DateTime.Now).TotalDays
                    })
                    .OrderBy(h => h.SoNgayConLai)
                    .ToList();

                // Hóa đơn gần đây
                var hoaDonGanDayList = danhSachHoaDon
                    .Where(h => h.TrangThai == "Đã thanh toán")
                    .OrderByDescending(h => h.NgayChuXacNhan)
                    .Take(5)
                    .Select(h => new HoaDonGanDay
                    {
                        MaHoaDon = h.MaHoaDon,
                        TenPhong = h.HopDongNavigation?.PhongNavigation?.TenPhong ?? "N/A",
                        TongTien = h.TongTien ?? 0,
                        TrangThai = h.TrangThai ?? "N/A"
                    })
                    .ToList();

                // Bài đăng
                int tongSoBaiDang = danhSachBaiDang.Count;
                int soBaiDangHienThi = danhSachBaiDang.Count(b => b.TrangThai == "Hiển thị" || b.TrangThai == "Hoạt động");
                int soBaiDangAn = tongSoBaiDang - soBaiDangHienThi;
                int soBaiDangThangNay = danhSachBaiDang.Count(b => b.NgayDang.HasValue && b.NgayDang.Value.Month == DateTime.Now.Month);

                var baiDangGanDayList = danhSachBaiDang
                    .OrderByDescending(b => b.NgayDang)
                    .Take(5)
                    .Select(b => new BaiDangGanDay
                    {
                        MaBaiDang = b.MaBaiDang,
                        TieuDe = b.TieuDe ?? "Không có tiêu đề",
                        TenPhong = b.PhongNavigation?.TenPhong ?? "N/A",
                        NgayDang = b.NgayDang ?? DateTime.Now,
                        TrangThai = b.TrangThai ?? "Ẩn"
                    })
                    .ToList();

                // Doanh thu theo tháng
                // ĐOẠN CODE ĐÃ SỬA CHUẨN XÁC:
                var doanhThuTheoThang = new List<HeThongQuanLyPhongTro.Models.DoanhThuTheoThang>();
                for (int i = 5; i >= 0; i--)
                {
                    var mThang = DateTime.Now.AddMonths(-i);
                    var tienHoaDon = danhSachHoaDon
                        .Where(h => h.Thang == mThang.Month && h.Nam == mThang.Year && h.TrangThai == "Đã thanh toán")
                        .Sum(h => h.TongTien ?? 0);

                    doanhThuTheoThang.Add(new HeThongQuanLyPhongTro.Models.DoanhThuTheoThang
                    {
                        Thang = mThang.Month,
                        Nam = mThang.Year,
                        DoanhThu = tienHoaDon
                    });
                }

                // Top phòng
                var thangHienTai = DateTime.Now.Month;
                var namHienTai = DateTime.Now.Year;
                var hoaDonThangNay = danhSachHoaDon
                    .Where(x => x.Thang == thangHienTai && x.Nam == namHienTai && x.TrangThai == "Đã thanh toán")
                    .ToList();

                var topPhongList = (from p in danhSachPhong
                                    join hd in danhSachHopDong on p.MaPhong equals hd.MaPhong into hdGroup
                                    from hd in hdGroup.DefaultIfEmpty()
                                    join h in hoaDonThangNay on (hd != null ? hd.MaHopDong : -1) equals h.MaHopDong into hGroup
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
                                    .ToList();

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
                    DoanhThuTheoThangList = doanhThuTheoThang,
                    TopPhongList = topPhongList
                };

                if (isSuperAdmin)
                {
                    return RedirectToAction("Index", "Admin");
                }
                else
                {
                    return View("ChuTroDashboard", model);
                }
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

        // ==================== QUẢN LÝ NGƯỜI Ở (CHỈ DÀNH RIÊNG CHO CHỦ TRỌ) ====================
        [HttpGet]
        public async Task<IActionResult> QuanLyNguoiO()
        {
            var role = HttpContext.Session.GetString("Role");
            var userId = HttpContext.Session.GetInt32("UserId");

            if (role != "ChuTro" || userId == null)
            {
                return RedirectToAction("Index", "Login");
            }

            // Thực hiện Join thêm bảng Phong và bảng KhachHang để lấy thông tin hiển thị tường minh
            var danhSachNguoiO = await (from no in _context.NguoiOHopDong
                                        join hd in _context.HopDong on no.MaHopDong equals hd.MaHopDong
                                        join p in _context.Phong on hd.MaPhong equals p.MaPhong
                                        join kh in _context.KhachHang on hd.MaKhachHang equals kh.MaKhachHang into khGroup
                                        from kh in khGroup.DefaultIfEmpty() // Tránh lỗi nếu hợp đồng chưa gán khách
                                        where hd.MaChuTro == userId.Value
                                        orderby no.MaNguoiO descending
                                        select new NguoiOHopDong
                                        {
                                            MaNguoiO = no.MaNguoiO,
                                            MaHopDong = no.MaHopDong,
                                            HoTen = no.HoTen,
                                            CCCD = no.CCCD,
                                            SoDienThoai = no.SoDienThoai,

                                            // Nạp dữ liệu vào Navigation Object để View có thể gọi ra dùng
                                            HopDongNavigation = new HopDong
                                            {
                                                MaHopDong = hd.MaHopDong,
                                                PhongNavigation = new Phong { TenPhong = p.TenPhong },
                                                KhachHangNavigation = kh != null ? new KhachHang { HoTen = kh.HoTen } : null
                                            }
                                        }).ToListAsync();

            // Gọi đích danh file Index nằm trong folder của bạn
            return View("~/Views/NguoiOhopDongs/Index.cshtml", danhSachNguoiO);
        }

        // ==================== ACTION HỖ TRỢ BÊN DƯỚI GIỮ NGUYÊN ====================
        [HttpGet]
        public async Task<IActionResult> GetInvoiceDetails(int id)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin" && role != "SuperAdmin" && role != "ChuTro")
                return Forbid();

            var hoaDon = await _context.HoaDon
                .Include(h => h.HopDongNavigation).ThenInclude(hd => hd.KhachHangNavigation)
                .Include(h => h.HopDongNavigation).ThenInclude(hd => hd.PhongNavigation)
                .FirstOrDefaultAsync(h => h.MaHoaDon == id);

            if (hoaDon == null) return NotFound();

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
            if (role != "Admin" && role != "SuperAdmin" && role != "ChuTro")
                return Forbid();

            var hopDong = await _context.HopDong
                .Include(h => h.PhongNavigation)
                .Include(h => h.KhachHangNavigation)
                .FirstOrDefaultAsync(h => h.MaHopDong == id);

            if (hopDong == null) return NotFound();

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuiThongBaoNhacNo()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin" && role != "SuperAdmin" && role != "ChuTro")
                return RedirectToAction("Index", "Login");

            var userId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var isSuperAdmin = (role == "Admin" || role == "SuperAdmin");
            var ngayHienTai = DateTime.Now;

            var queryHoaDon = _context.HoaDon
                .Include(h => h.HopDongNavigation).ThenInclude(hd => hd.KhachHangNavigation)
                .Include(h => h.HopDongNavigation).ThenInclude(hd => hd.PhongNavigation)
                .Where(h => h.TrangThai == "Chưa thanh toán"
                         && (h.Nam < ngayHienTai.Year || (h.Nam == ngayHienTai.Year && h.Thang <= ngayHienTai.Month)));

            if (!isSuperAdmin && role == "ChuTro")
            {
                var toaNhaIds = await _context.ToaNha.Where(t => t.MaChuTro == userId).Select(t => t.MaToaNha).ToListAsync();
                var phongIds = await _context.Phong.Where(p => toaNhaIds.Contains(p.MaToaNha)).Select(p => p.MaPhong).ToListAsync();
                var hopDongIds = await _context.HopDong.Where(p => phongIds.Contains(p.MaPhong)).Select(h => h.MaHopDong).ToListAsync();
                queryHoaDon = queryHoaDon.Where(h => hopDongIds.Contains(h.MaHopDong));
            }

            var hoaDonChuaThanhToan = await queryHoaDon.ToListAsync();

            if (!hoaDonChuaThanhToan.Any())
            {
                TempData["Info"] = "Không có hóa đơn nào quá hạn!";
                return RedirectToAction("Index");
            }

            var smtpServer = _configuration["EmailSettings:SmtpServer"];
            var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"]);
            var senderEmail = _configuration["EmailSettings:SenderEmail"];
            var senderPassword = _configuration["EmailSettings:SenderPassword"];

            int soGuiThanhCong = 0, soGuiThatBai = 0;

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

                if (!string.IsNullOrWhiteSpace(khachHang.Email))
                {
                    try
                    {
                        using var smtpClient = new SmtpClient(smtpServer)
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
                                        <p style='color: #555;'>Vui lòng thanh toán sớm để tránh phát sinh thêm chi phí.</p>
                                        <p style='color: #888; font-size: 13px; margin-top: 24px;'>Trân trọng,<br/><strong>Ban quản lý Phòng Trọ Xinh</strong></p>
                                    </div>
                                </div>"
                        };
                        mailMessage.To.Add(khachHang.Email);
                        await smtpClient.SendMailAsync(mailMessage);
                        soGuiThanhCong++;
                    }
                    catch { soGuiThatBai++; }
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã xử lý {soGuiThanhCong} email thành công, {soGuiThatBai} thất bại.";
            return RedirectToAction("Index");
        }
    }

    // Class giả lập tránh lỗi build nếu file Model chưa tạo
    public class DoanhThuTheoThang { public int Thang { get; set; } public int Nam { get; set; } public decimal DoanhThu { get; set; } }
}
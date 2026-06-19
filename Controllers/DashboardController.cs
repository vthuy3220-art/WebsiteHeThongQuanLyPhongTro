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

                // ========== THỐNG KÊ BIẾN ĐỘNG THEO THỰC TẾ CHỦ TRỌ ==========
                int tongSoPhong = danhSachPhong.Count;
                int soPhongDaThue = danhSachPhong.Count(p => p.TrangThai == "Đã thuê");
                int soPhongTrong = tongSoPhong - soPhongDaThue;

                // FIX DỨT ĐIỂM: Đếm số lượng khách hàng thực tế ĐANG THUÊ dựa trên hợp đồng HIỆU LỰC của chủ trọ này
                int tongSoKhachHang = 0;
                if (isSuperAdmin)
                {
                    tongSoKhachHang = await _context.KhachHang.CountAsync();
                }
                else
                {
                    // Chỉ đếm các mã khách hàng duy nhất xuất hiện trong danh sách hợp đồng hiệu lực của chủ trọ này
                    tongSoKhachHang = danhSachHopDong
                        .Where(h => h.TrangThai == "Hiệu lực" && h.MaKhachHang != null)
                        .Select(h => h.MaKhachHang)
                        .Distinct()
                        .Count();
                }

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

                // 🔥 TÍNH TOÁN % CHÍNH XÁC TUYỆT ĐỐI (Khử lỗi chia cho 0 nếu chủ trọ chưa lập phòng)
                double tyLeLapDay = tongSoPhong > 0 ? Math.Round(((double)soPhongDaThue / tongSoPhong) * 100, 1) : 0;
                double tyLeTrong = tongSoPhong > 0 ? Math.Round(((double)soPhongTrong / tongSoPhong) * 100, 1) : 0;

                // Đẩy các giá trị % thực tế này ra ViewBag để file View lấy ra hiển thị động công thức
                ViewBag.TyLeLapDay = tyLeLapDay;
                ViewBag.TyLeTrong = tyLeTrong;

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
                var doanhThuTheoThang = new List<HeThongQuanLyPhongTro.Models.DoanhThuTheoThang>();
                var resultDoanhThuThang = new List<HeThongQuanLyPhongTro.Models.DoanhThuTheoThang>();
                for (int i = 5; i >= 0; i--)
                {
                    var mThang = DateTime.Now.AddMonths(-i);
                    var tienHoaDon = danhSachHoaDon
                        .Where(h => h.Thang == mThang.Month && h.Nam == mThang.Year && h.TrangThai == "Đã thanh toán")
                        .Sum(h => h.TongTien ?? 0);

                    resultDoanhThuThang.Add(new HeThongQuanLyPhongTro.Models.DoanhThuTheoThang
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
                    TongSoKhachHang = tongSoKhachHang, // Trả về số thực chuẩn của chủ trọ
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

        // ==================== CÁC HÀM QUẢN LÝ NGƯỜI Ở / ĐIỀU HƯỚNG GIỮ NGUYÊN BẢN GỐC ====================
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

        [HttpGet]
        public async Task<IActionResult> GetInvoiceDetails(int id)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin" && role != "SuperAdmin" && role != "ChuTro") return Forbid();

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
            if (role != "Admin" && role != "SuperAdmin" && role != "ChuTro") return Forbid();

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
            if (role != "Admin" && role != "SuperAdmin" && role != "ChuTro") return RedirectToAction("Index", "Login");

            var userId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var isSuperAdmin = (role == "Admin" || role == "SuperAdmin");
            var ngayHienTai = DateTime.Now;

            var queryHoaDon = _context.HoaDon
                .Include(h => h.HopDongNavigation).ThenInclude(hd => hd.KhachHangNavigation)
                .Include(h => h.HopDongNavigation).ThenInclude(hd => hd.PhongNavigation)
                .Where(h => h.TrangThai == "Chưa thanh toán" && (h.Nam < ngayHienTai.Year || (h.Nam == ngayHienTai.Year && h.Thang <= ngayHienTai.Month)));

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
                        using var smtpClient = new SmtpClient(smtpServer) { Port = smtpPort, Credentials = new NetworkCredential(senderEmail, senderPassword), EnableSsl = true };
                        var mailMessage = new MailMessage { From = new MailAddress(senderEmail, "Phòng Trọ Xinh"), Subject = $"[Nhắc nhở] Hóa đơn tháng {hoaDon.Thang}/{hoaDon.Nam} chưa thanh toán", IsBodyHtml = true };
                        mailMessage.Body = $"Xin chào {khachHang.HoTen}, hóa đơn phòng {tenPhong} tháng {hoaDon.Thang}/{hoaDon.Nam} trị giá {hoaDon.TongTien?.ToString("N0")} đ đang quá hạn.";
                        mailMessage.To.Add(khachHang.Email);
                        await smtpClient.SendMailAsync(mailMessage);
                        soGuiThanhCong++;
                    }
                    catch { soGuiThatBai++; }
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
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
            var hopDongs = await _context.HopDong.Where(h => phongIds.Contains(h.MaPhong)).ToListAsync();
            var hopDongIds = hopDongs.Select(h => h.MaHopDong).ToList();
            var hoaDons = await _context.HoaDon.Where(hd => hopDongIds.Contains(hd.MaHopDong) && hd.TrangThai == "Đã thanh toán").ToListAsync();

            decimal tongDoanhThuTatCa = hoaDons.Sum(h => h.TongTien ?? 0);
            var thongKeToaNha = danhSachToaNha.Select(t => {
                var pIds = _context.Phong.Where(p => p.MaToaNha == t.MaToaNha).Select(p => p.MaPhong).ToList();
                var hIds = _context.HopDong.Where(h => pIds.Contains(h.MaPhong)).Select(h => h.MaHopDong).ToList();
                decimal doanhThuToa = hoaDons.Where(hd => hIds.Contains(hd.MaHopDong)).Sum(hd => hd.TongTien ?? 0);
                return new { TenToaNha = t.TenToaNha, DoanhThu = doanhThuToa, PhanTrach = tongDoanhThuTatCa > 0 ? Math.Round((double)doanhThuToa / (double)tongDoanhThuTatCa * 100, 1) : 0 };
            }).OrderByDescending(x => x.DoanhThu).ToList();
            ViewBag.ThongKeToaNha = thongKeToaNha;

            var thongKePhong = phongs.Select(p => {
                var hIds = hopDongs.Where(h => h.MaPhong == p.MaPhong).Select(h => h.MaHopDong).ToList();
                decimal doanhThuPhong = hoaDons.Where(hd => hIds.Contains(hd.MaHopDong)).Sum(hd => hd.TongTien ?? 0);
                return new { TenPhong = p.TenPhong, DoanhThu = doanhThuPhong, PhanTram = tongDoanhThuTatCa > 0 ? Math.Round((double)doanhThuPhong / (double)tongDoanhThuTatCa * 100, 1) : 0 };
            }).Where(x => x.DoanhThu > 0).OrderByDescending(x => x.DoanhThu).Take(10).ToList();
            ViewBag.ThongKePhong = thongKePhong;

            ViewBag.TongDoanhThuMụcTieu = tongDoanhThuTatCa;
            ViewBag.TongSoPhongMụcTieu = phongs.Count;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuiKhaoSatGiaHan(int id)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "ChuTro") return Forbid();

            var hopDong = await _context.HopDong.Include(h => h.KhachHangNavigation).Include(h => h.PhongNavigation).FirstOrDefaultAsync(h => h.MaHopDong == id);
            if (hopDong == null || hopDong.KhachHangNavigation == null) return RedirectToAction("Index");

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
            return RedirectToAction("Index");
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
            string tenKhach = hopDong.KhachHangNavigation?.HoTen ?? "Khách thuê";
            string textPhanHoi = luaChon == "GiaHan" ? "SẼ GIA HẠN TIẾP" : "SẼ TRẢ PHÒNG";

            _context.ThongBao.Add(new ThongBao
            {
                TieuDe = $"Phản hồi khảo sát phòng {tenPhong}",
                NoiDung = $"Khách hàng {tenKhach} ở phòng {tenPhong} đã phản hồi khảo sát: {textPhanHoi}.",
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
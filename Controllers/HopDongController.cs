using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using HeThongQuanLyPhongTro.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PuppeteerSharp;
using System.Text;

namespace HeThongQuanLyPhongTro.Controllers
{
    public class HopDongController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ThongBaoService _thongBaoService;

        public HopDongController(ApplicationDbContext context)
        {
            _context = context;
            _thongBaoService = new ThongBaoService(_context);
        }

        // ==================== HELPER METHODS ====================
        private int GetCurrentUserId()
        {
            return HttpContext.Session.GetInt32("UserId") ?? 0;
        }

        private string GetCurrentRole()
        {
            return HttpContext.Session.GetString("Role") ?? "";
        }

        private bool IsChuTro()
        {
            return GetCurrentRole() == "ChuTro";
        }

        // ==================== LOAD VIEW BAGS ====================
        private async Task LoadViewBags()
        {
            var role = GetCurrentRole();
            var userId = GetCurrentUserId();

            var queryPhong = _context.Phong
                .Where(p => p.TrangThai == "Trống");

            if (role == "ChuTro")
            {
                queryPhong = queryPhong.Where(p => p.MaChuTro == userId);
            }

            ViewBag.PhongList = await queryPhong.ToListAsync();
            ViewBag.KhachHangList = await _context.KhachHang.ToListAsync();
        }

        private async Task TaoHoaDonChoHopDong(int maHopDong, decimal giaPhong, DateTime ngayBatDau, DateTime ngayKetThuc)
        {
            int currentMonth = ngayBatDau.Month;
            int currentYear = ngayBatDau.Year;
            int endMonth = ngayKetThuc.Month;
            int endYear = ngayKetThuc.Year;

            var hopDong = await _context.HopDong
                .Include(h => h.KhachHangNavigation)  // ✅ Thêm Include để lấy thông tin khách
        .Include(h => h.PhongNavigation)      // ✅ Thêm Include để lấy thông tin phòng
                .FirstOrDefaultAsync(h => h.MaHopDong == maHopDong);

            int maChuTro = hopDong?.MaChuTro ?? 0;
            List<HoaDon> hoaDonMoiList = new List<HoaDon>();
            while (currentYear < endYear || (currentYear == endYear && currentMonth <= endMonth))
            {
                var exists = await _context.HoaDon
                    .AnyAsync(h => h.MaHopDong == maHopDong && h.Thang == currentMonth && h.Nam == currentYear);

                if (!exists)
                {
                    var hoaDon = new HoaDon
                    {
                        MaHopDong = maHopDong,
                        MaChuTro = maChuTro,
                        Thang = currentMonth,
                        Nam = currentYear,
                        TongTien = giaPhong,
                        TrangThai = "Chưa thanh toán",  // ✅ Tất cả đều là Chưa thanh toán
                        NgayTao = null
                    };
                    _context.HoaDon.Add(hoaDon);
                    hoaDonMoiList.Add(hoaDon);
                }

                currentMonth++;
                if (currentMonth > 12) { currentMonth = 1; currentYear++; }
            }

            await _context.SaveChangesAsync();
            if (hopDong?.KhachHangNavigation != null && _thongBaoService != null)
            {
                var khachHang = hopDong.KhachHangNavigation;
                var phong = hopDong.PhongNavigation;

                // Chỉ gửi 1 thông báo tổng hợp thay vì gửi từng tháng
                if (hoaDonMoiList.Any())
                {
                    // Lấy tháng đầu tiên và tháng cuối cùng
                    var thangDau = hoaDonMoiList.First();
                    var thangCuoi = hoaDonMoiList.Last();

                    string thongBaoNoiDung = $"Hợp đồng thuê phòng {phong?.TenPhong} đã được tạo thành công. " +
                                             $"Hóa đơn cho các tháng từ {thangDau.Thang}/{thangDau.Nam} đến {thangCuoi.Thang}/{thangCuoi.Nam} đã được tạo. " +
                                             $"Vui lòng kiểm tra và thanh toán đúng hạn!";

                    await _thongBaoService.GuiKhach(
                        khachHang.MaKhachHang,
                        "📋 Hóa đơn các tháng đã được tạo",
                        thongBaoNoiDung,
                        "info",
                        $"/KhachHang/HoaDonChiTiet/{hoaDonMoiList.First().MaHoaDon}"
                    );
                }
            }
        }
        // ==================== DANH SÁCH HỢP ĐỒNG ====================
        public async Task<IActionResult> Index(string searchString, string trangThai)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0)
            {
                return RedirectToAction("Index", "Login");
            }

            var hopDongs = _context.HopDong
                .Include(h => h.PhongNavigation)
                .Include(h => h.KhachHangNavigation)
                .AsQueryable();

            if (role == "ChuTro")
            {
                var phongIds = await _context.Phong
                    .Where(p => p.MaChuTro == userId)
                    .Select(p => p.MaPhong)
                    .ToListAsync();
                hopDongs = hopDongs.Where(h => phongIds.Contains(h.MaPhong));
            }
            else if (role == "Khach")
            {
                return RedirectToAction("HopDongCuaToi", "KhachHang");
            }

            if (!string.IsNullOrEmpty(searchString))
            {
                hopDongs = hopDongs.Where(h =>
                    (h.PhongNavigation != null && h.PhongNavigation.TenPhong.Contains(searchString)) ||
                    (h.KhachHangNavigation != null && h.KhachHangNavigation.HoTen.Contains(searchString)));
            }

            if (!string.IsNullOrEmpty(trangThai) && trangThai != "Tất cả")
            {
                hopDongs = hopDongs.Where(h => h.TrangThai == trangThai);
            }

            hopDongs = hopDongs
                .OrderBy(h => h.TrangThai == "Hiệu lực" ? 0 : 1)
                .ThenBy(h => h.PhongNavigation.TenPhong);

            ViewBag.SearchString = searchString;
            ViewBag.TrangThai = trangThai;
            ViewBag.TrangThaiList = new List<string> { "Tất cả", "Hiệu lực", "Hết hạn", "Đã hủy" };

            return View(await hopDongs.ToListAsync());
        }

        // ==================== TẠO HỢP ĐỒNG MỚI (GET) ====================
        public async Task<IActionResult> Create()
        {
            if (GetCurrentUserId() == 0)
            {
                return RedirectToAction("Index", "Login");
            }

            await LoadViewBags();
            return View();
        }

        // ==================== TẠO HỢP ĐỒNG MỚI (POST) ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            int maPhong,
            DateTime ngayBatDau,
            DateTime ngayKetThuc,
            decimal tienCoc,
            string HoTen,
            string SoDienThoai,
            string Email,
            string CCCD,
            string DiaChi,
            DateTime? NgaySinh,
            bool taoTaiKhoan,
            string tenDangNhap,
            string matKhau,
            List<string> NguoiOHoTen,
            List<string> NguoiOCCCD,
            List<string> NguoiOSDT)
        {
            var phong = await _context.Phong.FindAsync(maPhong);
            if (phong == null || phong.TrangThai != "Trống")
            {
                TempData["Error"] = "Phòng không còn trống!";
                await LoadViewBags();
                return View();
            }

            var role = GetCurrentRole();
            var userId = GetCurrentUserId();
            if (role == "ChuTro" && phong.MaChuTro != userId)
            {
                TempData["Error"] = "Bạn không có quyền tạo hợp đồng cho phòng này!";
                await LoadViewBags();
                return View();
            }

            if (ngayKetThuc < ngayBatDau)
            {
                TempData["Error"] = "Lỗi: Ngày kết thúc không được sớm hơn ngày bắt đầu hợp đồng!";
                await LoadViewBags();
                return View();
            }

            string sdtChuan = SoDienThoai?.Trim() ?? "";
            string cccdChuan = CCCD?.Trim() ?? "";
            string emailChuan = Email?.Trim() ?? "";

            if (sdtChuan.Length != 10 || !sdtChuan.All(char.IsDigit))
            {
                TempData["Error"] = "Lỗi: Số điện thoại phải bao gồm đúng 10 chữ số!";
                await LoadViewBags();
                return View();
            }

            if (cccdChuan.Length != 12 || !cccdChuan.All(char.IsDigit))
            {
                TempData["Error"] = "Lỗi: Số CCCD phải bao gồm đúng 12 chữ số!";
                await LoadViewBags();
                return View();
            }

            if (!emailChuan.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase) || emailChuan.Length <= 10)
            {
                TempData["Error"] = "Lỗi: Email đăng ký phải đúng định dạng @gmail.com!";
                await LoadViewBags();
                return View();
            }

            var danhSachMaKhachDangThue = await _context.HopDong
                .Where(h => h.TrangThai == "Hiệu lực")
                .Select(h => h.MaKhachHang)
                .ToListAsync();

            if (danhSachMaKhachDangThue.Any())
            {
                bool trungSdt = await _context.KhachHang.AnyAsync(k =>
                    danhSachMaKhachDangThue.Contains(k.MaKhachHang) && k.SoDienThoai == sdtChuan);
                if (trungSdt)
                {
                    TempData["Error"] = $"Lỗi: Số điện thoại '{sdtChuan}' đang thuộc về một người dùng có hợp đồng Hiệu lực!";
                    await LoadViewBags();
                    return View();
                }

                bool trungCccd = await _context.KhachHang.AnyAsync(k =>
                    danhSachMaKhachDangThue.Contains(k.MaKhachHang) && k.CCCD == cccdChuan);
                if (trungCccd)
                {
                    TempData["Error"] = $"Lỗi: Số CCCD '{cccdChuan}' đang thuộc về một người dùng có hợp đồng Hiệu lực!";
                    await LoadViewBags();
                    return View();
                }

                bool trungEmail = await _context.KhachHang.AnyAsync(k =>
                    danhSachMaKhachDangThue.Contains(k.MaKhachHang) && k.Email == emailChuan);
                if (trungEmail)
                {
                    TempData["Error"] = $"Lỗi: Email '{emailChuan}' đang thuộc về một người dùng có hợp đồng Hiệu lực!";
                    await LoadViewBags();
                    return View();
                }
            }

            int maKhachHangCuoi = 0;

            var khachHangTonTai = await _context.KhachHang
                .FirstOrDefaultAsync(k => k.SoDienThoai == sdtChuan);

            if (khachHangTonTai != null)
            {
                maKhachHangCuoi = khachHangTonTai.MaKhachHang;
                khachHangTonTai.HoTen = HoTen.Trim();
                khachHangTonTai.CCCD = cccdChuan;
                khachHangTonTai.Email = emailChuan;
                khachHangTonTai.DiaChi = DiaChi ?? "";
                khachHangTonTai.NgaySinh = NgaySinh;
                _context.KhachHang.Update(khachHangTonTai);
                await _context.SaveChangesAsync();
                TempData["Info"] = "Sử dụng lại hồ sơ khách hàng cũ và cập nhật thông tin mới!";
            }
            else
            {
                var khachHangMoi = new KhachHang
                {
                    HoTen = HoTen.Trim(),
                    SoDienThoai = sdtChuan,
                    Email = emailChuan,
                    CCCD = cccdChuan,
                    DiaChi = DiaChi ?? "",
                    NgaySinh = NgaySinh
                };
                _context.KhachHang.Add(khachHangMoi);
                await _context.SaveChangesAsync();
                maKhachHangCuoi = khachHangMoi.MaKhachHang;
            }

            if (taoTaiKhoan && !string.IsNullOrEmpty(tenDangNhap) && !string.IsNullOrEmpty(matKhau))
            {
                var tonTai = await _context.TaiKhoan.AnyAsync(t => t.TenDangNhap == tenDangNhap);
                if (!tonTai)
                {
                    var taiKhoan = new TaiKhoan
                    {
                        TenDangNhap = tenDangNhap,
                        MatKhau = matKhau,
                        VaiTro = "Khach",
                        TrangThai = "Hoạt động"
                    };
                    _context.TaiKhoan.Add(taiKhoan);
                    await _context.SaveChangesAsync();

                    var khachHang = await _context.KhachHang.FindAsync(maKhachHangCuoi);
                    if (khachHang != null)
                    {
                        khachHang.MaTaiKhoan = taiKhoan.MaTaiKhoan;
                        _context.KhachHang.Update(khachHang);
                        await _context.SaveChangesAsync();
                    }

                    TempData["TaiKhoan"] = $"Tài khoản đã tạo: {tenDangNhap} / {matKhau}";
                }
                else
                {
                    TempData["Warning"] = "Tên đăng nhập đã tồn tại, vui lòng chọn tên khác!";
                }
            }

            int maChuTro = phong.MaChuTro;

            var hopDong = new HopDong
            {
                MaPhong = maPhong,
                MaKhachHang = maKhachHangCuoi,
                MaChuTro = maChuTro,
                NgayBatDau = ngayBatDau,
                NgayKetThuc = ngayKetThuc,
                TienCoc = tienCoc,
                TrangThai = "Hiệu lực"
            };
            _context.HopDong.Add(hopDong);
            await _context.SaveChangesAsync();

            if (NguoiOHoTen != null && NguoiOHoTen.Any())
            {
                for (int i = 0; i < NguoiOHoTen.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(NguoiOHoTen[i]))
                    {
                        var nguoiO = new NguoiOHopDong
                        {
                            MaHopDong = hopDong.MaHopDong,
                            HoTen = NguoiOHoTen[i].Trim(),
                            CCCD = NguoiOCCCD != null && i < NguoiOCCCD.Count ? NguoiOCCCD[i] ?? "" : "",
                            SoDienThoai = NguoiOSDT != null && i < NguoiOSDT.Count ? NguoiOSDT[i] ?? "" : "",
                        };
                        _context.NguoiOHopDong.Add(nguoiO);
                    }
                }
                await _context.SaveChangesAsync();
            }

            phong.TrangThai = "Đã thuê";
            _context.Phong.Update(phong);
            await _context.SaveChangesAsync();

            await TaoHoaDonChoHopDong(hopDong.MaHopDong, phong.GiaPhong, ngayBatDau, ngayKetThuc);

            TempData["Success"] = $"Tạo hợp đồng thành công! Mã hợp đồng: {hopDong.MaHopDong}";
            return RedirectToAction(nameof(Index));
        }

        // ==================== TẠO HÓA ĐƠN HÀNG LOẠT (ĐÃ SỬA LỖI) ====================
        public async Task<IActionResult> TaoHangLoat()
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0 || role != "ChuTro")
            {
                TempData["Error"] = "Bạn không có quyền tạo hóa đơn!";
                return RedirectToAction("Index", "Home");
            }

            int ngayChot = 25;
            int dem = 0;
            int demSkipped = 0;
            int demFuture = 0;

            var hopDongs = await _context.HopDong
                .Include(h => h.PhongNavigation)
                .Where(h => h.TrangThai == "Hiệu lực" && h.MaChuTro == userId)
                .ToListAsync();

            foreach (var hopDong in hopDongs)
            {
                // ✅ SỬA LỖI: NgayBatDau có thể là DateTime?
                DateTime ngayBatDau;
                if (hopDong.NgayBatDau.HasValue)
                {
                    ngayBatDau = hopDong.NgayBatDau.Value;
                }
                else
                {
                    ngayBatDau = DateTime.Now;
                }

                // ✅ SỬA LỖI: NgayKetThuc có thể là DateTime?
                DateTime ngayKetThuc;
                if (hopDong.NgayKetThuc.HasValue)
                {
                    ngayKetThuc = hopDong.NgayKetThuc.Value;
                }
                else
                {
                    ngayKetThuc = DateTime.Now.AddMonths(6);
                }

                // Duyệt từ tháng bắt đầu đến tháng kết thúc
                for (int nam = ngayBatDau.Year; nam <= ngayKetThuc.Year; nam++)
                {
                    int thangTu = (nam == ngayBatDau.Year) ? ngayBatDau.Month : 1;
                    int thangDen = (nam == ngayKetThuc.Year) ? ngayKetThuc.Month : 12;

                    for (int thang = thangTu; thang <= thangDen; thang++)
                    {
                        bool exists = await _context.HoaDon
                            .AnyAsync(h => h.MaHopDong == hopDong.MaHopDong &&
                                           h.Thang == thang &&
                                           h.Nam == nam);

                        if (!exists && hopDong.PhongNavigation != null)
                        {
                            DateTime ngayLap;
                            try
                            {
                                ngayLap = new DateTime(nam, thang, ngayChot);
                            }
                            catch
                            {
                                ngayLap = new DateTime(nam, thang, 1).AddMonths(1).AddDays(-1);
                            }

                            if (ngayLap > ngayKetThuc)
                            {
                                ngayLap = ngayKetThuc;
                            }

                            string trangThai = ngayLap > DateTime.Now ? "Chờ" : "Chưa thanh toán";
                            if (trangThai == "Chờ") demFuture++;

                            var hoaDonMoi = new HoaDon
                            {
                                MaHopDong = hopDong.MaHopDong,
                                MaChuTro = userId,
                                Thang = thang,
                                Nam = nam,
                                NgayTao = ngayLap,
                                TongTien = hopDong.PhongNavigation.GiaPhong,
                                TrangThai = trangThai
                            };

                            _context.HoaDon.Add(hoaDonMoi);
                            dem++;
                        }
                        else
                        {
                            demSkipped++;
                        }
                    }
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"✅ Đã tạo {dem} hóa đơn mới! (Trong đó {demFuture} hóa đơn trong tương lai - trạng thái 'Chờ', bỏ qua {demSkipped} tháng đã có)";
            return RedirectToAction(nameof(Index));
        }
        // ==================== CHẤM DỨT HỢP ĐỒNG ====================
        [HttpGet]
        public async Task<IActionResult> ChamDut(int? id)
        {
            var role = GetCurrentRole();
            if (role != "SuperAdmin" && role != "Admin" && role != "ChuTro")
            {
                TempData["Error"] = "Bạn không có quyền thực hiện chức năng này!";
                return RedirectToAction("Index", "Login");
            }

            if (id == null) return NotFound();

            var hopDong = await _context.HopDong
                .Include(h => h.PhongNavigation)
                .Include(h => h.KhachHangNavigation)
                .FirstOrDefaultAsync(m => m.MaHopDong == id);

            if (hopDong == null) return NotFound();

            if (role == "ChuTro")
            {
                var userId = GetCurrentUserId();
                var phongCuaChuTro = await _context.Phong.FindAsync(hopDong.MaPhong);
                if (phongCuaChuTro == null || phongCuaChuTro.MaChuTro != userId)
                {
                    TempData["Error"] = "Bạn không có quyền chấm dứt hợp đồng này!";
                    return RedirectToAction("Index");
                }
            }

            return View(hopDong);
        }

        [HttpPost, ActionName("ChamDut")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChamDutConfirmed(int id)
        {
            var role = GetCurrentRole();
            var userId = GetCurrentUserId();

            if (role != "SuperAdmin" && role != "Admin" && role != "ChuTro")
            {
                TempData["Error"] = "Bạn không có quyền thực hiện chức năng này!";
                return RedirectToAction("Index", "Login");
            }

            var hopDong = await _context.HopDong
                .Include(h => h.PhongNavigation)
                .FirstOrDefaultAsync(h => h.MaHopDong == id);

            if (hopDong == null) return NotFound();

            if (role == "ChuTro")
            {
                var phongCuaChuTro = await _context.Phong.FindAsync(hopDong.MaPhong);
                if (phongCuaChuTro == null || phongCuaChuTro.MaChuTro != userId)
                {
                    TempData["Error"] = "Bạn không có quyền chấm dứt hợp đồng này!";
                    return RedirectToAction("Index");
                }
            }

            hopDong.TrangThai = "Đã hủy";
            _context.HopDong.Update(hopDong);

            var phongTrong = hopDong.PhongNavigation;
            if (phongTrong != null)
            {
                phongTrong.TrangThai = "Trống";
                _context.Phong.Update(phongTrong);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã chấm dứt hợp đồng!";
            return RedirectToAction(nameof(Index));
        }

        // ==================== CHI TIẾT HỢP ĐỒNG ====================
        public async Task<IActionResult> Details(int? id)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0 || role == "Admin" || role == "SuperAdmin")
            {
                return RedirectToAction("Index", "Login");
            }

            if (id == null) return NotFound();

            var hopDong = await _context.HopDong
                .Include(h => h.PhongNavigation).ThenInclude(p => p.ToaNha).ThenInclude(t => t.CoSo)
                .Include(h => h.KhachHangNavigation)
                .FirstOrDefaultAsync(m => m.MaHopDong == id);

            if (hopDong == null) return NotFound();

            if (role == "Khach")
            {
                var khachHang = await _context.KhachHang
                    .FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);
                if (khachHang == null || hopDong.MaKhachHang != khachHang.MaKhachHang)
                {
                    TempData["Error"] = "Bạn không có quyền xem hợp đồng này!";
                    return RedirectToAction("HopDongCuaToi", "KhachHang");
                }
            }
            else if (role == "ChuTro")
            {
                if (hopDong.MaChuTro != userId && hopDong.PhongNavigation?.MaChuTro != userId)
                {
                    TempData["Error"] = "Bạn không có quyền xem hợp đồng này!";
                    return RedirectToAction("Index");
                }
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
            ViewBag.Role = role;

            return View(hopDong);
        }

        // ==================== XUẤT PDF ====================
        public async Task<IActionResult> XuatPdf(int id)
        {
            var hopDong = await _context.HopDong
                .Include(h => h.PhongNavigation).ThenInclude(p => p.ToaNha).ThenInclude(t => t.CoSo)
                .Include(h => h.KhachHangNavigation)
                .FirstOrDefaultAsync(h => h.MaHopDong == id);

            if (hopDong == null) return NotFound();

            var nguoiOList = await _context.NguoiOHopDong
                .Where(n => n.MaHopDong == id)
                .ToListAsync();

            string htmlContent = TaoNoiDungHopDongHtml(hopDong, nguoiOList);

            await new BrowserFetcher().DownloadAsync();
            using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
            using var page = await browser.NewPageAsync();
            await page.SetContentAsync(htmlContent);
            var pdfBytes = await page.PdfDataAsync();

            return File(pdfBytes, "application/pdf", $"HopDong_{hopDong.MaHopDong}.pdf");
        }

        // ==================== HTML MẪU HỢP ĐỒNG CHUẨN PHÁP LÝ ====================
        private string TaoNoiDungHopDongHtml(HopDong hopDong, List<NguoiOHopDong> nguoiOList)
        {
            var ngayKy = hopDong.NgayBatDau ?? DateTime.Now;
            var thoiHanThang = 0;
            if (hopDong.NgayBatDau.HasValue && hopDong.NgayKetThuc.HasValue)
            {
                thoiHanThang = ((hopDong.NgayKetThuc.Value.Year - hopDong.NgayBatDau.Value.Year) * 12) + hopDong.NgayKetThuc.Value.Month - hopDong.NgayBatDau.Value.Month;
            }

            var sb = new StringBuilder();
            sb.Append(@"
    <!DOCTYPE html>
    <html>
    <head>
        <meta charset='utf-8' />
        <style>
            body { font-family: 'Times New Roman', Times, serif; font-size: 13pt; line-height: 1.5; margin: 0; padding: 0; color: #000; }
            .page { width: 210mm; min-height: 297mm; padding: 15mm; margin: auto; box-sizing: border-box; }
            .text-center { text-align: center; }
            .text-right { text-align: right; }
            .bold { font-weight: bold; }
            .italic { font-style: italic; }
            .underline { text-decoration: underline; }
            .header-title { font-size: 14pt; }
            .main-title { font-size: 16pt; font-weight: bold; margin-top: 20px; margin-bottom: 20px; }
            .indent { padding-left: 30px; }
            .table-sign { width: 100%; margin-top: 30px; border: none; }
            .table-sign td { width: 50%; text-align: center; vertical-align: top; border: none; }
            .sign-space { height: 80px; }
            table, th, td { border: 1px solid black; border-collapse: collapse; padding: 5px; }
            th { text-align: center; font-weight: bold; }
        </style>
    </head>
    <body>
        <div class='page'>
            <div class='text-center bold header-title'>CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</div>
            <div class='text-center italic' style='font-size: 13pt; margin-top: 5px;'>Độc lập - Tự do - Hạnh phúc</div>
    <p class='text-right italic'>Hôm nay, ngày " + ngayKy.Day + @" tháng " + ngayKy.Month + @" năm " + ngayKy.Year + @"</p>
        
            <div class='text-center main-title'>HỢP ĐỒNG THUÊ PHÒNG</div>
            
<p class='bold italic'>Chúng tôi gồm :</p>
            <p>BÊN A: BÊN CHO THUÊ</p>
            <p>Họ và tên : ........................................................................................................................</p>
            <p>CMND số : .................................... </p>
            <p>Thường Trú: ........................................................................................................................</p>
            <p>Số điện thoại : .....................................................................................................................</p>
            
            <p>BÊN B: BÊN THUÊ</p>
            <p>Họ và tên : " + (hopDong.KhachHangNavigation?.HoTen ?? ".......................................................") + @"</p>
            <p>CMND số : " + (hopDong.KhachHangNavigation?.CCCD ?? "....................................") + @" </p>
            <p>Thường trú: " + (hopDong.KhachHangNavigation?.DiaChi ?? "........................................................................................................") + @"</p>
            <p>Số điện thoại : " + (hopDong.KhachHangNavigation?.SoDienThoai ?? ".......................................................") + @"</p>
            
            <p>Sau khi thảo luận trên tinh thần dân chủ, cùng có lợi, Hai Bên thống nhất các nội dung sau:</p>
            
            <p class='bold'>Điều 1:</p>
            <p>1.1. Bên A đồng ý cho Bên B thuê căn phòng để ở.</p>
            <p>1.2. Bên A đồng ý cho Bên B thuê và Bên B cũng đồng ý thuê một căn phòng gắn liền với tòa nhà tại địa chỉ số " + (hopDong.PhongNavigation?.ToaNha?.DiaChi ?? "........................................................................................") + @" để sử dụng làm nơi để ở.</p>
            <p>1.3. Bên A cam kết quyền sử dụng phòng và tòa nhà gắn liền trên đất trên là tài sản sở hữu hợp pháp của Bên A. Mọi tranh chấp phát sinh từ tài sản cho thuê trên Bên A hoàn toàn chịu trách nhiệm trước pháp luật.</p>
            <p>1.4. Thời hạn của hợp đồng là " + thoiHanThang + @" tháng. Từ ngày " + (hopDong.NgayBatDau.HasValue ? hopDong.NgayBatDau.Value.ToString("dd/MM/yyyy") : "..../..../.......") + @" đến ngày " + (hopDong.NgayKetThuc.HasValue ? hopDong.NgayKetThuc.Value.ToString("dd/MM/yyyy") : "..../..../.......") + @"</p>
            
            <p class='bold'>Điều 2:</p>
            <p>2.1. Giá thuê: " + (hopDong.PhongNavigation?.GiaPhong ?? 0).ToString("N0") + @" VNĐ</p>
            <p>2.2. Chi phí dịch vụ hàng tháng:</p>
            <p class='indent'>- Tiền điện: ........................ đ/kWh</p>
            <p class='indent'>- Tiền nước: ........................ đ/khối</p>
            <p class='indent'>- Phí dịch vụ: ........................ đ/tháng</p>
            <p>2.3. Quá thời hạn 7 ngày mà bên B chưa thanh toán thì coi như bên B đã vi phạm hợp đồng, bên A có quyền thu hồi phòng, trục xuất đồ bên B, chấm dứt hợp đồng và không trả tiền đặt cọc.</p>
            <p>2.4. Bên B đặt cọc " + (hopDong.TienCoc ?? 0).ToString("N0") + @" VNĐ tiền phòng cho bên A. Để nhận lại khoản cọc này, bên B cần tuân thủ các điều kiện sau đây:</p>
            <p class='indent'>- Bên B phải đảm bảo tuân thủ các điều khoản quy định tại Điều 1;</p>
            <p class='indent'>- Bên B phải chủ động thông báo cho bên A ít nhất trước 30 ngày trước khi kết thúc hợp đồng quy định tại điều 1. Nếu hết thời hạn hợp đồng mà bên B không thông báo trước 30 ngày thì sẽ bị phạt tiền đặt cọc.</p>
            <p>2.5. Tiền cọc bên A sẽ hoàn trả lại cho bên B khi hết hạn hợp đồng + sau khi trừ chi phí sinh hoạt và thiệt hại do bên B gây ra (nếu có).</p>
            <p>2.6. Bên B có 7 ngày làm việc sau thời điểm bàn giao phòng để kiểm tra và yêu cầu bên A đảm bảo các trang thiết bị trong phòng ở tình trạng tốt.</p>
            
            <p class='bold'>Điều 3: Quyền và trách nhiệm bên A</p>
            <p>3.1. Giao phòng, trang thiết bị trong phòng cho bên B đúng ngày ký hợp đồng;</p>
            <p>3.2. Thực hiện việc kiểm tra định kỳ với trang thiết bị trong phòng do bên A cung cấp.</p>
            <p>3.3. Đảm bảo việc hoạt động của các trang thiết bị trong không gian chung của tòa nhà.</p>
            <p>3.4. Cho phép bên B đăng ký tạm trú.</p>
            <p>3.5. Trong thời gian ký hợp đồng mà bên A muốn lấy lại phòng, phải báo trước 01 tháng cho bên B và hoàn trả lại tiền cọc, không thu phí thuê phòng trong 01 tháng đó.</p>

            <div style=""page-break-before: always;""></div>


            <p class='bold'>Điều 4: Quyền và trách nhiệm bên B</p>
            <p>4.1. Trả tiền thuê phòng đúng hạn đã quy định tại điều 2.</p>
            <p>4.2. Hàng tháng bên B có trách nhiệm thanh toán bằng tiền mặt hoặc chuyển khoản vào số tài khoản bên A cung cấp.</p>
            <p>4.3. Chịu trách nhiệm và kinh phí sửa chữa với những đồ dùng, nội thất do bên B sử dụng (nếu có) và các khoản phí dịch vụ sử dụng tại phòng.</p>
            <p>4.4. Sử dụng đúng mục đích thuê phòng, khi cần sửa chữa, cải tạo theo yêu cầu sử dụng riêng, đóng đinh, khoan, cắt, lắp đặt các thiết bị phải được sự đồng ý của bên A.</p>
            <p>4.5. Đồ đạc trang thiết bị trong phòng phải có trách nhiệm bảo quản cẩn thận, hư hỏng do khách quan của thiết bị cần sửa chữa lại. Nếu hư hỏng do chủ quan mà không sửa chữa được cần bồi thường cho bên A bằng giá trị thị trường tại thời điểm hiện tại, hoặc mua thay thế giá trị ngang bằng hoặc cao hơn. Danh sách trang thiết bị sẽ được cung cấp cùng với hợp đồng.</p>
            <p>4.6. Tự giữ gìn an ninh cho tài sản của bản thân, chịu trách nhiệm với những tài sản bị mất của bên A cũng như những người khác trong tòa nhà do lỗi chủ quan của bên B.</p>
            <p>4.7. Nếu bên B có hành vi trộm cắp, bên A có quyền đơn phương chấm dứt hợp đồng, thu hồi phòng, không hoàn cọc và trình báo cơ quan có thẩm quyền xử lý.</p>
            <p>4.8. Giữ gìn phòng ở và các không gian chung gọn gàng, sạch sẽ, đảm bảo an toàn cháy nổ.</p>
            <p>4.9. Tự chịu trách nhiệm trước pháp luật về các vấn đề tự gây ra trong phòng của mình và không gian chung trong tòa nhà nếu có. Chịu trách nhiệm về các loại thuế, phí phát sinh theo yêu cầu của pháp luật (nếu có) trong phạm vi hợp đồng này.</p>
            <p>4.10. Khi trả phòng bên B có trách nhiệm dọn dẹp vệ sinh, bàn giao phòng và đồ đạc nguyên trạng cho bên A. Bên A sẽ tính ít nhất 500.000 VNĐ phí dọn dẹp vệ sinh trong trường hợp căn phòng do bê B hoàn trả không được dọn dẹp.</p>
            <p>4.11. Tuân thủ nội quy nơi ở. Nếu bên B cố tình không tuân thủ gây thiệt hại, ảnh hưởng nghiêm trọng đến căn phòng, tòa nhà và mọi người xung quanh thì bên B sẽ bị buộc chấm dứt hợp đồng và không hoàn cọc sau khi bên A thông báo.</p>
            <p>4.12. Khai báo tạm trú với chính quyền. Người thân, quen cư trú qua đêm cần báo bên A và tự chịu trách nhiệm về các thông tin khai báo.</p>
            <p>4.13. Việc người thân, quen đến chơi hay ở cùng cần đảm bảo tuyệt đối không làm ảnh hưởng đến các phòng và các nhà xung quanh.</p>
            <p>4.14. Về muộn ban đêm phải đóng cổng chung lại ngay, trường hợp quên đóng cửa gây hậu quả nghiêm trọng cho bên A và các bên liên quan, phải tự bồi thường thiệt hại.</p>
            
            
            <p class='bold'>Điều 5: Điều khoản chung</p>
            <p>5.1. Bên A và bên B thực hiện đúng các điều khoản ghi trong hợp đồng.</p>
            <p>5.2. Trường hợp tranh chấp hoặc một bên vi phạm hợp đồng thì hai bên cùng nhau bàn bạc giải quyết, nếu không giải quyết được thì yêu cầu cơ quan có thẩm quyền giải quyết.</p>
            <p>5.3. Hợp đồng được lập thành 02 bản có giá trị ngang nhau, mỗi bên giữ 01 bản.</p>

            <div class='text-right italic' style='margin-top: 20px;'>
                Hà Nội, ngày " + ngayKy.Day + @" tháng " + ngayKy.Month + @" năm " + ngayKy.Year + @"
            </div>

            <table class='table-sign'>
                <tr>
                    <td>
                        <div class='bold'>BÊN THUÊ</div>
                        <div class='sign-space'></div>
                        <div>" + (hopDong.KhachHangNavigation?.HoTen ?? "....................................") + @"</div>
                    </td>
                    <td>
                        <div class='bold'>BÊN CHO THUÊ</div>
                        <div class='sign-space'></div>
                        <div>................................................</div>
                    </td>
                </tr>
            </table>

            <div style='page-break-before: always;'></div>
            <div class='text-center main-title'>PHỤ LỤC</div>
            
            <p class='bold'>Tên các thành viên cùng thuê phòng:</p>
            <table style='width: 100%; margin-bottom: 20px;'>
                <thead>
                    <tr>
                        <th style='width: 10%;'>STT</th>
                        <th style='width: 40%;'>Họ và tên</th>
                        <th style='width: 25%;'>Số điện thoại</th> <th style='width: 25%;'>Số CMND/CCCD</th>
                    </tr>
                </thead>
                <tbody>");

            if (nguoiOList != null && nguoiOList.Any())
            {
                int stt = 1;
                foreach (var no in nguoiOList)
                {
                    // Kiểm tra nếu số điện thoại trống thì tự điền dấu chấm để điền tay
                    string sdtStr = string.IsNullOrEmpty(no.SoDienThoai) ? "...................." : no.SoDienThoai;

                    sb.Append(@"
                    <tr>
                        <td class='text-center'>" + stt++ + @"</td>
                        <td>" + no.HoTen + @"</td>
                        <td class='text-center'>" + sdtStr + @"</td> <td class='text-center'>" + (string.IsNullOrEmpty(no.CCCD) ? "...................." : no.CCCD) + @"</td>
                    </tr>");
                }
            }
            else
            {
                // Hiển thị một vài dòng trống nếu không có người ở cùng để điền tay
                for (int i = 1; i <= 5; i++)
                {
                    sb.Append(@"
                    <tr>
                        <td class='text-center'>" + i + @"</td>
                        <td></td>
                        <td></td>
                        <td></td>
                    </tr>");
                }
            }

            sb.Append(@"
                </tbody>
            </table>
            
            <p class='bold italic'>Theo hợp đồng thuê phòng bao gồm:</p>
            <p class='bold'>DANH MỤC TRANG THIẾT BỊ, CSVC KHI NHẬN VÀ TRẢ PHÒNG</p>
            <table style='width: 100%;'>
                <thead>
                    <tr>
                        <th rowspan='2' style='width: 5%;'>STT</th>
                        <th rowspan='2' style='width: 25%;'>Trang Thiết Bị, CSVC</th>
                        <th rowspan='2' style='width: 35%;'>Nội Dung Kiểm Tra</th>
                        <th colspan='3' style='width: 20%;'>Đánh giá chất lượng</th>
                        <th rowspan='2' style='width: 15%;'>Ghi chú</th>
                    </tr>
                    <tr>
                        <th>Tốt</th>
                        <th>Khá</th>
                        <th>TB</th>
                    </tr>
                </thead>
                <tbody>
                    <tr>
                        <td class='text-center'>1</td>
                        <td>...................</td>
                        <td>........................................</td>
			<td></td><td></td><td></td><td></td>
                    </tr>
                    <tr>
                        <td class='text-center'>2</td>
                        <td>.......................</td>
                        <td>........................................</td>
                        <td></td><td></td><td></td><td></td>
                    </tr>
                    <tr>
                        <td class='text-center'>3</td>
                        <td>.......................</td>
                        <td>.........................................</td>
                        <td></td><td></td><td></td><td></td>
                    </tr>
                    <tr>
                        <td class='text-center'>4</td>
                        <td>.......................</td>
                        <td>........................................</td>
                        <td></td><td></td><td></td><td></td>
                    </tr>
                    <tr>
                        <td class='text-center'>5</td>
                        <td>.......................</td>
                        <td>........................................</td>
                        <td></td><td></td><td></td><td></td>
                    </tr>
                    <tr>
                        <td class='text-center'>6</td>
                        <td>.......................</td>
                        <td>........................................</td>
                        <td></td><td></td><td></td><td></td>
                    </tr>
                    <tr>
                        <td class='text-center'>7</td>
                        <td>.......................</td>
                        <td>........................................</td>
                        <td></td><td></td><td></td><td></td>
                    </tr>
                    <tr>
                        <td class='text-center'>8</td>
                        <td>.......................</td>
                        <td>........................................</td>
                        <td></td><td></td><td></td><td></td>
                    </tr>
                    <tr>
                        <td class='text-center'>9</td>
                        <td>.......................</td>
                        <td>........................................</td>
                        <td></td><td></td><td></td><td></td>
                    </tr>
                    <tr>
                        <td class='text-center'>11</td>
                        <td>.......................</td>
                        <td>........................................</td>
                        <td></td><td></td><td></td><td></td>
                    </tr>
                    <tr>
                        <td class='text-center'>12</td>
                        <td>.......................</td>
                        <td>........................................</td>
                        <td></td><td></td><td></td><td></td>
                    </tr>
                </tbody>
            </table>

            <p style='margin-top: 20px;'>1. Sau 7 ngày kí hợp đồng, nếu không có ý kiến, chủ hợp đồng coi như đã đồng ý với nội dung trên.</p>
            <p>2. Không dán giấy lên tường, cửa, khoan đục nếu không có sự cho phép của chủ trọ</p>
            <p>3. Các mục trên mục nào đánh giá chất lượng không tốt, ghi vào mục ghi chú vì sao.</p>

            <table class='table-sign' style='margin-top: 50px;'>
                <tr>
                    <td>
                        <div class='bold'>Đại diện bên A</div>
                        <div class='sign-space'></div>
                        <div>................................................</div>
                    </td>
                    <td>
                        <div class='bold'>Đại diện bên B</div>
                        <div class='sign-space'></div>
                        <div>" + (hopDong.KhachHangNavigation?.HoTen ?? "....................................") + @"</div>
                    </td>
                </tr>
            </table>
        </div>
    </body>
    </html>");

            return sb.ToString();
        }
    }
}
using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using PuppeteerSharp;

namespace HeThongQuanLyPhongTro.Controllers
{
    public class HopDongController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HopDongController(ApplicationDbContext context)
        {
            _context = context;
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

            // ✅ PHÂN QUYỀN: Chủ trọ chỉ thấy phòng của mình
            if (role == "ChuTro")
            {
                queryPhong = queryPhong.Where(p => p.MaChuTro == userId);
            }

            ViewBag.PhongList = await queryPhong.ToListAsync();
            ViewBag.KhachHangList = await _context.KhachHang.ToListAsync();
        }

        // ==================== TẠO HÓA ĐƠN TỰ ĐỘNG ====================
        private async Task TaoHoaDonChoHopDong(int maHopDong, decimal giaPhong, DateTime ngayBatDau, DateTime ngayKetThuc)
        {
            int currentMonth = ngayBatDau.Month;
            int currentYear = ngayBatDau.Year;
            int endMonth = ngayKetThuc.Month;
            int endYear = ngayKetThuc.Year;

            var hopDong = await _context.HopDong
                .FirstOrDefaultAsync(h => h.MaHopDong == maHopDong);

            // ✅ PHÂN QUYỀN: Lấy MaChuTro từ hợp đồng
            int maChuTro = hopDong?.MaChuTro ?? 0;

            int ngayChotCoDinh = 25; // <--- Đặt ngày chốt cố định bạn muốn tại đây

            while (currentYear < endYear || (currentYear == endYear && currentMonth <= endMonth))
            {
                var existingHoaDon = await _context.HoaDon
                    .AnyAsync(h => h.MaHopDong == maHopDong && h.Thang == currentMonth && h.Nam == currentYear);

                if (!existingHoaDon)
                {
                    // 🔴 SỬA TẠI ĐÂY: Ép ngày tạo chạy theo tháng/năm của hóa đơn đó thay vì lấy ngày hiện tại
                    DateTime ngayLapDongBo = new DateTime(currentYear, currentMonth, ngayChotCoDinh);

                    var hoaDon = new HoaDon
                    {
                        MaHopDong = maHopDong,
                        MaChuTro = maChuTro,  // ✅ PHÂN QUYỀN: Lưu mã chủ trọ vào hóa đơn
                        Thang = currentMonth,
                        Nam = currentYear,
                        TongTien = giaPhong,
                        TrangThai = "Chờ thanh toán",
                        NgayTao = ngayLapDongBo // ✅ ĐÃ ĐỒNG BỘ: Luôn luôn là ngày 25 của tháng đó
                    };
                    _context.HoaDon.Add(hoaDon);
                }

                currentMonth++;
                if (currentMonth > 12)
                {
                    currentMonth = 1;
                    currentYear++;
                }
            }

            await _context.SaveChangesAsync();
        }        // ==================== DANH SÁCH HỢP ĐỒNG ====================
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

            // ✅ PHÂN QUYỀN: Chủ trọ chỉ thấy hợp đồng của phòng mình
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

            // ✅ PHÂN QUYỀN: Kiểm tra chủ trọ có quyền với phòng này không
            var role = GetCurrentRole();
            var userId = GetCurrentUserId();
            if (role == "ChuTro" && phong.MaChuTro != userId)
            {
                TempData["Error"] = "Bạn không có quyền tạo hợp đồng cho phòng này!";
                await LoadViewBags();
                return View();
            }

            // 🔴 1. KIỂM TRA LOGIC NGÀY THÁNG
            if (ngayKetThuc < ngayBatDau)
            {
                TempData["Error"] = "Lỗi: Ngày kết thúc không được sớm hơn ngày bắt đầu hợp đồng!";
                await LoadViewBags();
                return View();
            }

            // Chuẩn hóa dữ liệu đầu vào để kiểm tra chính xác
            string sdtChuan = SoDienThoai?.Trim() ?? "";
            string cccdChuan = CCCD?.Trim() ?? "";
            string emailChuan = Email?.Trim() ?? "";

            // 🔴 2. KIỂM TRA ĐỊNH DẠNG CƠ BẢN (ĐỘ DÀI & ĐUÔI EMAIL)
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

            // 🔴 3. KIỂM TRA TRÙNG LẶP VỚI KHÁCH ĐANG CÓ HỢP ĐỒNG HIỆU LỰC
            // Lấy danh sách MaKhachHang của những hợp đồng đang có hiệu lực
            var danhSachMaKhachDangThue = await _context.HopDong
                .Where(h => h.TrangThai == "Hiệu lực")
                .Select(h => h.MaKhachHang)
                .ToListAsync();

            if (danhSachMaKhachDangThue.Any())
            {
                // Kiểm tra trùng Số điện thoại
                bool trungSdt = await _context.KhachHang.AnyAsync(k =>
                    danhSachMaKhachDangThue.Contains(k.MaKhachHang) && k.SoDienThoai == sdtChuan);
                if (trungSdt)
                {
                    TempData["Error"] = $"Lỗi: Số điện thoại '{sdtChuan}' đang thuộc về một người dùng có hợp đồng Hiệu lực!";
                    await LoadViewBags();
                    return View();
                }

                // Kiểm tra trùng CCCD
                bool trungCccd = await _context.KhachHang.AnyAsync(k =>
                    danhSachMaKhachDangThue.Contains(k.MaKhachHang) && k.CCCD == cccdChuan);
                if (trungCccd)
                {
                    TempData["Error"] = $"Lỗi: Số CCCD '{cccdChuan}' đang thuộc về một người dùng có hợp đồng Hiệu lực!";
                    await LoadViewBags();
                    return View();
                }

                // Kiểm tra trùng Email
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

            // ========== XỬ LÝ TIẾP TỤC NẾU VƯỢT QUA TOÀN BỘ KIỂM TRA ==========
            // Tìm xem người này trước đó đã từng ở trọ hệ thống chưa (nhưng hợp đồng cũ đã hết hạn/hủy)
            var khachHangTonTai = await _context.KhachHang
                .FirstOrDefaultAsync(k => k.SoDienThoai == sdtChuan);

            if (khachHangTonTai != null)
            {
                maKhachHangCuoi = khachHangTonTai.MaKhachHang;
                // Cập nhật lại thông tin mới nhất nếu có thay đổi
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

            // ========== 2. TẠO TÀI KHOẢN (nếu được chọn) ==========
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

            // ========== 3. TẠO HỢP ĐỒNG ==========
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

            // ========== 4. THÊM DANH SÁCH NGƯỜI Ở ==========
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

            // ========== 5. CẬP NHẬT TRẠNG THÁI PHÒNG ==========
            phong.TrangThai = "Đã thuê";
            _context.Phong.Update(phong);
            await _context.SaveChangesAsync();

            // ========== 6. TẠO HÓA ĐƠN TỰ ĐỘNG ==========
            await TaoHoaDonChoHopDong(hopDong.MaHopDong, phong.GiaPhong, ngayBatDau, ngayKetThuc);

            TempData["Success"] = $"Tạo hợp đồng thành công! Mã hợp đồng: {hopDong.MaHopDong}";
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

            // ✅ PHÂN QUYỀN: Chủ trọ chỉ chấm dứt hợp đồng của mình
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

            // ✅ PHÂN QUYỀN: Chủ trọ chỉ chấm dứt hợp đồng của mình
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

            // Chặn quyền Admin hoàn toàn theo yêu cầu hệ thống của bạn
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

            // ✅ PHÂN QUYỀN BẢO MẬT: Khách thuê chỉ xem được đúng hợp đồng của mình
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
            // ✅ PHÂN QUYỀN BẢO MẬT: Chủ trọ chỉ xem được hợp đồng phòng thuộc toà nhà của mình quản lý
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
                    body { font-family: 'Times New Roman', Times, serif; font-size: 14pt; line-height: 1.5; margin: 0; padding: 0; color: #000; }
                    .page { width: 210mm; min-height: 297mm; padding: 20mm 15mm 20mm 25mm; margin: auto; box-sizing: border-box; }
                    .text-center { text-align: center; }
                    .text-right { text-align: right; }
                    .bold { font-weight: bold; }
                    .italic { font-style: italic; }
                    .underline { text-decoration: underline; }
                    .header-title { font-size: 13pt; text-transform: uppercase; }
                    .main-title { font-size: 16pt; font-weight: bold; margin-top: 30px; margin-bottom: 30px; text-transform: uppercase; }
                    .section-title { font-weight: bold; text-transform: uppercase; margin-top: 15px; margin-bottom: 5px; }
                    .indent { padding-left: 25px; }
                    .table-sign { width: 100%; margin-top: 40px; border: none; }
                    .table-sign td { width: 50%; text-align: center; vertical-align: top; border: none; }
                    .sign-space { height: 100px; }
                </style>
            </head>
            <body>
                <div class='page'>
                    <div class='text-center bold header-title'>CỘNG HOÀ XÃ HỘI CHỦ NGHĨA VIỆT NAM</div>
                    <div class='text-center bold underline' style='font-size: 14pt; margin-top: 5px;'>Độc lập – Tự do – Hạnh phúc</div>
                    <div class='text-center' style='margin-top: 10px;'>---------------</div>
                    
                    <div class='text-center main-title'>HỢP ĐỒNG CHO THUÊ PHÒNG</div>
                    
                    <p class='italic'>Hôm nay, ngày " + ngayKy.Day + @" tháng " + ngayKy.Month + @" năm " + ngayKy.Year + @"</p>
                    
                    <p class='bold italic'>Chúng tôi gồm :</p>
                    
                    <div class='section-title'>I . BÊN CHO THUÊ ( BÊN A )</div>
                    <div class='indent'>
                        <p><span class='bold'>Ông ( Bà ) :</span> ............................................................ <span class='bold'>. ĐT :</span> ....................................</p>
                        <p><span class='bold'>CCCD số :</span> ............................................................</p>
                        <p><span class='bold'>TT tại :</span> .....................................................................................................................................</p>
                        <p><span class='bold'>Là chủ sở hữu ngôi nhà số :</span> " + (hopDong.PhongNavigation?.ToaNha?.DiaChi ?? "........................................................................................") + @"</p>
                    </div>

                    <div class='section-title'>II . BÊN THUÊ ( BÊN B )</div>
                    <div class='indent'>
                        <p><span class='bold'>Đại diện : Ông ( Bà ) :</span> " + (hopDong.KhachHangNavigation?.HoTen ?? "....................................") + @" <span class='bold'>. SN :</span> " + (hopDong.KhachHangNavigation?.NgaySinh.HasValue == true ? hopDong.KhachHangNavigation.NgaySinh.Value.ToString("dd/MM/yyyy") : "....................") + @"</p>
                        <p><span class='bold'>CCCD số :</span> " + (hopDong.KhachHangNavigation?.CCCD ?? "....................................") + @"</p>
                        <p><span class='bold'>HKTT :</span> " + (hopDong.KhachHangNavigation?.DiaChi ?? "........................................................................") + @"</p>
                        <p><span class='bold'>ĐT :</span> " + (hopDong.KhachHangNavigation?.SoDienThoai ?? "....................................") + @"</p>
                        <p><span class='bold'>Tổng số người ở :</span> " + (nguoiOList != null ? (nguoiOList.Count + 1).ToString() : "1") + @" Người</p>
                        
                        <p class='bold italic' style='margin-top: 10px; margin-bottom: 5px;'>Người Ở Cùng :</p>");

            if (nguoiOList != null && nguoiOList.Any())
            {
                foreach (var no in nguoiOList)
                {
                    sb.Append("<p class='indent'>- <span class='bold'>Họ và tên :</span> " + no.HoTen + @" . <span class='bold'>CCCD :</span> " + (string.IsNullOrEmpty(no.CCCD) ? "...................." : no.CCCD) + @" . <span class='bold'>ĐT :</span> " + (string.IsNullOrEmpty(no.SoDienThoai) ? "...................." : no.SoDienThoai) + "</p>");
                }
            }
            else
            {
                sb.Append("<p class='indent italic'>Không có người ở cùng.</p>");
            }

            sb.Append(@"
                    </div>

                    <p class='italic' style='margin-top: 20px;'><span class='bold'>Sau khi thoả thuận , hai Bên cùng kí hợp đồng với các điều khoản sau đây :</span></p>
                    
                    <div class='section-title'>ĐIỀU I : NỘI DUNG HỢP ĐỒNG</div>
                    <div class='indent'>
                        <p>- Bên A đồng ý cho Bên B thuê phòng <span class='bold'>" + (hopDong.PhongNavigation?.TenPhong ?? "....................") + @"</span></p>
                        <p>- Trang thiết bị của phòng gồm có : Điều hoà + giường tủ + bình nóng lạnh + thiết bị vệ sinh .</p>
                        <p>- Trang thiết bị dùng chung của ngôi nhà : Máy giặt .</p>
                        <p>- Mục đích cho thuê : Để ở .</p>
                        <p>- Hợp đồng có thời hạn : <span class='bold'>" + thoiHanThang + @"</span> tháng , tính từ ngày <span class='bold'>" + (hopDong.NgayBatDau.HasValue ? hopDong.NgayBatDau.Value.ToString("dd/MM/yyyy") : "..../..../.......") + @"</span> đến hết ngày <span class='bold'>" + (hopDong.NgayKetThuc.HasValue ? hopDong.NgayKetThuc.Value.ToString("dd/MM/yyyy") : "..../..../.......") + @"</span></p>
                    </div>

                    <div class='section-title'>ĐIỀU II : GIÁ CẢ , ĐẶT CỌC VÀ PHƯƠNG THỨC THANH TOÁN</div>
                    <div class='indent'>
                        <p>1. Giá thuê phòng là : <span class='bold'>" + (hopDong.PhongNavigation?.GiaPhong ?? 0).ToString("N0") + @" đ/1 tháng</span> .</p>
                        <p>2. Giá thuê trên cố định trong 6 tháng đầu .</p>
                        <p>3. Giá dịch vụ :</p>
                        <p class='indent'>- Điện : ........đ/KWh</p>
                        <p class='indent'>- Nước : ......đ/người.</p>
                        <p class='indent'>- Dịch vụ chung ( Điện máy giặt + điện cầu thang + dọn vệ sinh ) : ......... / Người .</p>
                        <p>4. Phương thức thanh toán :</p>
                        <p class='indent'>- Bên B đặt cọc cho Bên A số tiền là : <span class='bold'>" + (hopDong.TienCoc ?? 0).ToString("N0") + @" đ</span> . Tiền đặt cọc sẽ được Bên A hoàn trả sau 07 ngày khi kết thúc HĐ . Số tiền đặt cọc này có thể được Bên A sử dụng để khắc phục sự cố hoặc vi phạm do Bên B gây ra , Bên B có trách nhiệm hoàn trả số tiền bồi thường này cho Bên A sau 02 ngày theo thông báo , nếu không thì Bên A có quyền đơn phương chấm dứt hợp đồng .</p>
                        <p class='indent'>- Bên B thanh toán cho Bên A tiền thuê phòng : 1 tháng/lần . Thời hạn đóng tiền là chuyển khoản từ ngày 28 Đến ngày 03 hàng tháng . Nếu trả chậm bị tính 100.000đ/ngày . Thời gian chậm trả không quá 03 ngày , số lần chậm trả không quá 02 lần .</p>
                        <p class='indent'>- Bên B thanh toán chi phí DV cố định ( nước + mạng + dv chung ) vào đầu tháng .</p>
                        <p class='indent'>- Hình thức thanh toán : Tiền mặt hoặc chuyển khoản .</p>
                    </div>

                    <div class='section-title'>ĐIỀU III : QUYỀN VÀ TRÁCH NHIỆM CỦA BÊN B</div>
                    <div class='indent'>
                        <p>1. Sử dụng phòng tại Điều 1 đúng mục đích , đóng tiền theo thời hạn quy định trong hợp đồng .</p>
                        <p>2. Sau 1 tuần đầu Bên A bàn giao trang thiết bị , trong quá trình sử dụng bị hư hỏng Bên B phải tự sửa chữa , thay thế , khắc phục hoặc bồi thường cho Bên A ( Bao gồm cả tắc đường thoát nước , cháy bóng đèn ) .</p>
                        <p>3. Nghiêm cấm mọi hành vi tàng trữ , sử dụng các chất ma tuý , chất dễ cháy nổ , mại dâm , cờ bạc .... Mọi hành vi vi phạm pháp luật Bên B hoàn toàn chịu trách nhiệm .</p>
                        <p>4. Không được đập phá tháo dỡ , không được thay đổi cấu trúc nhà , không đóng đinh , dán tranh ảnh , vẽ , bôi bẩn lên tường , cửa phòng .</p>
                        <p>5. Bên B cam kết thực hiện hợp đồng với thời hạn nêu trên , nếu bên B chuyển trước thời hạn sẽ bị mất toàn bộ số tiền cọc .Nếu muốn kết thúc hợp đồng theo đúng thời hạn hai Bên thoả thuận thì Bên B phải báo cho bên A trước 30 ngày , nếu ko báo sẽ bị phạt 50% tiền đặt cọc .</p>
                        <p>6. Các trường hợp thay đổi người ở hoặc chuyển nhượng phòng phải có sự đồng ý của Bên A .</p>
                        <p>7. Tuân thủ tuyệt đối nội quy của toàn nhà : không cờ bạc mại dâm , không sử dụng tàng trữ ma tuý , vũ khí trái phép , không cho người lạ ngủ qua đêm , tụ tập rượu chè , gây rối trật tự , mất vệ sinh , ý thức kém , làm ảnh hưởng tới người xung quanh . <span class='bold'>Nếu vi phạm sẽ bị phạt theo nội quy của toà nhà , hoặc Bên A có quyền đơn phương chấm dứt hợp đồng . ( Bên B sẽ không nhận được tiền đặt cọc )</span></p>
                        <p>8. Sau khi kết thúc Hợp đồng , Bên B có trách nhiệm thu dọn đồ đạc , trả lại phòng theo đúng nguyên trạng ban đầu và chịu chi phí 200.000đ để bên A thuê người dọn vệ sinh công nghiệp .</p>
                        <p>9. Bên B trách nhiệm đi khai báo với công an khu vực để làm tạm trú , tạm vắng .</p>
                        <p>10. Tuyệt đối đảm bảo an toàn PCCC , khóa gas , rút các thiết bị điện khi đi ra ngoài . Chịu hoàn toàn trách nhiệm nếu để xảy ra cháy nổ .</p>
                        <p>11. Sau khi hết thời hạn thuê nhà mà hai bên không có thỏa thuận gì khác thì hợp đồng sẽ tự động gia hạn thêm 06 tháng mà không cần kí lại .</p>
                    </div>

                    <div class='section-title'>ĐIỀU IV : CÁC THOẢ THUẬN KHÁC</div>
                    <div class='indent'>
                        <p>1. Mọi tài sản của Bên B thì Bên B phải tự bảo quản , tự chịu trách nhiệm nếu xảy ra mất mát . Bên A không chịu trách nhiệm với các vấn đề trộm cắp , cháy nổ , tai nạn liên quan tới tính mạng con người của Bên B trong quá trình thuê phòng .</p>
                        <p>2. Bên A có quyền chấm dứt hợp đồng trước hạn nếu Bên B vi phạm các điều khoản trong hợp đồng và Bên B không được nhận lại tiền đặt cọc .</p>
                        <p>3. Mọi tranh chấp phát sinh liên quan tới hợp đồng này nếu không thể giải quyết thông qua thương lượng , hoà giải sẽ được đưa ra toà án có thẩm quyền để giải quyết theo quy định của pháp luật .</p>
                        <p>4. Hợp đồng này được lập thành 02 bản , mỗi bên giữ 1 bản có giá trị như nhau . Hợp đồng này có hiệu lực kể từ ngày kí .</p>
                        <p>5. Sau khi kí hợp đồng Bên B nộp lại cho bên A bản photo CMND/CCCD của tất cả những người ở phòng mình .</p>
                    </div>

                    <table class='table-sign'>
                        <tr>
                            <td>
                                <div class='bold'>ĐẠI DIỆN BÊN A</div>
                                <div class='italic'>(Kí ghi rõ họ tên)</div>
                                <div class='sign-space'></div>
                                <div class='bold' style='margin-top: 30px;'>................................................</div>
                            </td>
                            <td>
                                <div class='bold'>ĐẠI DIỆN BÊN B</div>
                                <div class='italic'>(Kí ghi rõ họ tên)</div>
                                <div class='sign-space'></div>
                                <div class='bold' style='margin-top: 30px;'>" + (hopDong.KhachHangNavigation?.HoTen ?? "....................................") + @"</div>
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
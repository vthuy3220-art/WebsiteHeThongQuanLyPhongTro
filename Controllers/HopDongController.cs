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

        // GET: Xuất PDF hợp đồng
        public async Task<IActionResult> XuatPdf(int id)
        {
            var hopDong = await _context.HopDong
                .Include(h => h.PhongNavigation).ThenInclude(p => p.CoSo)
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

        // Hàm tạo nội dung HTML hợp đồng (giữ nguyên mẫu của bạn)
        private string TaoNoiDungHopDongHtml(HopDong hopDong, List<NguoiOHopDong> nguoiOList)
        {
            string danhSachNguoiOBang = "";
            if (nguoiOList != null && nguoiOList.Any())
            {
                int stt = 1;
                foreach (var n in nguoiOList)
                {
                    danhSachNguoiOBang += $@"
    <tr>
        <td class='text-center'>{stt}</td>
        <td>{n.HoTen}</td>
        <td>{n.CCCD}</td>
        <td>{n.SoDienThoai}</td>
        <td></td>
    </tr>";
                    stt++;
                }
            }
            else
            {
                danhSachNguoiOBang = @"
    <tr>
        <td colspan='5' class='text-center'>Không có người ở chung</td>
    </tr>";
            }

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <title>HỢP ĐỒNG THUÊ PHÒNG</title>
    <style>
        body {{ font-family: 'Times New Roman', Times, serif; font-size: 14px; margin: 40px; line-height: 1.5; }}
        .center {{ text-align: center; }}
        .bold {{ font-weight: bold; }}
        .title {{ font-size: 18px; font-weight: bold; text-transform: uppercase; margin: 20px 0; }}
        .indent {{ text-indent: 30px; }}
        .sign-left {{ float: left; width: 45%; text-align: center; }}
        .sign-right {{ float: right; width: 45%; text-align: center; }}
        .clear {{ clear: both; }}
        .dieu {{ margin: 15px 0; }}
        .dieu-title {{ font-size: 15px; font-weight: bold; margin: 15px 0 8px 0; }}
        .sign-line {{ border-top: 1px solid #000; width: 80%; margin: 35px auto 8px auto; }}
        table {{ width: 100%; border-collapse: collapse; margin: 15px 0; }}
        th, td {{ border: 1px solid #000; padding: 8px; text-align: left; }}
        th {{ background: #f0f0f0; text-align: center; }}
        .text-center {{ text-align: center; }}
        .header {{ margin-bottom: 20px; }}
    </style>
</head>
<body>
<div class='header'>
    <div class='center'>
        <div class='bold'>CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</div>
        <div>Độc lập - Tự do - Hạnh phúc</div>
        <div>---------------</div>
        <div class='title'>HỢP ĐỒNG THUÊ PHÒNG</div>
        <div>Số: <strong>{hopDong.MaHopDong}</strong></div>
        <div><i>(Hợp đồng được lập thành 02 bản có giá trị pháp lý như nhau)</i></div>
    </div>
</div>

<div class='indent'>
    - Căn cứ Bộ luật Dân sự số 91/2015/QH13 ngày 24/11/2015 của Quốc hội.<br>
    - Căn cứ vào nhu cầu và khả năng của các bên.
</div>

<div style='margin: 15px 0;'>
    Hôm nay, ngày <strong>{DateTime.Now:dd/MM/yyyy}</strong>, tại địa chỉ: <strong>{hopDong.PhongNavigation?.CoSo?.DiaChi ?? ""}</strong>
</div>

<div class='dieu-title'>CHÚNG TÔI GỒM:</div>

<div class='dieu'>
    <div class='dieu-title'>BÊN A (Bên cho thuê - Chủ trọ)</div>
    <div>- Họ và tên: <strong>Vũ Thị Thanh Thúy</strong></div>
    <div>- Số CCCD/CMND: 001082008547</div>
    <div>- Hộ khẩu thường trú: Ngọc Thụy – Long Biên – Hà Nội</div>
    <div>- Số điện thoại: 0869189018</div>
</div>

<div class='dieu'>
    <div class='dieu-title'>BÊN B (Bên thuê - Đại diện)</div>
    <div>- Họ và tên: <strong>{hopDong.KhachHangNavigation?.HoTen}</strong></div>
    <div>- Số CCCD/CMND: {hopDong.KhachHangNavigation?.CCCD}</div>
    <div>- Số điện thoại: {hopDong.KhachHangNavigation?.SoDienThoai}</div>
    <div>- Email: {hopDong.KhachHangNavigation?.Email}</div>
    <div>- Hộ khẩu thường trú: {hopDong.KhachHangNavigation?.DiaChi}</div>
</div>

<div class='dieu'>
    <div class='dieu-title'>DANH SÁCH NGƯỜI Ở CHUNG:</div>
    <table>
        <thead><tr><th>STT</th><th>Họ và tên</th><th>CCCD/CMND</th><th>Số điện thoại</th><th>Quan hệ</th></tr></thead>
        <tbody>{danhSachNguoiOBang}</tbody>
    </table>
</div>

<div class='dieu'>
    <div class='dieu-title'>Điều 1: ĐỐI TƯỢNG THUÊ</div>
    <div>1.1. Bên A đồng ý cho Bên B thuê căn phòng: <strong>{hopDong.PhongNavigation?.TenPhong}</strong></div>
    <div>1.2. Địa chỉ: <strong>{hopDong.PhongNavigation?.CoSo?.DiaChi}</strong></div>
    <div>1.3. Diện tích: <strong>{hopDong.PhongNavigation?.DienTich} m²</strong></div>
</div>

<div class='dieu'>
    <div class='dieu-title'>Điều 2: THỜI HẠN THUÊ</div>
    <div>Thời hạn thuê: <strong>{hopDong.NgayBatDau:dd/MM/yyyy}</strong> đến ngày <strong>{hopDong.NgayKetThuc:dd/MM/yyyy}</strong></div>
</div>

<div class='dieu'>
    <div class='dieu-title'>Điều 3: GIÁ THUÊ VÀ PHƯƠNG THỨC THANH TOÁN</div>
    <div>3.1. Giá thuê phòng: <strong>{(hopDong.PhongNavigation?.GiaPhong ?? 0):N0} đồng/tháng</strong></div>
    <div>3.2. Tiền đặt cọc: <strong>{(hopDong.TienCoc ?? 0):N0} đồng</strong></div>
    <div>3.3. Phương thức thanh toán: <strong>Chuyển khoản hoặc tiền mặt vào ngày 10 hàng tháng</strong></div>
</div>

<div class='dieu'>
    <div class='dieu-title'>Điều 4-9: CÁC ĐIỀU KHOẢN KHÁC</div>
    <div>Hai bên cam kết thực hiện đúng các điều khoản đã thỏa thuận.</div>
    <div>Hợp đồng được lập thành 02 bản, mỗi bên giữ 01 bản có giá trị pháp lý như nhau.</div>
</div>

<div class='sign-box' style='margin-top: 40px;'>
    <div class='sign-left'>
        <div class='bold'>BÊN CHO THUÊ (BÊN A)</div>
        <div class='sign-line'></div>
        <div>Vũ Thị Thanh Thúy</div>
    </div>
    <div class='sign-right'>
        <div class='bold'>BÊN THUÊ (BÊN B)</div>
        <div class='sign-line'></div>
        <div>{hopDong.KhachHangNavigation?.HoTen}</div>
    </div>
    <div class='clear'></div>
</div>
</body>
</html>";
        }

        // GET: Danh sách hợp đồng
        // GET: Danh sách hợp đồng (CHỈ ADMIN MỚI ĐƯỢC XEM)
        public async Task<IActionResult> Index(string searchString, string trangThai)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var role = HttpContext.Session.GetString("Role");

            if (userId == null)
            {
                return RedirectToAction("Index", "Login");
            }

            // CHỈ ADMIN MỚI ĐƯỢC XEM DANH SÁCH TẤT CẢ HỢP ĐỒNG
            if (role != "Admin")
            {
                if (role == "Khach")
                {
                    return RedirectToAction("HopDongCuaToi", "KhachHang");
                }
                return RedirectToAction("Index", "Login");
            }

            var hopDongs = _context.HopDong
                .Include(h => h.PhongNavigation)
                .Include(h => h.KhachHangNavigation)
                .AsQueryable();

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
            // ==========================================
            // PHẦN CODE MỚI THÊM: SẮP XẾP THEO YÊU CẦU
            // ==========================================
            hopDongs = hopDongs
                .OrderBy(h => h.TrangThai == "Hiệu lực" ? 0 : 1) // 1. Ưu tiên "Hiệu lực" đẩy lên đầu
                .ThenBy(h => h.PhongNavigation.TenPhong);        // 2. Sau đó sắp xếp theo tên phòng
                                                                 // ==========================================

            ViewBag.SearchString = searchString;
            ViewBag.TrangThai = trangThai;
            ViewBag.TrangThaiList = new List<string> { "Tất cả", "Hiệu lực", "Hết hạn", "Đã hủy" };

            return View(await hopDongs.ToListAsync());
        }

        // GET: Tạo hợp đồng mới
        public async Task<IActionResult> Create()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("Index", "Login");
            }

            ViewBag.PhongList = await _context.Phong
                .Include(p => p.CoSo)
                .Where(p => p.TrangThai == "Trống")
                .ToListAsync();

            ViewBag.KhachHangList = await _context.KhachHang.ToListAsync();

            return View();
        }


        // POST: Tạo hợp đồng mới (CÓ TẠO TÀI KHOẢN VÀ LIÊN KẾT)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            int maPhong,
            DateTime ngayBatDau,
            DateTime ngayKetThuc,
            decimal tienCoc,
            // Thông tin khách hàng
            string HoTen,
            string SoDienThoai,
            string Email,
            string CCCD,
            string DiaChi,
            DateTime? NgaySinh,
            // Thông tin tài khoản
            bool taoTaiKhoan,
            string tenDangNhap,
            string matKhau,
            // Danh sách người ở
            List<string> NguoiOHoTen,
            List<string> NguoiOCCCD,
            List<string> NguoiOSDT)
        {
            // Kiểm tra phòng còn trống không
            var phong = await _context.Phong.FindAsync(maPhong);
            if (phong == null || phong.TrangThai != "Trống")
            {
                TempData["Error"] = "Phòng không còn trống!";
                await LoadViewBags();
                return View();
            }

            int maKhachHangCuoi = 0;

            // ========== 1. TẠO HOẶC LẤY KHÁCH HÀNG ==========
            // Kiểm tra khách hàng đã tồn tại theo số điện thoại
            var khachHangTonTai = await _context.KhachHang
                .FirstOrDefaultAsync(k => k.SoDienThoai == SoDienThoai);

            if (khachHangTonTai != null)
            {
                maKhachHangCuoi = khachHangTonTai.MaKhachHang;
                TempData["Info"] = "Khách hàng đã tồn tại, sử dụng thông tin cũ!";
            }
            else
            {
                // Tạo khách hàng mới
                var khachHangMoi = new KhachHang
                {
                    HoTen = HoTen.Trim(),
                    SoDienThoai = SoDienThoai.Trim(),
                    Email = Email ?? "",
                    CCCD = CCCD ?? "",
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
                // Kiểm tra tên đăng nhập đã tồn tại
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

                    // Liên kết tài khoản với khách hàng
                    var khachHang = await _context.KhachHang.FindAsync(maKhachHangCuoi);
                    if (khachHang != null)
                    {
                        khachHang.MaTaiKhoan = taiKhoan.MaTaiKhoan;
                        _context.Update(khachHang);
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
            var hopDong = new HopDong
            {
                MaPhong = maPhong,
                MaKhachHang = maKhachHangCuoi,
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
            _context.Update(phong);
            await _context.SaveChangesAsync();

            // ========== 6. TẠO HÓA ĐƠN TỰ ĐỘNG ==========
            await TaoHoaDonChoHopDong(hopDong.MaHopDong, phong.GiaPhong, ngayBatDau, ngayKetThuc);

            TempData["Success"] = $"Tạo hợp đồng thành công! Mã hợp đồng: {hopDong.MaHopDong}";
            return RedirectToAction(nameof(Index));
        }

        // Load dữ liệu cho View Create
        private async Task LoadViewBags()
        {
            ViewBag.PhongList = await _context.Phong
                .Include(p => p.CoSo)
                .Where(p => p.TrangThai == "Trống")
                .ToListAsync();

            ViewBag.KhachHangList = await _context.KhachHang.ToListAsync();
        }

        // POST: Chấm dứt hợp đồng (CHỈ ADMIN)
        //[HttpPost]
        [ValidateAntiForgeryToken]
        // 1. GET: Hiển thị trang giao diện xác nhận thanh lý
        public async Task<IActionResult> ChamDut(int? id)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
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

            return View(hopDong);
        }

        // 2. POST: Xử lý logic lưu vào Database (Đây chính là đoạn code của bạn)
        [HttpPost, ActionName("ChamDut")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChamDutConfirmed(int id)
        {
            var role = HttpContext.Session.GetString("Role");

            if (role != "Admin")
            {
                TempData["Error"] = "Bạn không có quyền thực hiện chức năng này!";
                return RedirectToAction("Index", "Login");
            }

            var hopDong = await _context.HopDong
                .Include(h => h.PhongNavigation)
                .FirstOrDefaultAsync(h => h.MaHopDong == id);

            if (hopDong == null) return NotFound();

            // Cập nhật trạng thái hợp đồng (giữ nguyên logic của bạn)
            hopDong.TrangThai = "Đã hủy";
            _context.Update(hopDong);

            // Cập nhật trạng thái phòng (giữ nguyên logic của bạn)
            var phong = hopDong.PhongNavigation;
            if (phong != null)
            {
                phong.TrangThai = "Trống";
                _context.Update(phong);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã chấm dứt hợp đồng!";
            return RedirectToAction(nameof(Index));
        }

        // Tạo hóa đơn tự động
        private async Task TaoHoaDonChoHopDong(int maHopDong, decimal giaPhong, DateTime ngayBatDau, DateTime ngayKetThuc)
        {
            int currentMonth = ngayBatDau.Month;
            int currentYear = ngayBatDau.Year;
            int endMonth = ngayKetThuc.Month;
            int endYear = ngayKetThuc.Year;

            while (currentYear < endYear || (currentYear == endYear && currentMonth <= endMonth))
            {
                var existingHoaDon = await _context.HoaDon
                    .AnyAsync(h => h.MaHopDong == maHopDong && h.Thang == currentMonth && h.Nam == currentYear);

                if (!existingHoaDon)
                {
                    var hoaDon = new HoaDon
                    {
                        MaHopDong = maHopDong,
                        Thang = currentMonth,
                        Nam = currentYear,
                        TongTien = giaPhong,
                        TrangThai = "Chưa thanh toán",
                        NgayTao = DateTime.Now
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
        }

        // GET: Chi tiết hợp đồng
        public async Task<IActionResult> Details(int? id)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (id == null) return NotFound();

            var hopDong = await _context.HopDong
                .Include(h => h.PhongNavigation).ThenInclude(p => p.CoSo)
                .Include(h => h.KhachHangNavigation)
                .FirstOrDefaultAsync(m => m.MaHopDong == id);

            if (hopDong == null) return NotFound();

            // Kiểm tra nếu là khách hàng thì chỉ cho xem hợp đồng của mình
            var role = HttpContext.Session.GetString("Role");
            var userId = HttpContext.Session.GetInt32("UserId");

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
            ViewBag.Role = role;  // Truyền role sang View

            return View(hopDong);
        }

        // API: Lấy thông tin phòng
        [HttpGet]
        public async Task<IActionResult> GetPhongInfo(int maPhong)
        {
            var phong = await _context.Phong
                .Include(p => p.CoSo)
                .FirstOrDefaultAsync(p => p.MaPhong == maPhong);

            if (phong == null)
                return Json(new { success = false });

            return Json(new
            {
                success = true,
                giaPhong = phong.GiaPhong,
                dienTich = phong.DienTich,
                tenCoSo = phong.CoSo?.TenCoSo
            });
        }
    }
}
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

        // Hàm tạo nội dung HTML hợp đồng
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
        body {{
            font-family: 'Times New Roman', Times, serif;
            font-size: 14px;
            margin: 40px;
            line-height: 1.5;
        }}
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
        .dot-line {{ border-bottom: 1px dotted #000; min-width: 150px; display: inline-block; }}
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
    - Căn cứ Bộ luật Dân sự số 91/2015/QH13 ngày 24/11/2015 của Quốc hội nước Cộng hòa xã hội chủ nghĩa Việt Nam.<br>
    - Căn cứ vào nhu cầu và khả năng của các bên.
</div>

<div style='margin: 15px 0;'>
    Hôm nay, ngày <strong>{DateTime.Now:dd/MM/yyyy}</strong>, tại địa chỉ: <strong>{hopDong.PhongNavigation?.CoSo?.DiaChi ?? ""}</strong>
</div>

<div class='dieu-title'>CHÚNG TÔI GỒM:</div>

<!-- BÊN A -->
<div class='dieu'>
    <div class='dieu-title'>BÊN A (Bên cho thuê - Chủ trọ)</div>
    <div>- Họ và tên: <strong>Vũ Thị Thanh Thúy</strong></div>
    <div>- Số CCCD/CMND: 001082008547</div>
    <div>- Hộ khẩu thường trú: Ngọc Thụy – Long Biên – Hà Nội</div>
    <div>- Số điện thoại: 0869189018</div>
</div>

<!-- BÊN B -->
<div class='dieu'>
    <div class='dieu-title'>BÊN B (Bên thuê - Đại diện)</div>
    <div>- Họ và tên: <strong>{hopDong.KhachHangNavigation?.HoTen}</strong></div>
    <div>- Số CCCD/CMND: {hopDong.KhachHangNavigation?.CCCD}</div>
    <div>- Số điện thoại: {hopDong.KhachHangNavigation?.SoDienThoai}</div>
    <div>- Email: {hopDong.KhachHangNavigation?.Email}</div>
    <div>- Hộ khẩu thường trú: {hopDong.KhachHangNavigation?.DiaChi}</div>
    <div>- Ngày sinh: {hopDong.KhachHangNavigation?.NgaySinh:dd/MM/yyyy}</div>
</div>

<!-- DANH SÁCH NGƯỜI Ở -->
<div class='dieu'>
    <div class='dieu-title'>DANH SÁCH NGƯỜI Ở CHUNG:</div>
    <table>
        <thead>
            ｜｜DSML｜｜<th>STT</th><th>Họ và tên</th><th>CCCD/CMND</th><th>Số điện thoại</th><th>Quan hệ</th></tr>
        </thead>
        <tbody>
            {danhSachNguoiOBang}
        </tbody>
    </table>
</div>

<!-- Điều 1 -->
<div class='dieu'>
    <div class='dieu-title'>Điều 1: ĐỐI TƯỢNG THUÊ</div>
    <div>1.1. Bên A đồng ý cho Bên B thuê căn phòng: <strong>{hopDong.PhongNavigation?.TenPhong}</strong></div>
    <div>1.2. Địa chỉ: <strong>{hopDong.PhongNavigation?.CoSo?.DiaChi}</strong></div>
    <div>1.3. Diện tích: <strong>{hopDong.PhongNavigation?.DienTich} m²</strong></div>
    <div>1.4. Mục đích sử dụng: <strong>Để ở sinh hoạt </strong></div>
    <div>1.5. Trang thiết bị trong phòng bao gồm: (ghi rõ kèm tình trạng)</div>
    <div>- Điều hòa: ________________________________________________</div>
    <div>- Bình nóng lạnh: ____________________________________________</div>
    <div>- Giường, tủ: ________________________________________________</div>
    <div>- Các thiết bị khác: __________________________________________</div>
</div>

<!-- Điều 2 -->
<div class='dieu'>
    <div class='dieu-title'>Điều 2: THỜI HẠN THUÊ</div>
    <div>Thời hạn thuê: <strong>{hopDong.NgayBatDau:dd/MM/yyyy}</strong> đến ngày <strong>{hopDong.NgayKetThuc:dd/MM/yyyy}</strong></div>
    <div>Hợp đồng có thể được gia hạn khi hai bên có nhu cầu và thỏa thuận bằng văn bản.</div>
</div>

<!-- Điều 3 -->
<div class='dieu'>
    <div class='dieu-title'>Điều 3: GIÁ THUÊ VÀ PHƯƠNG THỨC THANH TOÁN</div>
    <div>3.1. Giá thuê phòng: <strong>{(hopDong.PhongNavigation?.GiaPhong ?? 0):N0} đồng/tháng</strong> (chưa bao gồm tiền điện, nước và các dịch vụ khác)</div>
    <div>3.2. Tiền đặt cọc: <strong>{(hopDong.TienCoc ?? 0):N0} đồng</strong></div>
    <div>3.3. Tiền điện: ________________________________________________</div>
    <div>3.4. Tiền nước: ________________________________________________</div>
    <div>3.5. Phí dịch vụ (rác, wifi, bảo vệ): ________________________________</div>
    <div>3.6. Phương thức thanh toán: <strong>Chuyển khoản hoặc tiền mặt vào ngày 10 hàng tháng</strong></div>
    <div>3.7. Thời hạn thanh toán: <strong>Chậm nhất ngày 15 hàng tháng</strong></div>
</div>

<!-- Điều 4 -->
<div class='dieu'>
    <div class='dieu-title'>Điều 4: QUYỀN CỦA BÊN A</div>
    <div>4.1. Yêu cầu Bên B thanh toán đầy đủ, đúng hạn.</div>
    <div>4.2. Kiểm tra định kỳ tình trạng phòng và việc sử dụng phòng.</div>
    <div>4.3. Đơn phương chấm dứt hợp đồng nếu Bên B vi phạm nghiêm trọng.</div>
</div>

<!-- Điều 5 -->
<div class='dieu'>
    <div class='dieu-title'>Điều 5: NGHĨA VỤ CỦA BÊN A</div>
    <div>5.1. Bàn giao phòng đúng thời hạn, đúng hiện trạng.</div>
    <div>5.2. Đảm bảo cơ sở vật chất hoạt động tốt.</div>
    <div>5.3. Sửa chữa kịp thời các hư hỏng do chất lượng công trình.</div>
</div>

<!-- Điều 6 -->
<div class='dieu'>
    <div class='dieu-title'>Điều 6: QUYỀN CỦA BÊN B</div>
    <div>6.1. Sử dụng phòng theo đúng mục đích đã thỏa thuận.</div>
    <div>6.2. Yêu cầu bảo trì, sửa chữa khi có hư hỏng từ phía Bên A.</div>
</div>

<!-- Điều 7 -->
<div class='dieu'>
    <div class='dieu-title'>Điều 7: NGHĨA VỤ CỦA BÊN B</div>
    <div>7.1. Thanh toán đầy đủ và đúng hạn.</div>
    <div>7.2. Giữ gìn vệ sinh chung, không gây ồn ào, mất trật tự.</div>
    <div>7.3. Không tự ý sửa chữa, cơi nới hoặc cho người khác thuê lại.</div>
    <div>7.4. Bồi thường toàn bộ thiệt hại nếu làm hư hỏng tài sản.</div>
    <div>7.5. Thông báo trước 30 ngày nếu có nhu cầu chấm dứt hợp đồng trước hạn.</div>
</div>

<!-- Điều 8 -->
<div class='dieu'>
    <div class='dieu-title'>Điều 8: XỬ LÝ KHI VI PHẠM</div>
    <div>8.1. Nếu Bên B thanh toán chậm quá 07 ngày so với hạn, Bên A có quyền thu thêm 0,05%/ngày trên số tiền chậm.</div>
    <div>8.2. Nếu Bên B vi phạm nghiêm trọng, Bên A có quyền đơn phương chấm dứt hợp đồng và không hoàn trả tiền cọc.</div>
</div>

<!-- Điều 9 -->
<div class='dieu'>
    <div class='dieu-title'>Điều 9: ĐIỀU KHOẢN CHUNG</div>
    <div>9.1. Hợp đồng có hiệu lực kể từ ngày ký.</div>
    <div>9.2. Hai bên cam kết thực hiện đúng các điều khoản đã thỏa thuận.</div>
    <div>9.3. Mọi tranh chấp được giải quyết bằng thương lượng hoặc theo quy định của pháp luật.</div>
    <div>9.4. Hợp đồng được lập thành 02 (hai) bản, mỗi bên giữ 01 bản có giá trị pháp lý như nhau.</div>
    <div>9.5. Bản mềm được lưu trữ trên hệ thống quản lý phòng trọ để tra cứu khi cần.</div>
</div>

<!-- KÝ TÊN -->
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

<div style='margin-top: 30px; text-align: center;'>
    <div class='bold'>Xác nhận của người làm chứng (nếu có)</div>
    <div style='margin-top: 30px;'>(Ký, ghi rõ họ tên)</div>
</div>

</body>
</html>";
        }

        // GET: Danh sách hợp đồng
        public async Task<IActionResult> Index(string searchString, string trangThai)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
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

            ViewBag.SearchString = searchString;
            ViewBag.TrangThai = trangThai;
            ViewBag.TrangThaiList = new List<string> { "Tất cả", "Hiệu lực", "Hết hạn", "Đã hủy" };

            return View(await hopDongs.ToListAsync());
        }

        // GET: Tạo hợp đồng mới
        public IActionResult Create()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("Index", "Login");
            }

            ViewBag.PhongList = _context.Phong
                .Include(p => p.CoSo)
                .Where(p => p.TrangThai == "Trống")
                .ToList();

            ViewBag.KhachHangList = _context.KhachHang.ToList();

            return View();
        }

        // POST: Tạo hợp đồng mới
        [HttpPost]
        [ValidateAntiForgeryToken]
        
        public async Task<IActionResult> Create(
    int maPhong, int maKhachHang, DateTime ngayBatDau, DateTime ngayKetThuc, decimal tienCoc,
    string HoTen, string SoDienThoai, string Email, string CCCD, string DiaChi,
    List<string> NguoiOHoTen, List<string> NguoiOCCCD, List<string> NguoiOSDT)
        {
            var phong = await _context.Phong.FindAsync(maPhong);
            if (phong == null || phong.TrangThai != "Trống")
            {
                TempData["Error"] = "Phòng không còn trống!";
                return RedirectToAction("Create");
            }

            // Tạo khách hàng
            var khachHang = new KhachHang
            {
                HoTen = HoTen.Trim(),
                SoDienThoai = SoDienThoai.Trim(),
                Email = Email ?? "",
                CCCD = CCCD ?? "",
                DiaChi = DiaChi ?? ""
            };
            _context.KhachHang.Add(khachHang);
            await _context.SaveChangesAsync();

            // Tạo hợp đồng
            var hopDong = new HopDong
            {
                MaPhong = maPhong,
                MaKhachHang = khachHang.MaKhachHang,
                NgayBatDau = ngayBatDau,
                NgayKetThuc = ngayKetThuc,
                TienCoc = tienCoc,
                TrangThai = "Hiệu lực"
            };
            _context.HopDong.Add(hopDong);
            await _context.SaveChangesAsync();

            // Thêm danh sách người ở
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
                            CCCD = NguoiOCCCD?[i] ?? "",
                            SoDienThoai = NguoiOSDT?[i] ?? ""
                        };
                        _context.NguoiOHopDong.Add(nguoiO);
                    }
                }
            }

            phong.TrangThai = "Đã thuê";
            _context.Update(phong);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Tạo hợp đồng thành công! Mã: {hopDong.MaHopDong}";
            return RedirectToAction(nameof(Index));
        }
        // POST: Chấm dứt hợp đồng
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChamDut(int id)
        {
            var hopDong = await _context.HopDong
                .Include(h => h.PhongNavigation)
                .FirstOrDefaultAsync(h => h.MaHopDong == id);

            if (hopDong == null) return NotFound();

            hopDong.TrangThai = "Đã hủy";
            _context.Update(hopDong);

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